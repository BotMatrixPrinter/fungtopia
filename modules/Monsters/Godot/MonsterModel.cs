using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Monsters.View;

/// <summary>
/// One monster (caterpillar.glb, ant.glb, tarantula.glb or ladybug.glb from modules/Monsters/Models),
/// drawn toon-shaded with ink outlines and animated procedurally: the caterpillar crawls in a wave
/// down its segments, the ant scurries on six legs, the tarantula skitters on eight, crouches and
/// leaps, the ladybug waddles, tucks in its head and legs and rolls; all lunge when they bite,
/// flash white when hit, turn green with rising bubbles while poisoned, pop into a puff of spores
/// when defeated, and show a small health bar above them while hurt. Faces -Z at zero rotation.
/// </summary>
public partial class MonsterModel : Node3D
{
    private const string Models = "res://modules/Monsters/Models/";
    private const string Shaders = "res://modules/Monsters/Godot/Shaders/";
    /// <summary>Seconds of the defeat poof.</summary>
    public const float PoofSeconds = 0.4f;

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "EyeShine", "Blush", "Mouth", "Brow" };
    private static readonly Dictionary<MonsterKind, (GltfDocument, GltfState)?> s_models = new();
    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;

    private readonly Node3D _rig = new() { Name = "Rig" };
    private readonly List<GeometryInstance3D> _meshes = new();
    private Part _head, _thorax, _abdomen, _body, _shell;
    private readonly Part[] _segments = new Part[4];
    private readonly Part[] _legs = new Part[8]; // L1 R1 L2 R2 L3 R3 (L4 R4)

    private CpuParticles3D _bubbles = null!;
    private CpuParticles3D _spores = null!;
    private MeshInstance3D _puff = null!;
    private StandardMaterial3D _puffMaterial = null!;
    private Node3D _bar = null!;
    private MeshInstance3D _barFill = null!;
    private StandardMaterial3D _barFillMaterial = null!;

    private float _time;
    private float _phase;
    private float _moveAmount;
    private float _poison;
    private float _shownFlash = -1f;
    private float _shownPoison = -1f;
    private bool _poofed;
    private float _roll;

    public MonsterKind Kind { get; private set; }

    /// <summary>Height of the health bar above the feet, meters.</summary>
    private float BarHeight => Kind switch
    {
        MonsterKind.Ant => 1.2f,
        MonsterKind.Caterpillar => 1.15f,
        MonsterKind.Tarantula => 1.45f,
        _ => 1.35f,
    };

    /// <summary>The kind's chase speed, where its walk cycle is at full stride.</summary>
    private float FullStride => MonstersModule.StatsOf(Kind).ChaseSpeed;

    /// <summary>A part of the model and its rest transform, so animation always starts from rest.</summary>
    private struct Part
    {
        public Node3D? Node;
        public Transform3D Rest;

        public Part(Node3D? node)
        {
            Node = node;
            Rest = node?.Transform ?? Transform3D.Identity;
        }

        public readonly void Pose(Vector3 rotation, Vector3 offset = default, float squash = 0f)
        {
            if (Node == null)
                return;
            var basis = Basis.FromEuler(rotation) * Rest.Basis * Basis.FromScale(new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f));
            Node.Transform = new Transform3D(basis, Rest.Origin + offset);
        }
    }

    public static MonsterModel Create(MonsterKind kind)
    {
        var model = new MonsterModel { Name = kind.ToString(), Kind = kind };
        model.Build();
        return model;
    }

    private void Build()
    {
        AddChild(_rig);
        var scene = LoadModel(Kind);
        if (scene != null)
        {
            _rig.AddChild(scene);
            CollectMeshes(scene);
            _head = new Part(Find(scene, "Head"));
            _thorax = new Part(Find(scene, "Thorax"));
            _abdomen = new Part(Find(scene, "Abdomen"));
            _body = new Part(Find(scene, "Body"));
            _shell = new Part(Find(scene, "Shell"));
            for (int i = 0; i < _segments.Length; i++)
                _segments[i] = new Part(Find(scene, $"Seg{i + 1}"));
            for (int i = 0; i < 4; i++)
            {
                _legs[2 * i] = new Part(Find(scene, $"LegL{i + 1}"));
                _legs[2 * i + 1] = new Part(Find(scene, $"LegR{i + 1}"));
            }
        }
        BuildBubbles();
        BuildPoof();
        BuildBar();
    }

    /// <summary>
    /// A new copy of the kind's model. The .glb is read once at run time from its bytes, so the
    /// game runs without the editor having imported it first (a fresh checkout loads headless).
    /// </summary>
    private static Node3D? LoadModel(MonsterKind kind)
    {
        if (!s_models.TryGetValue(kind, out var parsed))
        {
            string path = Models + kind.ToString().ToLowerInvariant() + ".glb";
            byte[] bytes = FileAccess.GetFileAsBytes(path);
            var document = new GltfDocument();
            var state = new GltfState();
            if (bytes.Length == 0)
                GD.PushWarning($"Fungtopia: Monsters: model {path} is missing.");
            else if (document.AppendFromBuffer(bytes, Models, state) is var error && error != Error.Ok)
                GD.PushWarning($"Fungtopia: Monsters: model {path} could not be read ({error}).");
            else
                parsed = (document, state);
            s_models[kind] = parsed;
        }
        return parsed is var (doc, st) ? doc.GenerateScene(st) as Node3D : null;
    }

    private static Node3D? Find(Node root, string name) => root.FindChild(name, true, false) as Node3D;

    private void CollectMeshes(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                mesh.SetSurfaceOverrideMaterial(i, ToonMaterial(mesh.Mesh.SurfaceGetMaterial(i)));
            _meshes.Add(mesh);
        }
        foreach (var child in node.GetChildren())
            CollectMeshes(child);
    }

    /// <summary>The toon material for a glTF material: its color, an ink outline unless it is a small detail.</summary>
    private static ShaderMaterial ToonMaterial(Material? source)
    {
        string name = source?.ResourceName ?? "";
        var color = source is BaseMaterial3D standard ? standard.AlbedoColor : Colors.White;
        string key = name + color.ToHtml();
        if (s_materials.TryGetValue(key, out var cached))
            return cached;
        s_toon ??= GD.Load<Shader>(Shaders + "toon.gdshader");
        s_outline ??= new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "outline.gdshader") };
        var material = new ShaderMaterial { Shader = s_toon };
        // glTF stores linear colors; the shader's albedo is an sRGB color like Blender's swatch.
        material.SetShaderParameter("albedo", color.SrgbToLinear());
        if (name == "EyeShine")
            material.SetShaderParameter("self_light", 0.6f);
        if (!NoOutline.Contains(name))
            material.NextPass = s_outline;
        s_materials[key] = material;
        return material;
    }

    private static StandardMaterial3D Unshaded(Color color, bool vertexColor = false) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = color,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = vertexColor,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    private void BuildBubbles()
    {
        // Little green bubbles rising off a poisoned monster.
        _bubbles = new CpuParticles3D
        {
            Name = "PoisonBubbles",
            Emitting = false,
            Amount = 14,
            Lifetime = 1.1,
            Mesh = new SphereMesh { Radius = 0.045f, Height = 0.09f, RadialSegments = 8, Rings = 4 },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.4f,
            Direction = Vector3.Up,
            Spread = 25f,
            InitialVelocityMin = 0.3f,
            InitialVelocityMax = 0.7f,
            Gravity = new Vector3(0f, 0.5f, 0f),
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.4f,
            Position = new Vector3(0f, 0.55f, 0f),
            ColorRamp = new Gradient
            {
                Offsets = new[] { 0f, 0.7f, 1f },
                Colors = new[] { new Color(0.55f, 1f, 0.35f, 0.95f), new Color(0.6f, 1f, 0.4f, 0.8f), new Color(0.7f, 1f, 0.5f, 0f) },
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _bubbles.MaterialOverride = Unshaded(Colors.White, vertexColor: true);
        AddChild(_bubbles);
    }

    private void BuildPoof()
    {
        // A soft white puff and a burst of floating spores when the monster is defeated.
        _puffMaterial = Unshaded(new Color(1f, 0.97f, 0.9f, 0f));
        _puff = new MeshInstance3D
        {
            Name = "Puff",
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = _puffMaterial,
            Position = new Vector3(0f, 0.4f, 0f),
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_puff);
        _spores = new CpuParticles3D
        {
            Name = "Spores",
            Emitting = false,
            OneShot = true,
            Explosiveness = 0.95f,
            Amount = 28,
            Lifetime = 1.6,
            Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 6, Rings = 3 },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.3f,
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = 1.2f,
            InitialVelocityMax = 2.6f,
            Gravity = new Vector3(0f, 0.25f, 0f),
            DampingMin = 2f,
            DampingMax = 3.5f,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.5f,
            Position = new Vector3(0f, 0.4f, 0f),
            ColorRamp = new Gradient
            {
                Offsets = new[] { 0f, 0.6f, 1f },
                Colors = new[] { new Color(1f, 0.92f, 0.55f, 1f), new Color(1f, 0.75f, 0.8f, 0.85f), new Color(1f, 0.9f, 0.95f, 0f) },
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _spores.MaterialOverride = Unshaded(Colors.White, vertexColor: true);
        AddChild(_spores);
    }

    private void BuildBar()
    {
        // A small rounded-looking bar: a dark back and a colored fill, turned to the camera.
        _bar = new Node3D { Name = "HealthBar", Position = new Vector3(0f, BarHeight, 0f), Visible = false };
        AddChild(_bar);
        var back = Unshaded(new Color(0.18f, 0.1f, 0.12f, 0.85f));
        back.NoDepthTest = true;
        back.RenderPriority = 1;
        _bar.AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(0.72f, 0.12f) },
            MaterialOverride = back,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        _barFillMaterial = Unshaded(new Color(0.45f, 0.9f, 0.3f));
        _barFillMaterial.NoDepthTest = true;
        _barFillMaterial.RenderPriority = 2;
        _barFill = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(0.64f, 0.07f) },
            MaterialOverride = _barFillMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _bar.AddChild(_barFill);
    }

    /// <summary>Back to a fresh monster (a new one took this slot).</summary>
    public void Reset()
    {
        _poofed = false;
        _poison = 0f;
        _puff.Visible = false;
        _spores.Emitting = false;
        _rig.Visible = true;
        _rig.Transform = Transform3D.Identity;
    }

    /// <summary>Advances the animation by <paramref name="delta"/> seconds of frame time.</summary>
    /// <param name="speed">Horizontal speed, meters per second.</param>
    /// <param name="hurtSeconds">Seconds since it was last hurt (a short white flash).</param>
    /// <param name="poisoned">Green tint and bubbles.</param>
    /// <param name="sinceBite">Seconds since its last bite (a lunge).</param>
    /// <param name="alive">False: the poof, then nothing.</param>
    /// <param name="deadSeconds">Seconds since the defeat.</param>
    /// <param name="health">Health left, 0..1; the bar shows below 1.</param>
    /// <param name="action">A hard monster's attack under way (crouch, leap, curl, charge).</param>
    /// <param name="actionSeconds">Seconds since <paramref name="action"/> began.</param>
    public void Animate(float delta, float speed, float hurtSeconds, bool poisoned, float sinceBite, bool alive,
        float deadSeconds, float health, MonsterAction action = MonsterAction.None, float actionSeconds = 0f)
    {
        _time += delta;
        bool ant = Kind == MonsterKind.Ant;
        bool rolling = action == MonsterAction.Charge;
        float moveTarget = alive && !rolling ? Math.Clamp(speed / FullStride, 0f, 1f) : 0f;
        _moveAmount = Mathf.MoveToward(_moveAmount, moveTarget, delta * 5f);
        // Crawling and scurrying keep pace with the ground; a slow idle wiggle while standing.
        float stride = Kind switch { MonsterKind.Ant => 9f, MonsterKind.Tarantula => 7f, MonsterKind.Ladybug => 10f, _ => 5.5f };
        _phase = (_phase + delta * (speed * stride + 1.5f)) % Mathf.Tau;
        _poison = Mathf.MoveToward(_poison, poisoned && alive ? 1f : 0f, delta * 3f);
        float flash = alive ? Math.Clamp(1f - hurtSeconds / 0.25f, 0f, 1f) : 0f;
        float bite = sinceBite < 0.35f ? MathF.Sin(sinceBite / 0.35f * Mathf.Pi) : 0f;

        _rig.Transform = Transform3D.Identity;
        switch (Kind)
        {
            case MonsterKind.Ant:
                Scurry(bite);
                break;
            case MonsterKind.Tarantula:
                Skitter(bite, alive ? action : MonsterAction.None, actionSeconds);
                break;
            case MonsterKind.Ladybug:
                Waddle(bite, alive ? action : MonsterAction.None, actionSeconds, speed, delta);
                break;
            default:
                Crawl(bite);
                break;
        }

        // Defeat: a little hop and squash, then gone in a puff of spores.
        if (!alive)
        {
            if (!_poofed)
            {
                _poofed = true;
                _spores.Restart();
                _spores.Emitting = true;
            }
            float t = Math.Clamp(deadSeconds / PoofSeconds, 0f, 1f);
            float grow = t < 0.35f ? 1f + 0.25f * MathF.Sin(t / 0.35f * Mathf.Pi * 0.5f) : 1.25f * (1f - (t - 0.35f) / 0.65f);
            _rig.Scale = new Vector3(grow * (1f + 0.3f * t), grow * (1f - 0.3f * t), grow * (1f + 0.3f * t));
            _rig.Visible = t < 1f;
            float puff = Math.Clamp(deadSeconds / 0.7f, 0f, 1f);
            _puff.Visible = puff < 1f;
            _puff.Scale = Vector3.One * (0.4f + 1.4f * MathF.Sqrt(puff));
            _puffMaterial.AlbedoColor = new Color(1f, 0.97f, 0.9f, 0.85f * (1f - puff) * (1f - puff));
        }
        else
        {
            _rig.Visible = true;
            _puff.Visible = false;
        }

        _bubbles.Emitting = poisoned && alive;

        bool showBar = alive && health < 0.999f;
        _bar.Visible = showBar;
        if (showBar)
        {
            float f = Math.Clamp(health, 0f, 1f);
            _barFill.Scale = new Vector3(Math.Max(f, 0.001f), 1f, 1f);
            _barFill.Position = new Vector3(-0.32f * (1f - f), 0f, 0.002f);
            var good = new Color(0.45f, 0.9f, 0.3f);
            var low = new Color(1f, 0.38f, 0.3f);
            _barFillMaterial.AlbedoColor = poisoned ? new Color(0.62f, 0.95f, 0.2f) : low.Lerp(good, f);
            var camera = GetViewport()?.GetCamera3D();
            if (camera != null)
                _bar.GlobalBasis = camera.GlobalBasis.Orthonormalized();
        }

        if (MathF.Abs(flash - _shownFlash) > 0.01f || MathF.Abs(_poison - _shownPoison) > 0.01f)
        {
            _shownFlash = flash;
            _shownPoison = _poison;
            foreach (var mesh in _meshes)
            {
                mesh.SetInstanceShaderParameter("flash", flash);
                mesh.SetInstanceShaderParameter("poison", _poison);
            }
        }
    }

    /// <summary>The caterpillar: a hump travels from the tail to the head, each segment rising and squashing in turn.</summary>
    private void Crawl(float bite)
    {
        float amount = 0.25f + 0.75f * _moveAmount;
        for (int i = 0; i < _segments.Length; i++)
        {
            float wave = MathF.Sin(_phase + i * 1.1f);
            float lift = MathF.Max(0f, wave) * 0.09f * amount;
            float squash = -wave * 0.07f * amount;
            float sway = MathF.Sin(_phase * 0.5f + i * 0.8f) * 0.05f * (1f - _moveAmount);
            _segments[i].Pose(Vector3.Zero, new Vector3(sway, lift, 0f), squash);
        }
        float headBob = MathF.Max(0f, MathF.Sin(_phase - 1.1f)) * 0.05f * amount;
        float tilt = MathF.Sin(_time * 1.7f) * 0.06f;
        _head.Pose(new Vector3(-0.25f * bite, 0f, tilt), new Vector3(0f, headBob + 0.05f * bite, -0.18f * bite));
    }

    /// <summary>The ant: two alternating tripods of legs, a bobbing striped back end, a nodding head.</summary>
    private void Scurry(float bite)
    {
        float amount = 0.15f + 0.85f * _moveAmount;
        for (int i = 0; i < 6; i++)
        {
            bool left = i % 2 == 0;
            int pair = i / 2;
            // L1, R2, L3 step together, then R1, L2, R3.
            bool groupA = left == (pair % 2 == 0);
            float s = MathF.Sin(_phase + (groupA ? 0f : Mathf.Pi));
            float swing = s * 0.5f * amount;
            float lift = MathF.Max(0f, MathF.Cos(_phase + (groupA ? 0f : Mathf.Pi))) * 0.35f * amount;
            _legs[i].Pose(new Vector3(0f, swing, left ? -lift : lift));
        }
        float bob = MathF.Abs(MathF.Sin(_phase)) * 0.025f * amount;
        _thorax.Pose(Vector3.Zero, new Vector3(0f, bob, 0f));
        _abdomen.Pose(new Vector3(MathF.Sin(_phase * 2f) * 0.08f * amount, MathF.Sin(_phase) * 0.06f * amount, 0f),
            new Vector3(0f, bob, 0f));
        float nod = MathF.Sin(_phase * 2f) * 0.05f * amount + MathF.Sin(_time * 2.1f) * 0.04f;
        _head.Pose(new Vector3(nod - 0.3f * bite, 0f, MathF.Sin(_time * 1.3f) * 0.05f),
            new Vector3(0f, bob, -0.15f * bite));
    }

    /// <summary>
    /// The tarantula: two alternating sets of four legs, a bouncing furry body; it sinks down and
    /// draws its legs in while it crouches, then flies up in an arc with its legs reaching forward.
    /// </summary>
    private void Skitter(float bite, MonsterAction action, float actionSeconds)
    {
        float amount = 0.15f + 0.85f * _moveAmount;
        float crouch = action == MonsterAction.Crouch ? Math.Clamp(actionSeconds / (MonstersModule.PounceWindup * 0.6f), 0f, 1f) : 0f;
        float leap = action == MonsterAction.Leap ? Math.Clamp(actionSeconds / MonstersModule.PounceSeconds, 0f, 1f) : -1f;
        for (int i = 0; i < 8; i++)
        {
            bool left = i % 2 == 0;
            int pair = i / 2;
            // L1, R2, L3, R4 step together, then R1, L2, R3, L4.
            bool groupA = left == (pair % 2 == 0);
            float s = MathF.Sin(_phase + (groupA ? 0f : Mathf.Pi));
            float swing = s * 0.35f * amount;
            float lift = MathF.Max(0f, MathF.Cos(_phase + (groupA ? 0f : Mathf.Pi))) * 0.3f * amount;
            if (leap >= 0f)
            {
                // Front legs reach forward, back legs trail.
                swing = (pair < 2 ? 0.5f : -0.5f) * (left ? 1f : -1f);
                lift = 0.35f;
            }
            // A crouch splays the knees out and up.
            lift += 0.25f * crouch;
            _legs[i].Pose(new Vector3(0f, swing, left ? -lift : lift));
        }
        float bob = MathF.Abs(MathF.Sin(_phase)) * 0.04f * amount;
        float breathe = MathF.Sin(_time * 2.4f) * 0.015f;
        _body.Pose(new Vector3(-0.25f * bite - 0.12f * crouch + (leap >= 0f ? 0.15f : 0f), 0f, MathF.Sin(_time * 1.5f) * 0.04f),
            new Vector3(0f, bob + breathe - 0.16f * crouch, -0.15f * bite), squash: 0.12f * crouch);
        if (leap >= 0f)
        {
            // Up and down in an arc (the Core moves it along the ground).
            float height = 4f * MonstersModule.PounceHeight * leap * (1f - leap);
            _rig.Transform = new Transform3D(Basis.Identity, new Vector3(0f, height, 0f));
        }
        else if (crouch > 0f)
        {
            // A little wiggle just before the leap.
            _rig.Transform = new Transform3D(new Basis(Vector3.Up, MathF.Sin(_time * 40f) * 0.04f * crouch), Vector3.Zero);
        }
    }

    /// <summary>
    /// The ladybug: little steps of six stubby legs, the shell rocking side to side; it tucks its
    /// head and legs in while it curls up, then rolls over and over along the ground.
    /// </summary>
    private void Waddle(float bite, MonsterAction action, float actionSeconds, float speed, float delta)
    {
        float amount = 0.15f + 0.85f * _moveAmount;
        float tuck = action switch
        {
            MonsterAction.Curl => Math.Clamp(actionSeconds / (MonstersModule.ChargeWindup * 0.5f), 0f, 1f),
            MonsterAction.Charge => 1f,
            _ => 0f,
        };
        for (int i = 0; i < 6; i++)
        {
            bool left = i % 2 == 0;
            int pair = i / 2;
            bool groupA = left == (pair % 2 == 0);
            float s = MathF.Sin(_phase + (groupA ? 0f : Mathf.Pi));
            float lift = MathF.Max(0f, MathF.Cos(_phase + (groupA ? 0f : Mathf.Pi))) * 0.3f * amount;
            _legs[i].Pose(new Vector3(0f, s * 0.4f * amount, left ? -lift - 0.9f * tuck : lift + 0.9f * tuck),
                new Vector3(left ? 0.12f * tuck : -0.12f * tuck, 0.1f * tuck, 0f));
        }
        float rock = MathF.Sin(_phase) * 0.06f * amount;
        _shell.Pose(new Vector3(0f, 0f, rock), new Vector3(0f, MathF.Abs(MathF.Sin(_phase)) * 0.02f * amount, 0f));
        _head.Pose(new Vector3(-0.25f * bite + MathF.Sin(_time * 1.9f) * 0.05f, 0f, rock * 0.5f),
            new Vector3(0f, -0.08f * tuck, -0.15f * bite + 0.3f * tuck));
        if (action == MonsterAction.Charge)
        {
            // Rolling forward like a ball: turn about the shell's middle as it covers the ground.
            _roll = (_roll + delta * speed / 0.42f) % Mathf.Tau;
            var pivot = new Vector3(0f, 0.42f, 0f);
            var basis = new Basis(Vector3.Right, -_roll);
            _rig.Transform = new Transform3D(basis, pivot - basis * pivot);
        }
        else
        {
            _roll = 0f;
            if (tuck > 0f)
                _rig.Transform = new Transform3D(new Basis(Vector3.Forward, MathF.Sin(_time * 30f) * 0.05f * tuck), Vector3.Zero);
        }
    }
}
