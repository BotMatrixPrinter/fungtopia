using System;
using System.Numerics;
using System.Text.Json;
using Fungtopia.Bow;
using Fungtopia.Dialogue;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Fungtopia.Wardrobe;

namespace Fungtopia.Player;

/// <summary>
/// The player character in the whole world (Fungtopia.Map): starts at the home door in Mushroom
/// Town, walks relative to the camera, turns smoothly, jumps, follows the ground, slides along
/// trunks, buildings and the edge of the walkable ground, stands still while a dialogue is open,
/// takes damage, stays down after defeat until the next <see cref="TeleportPlayer"/>.
/// Hidden until the start screen opens; shown standing idle at the home door, facing the camera's
/// front view, while it is open; in play from its <see cref="GameStart"/>, which picks the character.
/// While the wardrobe or the shop is open (<see cref="WardrobeState.Open"/>) it stands idle too,
/// turned for viewing by the Wardrobe's <see cref="PreviewYaw"/> (<see cref="ShownFacing"/>).
/// Publishes <see cref="PlayerState"/> every tick and <see cref="PlayerDefeated"/> once per defeat.
/// Saved games: answers <see cref="CollectSave"/> with its <see cref="SaveModules.Player"/> part and
/// restores it from <see cref="RestorePart"/>.
/// </summary>
public sealed class PlayerModule : IModule, IMessageHandler<PlayerInput>, IMessageHandler<DamagePlayer>,
    IMessageHandler<TeleportPlayer>, IMessageHandler<DialogueState>, IMessageHandler<BowState>,
    IMessageHandler<GameStart>, IMessageHandler<StartScreenState>, IMessageHandler<AppearanceState>,
    IMessageHandler<WardrobeState>, IMessageHandler<PreviewYaw>, IMessageHandler<CollectSave>, IMessageHandler<RestorePart>
{
    public const float WalkSpeed = 5f;
    public const float JumpHeight = 1.2f;
    public const float Gravity = 20f;
    /// <summary>Fastest turn, radians per second.</summary>
    public const float TurnSpeed = 10f;
    /// <summary>The player's round footprint, meters.</summary>
    public const float Radius = 0.4f;
    public const int MaxHealth = 10;
    public const float InvulnerableAfterHit = 0.7f;
    /// <summary>How fast <see cref="PreviewTurn"/> eases toward the latest <see cref="PreviewYaw"/>, per second.</summary>
    public const float PreviewTurnSharpness = 12f;
    /// <summary>The version of the saved part (<see cref="SaveJson"/>); other versions are not restored.</summary>
    public const int SaveVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>The saved part: feet position, area and facing, character and health.</summary>
    private sealed record SavedPlayer(int Version, float X, float Y, float Z, float Facing, string? Area, string? Character, int Health);

    private IMessageBus? _bus;
    private PlayerInput _input;
    private bool _jumpQueued;
    /// <summary>Look.X (yaw right) added up since the last tick.</summary>
    private float _lookX;
    private bool _defeatPending;
    private bool _talking;
    private float _bowDraw;
    private bool _screenOpen;
    private bool _wardrobeOpen;
    private float _previewYaw;
    private bool _started;
    private bool _loggedBadSave;

    /// <summary>Where a save that cannot be restored is told (once).</summary>
    public Action<string> Log { get; set; } = Console.WriteLine;

    /// <summary>The setting that picks the character (Girl by default) until <see cref="GameStart"/> picks it.</summary>
    public CharacterKind Character { get; set; } = CharacterKind.Girl;

    public Vector3 Position { get; private set; } = Places.HomeDoor.Position;
    public float Facing { get; private set; } = Places.HomeDoor.Yaw;
    public Vector3 Velocity { get; private set; }
    public bool Grounded { get; private set; } = true;
    public int Health { get; private set; } = MaxHealth;
    public bool Alive { get; private set; } = true;

    /// <summary>
    /// Whether the view draws the player: while <see cref="StartScreenState.Open"/> (standing idle
    /// at the home door for character creation) and from <see cref="GameStart"/> on; false only
    /// before the start screen opens.
    /// </summary>
    public bool Visible => _started || _screenOpen;

    /// <summary>Whether <see cref="GameStart"/> has come: until then damage is ignored.</summary>
    public bool Started => _started;

    /// <summary>Whether the player takes input (and draws the bow): started, the start screen and the wardrobe closed.</summary>
    private bool InPlay => _started && !_screenOpen && !_wardrobeOpen;

    /// <summary>Whether a screen that shows the character (start screen, wardrobe, shop) is open: it stands idle.</summary>
    public bool OnScreen => _screenOpen || _wardrobeOpen;

    /// <summary>
    /// How far the character is turned for viewing from <see cref="Facing"/>, radians (positive turns
    /// left, like yaw): eased toward the latest <see cref="PreviewYaw"/> while <see cref="OnScreen"/>,
    /// back toward 0 once closed. <see cref="Facing"/> itself (the camera's front view) does not turn.
    /// </summary>
    public float PreviewTurn { get; private set; }

    /// <summary>The yaw the view turns the model to: <see cref="Facing"/> + <see cref="PreviewTurn"/>.</summary>
    public float ShownFacing => WrapAngle(Facing + PreviewTurn);

    /// <summary>The camera's yaw as the player knows it (Godot's convention, like <see cref="Facing"/>).</summary>
    public float CameraYaw { get; private set; }

    /// <summary>Seconds of invulnerability left after a hit (the view flashes meanwhile).</summary>
    public float InvulnerableSeconds { get; private set; }

    /// <summary>Seconds since defeat while not <see cref="Alive"/>.</summary>
    public float DefeatedSeconds { get; private set; }

    /// <summary>Number of ticks run; the view uses it to see new ticks.</summary>
    public long Ticks { get; private set; }

    /// <summary>The archery pose, advanced every tick from the latest <see cref="BowState.Draw"/>.</summary>
    public ArcheryState Archery { get; private set; } = ArcheryState.Relaxed;

    /// <summary>The latest input's aim (<c>PlayerInput.AimDirection</c>), valid when <see cref="HasAim"/>.</summary>
    public Vector3 AimDirection => _input.AimDirection;

    /// <summary>The latest <see cref="BowState.Target"/> (zero before any): where the nocked arrow points (<see cref="ArcheryPose.ArrowAim"/>).</summary>
    public Vector3 BowTarget { get; private set; }

    /// <summary>The latest input's <c>PlayerInput.HasAim</c>.</summary>
    public bool HasAim => _input.HasAim;

    /// <summary>The latest <see cref="AppearanceState"/> (the Wardrobe's), valid when <see cref="HasAppearance"/>.</summary>
    public AppearanceState Appearance { get; private set; }

    /// <summary>Whether an <see cref="AppearanceState"/> has arrived; until then the character keeps its built-in look.</summary>
    public bool HasAppearance { get; private set; }

    /// <summary>Counts the changes of <see cref="Appearance"/>, so the view redresses the character only when it changes.</summary>
    public int AppearanceVersion { get; private set; }

    /// <summary>What the character looks like: <see cref="Appearance"/> on <see cref="Character"/>, or its built-in look.</summary>
    public CharacterLook Look => HasAppearance ? CharacterLook.From(Appearance, Character) : CharacterLook.Default(Character);

    /// <summary>The state published on the latest tick.</summary>
    public PlayerState State { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerInput>(this);
        bus.Subscribe<DamagePlayer>(this);
        bus.Subscribe<TeleportPlayer>(this);
        bus.Subscribe<DialogueState>(this);
        bus.Subscribe<BowState>(this);
        bus.Subscribe<GameStart>(this);
        bus.Subscribe<StartScreenState>(this);
        bus.Subscribe<AppearanceState>(this);
        bus.Subscribe<WardrobeState>(this);
        bus.Subscribe<PreviewYaw>(this);
        bus.Subscribe<CollectSave>(this);
        bus.Subscribe<RestorePart>(this);
        State = MakeState();
    }

    /// <summary>Puts the player standing on the ground at (x, z) of <paramref name="feet"/>.</summary>
    public void PlaceAt(Vector3 feet, float facing = 0f)
    {
        Position = new Vector3(feet.X, Ground.HeightAt(feet.X, feet.Z), feet.Z);
        Facing = WrapAngle(facing);
        Velocity = Vector3.Zero;
        Grounded = true;
        State = MakeState();
    }

    public void Handle(in PlayerInput message)
    {
        if (!InPlay)
        {
            StopInput();
            return;
        }
        _input = message;
        if (message.Jump)
            _jumpQueued = true;
        if (float.IsFinite(message.Look.X))
            _lookX += message.Look.X;
    }

    /// <summary>The camera's yaw from the tick's input (on the tick, so a screen opened meanwhile stops it).</summary>
    private void FollowCamera()
    {
        Vector3 aim = _input.AimDirection;
        if (_input.HasAim && aim.X * aim.X + aim.Z * aim.Z > 0.0001f)
            CameraYaw = MathF.Atan2(-aim.X, -aim.Z);
        else
            CameraYaw = WrapAngle(CameraYaw - _lookX);
        _lookX = 0f;
    }

    /// <summary>Moves there at once; after a defeat also brings the player back with full health.</summary>
    public void Handle(in TeleportPlayer message)
    {
        Vector3 p = message.Position;
        if (!float.IsFinite(p.X) || !float.IsFinite(p.Z))
            return;
        if (!Alive)
        {
            Alive = true;
            Health = MaxHealth;
            DefeatedSeconds = 0f;
            InvulnerableSeconds = 0f;
        }
        _jumpQueued = false;
        PlaceAt(p, message.Facing);
    }

    public void Handle(in DialogueState message) => _talking = message.Open;

    /// <summary>Out of play (behind the start screen) the bow stays on the back: idle only.</summary>
    public void Handle(in BowState message)
    {
        _bowDraw = InPlay ? message.Draw : 0f;
        BowTarget = message.Target;
    }

    public void Handle(in StartScreenState message)
    {
        _screenOpen = message.Open;
        if (_screenOpen)
            StopInput();
    }

    /// <summary>The wardrobe or the shop: while open the character stands idle, input ignored.</summary>
    public void Handle(in WardrobeState message)
    {
        _wardrobeOpen = message.Open;
        if (_wardrobeOpen)
            StopInput();
    }

    /// <summary>Keeps the latest turn (0 faces the camera, positive turns the front to the screen's right).</summary>
    public void Handle(in PreviewYaw message)
    {
        if (float.IsFinite(message.Yaw))
            _previewYaw = WrapAngle(message.Yaw);
    }

    private void StopInput()
    {
        _input = default;
        _jumpQueued = false;
        _lookX = 0f;
        _bowDraw = 0f;
    }

    /// <summary>Keeps the latest look (it arrives every tick; only a change counts).</summary>
    public void Handle(in AppearanceState message)
    {
        if (HasAppearance && Same(Appearance, message))
            return;
        Appearance = message;
        HasAppearance = true;
        AppearanceVersion++;
    }

    private static bool Same(in AppearanceState a, in AppearanceState b) =>
        a.Skin == b.Skin && a.HairStyle == b.HairStyle && a.HairColor == b.HairColor && a.EyeColor == b.EyeColor &&
        string.Equals(a.Top, b.Top, StringComparison.Ordinal) && string.Equals(a.Bottom, b.Bottom, StringComparison.Ordinal) &&
        string.Equals(a.Shoes, b.Shoes, StringComparison.Ordinal) && string.Equals(a.Head, b.Head, StringComparison.Ordinal) &&
        string.Equals(a.Face, b.Face, StringComparison.Ordinal) && string.Equals(a.Back, b.Back, StringComparison.Ordinal);

    /// <summary>The first one picks the character and puts the player in play at the home door; later ones are ignored.</summary>
    public void Handle(in GameStart message)
    {
        if (_started)
            return;
        Character = message.Character == GameStart.Boy ? CharacterKind.Boy : CharacterKind.Girl;
        _started = true;
        _input = default;
        _jumpQueued = false;
        PlaceAt(Places.HomeDoor.Position, Places.HomeDoor.Yaw);
    }

    public void Handle(in CollectSave message) =>
        _bus?.Publish(new SavePart(message.RequestId, SaveModules.Player, SaveJson()));

    /// <summary>The player's saved state as compact JSON, what <see cref="CollectSave"/> is answered with.</summary>
    public string SaveJson() => JsonSerializer.Serialize(
        new SavedPlayer(SaveVersion, Position.X, Position.Y, Position.Z, Facing,
            Areas.At(Position.X, Position.Z).ToString(), Character.ToString(), Health),
        JsonOptions);

    /// <summary>
    /// Puts the player back as saved: in play, standing at the saved spot (on the ground there, or
    /// at the area's entry when that spot is not standable any more), facing, character and health.
    /// Publishes nothing (no defeat, no damage). Unreadable parts keep the current state.
    /// </summary>
    public void Handle(in RestorePart message)
    {
        if (message.Module != SaveModules.Player)
            return;
        if (!TryReadSave(message.Json, out var saved))
        {
            if (!_loggedBadSave)
            {
                _loggedBadSave = true;
                Log("Fungtopia: Player: the saved player cannot be read; keeping the current position and health.");
            }
            return;
        }
        Character = saved.Character;
        // A save made while defeated comes back standing, as after the clinic's teleport.
        Health = saved.Health > 0 ? saved.Health : MaxHealth;
        Alive = true;
        DefeatedSeconds = 0f;
        InvulnerableSeconds = 0f;
        _defeatPending = false;
        _started = true;
        StopInput();
        Archery = ArcheryState.Relaxed;
        var place = StandablePlace(saved.Position, saved.Facing, saved.Area);
        PlaceAt(place.Position, place.Yaw);
    }

    private readonly struct Restored
    {
        public readonly Vector3 Position;
        public readonly float Facing;
        public readonly Area Area;
        public readonly CharacterKind Character;
        public readonly int Health;

        public Restored(Vector3 position, float facing, Area area, CharacterKind character, int health)
        {
            Position = position;
            Facing = facing;
            Area = area;
            Character = character;
            Health = health;
        }
    }

    private static bool TryReadSave(string? json, out Restored restored)
    {
        restored = default;
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            var s = JsonSerializer.Deserialize<SavedPlayer>(json, JsonOptions);
            if (s == null || s.Version != SaveVersion)
                return false;
            if (!float.IsFinite(s.X) || !float.IsFinite(s.Y) || !float.IsFinite(s.Z) || !float.IsFinite(s.Facing))
                return false;
            if (s.Health < 0 || s.Health > MaxHealth)
                return false;
            if (!Enum.TryParse(s.Area, false, out Area area) || !Enum.IsDefined(area) || int.TryParse(s.Area, out _))
                return false;
            if (!Enum.TryParse(s.Character, false, out CharacterKind character) || !Enum.IsDefined(character) || int.TryParse(s.Character, out _))
                return false;
            restored = new Restored(new Vector3(s.X, s.Y, s.Z), s.Facing, area, character, s.Health);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// The saved spot when the player can stand there in the saved area; otherwise that area's
    /// entry (the house interior's door, an area's signpost, the home door for the road).
    /// </summary>
    private static Place StandablePlace(Vector3 feet, float facing, Area area)
    {
        if (CanStand(feet.X, feet.Z) && Areas.At(feet.X, feet.Z) == area)
            return new Place(feet, facing);
        return area switch
        {
            Area.Home => Places.HomeInterior,
            Area.Town or Area.Forest or Area.PetCity or Area.DeepForest => Places.Signpost(area),
            _ => Places.HomeDoor,
        };
    }

    public void Handle(in DamagePlayer message)
    {
        if (!_started || !Alive || message.Amount <= 0 || InvulnerableSeconds > 0f)
            return;
        Health = Math.Max(0, Health - message.Amount);
        InvulnerableSeconds = InvulnerableAfterHit;
        if (Health == 0)
        {
            Alive = false;
            DefeatedSeconds = 0f;
            _defeatPending = true;
        }
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("PlayerModule.Tick before Start.");
        float dt = (float)context.StepSeconds;
        Ticks++;

        if (InvulnerableSeconds > 0f)
            InvulnerableSeconds = MathF.Max(0f, InvulnerableSeconds - dt);

        if (_defeatPending)
        {
            _defeatPending = false;
            _bus.Publish(new PlayerDefeated(Position));
        }
        if (!Alive)
            DefeatedSeconds += dt;

        if (OnScreen)
        {
            // Standing idle for viewing: no walking, jump or bow, on the ground.
            StopInput();
            Archery = ArcheryState.Relaxed;
            Position = new Vector3(Position.X, Ground.HeightAt(Position.X, Position.Z), Position.Z);
            Velocity = Vector3.Zero;
            Grounded = true;
        }
        else
        {
            FollowCamera();
            Move(dt);
            _jumpQueued = false;
            Archery = ArcheryPose.Advance(Archery, _bowDraw, dt);
            // The next screen starts facing the camera.
            _previewYaw = 0f;
        }
        // The camera faces the character at yaw Facing + π, so turning its front to the screen's
        // right is turning it left (positive yaw) by PreviewYaw; eased the short way round.
        float diff = WrapAngle(_previewYaw - PreviewTurn);
        PreviewTurn = MathF.Abs(diff) < 1e-4f
            ? _previewYaw
            : WrapAngle(PreviewTurn + diff * (1f - MathF.Exp(-PreviewTurnSharpness * dt)));

        State = MakeState();
        _bus.Publish(State);
    }

    private void Move(float dt)
    {
        Vector3 start = Position;
        float vy = Velocity.Y;

        // The wanted horizontal velocity, from the stick relative to the camera's yaw.
        bool canWalk = Alive && !_talking;
        Vector2 move = canWalk ? _input.Move : Vector2.Zero;
        if (!float.IsFinite(move.X) || !float.IsFinite(move.Y))
            move = Vector2.Zero;
        float length = move.Length();
        if (length > 1f)
            move /= length;
        float sin = MathF.Sin(CameraYaw), cos = MathF.Cos(CameraYaw);
        // Camera forward is (-sin, -cos) in (x, z); its right is (cos, -sin).
        float wantX = (move.X * cos - move.Y * sin) * WalkSpeed;
        float wantZ = (-move.X * sin - move.Y * cos) * WalkSpeed;

        // Facing: toward the aim while Fire is held, else toward the move direction.
        if (Alive)
        {
            Vector3 aim = _input.AimDirection;
            if (_input.Fire && _input.HasAim && aim.X * aim.X + aim.Z * aim.Z > 0.0001f)
                TurnToward(MathF.Atan2(-aim.X, -aim.Z), dt);
            else if (_input.Fire)
                TurnToward(CameraYaw, dt);
            else if (length > 0.05f)
                TurnToward(MathF.Atan2(-wantX, -wantZ), dt);
        }

        if (canWalk && _jumpQueued && Grounded)
        {
            vy = MathF.Sqrt(2f * Gravity * JumpHeight);
            Grounded = false;
        }

        float x = start.X + wantX * dt;
        float z = start.Z + wantZ * dt;
        ResolveCollisions(start.X, start.Z, ref x, ref z);

        float ground = Ground.HeightAt(x, z);
        float y;
        if (Grounded)
        {
            // Walking: stay on the ground, also down slopes (the walkable ground has no ledges).
            y = ground;
            vy = 0f;
        }
        else
        {
            // Exact for constant gravity, so the jump peaks at JumpHeight whatever the step.
            y = start.Y + vy * dt - 0.5f * Gravity * dt * dt;
            vy -= Gravity * dt;
            if (y <= ground)
            {
                y = ground;
                vy = 0f;
                Grounded = true;
            }
        }

        Position = new Vector3(x, y, z);
        Velocity = new Vector3((x - start.X) / dt, vy, (z - start.Z) / dt);
    }

    /// <summary>Whether a player standing at (x, z) is on walkable ground and clear of obstacles.</summary>
    public static bool CanStand(float x, float z) =>
        Areas.Walkable(x, z, Radius) && !Ground.Blocks(x, z, Radius);

    /// <summary>
    /// Pushes (x, z) out of every obstacle it overlaps (trunks, buildings) and back onto the
    /// walkable ground, each along its normal so motion along the edge stays (sliding). Where
    /// that does not settle (a tight corner) the player stays at (startX, startZ).
    /// </summary>
    private static void ResolveCollisions(float startX, float startZ, ref float x, ref float z)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            bool moved = false;
            if (Ground.Blocks(x, z, Radius))
            {
                foreach (var o in Ground.Obstacles)
                {
                    float dx = x - o.X, dz = z - o.Z, r = o.Radius + Radius;
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
                    x = o.X + dx * push;
                    z = o.Z + dz * push;
                }
                moved = true;
            }
            if (!Areas.Walkable(x, z, Radius))
            {
                // Back inside along the walkable ground's normal (the gradient of its depth).
                const float h = 0.01f;
                float gx = Areas.Depth(x + h, z) - Areas.Depth(x - h, z);
                float gz = Areas.Depth(x, z + h) - Areas.Depth(x, z - h);
                float g = MathF.Sqrt(gx * gx + gz * gz);
                if (g < 1e-6f)
                    break;
                float push = Radius - Areas.Depth(x, z) + 1e-3f;
                x += gx / g * push;
                z += gz / g * push;
                moved = true;
            }
            if (!moved)
                return;
        }
        if (!CanStand(x, z) && CanStand(startX, startZ))
        {
            x = startX;
            z = startZ;
        }
    }

    private void TurnToward(float target, float dt)
    {
        float diff = WrapAngle(target - Facing);
        float step = TurnSpeed * dt;
        Facing = WrapAngle(Facing + Math.Clamp(diff, -step, step));
    }

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

    private PlayerState MakeState() =>
        new(Position, Facing, Velocity, Grounded, Health, MaxHealth, Alive, Character, Areas.At(Position.X, Position.Z));
}
