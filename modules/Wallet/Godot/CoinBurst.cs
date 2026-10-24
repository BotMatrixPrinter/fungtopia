using System.Collections.Generic;
using Godot;

namespace Fungtopia.Wallet.View;

/// <summary>
/// One burst of Mushroom Coins (coin.glb from modules/Wallet/Models), toon-shaded with ink
/// outlines: a handful of gold coins pop out of a kill, fly up spinning, fall back a little and
/// shrink away, with golden sparkles and a "+50 M¢" rising above them. Built once and replayed
/// with <see cref="Play"/>, so a burst does not allocate.
/// </summary>
public partial class CoinBurst : Node3D
{
    private const string Models = "res://modules/Wallet/Models/";
    private const string Shaders = "res://modules/Wallet/Godot/Shaders/";
    /// <summary>Seconds a burst lasts.</summary>
    public const float Seconds = 1.4f;
    private const int CoinCount = 7;
    private const float Gravity = 7f;

    /// <summary>Surfaces drawn without an ink outline (small soft details).</summary>
    private static readonly HashSet<string> NoOutline = new() { "EyeShine", "Spots", "Eye" };
    private static readonly Dictionary<string, ShaderMaterial> s_materials = new();
    private static (GltfDocument, GltfState)? s_model;
    private static bool s_loaded;
    private static Shader? s_toon;
    private static ShaderMaterial? s_outline;

    private readonly Node3D[] _coins = new Node3D[CoinCount];
    private readonly Vector3[] _velocity = new Vector3[CoinCount];
    private readonly float[] _spin = new float[CoinCount];
    private CpuParticles3D _sparkles = null!;
    private Label3D _label = null!;
    private float _time = Seconds;

    /// <summary>True while the burst is playing.</summary>
    public bool Playing => _time < Seconds;

    public static CoinBurst Create()
    {
        var burst = new CoinBurst { Name = "CoinBurst", Visible = false };
        burst.Build();
        return burst;
    }

    /// <summary>One toon-shaded coin, its faces looking along +Z and -Z.</summary>
    public static Node3D CreateCoin()
    {
        var coin = new Node3D { Name = "Coin" };
        if (LoadModel() is { } model)
        {
            coin.AddChild(model);
            ApplyToon(model);
        }
        return coin;
    }

    private void Build()
    {
        for (int i = 0; i < CoinCount; i++)
        {
            _coins[i] = CreateCoin();
            _coins[i].Name = $"Coin{i}";
            AddChild(_coins[i]);
        }
        _sparkles = new CpuParticles3D
        {
            Name = "Sparkles",
            Emitting = false,
            OneShot = true,
            Explosiveness = 0.9f,
            Amount = 24,
            Lifetime = 0.9,
            Mesh = new SphereMesh { Radius = 0.045f, Height = 0.09f, RadialSegments = 6, Rings = 3 },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.25f,
            Direction = Vector3.Up,
            Spread = 70f,
            InitialVelocityMin = 1.5f,
            InitialVelocityMax = 3f,
            Gravity = new Vector3(0f, -2f, 0f),
            DampingMin = 1.5f,
            DampingMax = 2.5f,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.4f,
            ColorRamp = new Gradient
            {
                Offsets = new[] { 0f, 0.6f, 1f },
                Colors = new[] { new Color(1.8f, 1.75f, 1.3f, 1f), new Color(1.7f, 1.45f, 0.6f, 1f), new Color(1.6f, 1.3f, 0.45f, 0f) },
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true,
                VertexColorIsSrgb = true,
            },
        };
        AddChild(_sparkles);
        _label = new Label3D
        {
            Name = "Amount",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 56,
            OutlineSize = 14,
            PixelSize = 0.006f,
            Modulate = new Color(1.3f, 0.95f, 0.1f),
            OutlineModulate = new Color(0.36f, 0.18f, 0.08f),
            NoDepthTest = true,
            RenderPriority = 2,
            OutlineRenderPriority = 1,
        };
        AddChild(_label);
    }

    /// <summary>Starts the burst at <paramref name="position"/> (the feet of the defeated monster).</summary>
    public void Play(Vector3 position, int amount, int seed)
    {
        Position = position;
        Visible = true;
        _time = 0f;
        _label.Text = $"+{amount} M¢";
        for (int i = 0; i < CoinCount; i++)
        {
            // Spread round in a ring, each burst turned a little differently.
            float angle = Mathf.Tau * (i + 0.37f * seed) / CoinCount;
            float wobble = Hash(seed * 31 + i);
            float outward = 0.9f + 0.8f * wobble;
            _velocity[i] = new Vector3(Mathf.Cos(angle) * outward, 3.6f + 1.2f * Hash(seed * 17 + i * 7), Mathf.Sin(angle) * outward);
            _spin[i] = (8f + 6f * wobble) * (i % 2 == 0 ? 1f : -1f);
        }
        _sparkles.Position = new Vector3(0f, 0.5f, 0f);
        _sparkles.Restart();
        Pose();
    }

    public override void _Process(double delta)
    {
        if (!Playing)
            return;
        _time += (float)delta;
        if (!Playing)
        {
            Visible = false;
            return;
        }
        Pose();
    }

    private void Pose()
    {
        float t = _time;
        // Pop in quickly, shrink away in the last third.
        float grow = Mathf.Clamp(t / 0.12f, 0f, 1f);
        float shrink = Mathf.Clamp((Seconds - t) / (Seconds * 0.35f), 0f, 1f);
        float scale = 1.6f * Mathf.Ease(grow, 0.4f) * shrink;
        for (int i = 0; i < CoinCount; i++)
        {
            var v = _velocity[i];
            var offset = new Vector3(v.X * t, 0.5f + v.Y * t - 0.5f * Gravity * t * t, v.Z * t);
            offset.Y = Mathf.Max(offset.Y, 0.15f);
            var basis = new Basis(Vector3.Up, _spin[i] * t + i).Scaled(new Vector3(scale, scale, scale));
            _coins[i].Transform = new Transform3D(basis, offset);
        }
        float rise = 1.1f + 0.9f * Mathf.Ease(Mathf.Clamp(t / Seconds, 0f, 1f), 0.5f);
        _label.Position = new Vector3(0f, rise, 0f);
        float alpha = Mathf.Clamp((Seconds - t) / 0.4f, 0f, 1f);
        _label.Modulate = new Color(1.3f, 0.95f, 0.1f, alpha);
        _label.OutlineModulate = new Color(0.36f, 0.18f, 0.08f, alpha);
    }

    private static float Hash(int n)
    {
        uint x = (uint)n * 2654435761u;
        x ^= x >> 15;
        x *= 2246822519u;
        x ^= x >> 13;
        return (x & 0xFFFF) / 65535f;
    }

    /// <summary>
    /// A new copy of the coin model. The .glb is read once at run time from its bytes, so the
    /// game runs without the editor having imported it first (a fresh checkout loads headless).
    /// </summary>
    private static Node3D? LoadModel()
    {
        if (!s_loaded)
        {
            s_loaded = true;
            string path = Models + "coin.glb";
            byte[] bytes = FileAccess.GetFileAsBytes(path);
            var document = new GltfDocument();
            var state = new GltfState();
            if (bytes.Length == 0)
                GD.PushWarning($"Fungtopia: Wallet: model {path} is missing.");
            else if (document.AppendFromBuffer(bytes, Models, state) is var error && error != Error.Ok)
                GD.PushWarning($"Fungtopia: Wallet: model {path} could not be read ({error}).");
            else
                s_model = (document, state);
        }
        return s_model is var (doc, st) ? doc.GenerateScene(st) as Node3D : null;
    }

    private static void ApplyToon(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                mesh.SetSurfaceOverrideMaterial(i, ToonMaterial(mesh.Mesh.SurfaceGetMaterial(i)));
        }
        foreach (var child in node.GetChildren())
            ApplyToon(child);
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
        material.SetShaderParameter("self_light", name switch { "EyeShine" => 0.6f, "Gold" or "GoldRim" => 0.25f, _ => 0f });
        if (!NoOutline.Contains(name))
            material.NextPass = s_outline;
        s_materials[key] = material;
        return material;
    }
}
