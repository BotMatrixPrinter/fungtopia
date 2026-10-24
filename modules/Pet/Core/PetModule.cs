using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fungtopia.Map;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.StartScreen;

namespace Fungtopia.Pet;

/// <summary>
/// The player's pets: the starter (a Mushroom Cat or a Mushroom Dog, chosen on the start screen
/// and given by <see cref="GameStart"/>; no pet before it) and any adopted with
/// <see cref="AdoptPet"/> (the Mushroom Crow). Only the active pet is out with the player: it
/// follows them at about 2 m (the Crow hovers about 2 m up beside them), catches up when far and
/// appears beside them when over 30 m away or when they are teleported; goes for the nearest
/// alive monster within 8 m of the player, bites it every second (the Crow swoops down and pecks
/// every 0.8 s) and comes back; runs around obstacles on the Map's walkable ground; sits by the
/// defeated player. Publishes <see cref="PetState"/> and <see cref="OwnedPets"/> every tick and
/// <see cref="HitMonster"/> for each bite or peck. Saves the owned and active pets in the "Pet"
/// part of a save game (<see cref="CollectSave"/>, <see cref="RestorePart"/>).
/// </summary>
public sealed class PetModule : IModule, IMessageHandler<PlayerState>, IMessageHandler<MonsterState>,
    IMessageHandler<TeleportPlayer>, IMessageHandler<AdoptPet>, IMessageHandler<SetActivePet>,
    IMessageHandler<GameStart>, IMessageHandler<CollectSave>, IMessageHandler<RestorePart>
{
    /// <summary>The version written in the "Pet" save part; restoring any other one keeps the current state.</summary>
    public const int SaveVersion = 1;

    /// <summary>How far from the player the pet waits, meters.</summary>
    public const float FollowDistance = 2f;
    /// <summary>A following pet stays put until the player is this much farther than <see cref="FollowDistance"/>.</summary>
    public const float FollowSlack = 0.5f;
    /// <summary>Running speed near the player, meters per second (the player walks at 5).</summary>
    public const float TrotSpeed = 6f;
    /// <summary>Running speed when far behind, meters per second.</summary>
    public const float CatchUpSpeed = 9f;
    /// <summary>Running speed toward a monster, meters per second.</summary>
    public const float AttackSpeed = 7f;
    /// <summary>Over this horizontal distance from the player the pet appears beside them.</summary>
    public const float TeleportDistance = 30f;
    /// <summary>The pet goes for monsters within this horizontal distance of the player.</summary>
    public const float AttackRange = 8f;
    /// <summary>It gives up on a target this far from the player.</summary>
    public const float GiveUpRange = 10f;
    /// <summary>Gap between the pet's and the monster's footprints within which it can bite, meters.</summary>
    public const float BiteReach = 0.4f;
    /// <summary>Seconds between the Cat's and the Dog's bites.</summary>
    public const float BiteInterval = 1f;
    /// <summary>Seconds between the Crow's pecks.</summary>
    public const float PeckInterval = 0.8f;
    public const int BiteDamage = 1;
    /// <summary><see cref="HitMonster.Source"/> of the pet's bites and pecks.</summary>
    public const int BiteSource = 1;
    /// <summary>How far from the defeated player the pet sits, meters.</summary>
    public const float SitDistance = 1.1f;
    /// <summary>The pet's round footprint, meters.</summary>
    public const float Radius = 0.35f;
    /// <summary>Fastest turn, radians per second.</summary>
    public const float TurnSpeed = 12f;
    /// <summary>Extra room the pet keeps from obstacles when steering around them, meters.</summary>
    public const float Clearance = 0.3f;
    /// <summary>How far ahead the pet looks for obstacles in its way, meters.</summary>
    public const float LookAhead = 3f;
    /// <summary>Monsters remembered per tick (more are ignored).</summary>
    public const int MaxMonsters = 128;
    /// <summary>How high above the ground the Crow flies while following, meters.</summary>
    public const float HoverHeight = 2f;
    /// <summary>The Crow pecks once it has swooped down to this height above the ground, meters.</summary>
    public const float PeckHeight = 0.4f;
    /// <summary>How far to the player's right the Crow hovers, meters.</summary>
    public const float CrowSide = 1.2f;
    /// <summary>How fast the Crow climbs back up, meters per second.</summary>
    public const float ClimbSpeed = 3f;
    /// <summary>How fast the Crow swoops down, meters per second.</summary>
    public const float SwoopSpeed = 6f;
    /// <summary>The Crow starts swooping down when this much farther from its target than biting reach, meters.</summary>
    public const float SwoopStart = 1.5f;

    private IMessageBus? _bus;
    private Vector3 _playerPosition;
    private float _playerFacing;
    private bool _playerAlive = true;
    private bool _hasPlayer;
    private readonly MonsterState[] _monsters = new MonsterState[MaxMonsters];
    private int _monsterCount;
    private float _biteCooldown;
    private bool _following;
    private int _avoidIndex = -1;
    private float _avoidSide;
    private bool _ownsCat, _ownsDog, _ownsCrow;
    private Vector2 _xz;
    /// <summary>Appeared beside the player between ticks (a teleport, a pet switch): the next tick reports it.</summary>
    private bool _teleportPending;

    /// <summary>Where a save part that cannot be restored is reported (one line each).</summary>
    public Action<string> Log { get; set; } = Console.WriteLine;

    public PetModule()
    {
        var door = Places.HomeDoor;
        _xz = Beside(door.Position, door.Yaw, 1.5f);
        Altitude = 0f;
        SetPosition();
    }

    /// <summary>
    /// Whether the game has started (<see cref="GameStart"/> came): before it the player owns no
    /// pet, none is out and nothing is published.
    /// </summary>
    public bool Started { get; private set; }

    /// <summary>The active pet: the one that follows the player and fights.</summary>
    public PetKind Kind { get; private set; } = PetKind.Cat;

    /// <summary>The owned pets and the active one, as published every tick.</summary>
    public OwnedPets Owned => new(_ownsCat, _ownsDog, _ownsCrow, Kind);

    public bool Owns(PetKind kind) => Owned.Owns(kind);

    /// <summary>Where the active pet's feet are (in the air while the Crow flies).</summary>
    public Vector3 Position { get; private set; }
    public float Facing { get; private set; }

    /// <summary>Height of its feet above the ground, meters (0 except for the flying Crow).</summary>
    public float Altitude { get; private set; }

    /// <summary>Velocity this tick, meters per second (zero on a teleport).</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>The monster it goes for, or -1.</summary>
    public int TargetId { get; private set; } = -1;

    public bool Attacking => TargetId >= 0;

    /// <summary>Sitting by the defeated player (the Crow lands to sit).</summary>
    public bool Sitting { get; private set; }

    /// <summary>Whether it appeared beside the player on the latest tick.</summary>
    public bool Teleported { get; private set; }

    /// <summary>Seconds since its last bite or peck (the view pounces or pecks on one).</summary>
    public float SinceBite { get; private set; } = 100f;

    /// <summary>Number of ticks run; the view uses it to see new ticks.</summary>
    public long Ticks { get; private set; }

    /// <summary>The state published on the latest tick.</summary>
    public PetState State { get; private set; }

    /// <summary>Seconds between attacks of a pet: the Crow pecks a little faster than the others bite.</summary>
    public static float AttackInterval(PetKind kind) => kind == PetKind.Crow ? PeckInterval : BiteInterval;

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<MonsterState>(this);
        bus.Subscribe<TeleportPlayer>(this);
        bus.Subscribe<AdoptPet>(this);
        bus.Subscribe<SetActivePet>(this);
        bus.Subscribe<GameStart>(this);
        bus.Subscribe<CollectSave>(this);
        bus.Subscribe<RestorePart>(this);
        State = MakeState();
    }

    /// <summary>
    /// Puts the pet at (x, z) of <paramref name="feet"/>: on the ground, or hovering
    /// <see cref="HoverHeight"/> above it for the Crow.
    /// </summary>
    public void PlaceAt(Vector3 feet, float facing = 0f)
    {
        _xz = new Vector2(feet.X, feet.Z);
        Altitude = Kind == PetKind.Crow ? HoverHeight : 0f;
        SetPosition();
        Facing = WrapAngle(facing);
        Velocity = Vector3.Zero;
        _following = false;
        State = MakeState();
    }

    public void Handle(in PlayerState message)
    {
        _playerPosition = message.Position;
        _playerFacing = message.Facing;
        _playerAlive = message.Alive;
        _hasPlayer = true;
    }

    public void Handle(in MonsterState message)
    {
        if (_monsterCount < _monsters.Length)
            _monsters[_monsterCount++] = message;
    }

    /// <summary>The player is moved at once (fast travel, the house door, the clinic): the pet comes along.</summary>
    public void Handle(in TeleportPlayer message)
    {
        var p = message.Position;
        _playerPosition = OnGround(p.X, p.Z);
        _playerFacing = message.Facing;
        _playerAlive = true;
        _hasPlayer = true;
        if (!Started)
            return;
        TargetId = -1;
        Sitting = false;
        TeleportBeside(_playerPosition);
        _teleportPending = true;
        State = MakeState();
    }

    /// <summary>
    /// The game starts: the player owns only the chosen starter (<see cref="GameStart.Dog"/> is the
    /// Dog, anything else the Cat), which comes out beside them. Only the first one counts.
    /// </summary>
    public void Handle(in GameStart message)
    {
        if (Started)
            return;
        Started = true;
        var kind = message.StarterPet == GameStart.Dog ? PetKind.Dog : PetKind.Cat;
        _ownsCat = kind == PetKind.Cat;
        _ownsDog = kind == PetKind.Dog;
        _ownsCrow = false;
        Kind = kind;
        Altitude = 0f;
        if (_hasPlayer)
        {
            TeleportBeside(_playerPosition);
            _teleportPending = true;
        }
        else
            SetPosition();
        State = MakeState();
    }

    public void Handle(in AdoptPet message)
    {
        if (!Started || !Defined(message.Kind))
            return;
        switch (message.Kind)
        {
            case PetKind.Cat: _ownsCat = true; break;
            case PetKind.Dog: _ownsDog = true; break;
            case PetKind.Crow: _ownsCrow = true; break;
        }
        SwitchTo(message.Kind);
    }

    public void Handle(in SetActivePet message)
    {
        if (Defined(message.Kind) && Owns(message.Kind))
            SwitchTo(message.Kind);
    }

    /// <summary>The active pet goes home; <paramref name="kind"/> comes out beside the player.</summary>
    private void SwitchTo(PetKind kind)
    {
        if (kind == Kind)
            return;
        Kind = kind;
        TargetId = -1;
        Sitting = false;
        _biteCooldown = 0f;
        SinceBite = 100f;
        Altitude = kind == PetKind.Crow ? HoverHeight : 0f;
        if (_hasPlayer)
        {
            TeleportBeside(_playerPosition);
            _teleportPending = true;
        }
        else
            SetPosition();
        State = MakeState();
    }

    /// <summary>The "Pet" save part: owned pets and the active one (null before the game starts).</summary>
    private sealed record SaveData(
        [property: JsonPropertyName("v")] int Version,
        [property: JsonPropertyName("cat")] bool Cat,
        [property: JsonPropertyName("dog")] bool Dog,
        [property: JsonPropertyName("crow")] bool Crow,
        [property: JsonPropertyName("active")] string? Active);

    private static readonly JsonSerializerOptions SaveJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    /// <summary>Answers a save at once with the owned pets and the active one.</summary>
    public void Handle(in CollectSave message)
    {
        var data = new SaveData(SaveVersion, _ownsCat, _ownsDog, _ownsCrow, Started ? Kind.ToString() : null);
        _bus?.Publish(new SavePart(message.RequestId, SaveModules.Pet, JsonSerializer.Serialize(data, SaveJson)));
    }

    /// <summary>
    /// Restores the owned and active pets of a loaded game, quietly: nothing is published (no
    /// adoption, no reward); the active pet just appears beside the player. A part that cannot be
    /// read (bad JSON, another version, an active pet not owned) keeps the current state and is logged.
    /// </summary>
    public void Handle(in RestorePart message)
    {
        if (message.Module != SaveModules.Pet)
            return;
        SaveData? data;
        try
        {
            data = message.Json == null ? null : JsonSerializer.Deserialize<SaveData>(message.Json, SaveJson);
        }
        catch (JsonException)
        {
            data = null;
        }
        if (data == null || data.Version != SaveVersion)
        {
            Log($"Fungtopia: Pet: cannot restore the saved pets (unreadable or version {data?.Version}); keeping the current ones.");
            return;
        }
        bool none = !data.Cat && !data.Dog && !data.Crow;
        PetKind kind = PetKind.Cat;
        bool valid = data.Active == null
            ? none
            : Enum.TryParse(data.Active, false, out kind) && Defined(kind)
              && new OwnedPets(data.Cat, data.Dog, data.Crow, kind).Owns(kind);
        if (!valid)
        {
            Log($"Fungtopia: Pet: cannot restore the saved pets (active pet '{data.Active}' not owned); keeping the current ones.");
            return;
        }
        Started = data.Active != null;
        _ownsCat = data.Cat;
        _ownsDog = data.Dog;
        _ownsCrow = data.Crow;
        Kind = kind;
        TargetId = -1;
        Sitting = false;
        _biteCooldown = 0f;
        SinceBite = 100f;
        _following = false;
        Altitude = Started && kind == PetKind.Crow ? HoverHeight : 0f;
        if (Started && _hasPlayer)
        {
            TeleportBeside(_playerPosition);
            _teleportPending = true;
        }
        else
            SetPosition();
        State = MakeState();
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("PetModule.Tick before Start.");
        float dt = (float)context.StepSeconds;
        Ticks++;
        Teleported = _teleportPending;
        _teleportPending = false;
        if (!Started)
        {
            _monsterCount = 0;
            return;
        }
        SinceBite += dt;
        if (_biteCooldown > 0f)
            _biteCooldown -= dt;

        Vector3 start = Position;
        if (_hasPlayer)
            Think(dt);
        Velocity = Teleported || dt <= 0f ? Vector3.Zero : (Position - start) / dt;

        _monsterCount = 0; // the Monsters module publishes every monster again next tick
        State = MakeState();
        _bus.Publish(State);
        var owned = Owned;
        _bus.Publish(owned);
    }

    private void Think(float dt)
    {
        Vector3 player = _playerPosition;
        bool crow = Kind == PetKind.Crow;
        if (Flat(Position, player) > TeleportDistance)
            TeleportBeside(player);

        if (!_playerAlive)
        {
            TargetId = -1;
            _following = false;
            if (!Sitting)
                Sitting = !MoveToward(player, SitDistance, Speed(Flat(Position, player) - SitDistance), dt);
            // The Crow lands beside the player to sit.
            Fly(0f, dt);
            if (Sitting)
                TurnToward(Yaw(player - Position), dt);
            return;
        }
        Sitting = false;

        ChooseTarget(player);
        if (TargetId >= 0)
        {
            ref readonly var m = ref FindMonster(TargetId);
            float reach = m.Radius + Radius + BiteReach;
            if (Flat(Position, m.Position) > reach)
                MoveToward(m.Position, reach - 0.1f, AttackSpeed, dt);
            float gap = Flat(Position, m.Position);
            // The Crow swoops down as it closes in and pecks once it is low enough.
            if (crow)
                Fly(gap <= reach + SwoopStart ? PeckHeight : HoverHeight, dt);
            if (gap <= reach)
            {
                TurnToward(Yaw(m.Position - Position), dt);
                if (_biteCooldown <= 1e-4f && (!crow || Altitude <= PeckHeight + 1e-3f))
                {
                    _biteCooldown = AttackInterval(Kind);
                    SinceBite = 0f;
                    _bus!.Publish(new HitMonster(m.MonsterId, BiteDamage, 0f, 0f, BiteSource));
                }
            }
            _following = false;
            return;
        }

        // Following: wait near the player (the Crow beside their right shoulder), start again
        // once they are a little farther away.
        Vector3 goal = player;
        float stopAt = FollowDistance;
        if (crow)
        {
            var spot = new Vector2(player.X, player.Z) + Right(_playerFacing) * CrowSide;
            goal = new Vector3(spot.X, player.Y, spot.Y);
            stopAt = 0f;
            Fly(HoverHeight, dt);
        }
        float distance = Flat(Position, goal);
        if (!_following && distance > stopAt + FollowSlack)
            _following = true;
        if (_following)
            _following = MoveToward(goal, stopAt, Speed(distance - stopAt), dt);
        if (!_following)
            TurnToward(crow ? _playerFacing : Yaw(player - Position), dt * 0.4f);
    }

    /// <summary>Climbs or swoops toward <paramref name="height"/> above the ground (the Crow; others stay on it).</summary>
    private void Fly(float height, float dt)
    {
        if (Kind != PetKind.Crow)
            height = 0f;
        float rate = height > Altitude ? ClimbSpeed : SwoopSpeed;
        float diff = height - Altitude;
        Altitude += Math.Clamp(diff, -rate * dt, rate * dt);
        SetPosition();
    }

    /// <summary>Keeps a target while it is alive and near the player; else picks the nearest one to the player.</summary>
    private void ChooseTarget(Vector3 player)
    {
        if (TargetId >= 0)
        {
            int i = IndexOf(TargetId);
            if (i >= 0 && _monsters[i].Alive && Flat(_monsters[i].Position, player) <= GiveUpRange)
                return;
            TargetId = -1;
        }
        float best = AttackRange;
        for (int i = 0; i < _monsterCount; i++)
        {
            ref readonly var m = ref _monsters[i];
            if (!m.Alive)
                continue;
            float d = Flat(m.Position, player);
            if (d <= best)
            {
                best = d;
                TargetId = m.MonsterId;
            }
        }
    }

    private int IndexOf(int monsterId)
    {
        for (int i = 0; i < _monsterCount; i++)
            if (_monsters[i].MonsterId == monsterId)
                return i;
        return -1;
    }

    private ref readonly MonsterState FindMonster(int monsterId) => ref _monsters[IndexOf(monsterId)];

    /// <summary>Running speed for a pet <paramref name="behind"/> meters short of where it wants to be.</summary>
    private static float Speed(float behind)
    {
        float top = TrotSpeed + (CatchUpSpeed - TrotSpeed) * Math.Clamp((behind - 3f) / 4f, 0f, 1f);
        // Slows down as it arrives, so it settles instead of stopping dead.
        return Math.Clamp(behind * 4f, 1.5f, top);
    }

    /// <summary>
    /// Runs toward <paramref name="goal"/> until <paramref name="stopAt"/> meters from it,
    /// steering around obstacles and staying on walkable ground. Returns whether it still has a way to go.
    /// </summary>
    private bool MoveToward(Vector3 goal, float stopAt, float speed, float dt)
    {
        var pos = _xz;
        var to = new Vector2(goal.X, goal.Z) - pos;
        float distance = to.Length();
        float left = distance - stopAt;
        if (left <= 0.01f)
            return false;
        Vector2 dir = Steer(pos, to / distance, distance);
        float step = MathF.Min(speed * dt, left);
        Vector2 next = pos + dir * step;
        ResolveCollisions(ref next);
        if (!Allowed(pos, next))
        {
            // At the edge of the walkable ground: slide along it.
            var alongX = new Vector2(next.X, pos.Y);
            var alongZ = new Vector2(pos.X, next.Y);
            next = Allowed(pos, alongX) ? alongX : Allowed(pos, alongZ) ? alongZ : pos;
        }
        if ((next - pos).LengthSquared() > 1e-8f)
            TurnToward(Yaw(new Vector3(next.X - pos.X, 0f, next.Y - pos.Y)), dt);
        _xz = next;
        SetPosition();
        return true;
    }

    /// <summary>Whether it may step from <paramref name="from"/> to <paramref name="to"/>: onto walkable ground, or at least back toward it.</summary>
    private static bool Allowed(Vector2 from, Vector2 to) =>
        !Ground.Blocks(to.X, to.Y, Radius - 1e-3f)
        && (Areas.Walkable(to.X, to.Y, Radius) || Areas.Depth(to.X, to.Y) > Areas.Depth(from.X, from.Y));

    /// <summary>
    /// The direction to run in: <paramref name="dir"/>, or past the edge of the nearest obstacle in
    /// the way (kept on the same side as long as that obstacle is in the way).
    /// </summary>
    private Vector2 Steer(Vector2 pos, Vector2 dir, float distance)
    {
        float look = MathF.Min(distance, LookAhead);
        int hit = -1;
        float hitAlong = float.MaxValue;
        var all = Ground.Obstacles;
        for (int i = 0; i < all.Length; i++)
        {
            var rel = all[i].Center - pos;
            float r = all[i].Radius + Radius + Clearance;
            float along = Vector2.Dot(rel, dir);
            // Behind, beyond the goal, past where it looks, or beside the way.
            if (along <= 0f || along > distance || along - r > look)
                continue;
            float across = dir.X * rel.Y - dir.Y * rel.X;
            if (MathF.Abs(across) >= r || along >= hitAlong)
                continue;
            hit = i;
            hitAlong = along;
        }
        if (hit < 0)
        {
            _avoidIndex = -1;
            return dir;
        }

        var o = all[hit];
        var toCenter = o.Center - pos;
        float d = toCenter.Length();
        float rr = o.Radius + Radius + Clearance;
        var u = toCenter / d;
        // Graze the edge of the padded obstacle; once inside the padding, run along it and a little out.
        float angle = d > rr ? MathF.Asin(rr / d) : MathF.PI * 0.5f + 0.25f;
        if (hit != _avoidIndex)
        {
            _avoidIndex = hit;
            float across = dir.X * toCenter.Y - dir.Y * toCenter.X;
            // The obstacle is left of the way (across > 0): go right of it, turning clockwise from it.
            _avoidSide = across >= 0f ? -1f : 1f;
        }
        float a = angle * _avoidSide;
        float sin = MathF.Sin(a), cos = MathF.Cos(a);
        return new Vector2(u.X * cos - u.Y * sin, u.X * sin + u.Y * cos);
    }

    /// <summary>Pushes (x, z) out of every obstacle it overlaps.</summary>
    private static void ResolveCollisions(ref Vector2 p)
    {
        for (int pass = 0; pass < 3 && Ground.Blocks(p.X, p.Y, Radius); pass++)
        {
            foreach (var o in Ground.Obstacles)
            {
                float dx = p.X - o.X, dz = p.Y - o.Z, r = o.Radius + Radius;
                float d2 = dx * dx + dz * dz;
                if (d2 >= r * r)
                    continue;
                float d = MathF.Sqrt(d2);
                if (d < 1e-5f)
                {
                    dx = 0f;
                    dz = 1f;
                    d = 1f;
                }
                float push = (r + 1e-4f) / d;
                p = new Vector2(o.X + dx * push, o.Z + dz * push);
            }
        }
    }

    /// <summary>Appears beside the player (the Crow up in the air): to their right, left, behind or in front.</summary>
    private void TeleportBeside(Vector3 player)
    {
        _xz = Beside(player, _playerFacing, Kind == PetKind.Crow ? CrowSide : 1.5f);
        Altitude = Kind == PetKind.Crow ? HoverHeight : 0f;
        SetPosition();
        Facing = WrapAngle(_playerFacing);
        _following = false;
        _avoidIndex = -1;
        Teleported = true;
    }

    /// <summary>A free spot <paramref name="gap"/> (or 2, 3 times that) to the right, left, behind or in front of (x, z) of <paramref name="at"/>.</summary>
    private static Vector2 Beside(Vector3 at, float facing, float gap)
    {
        var right = Right(facing);
        var forward = new Vector2(-MathF.Sin(facing), -MathF.Cos(facing));
        var center = new Vector2(at.X, at.Z);
        for (int ring = 1; ring <= 3; ring++)
        {
            float d = gap * ring;
            Vector2 spot = center;
            if (Free(center + right * d, ref spot) || Free(center - right * d, ref spot)
                || Free(center - forward * d, ref spot) || Free(center + forward * d, ref spot))
                return spot;
        }
        return center;
    }

    private static bool Free(Vector2 p, ref Vector2 spot)
    {
        if (!Areas.Walkable(p.X, p.Y, Radius) || Ground.Blocks(p.X, p.Y, Radius + Clearance))
            return false;
        spot = p;
        return true;
    }

    /// <summary>The right of a yaw <paramref name="facing"/> in (x, z).</summary>
    private static Vector2 Right(float facing) => new(MathF.Cos(facing), -MathF.Sin(facing));

    private void SetPosition()
    {
        var g = OnGround(_xz.X, _xz.Y);
        Position = new Vector3(g.X, g.Y + Altitude, g.Z);
    }

    private void TurnToward(float target, float dt)
    {
        float diff = WrapAngle(target - Facing);
        float step = TurnSpeed * dt;
        Facing = WrapAngle(Facing + Math.Clamp(diff, -step, step));
    }

    /// <summary>The yaw (Godot's convention) of a horizontal direction.</summary>
    private static float Yaw(Vector3 direction) => MathF.Atan2(-direction.X, -direction.Z);

    private static float Flat(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    private static Vector3 OnGround(float x, float z) => new(x, Ground.HeightAt(x, z), z);

    private static bool Defined(PetKind kind) => kind is PetKind.Cat or PetKind.Dog or PetKind.Crow;

    /// <summary>The angle in (-π, π].</summary>
    public static float WrapAngle(float a)
    {
        if (!float.IsFinite(a))
            return 0f;
        a %= MathF.Tau;
        if (a > MathF.PI)
            a -= MathF.Tau;
        else if (a <= -MathF.PI)
            a += MathF.Tau;
        return a;
    }

    private PetState MakeState() => new(Kind, Position, Facing, Attacking);
}
