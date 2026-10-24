using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Bow.View;

/// <summary>
/// Draws the arrows of the bow (the Player module draws the bow itself and the nocked arrow in the
/// character's hands): every arrow of the pool, placed between the last two fixed ticks along its
/// flight, with a faint spore trail while it flies, a little spore puff when it hits a monster, and
/// shrinking away at the end of its time stuck in the ground or a trunk.
/// </summary>
public partial class BowView : Node3D, IModuleView
{
    private const float ArrowLength = 0.72f;
    private const int Puffs = 4;

    private BowModule? _bow;
    private Node3D[] _arrows = Array.Empty<Node3D>();
    private CpuParticles3D[] _trails = Array.Empty<CpuParticles3D>();
    private CpuParticles3D[] _puffs = Array.Empty<CpuParticles3D>();
    private ArrowPhase[] _shownPhase = Array.Empty<ArrowPhase>();
    private int[] _shownShot = Array.Empty<int>();
    private int _nextPuff;

    public void Bind(IModule module) => _bow = (BowModule)module;

    public override void _Ready()
    {
        if (_bow == null)
            return;
        Build();
    }

    private void Build()
    {
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
        if (_bow == null)
            return;
        DrawArrows((float)Engine.GetPhysicsInterpolationFraction());
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

    private static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
}
