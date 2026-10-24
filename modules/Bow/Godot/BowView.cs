using System;
using Fungtopia.Player;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Bow.View;

/// <summary>
/// Draws the bow: while drawing, the bow held in the character's hands at chest height pointing
/// along the aim, its string pulled back with a poison arrow nocked; and every arrow of the pool,
/// placed between the last two fixed ticks along its flight, with a faint spore trail while it
/// flies, a little spore puff when it hits a monster, and shrinking away at the end of its time
/// stuck in the ground or a trunk.
/// </summary>
public partial class BowView : Node3D, IModuleView
{
    // The bow model's tips (bow.py): this far behind the grip and above/below it.
    private const float TipBack = 0.2176f;
    private const float TipHalfSpan = 0.3681f;
    private const float ArrowLength = 0.72f;
    /// <summary>How far the string comes back at a full draw, meters.</summary>
    private const float FullPull = 0.26f;
    /// <summary>The bow is held this far out in front of the chest.</summary>
    private const float HoldForward = 0.32f;
    /// <summary>Seconds the bow stays in the hands after a shot.</summary>
    private const float HoldAfterShot = 0.35f;
    private const int Puffs = 4;

    private BowModule? _bow;
    private Node3D? _held;
    private Node3D? _heldBow;
    private Node3D? _nocked;
    private MeshInstance3D? _stringTop;
    private MeshInstance3D? _stringBottom;
    private Node3D[] _arrows = Array.Empty<Node3D>();
    private CpuParticles3D[] _trails = Array.Empty<CpuParticles3D>();
    private CpuParticles3D[] _puffs = Array.Empty<CpuParticles3D>();
    private ArrowPhase[] _shownPhase = Array.Empty<ArrowPhase>();
    private int[] _shownShot = Array.Empty<int>();
    private int _nextPuff;

    private PlayerState _previousPlayer;
    private PlayerState _currentPlayer;
    private float _previousDraw;
    private float _currentDraw;
    private long _seenTicks = -1;
    private float _sinceShot = float.MaxValue;
    private float _shownPull;

    public void Bind(IModule module) => _bow = (BowModule)module;

    public override void _Ready()
    {
        if (_bow == null)
            return;
        Build();
        _previousPlayer = _currentPlayer = _bow.Player;
        _previousDraw = _currentDraw = _bow.Draw;
        _seenTicks = _bow.Ticks;
    }

    private void Build()
    {
        _held = new Node3D { Name = "HeldBow", Visible = false };
        AddChild(_held);
        _heldBow = ToonModels.Load(ToonModels.Models + "bow.glb");
        _held.AddChild(_heldBow);
        var stringMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.98f, 0.95f, 0.86f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var stringMesh = new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.006f, Height = 1f, RadialSegments = 6, Rings = 1 };
        _stringTop = new MeshInstance3D { Mesh = stringMesh, MaterialOverride = stringMaterial };
        _stringBottom = new MeshInstance3D { Mesh = stringMesh, MaterialOverride = stringMaterial };
        _held.AddChild(_stringTop);
        _held.AddChild(_stringBottom);
        _nocked = ToonModels.Load(ToonModels.Models + "arrow.glb");
        _held.AddChild(_nocked);

        int n = BowModule.PoolSize;
        _arrows = new Node3D[n];
        _trails = new CpuParticles3D[n];
        _shownPhase = new ArrowPhase[n];
        _shownShot = new int[n];
        for (int i = 0; i < n; i++)
        {
            _arrows[i] = ToonModels.Load(ToonModels.Models + "arrow.glb");
            _arrows[i].Name = $"Arrow{i}";
            _arrows[i].Visible = false;
            AddChild(_arrows[i]);
            _trails[i] = SporeTrail();
            AddChild(_trails[i]);
        }
        _puffs = new CpuParticles3D[Puffs];
        for (int i = 0; i < Puffs; i++)
        {
            _puffs[i] = SporePuff();
            AddChild(_puffs[i]);
        }
    }

    /// <summary>Faint lilac and green spores drifting up behind a flying arrow.</summary>
    private static CpuParticles3D SporeTrail()
    {
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(0.85f, 0.7f, 1f, 0.55f));
        ramp.SetColor(1, new Color(0.6f, 1f, 0.55f, 0f));
        return new CpuParticles3D
        {
            Emitting = false,
            Amount = 28,
            Lifetime = 0.7,
            LocalCoords = false,
            Mesh = new SphereMesh { Radius = 0.022f, Height = 0.044f, RadialSegments = 6, Rings = 3 },
            MaterialOverride = SporeMaterial(),
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.04f,
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = 0.05f,
            InitialVelocityMax = 0.25f,
            Gravity = new Vector3(0f, 0.25f, 0f),
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.3f,
            ColorRamp = ramp,
        };
    }

    /// <summary>A little burst of poison spores where an arrow hits a monster.</summary>
    private static CpuParticles3D SporePuff()
    {
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(0.72f, 0.45f, 0.95f, 0.9f));
        ramp.SetColor(1, new Color(0.55f, 0.95f, 0.4f, 0f));
        return new CpuParticles3D
        {
            Emitting = false,
            OneShot = true,
            Explosiveness = 1f,
            Amount = 18,
            Lifetime = 0.6,
            LocalCoords = false,
            Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 8, Rings = 4 },
            MaterialOverride = SporeMaterial(),
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = 0.6f,
            InitialVelocityMax = 1.6f,
            Gravity = new Vector3(0f, -0.6f, 0f),
            DampingMin = 1.5f,
            DampingMax = 2.5f,
            ScaleAmountMin = 0.7f,
            ScaleAmountMax = 1.6f,
            ColorRamp = ramp,
        };
    }

    private static StandardMaterial3D SporeMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    public override void _Process(double delta)
    {
        if (_bow == null || _held == null)
            return;
        if (_bow.Ticks != _seenTicks)
        {
            // More than one tick since the last frame: start from the newest only.
            bool one = _bow.Ticks == _seenTicks + 1;
            _previousPlayer = one ? _currentPlayer : _bow.Player;
            _previousDraw = one ? _currentDraw : _bow.Draw;
            if (_bow.Draw == 0f && _currentDraw > 0f && _bow.Player.Alive)
                _sinceShot = 0f;
            _currentPlayer = _bow.Player;
            _currentDraw = _bow.Draw;
            _seenTicks = _bow.Ticks;
        }
        float alpha = (float)Engine.GetPhysicsInterpolationFraction();
        float dt = (float)delta;
        _sinceShot += dt;
        DrawHeldBow(alpha, dt);
        DrawArrows(alpha);
    }

    private void DrawHeldBow(float alpha, float dt)
    {
        bool show = _bow!.HasPlayer && _currentPlayer.Alive && (_currentDraw > 0f || _sinceShot < HoldAfterShot);
        _held!.Visible = show;
        if (!show)
            return;
        var from = ToGodot(_previousPlayer.Position);
        var to = ToGodot(_currentPlayer.Position);
        var feet = from.DistanceTo(to) > 2f ? to : from.Lerp(to, alpha);

        var aim = ToGodot(_bow.AimDirection);
        var flat = new Vector3(aim.X, 0f, aim.Z);
        flat = flat.LengthSquared() > 1e-6f ? flat.Normalized() : Vector3.Forward;
        var basis = Look(aim);
        var chest = feet + Vector3.Up * BowModule.ChestHeight + flat * HoldForward;
        // A little pop in when the bow comes out, and settling back after a shot.
        float pop = Mathf.Clamp(_currentDraw > 0f ? 1f : 1f - _sinceShot / HoldAfterShot, 0f, 1f);
        _held.Transform = new Transform3D(basis.Scaled(Vector3.One * (0.6f + 0.4f * pop)), chest);

        float draw = Mathf.Lerp(_previousDraw, _currentDraw, alpha);
        // The string snaps forward after a shot.
        float pullTarget = draw * FullPull;
        _shownPull = pullTarget < _shownPull ? Mathf.MoveToward(_shownPull, pullTarget, dt * 6f) : pullTarget;
        var nock = new Vector3(0f, 0f, TipBack + _shownPull);
        // A tiny tremble at a full draw.
        if (draw >= 0.999f)
            nock.X += 0.004f * MathF.Sin((float)Time.GetTicksMsec() * 0.06f);
        Rope(_stringTop!, new Vector3(0f, TipHalfSpan, TipBack), nock);
        Rope(_stringBottom!, new Vector3(0f, -TipHalfSpan, TipBack), nock);
        _nocked!.Visible = _currentDraw > 0f;
        _nocked.Position = nock + new Vector3(0f, 0f, -ArrowLength);
        // The limbs bend a little as the string comes back.
        _heldBow!.Scale = new Vector3(1f, 1f - 0.08f * draw, 1f + 0.25f * draw);
    }

    private void DrawArrows(float alpha)
    {
        var arrows = _bow!.Arrows;
        for (int i = 0; i < arrows.Length; i++)
        {
            ref readonly Arrow a = ref arrows[i];
            // A flying arrow gone before its time hit a monster.
            if (_shownPhase[i] == ArrowPhase.Flying && a.Phase == ArrowPhase.Free && a.Shot == _shownShot[i]
                && a.FlightSeconds < BowModule.MaxFlightSeconds - 0.01f)
                Puff(ToGodot(a.Position));
            _shownPhase[i] = a.Phase;
            _shownShot[i] = a.Shot;

            var node = _arrows[i];
            var trail = _trails[i];
            if (a.Phase == ArrowPhase.Free)
            {
                node.Visible = false;
                trail.Emitting = false;
                continue;
            }
            var position = ToGodot(a.PreviousPosition).Lerp(ToGodot(a.Position), alpha);
            float scale = 1f;
            if (a.Phase == ArrowPhase.Stuck)
                scale = Mathf.Clamp((BowModule.StickSeconds - a.StuckSeconds) / 0.4f, 0f, 1f);
            node.Visible = scale > 0f;
            node.Transform = new Transform3D(Look(ToGodot(a.Direction)).Scaled(Vector3.One * scale), position);
            trail.Emitting = a.Phase == ArrowPhase.Flying;
            // The trail streams from the middle of the shaft.
            trail.Position = position - ToGodot(a.Direction) * (ArrowLength * 0.5f);
        }
    }

    private void Puff(Vector3 at)
    {
        var puff = _puffs[_nextPuff];
        _nextPuff = (_nextPuff + 1) % _puffs.Length;
        puff.Position = at;
        puff.Restart();
    }

    /// <summary>A basis whose -Z (the models' forward) points along <paramref name="direction"/>.</summary>
    private static Basis Look(Vector3 direction)
    {
        if (direction.LengthSquared() < 1e-8f)
            return Basis.Identity;
        direction = direction.Normalized();
        var up = MathF.Abs(direction.Y) > 0.99f ? Vector3.Back : Vector3.Up;
        return Basis.LookingAt(direction, up);
    }

    /// <summary>Stretches a unit cylinder (along Y) from <paramref name="a"/> to <paramref name="b"/>.</summary>
    private static void Rope(MeshInstance3D rope, Vector3 a, Vector3 b)
    {
        var axis = b - a;
        float length = axis.Length();
        var y = axis / length;
        var x = y.Cross(Vector3.Back);
        x = x.LengthSquared() > 1e-6f ? x.Normalized() : Vector3.Right;
        var z = x.Cross(y);
        rope.Transform = new Transform3D(new Basis(x, y * length, z), (a + b) * 0.5f);
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
}
