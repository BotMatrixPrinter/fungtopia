using System;
using System.Numerics;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;

namespace Fungtopia.Townsfolk;

/// <summary>
/// Pet City's background life: <see cref="FolkCount"/> mushroom folk walking between shop doors,
/// benches, shop windows, the plaza and the avenue, chatting in pairs, sitting, going in and out of
/// buildings, some walking a pet; <see cref="RoamingPetCount"/> cats, dogs and crows roaming on
/// their own; and butterflies over the planters and the plaza by day. It steps aside round the
/// player (from <see cref="PlayerState"/>) and never leaves Pet City. At night (<see cref="TimeOfDay"/>)
/// the night owls (about a third) stay out and the others are in by dusk and come out after dawn.
/// It simulates only while the player is within <see cref="ActiveMargin"/> of Pet City
/// (<see cref="Active"/>), publishes nothing, and does not allocate per tick.
/// </summary>
public sealed class TownsfolkModule : IModule, IMessageHandler<PlayerState>, IMessageHandler<TimeOfDay>
{
    public const int FolkCount = 25;
    public const int RoamingPetCount = 10;
    /// <summary>Folk walking a pet (the pets after the roaming ones).</summary>
    public const int OwnedPetCount = 6;
    public const int ButterflyCount = 16;
    public const int Seed = 20261025;

    /// <summary>A townsperson's radius for walking round obstacles (meters).</summary>
    public const float FolkRadius = 0.35f;
    public const float PetRadius = 0.25f;

    /// <summary>Walking folk keep this far from the player (meters, centers).</summary>
    public const float PlayerClearance = 1.1f;

    /// <summary>Standing folk shuffle aside when the player comes closer than this (meters).</summary>
    public const float StepAsideRadius = 0.95f;

    /// <summary>Pet City is simulated and drawn while the player is within this many meters of its rectangle.</summary>
    public const float ActiveMargin = 45f;

    /// <summary>Crows on the ground fly off when the player comes this close (meters).</summary>
    public const float CrowStartle = 2.5f;

    /// <summary>How far a pet walks beside its owner (meters to the side, and behind).</summary>
    public const float LeashSide = 0.75f, LeashBack = 0.35f;

    /// <summary>Butterflies move on only to flowers this close (meters).</summary>
    public const float MaxFlowerHop = 14f;

    private static readonly float[] s_tries = { 0f, 0.35f, -0.35f, 0.7f, -0.7f, 1.05f, -1.05f, 1.45f, -1.45f, 1.9f, -1.9f, 2.5f, -2.5f };

    private readonly Folk[] _folk = new Folk[FolkCount];
    private readonly Critter[] _pets = new Critter[RoamingPetCount + OwnedPetCount];
    private readonly Butterfly[] _butterflies = new Butterfly[ButterflyCount];
    private readonly int[] _seatTaken;
    private readonly Random _rng = new(Seed);
    private IMessageBus? _bus;
    private bool _teleport;
    private Vector3 _teleportTo;
    private float _teleportYaw;
    private Vector2 _player;
    private bool _hasPlayer;
    private bool _playerAlive;

    public TownsfolkModule()
    {
        _seatTaken = new int[CityLife.Seats.Length];
        Array.Fill(_seatTaken, -1);
        Populate();
    }

    public ReadOnlySpan<Folk> Folk => _folk;

    /// <summary>The roaming pets first (<see cref="RoamingPetCount"/>), then those walking with an owner.</summary>
    public ReadOnlySpan<Critter> Pets => _pets;

    public ReadOnlySpan<Butterfly> Butterflies => _butterflies;

    /// <summary>The latest hour (<see cref="TimeOfDay.Hour"/>); 9:00 until the first one.</summary>
    public float Hour { get; private set; } = 9f;

    public bool IsNight => TimeOfDay.IsNightAt(Hour);

    /// <summary>How far out the butterflies are: 1 by day, fading to 0 (all hidden) at night.</summary>
    public float ButterfliesOut { get; private set; } = 1f;

    /// <summary>The player is in or near Pet City: the simulation runs and the view draws.</summary>
    public bool Active { get; private set; }

    /// <summary>Seconds simulated (only while <see cref="Active"/>).</summary>
    public double Seconds { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<TimeOfDay>(this);
    }

    public void Handle(in PlayerState message)
    {
        _player = new Vector2(message.Position.X, message.Position.Z);
        _playerAlive = message.Alive;
        _hasPlayer = true;
    }

    public void Handle(in TimeOfDay message) => Hour = message.Hour;

    /// <summary>Where <c>TOWNSFOLK_START=plaza</c> puts the player for screenshots: the avenue south of the plaza, looking north at it.</summary>
    public static readonly Vector3 ScreenshotSpot = new(1.5f, Ground.HeightAt(1.5f, -196f), -196f);

    public const float ScreenshotYaw = 0f;

    /// <summary>
    /// For screenshots only (the view calls it when <c>TOWNSFOLK_START=plaza</c>): publishes one
    /// <see cref="TeleportPlayer"/> to <paramref name="at"/> on the next tick. Never called in play.
    /// </summary>
    public void TeleportPlayerOnce(Vector3 at, float yaw)
    {
        _teleport = true;
        _teleportTo = at;
        _teleportYaw = yaw;
    }

    /// <summary>Whether a player at (x, z) is near enough to Pet City for it to come alive.</summary>
    public static bool Near(float x, float z) => Areas.PetCity.Depth(x, z) > -ActiveMargin;

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("TownsfolkModule.Tick before Start.");
        if (_teleport)
        {
            _teleport = false;
            _bus.Publish(new TeleportPlayer(_teleportTo, _teleportYaw));
        }
        bool active = _hasPlayer && Near(_player.X, _player.Y);
        if (active && !Active)
            Settle();
        Active = active;
        if (!active)
            return;
        float dt = (float)context.StepSeconds;
        Seconds += dt;
        for (int i = 0; i < _folk.Length; i++)
        {
            _folk[i].Previous = _folk[i].Position;
            _folk[i].PreviousYaw = _folk[i].Yaw;
        }
        for (int i = 0; i < _pets.Length; i++)
        {
            _pets[i].Previous = _pets[i].Position;
            _pets[i].PreviousYaw = _pets[i].Yaw;
        }
        for (int i = 0; i < _folk.Length; i++)
            TickFolk(i, dt);
        KeepApart(dt);
        for (int i = 0; i < _pets.Length; i++)
        {
            if (_pets[i].Owner >= 0)
                TickOwned(i, dt);
            else if (_pets[i].Kind == CritterKind.Crow)
                TickCrow(i, dt);
            else
                TickRoamer(i, dt);
        }
        TickButterflies(dt);
    }

    // Setting up.

    private float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    private Vector2 RandomOpen(bool busy)
    {
        var open = CityLife.Open;
        return open[busy ? _rng.Next(CityLife.BusyCount) : _rng.Next(open.Length)];
    }

    private static Vector3 OnGround(Vector2 at) => new(at.X, Ground.HeightAt(at.X, at.Y), at.Y);

    private void Populate()
    {
        for (int i = 0; i < _folk.Length; i++)
        {
            var at = RandomOpen(_rng.Next(3) > 0);
            ref var f = ref _folk[i];
            f.Position = f.Previous = OnGround(at);
            f.Yaw = f.PreviousYaw = R(-MathF.PI, MathF.PI);
            f.Partner = f.Seat = f.Door = f.Pet = -1;
            // About a third stay out at night.
            f.NightOwl = i % 3 == 0;
            f.HomeHour = R(18f, 19.3f);
            f.WakeHour = R(6.3f, 8.5f);
            f.WalkSpeed = R(0.95f, 1.45f);
            f.Side = _rng.Next(2) == 0 ? 1f : -1f;
            f.Look = Looks.Mix(_rng);
            f.State = FolkState.Waiting;
            f.Timer = R(0.2f, 3f);
        }
        for (int i = 0; i < _pets.Length; i++)
        {
            ref var p = ref _pets[i];
            p.Owner = p.Other = p.Roof = -1;
            if (i < RoamingPetCount)
                p.Kind = i < 4 ? CritterKind.Cat : i < 7 ? CritterKind.Dog : CritterKind.Crow;
            else
            {
                // Every fourth folk walks a cat or a dog.
                p.Kind = i % 2 == 0 ? CritterKind.Dog : CritterKind.Cat;
                p.Owner = 1 + (i - RoamingPetCount) * 4;
                _folk[p.Owner].Pet = i;
            }
            p.Color = p.Kind == CritterKind.Crow ? Looks.Feathers[_rng.Next(Looks.Feathers.Length)] : Looks.Furs[_rng.Next(Looks.Furs.Length)];
            p.Scale = R(0.85f, 1.12f);
            var at = p.Owner >= 0 ? BesideOwner(p.Owner) : RandomOpen(false);
            if (p.Owner >= 0 && !CityLife.Clear(at, PetRadius))
                at = new Vector2(_folk[p.Owner].Position.X, _folk[p.Owner].Position.Z);
            p.Position = p.Previous = OnGround(at);
            p.Yaw = p.PreviousYaw = p.Owner >= 0 ? _folk[p.Owner].Yaw : R(-MathF.PI, MathF.PI);
            p.State = p.Owner >= 0 ? CritterState.Follow : p.Kind == CritterKind.Crow ? CritterState.Peck : CritterState.Sniff;
            p.Timer = R(0.5f, 4f);
        }
        var flowers = CityLife.Flowers;
        for (int i = 0; i < _butterflies.Length; i++)
        {
            ref var b = ref _butterflies[i];
            b.Flower = i % flowers.Length;
            b.Position = b.Previous = b.Target = flowers[b.Flower] + new Vector3(R(-1f, 1f), R(0.4f, 1.4f), R(-1f, 1f));
            b.Color = Looks.Wings[_rng.Next(Looks.Wings.Length)];
            b.Timer = R(0f, 1f);
        }
    }

    /// <summary>
    /// Coming alive after the player was away (no ticks meanwhile): those who should be in for the
    /// night are in, those who should be up are out.
    /// </summary>
    private void Settle()
    {
        for (int i = 0; i < _folk.Length; i++)
        {
            ref var f = ref _folk[i];
            if (HomeTime(f) && TimeOfDay.IsNightAt(Hour))
            {
                if (f.Visible)
                    GoIn(i, NearestDoor(Flat(f.Position)), true);
            }
            else if (f.State == FolkState.Inside && f.InForNight && !HomeTime(f))
                ComeOut(i);
            f.Previous = f.Position;
            f.PreviousYaw = f.Yaw;
        }
        for (int i = 0; i < _pets.Length; i++)
        {
            _pets[i].Previous = _pets[i].Position;
            _pets[i].PreviousYaw = _pets[i].Yaw;
        }
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    // Townsfolk.

    private bool HomeTime(in Folk f) => !f.NightOwl && (Hour >= f.HomeHour || Hour < f.WakeHour);

    private static int NearestDoor(Vector2 at)
    {
        var doors = CityLife.Doors;
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int d = 0; d < doors.Length; d++)
        {
            float distance = Vector2.DistanceSquared(doors[d].At, at);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = d;
            }
        }
        return best;
    }

    private void TickFolk(int i, float dt)
    {
        ref var f = ref _folk[i];
        f.Speed = 0f;
        if (f.State == FolkState.Inside)
        {
            f.Timer -= dt;
            if (f.InForNight ? !HomeTime(f) : f.Timer <= 0f)
            {
                if (!f.InForNight && HomeTime(f))
                    f.InForNight = true;
                else
                    ComeOut(i);
            }
            return;
        }
        if (HomeTime(f))
        {
            // Dusk: anyone not a night owl still out slips in at once.
            if (TimeOfDay.IsNightAt(Hour))
            {
                GoIn(i, f.Errand == Errand.Home && f.Door >= 0 ? f.Door : NearestDoor(Flat(f.Position)), true);
                return;
            }
            if (f.Errand != Errand.Home)
            {
                Release(i);
                f.Door = NearestDoor(Flat(f.Position));
                WalkTo(i, CityLife.Doors[f.Door].At, CityLife.Doors[f.Door].Yaw, Errand.Home);
            }
        }

        switch (f.State)
        {
            case FolkState.Walking:
                f.Timer -= dt;
                if (f.Errand == Errand.Chat && (f.Partner < 0 || _folk[f.Partner].Partner != i))
                {
                    f.Partner = -1;
                    f.Errand = Errand.Stroll;
                }
                if (Walk(ref f.Position, ref f.Yaw, ref f.Speed, f.Target, f.WalkSpeed, FolkRadius, dt, f.Side, true))
                    Arrive(i);
                else if (f.Timer <= 0f)
                {
                    // Stuck or too long: try something else (try the other way round next time).
                    f.Side = -f.Side;
                    if (f.Errand == Errand.Home)
                        GoIn(i, f.Door, true);
                    else
                        Rest(i, R(0.5f, 1.5f));
                }
                break;
            case FolkState.Sitting:
                f.Timer -= dt;
                f.Yaw = CityLife.TurnToward(f.Yaw, f.TargetYaw, dt * 4f);
                if (f.Timer <= 0f)
                {
                    // Stand up in front of the bench.
                    f.Position = OnGround(CityLife.SeatApproach[f.Seat]);
                    f.Previous = f.Position;
                    Release(i);
                    Rest(i, R(0.5f, 1.2f));
                }
                break;
            case FolkState.Chatting:
                f.Timer -= dt;
                if (f.Partner < 0 || _folk[f.Partner].State != FolkState.Chatting)
                {
                    f.Partner = -1;
                    Rest(i, R(0.5f, 1.5f));
                    break;
                }
                ref var other = ref _folk[f.Partner];
                f.Yaw = CityLife.TurnToward(f.Yaw, CityLife.YawOf(Flat(other.Position - f.Position)), dt * 5f);
                // They take turns talking, a few seconds each.
                if (i < f.Partner && MathF.Floor((f.Timer + dt) / 2.6f) != MathF.Floor(f.Timer / 2.6f))
                {
                    f.Speaking = !f.Speaking;
                    other.Speaking = !f.Speaking;
                }
                StepAside(ref f.Position, ref f.Speed, FolkRadius, dt);
                if (f.Timer <= 0f)
                {
                    int partner = f.Partner;
                    f.Partner = -1;
                    _folk[partner].Partner = -1;
                    _folk[partner].Timer = 0f;
                    Rest(i, R(0.3f, 1.2f));
                    Rest(partner, R(0.6f, 1.6f));
                }
                break;
            case FolkState.Browsing:
                f.Timer -= dt;
                f.Yaw = CityLife.TurnToward(f.Yaw, f.TargetYaw, dt * 4f);
                StepAside(ref f.Position, ref f.Speed, FolkRadius, dt);
                if (f.Timer <= 0f)
                    Rest(i, R(0.3f, 1f));
                break;
            default:
                f.Timer -= dt;
                StepAside(ref f.Position, ref f.Speed, FolkRadius, dt);
                if (f.Errand == Errand.Chat)
                {
                    int p = f.Partner;
                    if (p < 0 || _folk[p].Partner != i)
                    {
                        f.Partner = -1;
                        f.Errand = Errand.Stroll;
                    }
                    else
                    {
                        f.Yaw = CityLife.TurnToward(f.Yaw, CityLife.YawOf(Flat(_folk[p].Position - f.Position)), dt * 5f);
                        if (_folk[p].State == FolkState.Waiting && _folk[p].Errand == Errand.Chat)
                        {
                            float chat = R(7f, 16f);
                            f.State = _folk[p].State = FolkState.Chatting;
                            f.Errand = _folk[p].Errand = Errand.Stroll;
                            f.Timer = _folk[p].Timer = chat;
                            f.Speaking = true;
                            _folk[p].Speaking = false;
                            break;
                        }
                    }
                    if (f.Timer <= 0f)
                    {
                        if (p >= 0 && _folk[p].Partner == i)
                        {
                            _folk[p].Partner = -1;
                            _folk[p].Errand = Errand.Stroll;
                        }
                        f.Partner = -1;
                        f.Errand = Errand.Stroll;
                    }
                    break;
                }
                if (f.Timer <= 0f)
                    Decide(i);
                break;
        }
        f.Position.Y = Ground.HeightAt(f.Position.X, f.Position.Z);
    }

    /// <summary>Stand a moment, then decide what next.</summary>
    private void Rest(int i, float seconds)
    {
        ref var f = ref _folk[i];
        f.State = FolkState.Waiting;
        f.Errand = Errand.Stroll;
        f.Timer = seconds;
    }

    private void WalkTo(int i, Vector2 target, float yaw, Errand errand)
    {
        ref var f = ref _folk[i];
        f.State = FolkState.Walking;
        f.Errand = errand;
        f.Target = target;
        f.TargetYaw = yaw;
        // Patience: twice the straight walk, plus a little.
        f.Timer = Vector2.Distance(Flat(f.Position), target) / f.WalkSpeed * 2.2f + 6f;
    }

    /// <summary>Leaves a seat or a chat.</summary>
    private void Release(int i)
    {
        ref var f = ref _folk[i];
        if (f.Seat >= 0 && _seatTaken[f.Seat] == i)
            _seatTaken[f.Seat] = -1;
        f.Seat = -1;
        if (f.Partner >= 0 && _folk[f.Partner].Partner == i)
            _folk[f.Partner].Partner = -1;
        f.Partner = -1;
    }

    private void Decide(int i)
    {
        ref var f = ref _folk[i];
        float roll = (float)_rng.NextDouble();
        if (roll < 0.2f && TryChat(i))
            return;
        if (roll < 0.38f)
        {
            int seat = _rng.Next(_seatTaken.Length);
            if (_seatTaken[seat] < 0)
            {
                _seatTaken[seat] = i;
                f.Seat = seat;
                WalkTo(i, CityLife.SeatApproach[seat], CityLife.Seats[seat].Yaw, Errand.Sit);
                return;
            }
        }
        if (roll < 0.52f)
        {
            f.Door = _rng.Next(CityLife.Doors.Length);
            WalkTo(i, CityLife.Doors[f.Door].At, CityLife.Doors[f.Door].Yaw, Errand.Shop);
            return;
        }
        if (roll < 0.66f)
        {
            var window = CityLife.Windows[_rng.Next(CityLife.Windows.Length)];
            WalkTo(i, window.At, window.Yaw, Errand.Browse);
            return;
        }
        var to = RandomOpen(_rng.Next(4) > 0);
        WalkTo(i, to, CityLife.YawOf(to - Flat(f.Position)), Errand.Stroll);
    }

    /// <summary>Finds someone strolling or standing about nearby and walks to meet them for a chat.</summary>
    private bool TryChat(int i)
    {
        ref var f = ref _folk[i];
        var here = Flat(f.Position);
        int start = _rng.Next(_folk.Length);
        for (int k = 0; k < _folk.Length; k++)
        {
            int j = (start + k) % _folk.Length;
            ref var g = ref _folk[j];
            if (j == i || g.Partner >= 0 || g.Errand != Errand.Stroll || HomeTime(g)
                || (g.State != FolkState.Walking && g.State != FolkState.Waiting))
                continue;
            var there = Flat(g.Position);
            if (Vector2.Distance(here, there) > 22f)
                continue;
            var middle = (here + there) * 0.5f;
            var apart = here - there;
            apart = apart.LengthSquared() > 1e-4f ? Vector2.Normalize(apart) : new Vector2(1f, 0f);
            var mine = middle + apart * 0.6f;
            var theirs = middle - apart * 0.6f;
            if (!CityLife.Clear(mine, FolkRadius + 0.3f) || !CityLife.Clear(theirs, FolkRadius + 0.3f))
                continue;
            WalkTo(i, mine, CityLife.YawOf(-apart), Errand.Chat);
            WalkTo(j, theirs, CityLife.YawOf(apart), Errand.Chat);
            f.Partner = j;
            g.Partner = i;
            return true;
        }
        return false;
    }

    private void Arrive(int i)
    {
        ref var f = ref _folk[i];
        switch (f.Errand)
        {
            case Errand.Sit:
                f.State = FolkState.Sitting;
                f.Position = OnGround(CityLife.Seats[f.Seat].At);
                f.TargetYaw = CityLife.Seats[f.Seat].Yaw;
                f.Timer = R(10f, 30f);
                break;
            case Errand.Shop:
                GoIn(i, f.Door, false);
                break;
            case Errand.Home:
                GoIn(i, f.Door, true);
                break;
            case Errand.Browse:
                f.State = FolkState.Browsing;
                f.Timer = R(4f, 10f);
                break;
            case Errand.Chat:
                f.State = FolkState.Waiting;
                f.Timer = 25f;
                break;
            default:
                Rest(i, R(1f, 4f));
                break;
        }
    }

    private void GoIn(int i, int door, bool forNight)
    {
        ref var f = ref _folk[i];
        Release(i);
        f.State = FolkState.Inside;
        f.Errand = Errand.Stroll;
        f.Door = door;
        f.InForNight = forNight;
        f.Timer = R(12f, 45f);
        f.Speed = 0f;
        f.Position = OnGround(CityLife.Doors[door].At);
        f.Previous = f.Position;
    }

    private void ComeOut(int i)
    {
        ref var f = ref _folk[i];
        f.InForNight = false;
        var door = CityLife.Doors[f.Door >= 0 ? f.Door : 0];
        f.Position = f.Previous = OnGround(door.At);
        f.Yaw = f.PreviousYaw = door.Yaw + MathF.PI;
        Rest(i, R(0.4f, 1.5f));
    }

    /// <summary>Folk standing about shuffle out of the player's way.</summary>
    private void StepAside(ref Vector3 position, ref float speed, float radius, float dt)
    {
        if (!_hasPlayer)
            return;
        var at = Flat(position);
        var away = at - _player;
        float distance = away.Length();
        if (distance >= StepAsideRadius)
            return;
        away = distance > 1e-3f ? away / distance : new Vector2(1f, 0f);
        float step = 1.6f * dt;
        for (int k = 0; k < s_tries.Length; k++)
        {
            var d = Turn(away, s_tries[k]);
            var to = at + d * step;
            if (Vector2.Distance(to, _player) > distance && CityLife.Clear(to, radius))
            {
                position.X = to.X;
                position.Z = to.Y;
                speed = 1.6f;
                return;
            }
        }
    }

    private static Vector2 Turn(Vector2 d, float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
    }

    /// <summary>
    /// One step toward <paramref name="goal"/> round obstacles, staying in Pet City (and out of the
    /// player's way when <paramref name="avoidPlayer"/>). True on arriving.
    /// </summary>
    private bool Walk(ref Vector3 position, ref float yaw, ref float speedOut, Vector2 goal, float speed, float radius,
        float dt, float side, bool avoidPlayer)
    {
        var at = Flat(position);
        var to = goal - at;
        float distance = to.Length();
        float step = speed * dt;
        if (distance <= MathF.Max(step, 0.12f))
        {
            if (CityLife.Clear(goal, radius * 0.5f) || distance < 0.05f)
            {
                position.X = goal.X;
                position.Z = goal.Y;
                return true;
            }
            return false;
        }
        var want = to / distance;
        float look = MathF.Min(0.8f, distance);
        bool nearPlayer = avoidPlayer && _hasPlayer && _playerAlive && Vector2.Distance(at, _player) < PlayerClearance + 1.5f;
        for (int k = 0; k < s_tries.Length; k++)
        {
            var d = Turn(want, s_tries[k] * side);
            var next = at + d * step;
            if (!CityLife.Clear(next, radius) || (look > step && !CityLife.Clear(at + d * look, radius)))
                continue;
            if (nearPlayer)
            {
                float now = Vector2.Distance(at, _player);
                float then = Vector2.Distance(at + d * MathF.Max(look, 0.4f), _player);
                if (then < PlayerClearance && then < now)
                    continue;
                // Already within the clearance: not even this step closer (a path curving round the player).
                if (now < PlayerClearance && Vector2.Distance(next, _player) < now)
                    continue;
            }
            position.X = next.X;
            position.Z = next.Y;
            yaw = CityLife.TurnToward(yaw, CityLife.YawOf(d), dt * 7f);
            speedOut = speed;
            return false;
        }
        return false;
    }

    /// <summary>Walking folk do not walk through each other: a gentle push apart.</summary>
    private void KeepApart(float dt)
    {
        for (int i = 0; i < _folk.Length; i++)
        {
            ref var a = ref _folk[i];
            if (a.State != FolkState.Walking)
                continue;
            for (int j = 0; j < _folk.Length; j++)
            {
                ref var b = ref _folk[j];
                if (j == i || !b.Visible || b.State == FolkState.Sitting)
                    continue;
                var d = Flat(a.Position - b.Position);
                float distance = d.Length();
                if (distance >= 0.7f || distance < 1e-4f)
                    continue;
                var to = Flat(a.Position) + d / distance * (0.7f - distance) * MathF.Min(1f, dt * 4f);
                if (CityLife.Clear(to, FolkRadius))
                    a.Position = OnGround(to);
            }
        }
    }

    // Pets.

    private Vector2 BesideOwner(int owner)
    {
        ref var f = ref _folk[owner];
        var forward = CityLife.Facing(f.Yaw);
        var right = new Vector2(-forward.Y, forward.X);
        return Flat(f.Position) + right * LeashSide - forward * LeashBack;
    }

    private void TickOwned(int i, float dt)
    {
        ref var p = ref _pets[i];
        ref var f = ref _folk[p.Owner];
        p.Speed = 0f;
        if (!f.Visible)
        {
            p.State = CritterState.Inside;
            p.Position = p.Previous = f.Position;
            return;
        }
        if (p.State == CritterState.Inside)
        {
            var out_ = BesideOwner(p.Owner);
            p.Position = p.Previous = OnGround(CityLife.Clear(out_, PetRadius) ? out_ : Flat(f.Position));
            p.Yaw = p.PreviousYaw = f.Yaw;
        }
        var spot = BesideOwner(p.Owner);
        if (f.State == FolkState.Sitting)
        {
            // Sits in front of the bench, by the owner's feet.
            spot = CityLife.SeatApproach[f.Seat] + new Vector2(-CityLife.Facing(f.Yaw).Y, CityLife.Facing(f.Yaw).X) * 0.3f;
        }
        float distance = Vector2.Distance(Flat(p.Position), spot);
        bool owner_moving = f.State == FolkState.Walking && f.Speed > 0f;
        if (distance > 0.15f && CityLife.Clear(spot, PetRadius * 0.5f))
        {
            float speed = MathF.Min(3.2f, MathF.Max(owner_moving ? f.WalkSpeed : 0.8f, distance * 2.5f));
            Walk(ref p.Position, ref p.Yaw, ref p.Speed, spot, speed, PetRadius, dt, 1f, false);
            p.State = CritterState.Follow;
        }
        else if (Vector2.Distance(Flat(p.Position), Flat(f.Position)) > 2.5f)
            Walk(ref p.Position, ref p.Yaw, ref p.Speed, Flat(f.Position), 3f, PetRadius, dt, 1f, false);
        else
        {
            p.State = owner_moving ? CritterState.Follow : CritterState.Sit;
            var look = f.State == FolkState.Sitting || f.State == FolkState.Chatting ? f.Position - p.Position : default;
            float yaw = look.LengthSquared() > 0.01f ? CityLife.YawOf(Flat(look)) : f.Yaw;
            p.Yaw = CityLife.TurnToward(p.Yaw, yaw, dt * 4f);
        }
        p.Position.Y = Ground.HeightAt(p.Position.X, p.Position.Z);
    }

    private void TickRoamer(int i, float dt)
    {
        ref var p = ref _pets[i];
        p.Speed = 0f;
        p.Timer -= dt;
        switch (p.State)
        {
            case CritterState.Wander:
                if (Walk(ref p.Position, ref p.Yaw, ref p.Speed, p.Target, p.Kind == CritterKind.Dog ? 1.4f : 1.1f, PetRadius, dt, 1f, true)
                    || p.Timer <= 0f)
                    StartRoam(i, p.Timer <= 0f ? CritterState.Sniff : CritterState.Wander);
                break;
            case CritterState.Chase:
            {
                ref var prey = ref _pets[p.Other];
                var goal = Flat(prey.Position);
                Walk(ref p.Position, ref p.Yaw, ref p.Speed, goal, 3.6f, PetRadius, dt, 1f, true);
                if (p.Timer <= 0f || prey.State != CritterState.Flee || Vector2.Distance(Flat(p.Position), goal) < 0.55f)
                    EndChase(i);
                break;
            }
            case CritterState.Flee:
            {
                ref var chaser = ref _pets[p.Other];
                var away = Flat(p.Position - chaser.Position);
                away = away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : CityLife.Facing(p.Yaw);
                // Run away, curving round when cornered.
                float before = p.Speed;
                Walk(ref p.Position, ref p.Yaw, ref p.Speed, Flat(p.Position) + away * 3f, 3.3f, PetRadius, dt, (i & 1) == 0 ? 1f : -1f, true);
                _ = before;
                if (chaser.State != CritterState.Chase || chaser.Other != i)
                    StartRoam(i, CritterState.Sniff);
                break;
            }
            default:
                // Sniffing or napping, then something else; shuffles out of the player's way.
                StepAside(ref p.Position, ref p.Speed, PetRadius, dt);
                if (p.Timer <= 0f)
                    DecideRoamer(i);
                break;
        }
        p.Position.Y = Ground.HeightAt(p.Position.X, p.Position.Z);
    }

    private void DecideRoamer(int i)
    {
        float roll = (float)_rng.NextDouble();
        if (IsNight ? roll < 0.6f : roll < 0.22f)
        {
            StartRoam(i, CritterState.Nap);
            return;
        }
        if (roll < 0.55f && !IsNight && TryChase(i))
            return;
        StartRoam(i, roll < 0.75f ? CritterState.Wander : CritterState.Sniff);
    }

    private void StartRoam(int i, CritterState state)
    {
        ref var p = ref _pets[i];
        p.State = state;
        p.Other = -1;
        switch (state)
        {
            case CritterState.Nap:
                p.Timer = R(12f, 35f);
                break;
            case CritterState.Sniff:
                p.Timer = R(1.5f, 4.5f);
                break;
            default:
                // Somewhere not far: a few meters to a dozen.
                var here = Flat(p.Position);
                var to = here;
                for (int k = 0; k < 8; k++)
                {
                    var c = RandomOpen(_rng.Next(2) == 0);
                    if (Vector2.Distance(c, here) < 14f)
                    {
                        to = c;
                        break;
                    }
                    to = c;
                }
                p.Target = to;
                p.Timer = Vector2.Distance(here, to) / 1.1f * 2f + 4f;
                break;
        }
    }

    private bool TryChase(int i)
    {
        ref var p = ref _pets[i];
        for (int j = 0; j < RoamingPetCount; j++)
        {
            ref var q = ref _pets[j];
            if (j == i || q.Kind == CritterKind.Crow || q.State is not (CritterState.Wander or CritterState.Sniff)
                || Vector2.Distance(Flat(p.Position), Flat(q.Position)) > 20f)
                continue;
            p.State = CritterState.Chase;
            p.Other = j;
            p.Timer = R(4f, 7f);
            q.State = CritterState.Flee;
            q.Other = i;
            q.Timer = p.Timer + 1f;
            return true;
        }
        return false;
    }

    private void EndChase(int i)
    {
        ref var p = ref _pets[i];
        int prey = p.Other;
        StartRoam(i, CritterState.Sniff);
        if (prey >= 0 && _pets[prey].State == CritterState.Flee && _pets[prey].Other == i)
            StartRoam(prey, CritterState.Sniff);
    }

    private void TickCrow(int i, float dt)
    {
        ref var p = ref _pets[i];
        p.Speed = 0f;
        p.Timer -= dt;
        switch (p.State)
        {
            case CritterState.Fly:
            {
                p.Progress = MathF.Min(1f, p.Progress + dt / p.FlightSeconds);
                float t = p.Progress;
                var line = Vector3.Lerp(p.From, p.To, t);
                float arc = MathF.Sin(t * MathF.PI) * (1.5f + Vector3.Distance(p.From, p.To) * 0.12f);
                p.Position = line + new Vector3(0f, arc, 0f);
                p.Speed = Vector3.Distance(p.From, p.To) / p.FlightSeconds;
                var d = p.To - p.From;
                if (d.X * d.X + d.Z * d.Z > 1e-4f)
                    p.Yaw = CityLife.TurnToward(p.Yaw, CityLife.YawOf(new Vector2(d.X, d.Z)), dt * 6f);
                if (t >= 1f)
                {
                    p.Position = p.To;
                    p.State = p.Roof >= 0 ? CritterState.Perch : CritterState.Peck;
                    p.Timer = p.Roof >= 0 ? R(8f, 25f) : R(1f, 3f);
                }
                return;
            }
            case CritterState.Perch:
                if (p.Timer <= 0f && !IsNight)
                    FlyTo(i, OnGround(RandomOpen(true)), -1);
                else if (p.Timer <= 0f)
                    p.Timer = R(5f, 20f);
                return;
            case CritterState.Hop:
                if (Walk(ref p.Position, ref p.Yaw, ref p.Speed, p.Target, 0.9f, 0.15f, dt, 1f, true) || p.Timer <= 0f)
                {
                    p.State = CritterState.Peck;
                    p.Timer = R(0.8f, 2.5f);
                }
                break;
            default:
                if (p.Timer <= 0f)
                    DecideCrow(i);
                break;
        }
        p.Position.Y = Ground.HeightAt(p.Position.X, p.Position.Z);
        // Startled by the player: off to a rooftop.
        if (_hasPlayer && Vector2.Distance(Flat(p.Position), _player) < CrowStartle)
            FlyToRoof(i);
    }

    private void DecideCrow(int i)
    {
        ref var p = ref _pets[i];
        float roll = (float)_rng.NextDouble();
        if (IsNight || roll < 0.2f)
        {
            FlyToRoof(i);
            return;
        }
        if (roll < 0.3f)
        {
            FlyTo(i, OnGround(RandomOpen(true)), -1);
            return;
        }
        if (roll < 0.7f)
        {
            var here = Flat(p.Position);
            var to = here + Turn(new Vector2(1f, 0f), R(-MathF.PI, MathF.PI)) * R(0.8f, 2.5f);
            if (CityLife.Clear(to, 0.2f))
            {
                p.State = CritterState.Hop;
                p.Target = to;
                p.Timer = 4f;
                return;
            }
        }
        p.State = CritterState.Peck;
        p.Timer = R(1f, 3f);
    }

    private void FlyToRoof(int i)
    {
        int roof = _rng.Next(CityLife.Roofs.Length);
        FlyTo(i, CityLife.Roofs[roof], roof);
    }

    private void FlyTo(int i, Vector3 to, int roof)
    {
        ref var p = ref _pets[i];
        p.State = CritterState.Fly;
        p.From = p.Position;
        p.To = to;
        p.Roof = roof;
        p.Progress = 0f;
        p.FlightSeconds = MathF.Max(1.2f, Vector3.Distance(p.From, to) / 7f);
    }

    // Butterflies.

    private void TickButterflies(float dt)
    {
        ButterfliesOut = Math.Clamp(ButterfliesOut + (IsNight ? -dt : dt) / 3f, 0f, 1f);
        if (ButterfliesOut <= 0f)
            return;
        var flowers = CityLife.Flowers;
        for (int i = 0; i < _butterflies.Length; i++)
        {
            ref var b = ref _butterflies[i];
            b.Previous = b.Position;
            b.Timer -= dt;
            if (b.Timer <= 0f)
            {
                // Now and then off to another flower.
                if (_rng.Next(8) == 0)
                {
                    int next = _rng.Next(flowers.Length);
                    if (Vector3.Distance(flowers[next], flowers[b.Flower]) < MaxFlowerHop)
                        b.Flower = next;
                }
                var flower = flowers[b.Flower];
                b.Target = flower + new Vector3(R(-1.6f, 1.6f), R(0.3f, 1.5f), R(-1.6f, 1.6f));
                b.Timer = R(0.6f, 1.8f);
            }
            var to = b.Target - b.Position;
            float distance = to.Length();
            var want = distance > 1e-3f ? to / distance * MathF.Min(1.6f, distance * 2f + 0.4f) : Vector3.Zero;
            // A fluttery zigzag on top.
            var flutter = new Vector3(R(-1f, 1f), R(-1.2f, 1.2f), R(-1f, 1f)) * 2.5f;
            b.Velocity += (want - b.Velocity) * MathF.Min(1f, dt * 3f) + flutter * dt;
            b.Position += b.Velocity * dt;
            float floor = Ground.HeightAt(b.Position.X, b.Position.Z) + 0.25f;
            if (b.Position.Y < floor)
                b.Position.Y = floor;
        }
    }
}
