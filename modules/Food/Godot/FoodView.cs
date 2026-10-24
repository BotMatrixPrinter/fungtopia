using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Food.View;

/// <summary>
/// Draws the Food Core: its <see cref="FoodPanels"/> (the stall's prompt, the menu and the food
/// bar, with pictures of the foods rendered from their models in small off-screen viewports), and
/// while eating the food held up in front of the player getting smaller bite by bite with crumbs
/// falling, then a little "+3 ♥" rising over the player's head. Reads the menu's own buttons and
/// hands them to the Core (Interact, Eat and the food selection come through the Input module).
/// </summary>
public partial class FoodView : Node3D, IModuleView
{
    /// <summary>Seconds the "+3 ♥" rises and fades.</summary>
    private const float HealSeconds = 1.3f;

    private FoodModule? _food;
    private FoodPanels _panels = null!;
    private readonly Node3D?[] _held = new Node3D?[FoodKinds.Count];
    private CpuParticles3D _crumbs = null!;
    private CpuParticles3D _burst = null!;
    private Label3D _healed = null!;
    private float _healTime = HealSeconds;
    private int _shownMeals;
    private float _time;

    public void Bind(IModule module) => _food = (FoodModule)module;

    /// <summary>The Core drawn (the preview binds its own).</summary>
    public FoodModule? Food => _food;

    public override void _Ready()
    {
        _panels = new FoodPanels();
        for (int kind = 0; kind < FoodKinds.Count; kind++)
            _panels.Pictures[kind] = Picture(kind);
        AddChild(_panels);

        for (int kind = 0; kind < FoodKinds.Count; kind++)
        {
            _held[kind] = FoodModels.Instance(FoodModels.Foods, FoodModels.Names[kind]);
            if (_held[kind] == null)
                continue;
            _held[kind]!.Visible = false;
            AddChild(_held[kind]);
        }

        var crumb = FoodModels.Instance(FoodModels.Foods, "Crumb");
        Mesh crumbMesh = FindMesh(crumb)?.Mesh ?? new BoxMesh { Size = new Vector3(0.014f, 0.012f, 0.01f) };
        crumb?.Free();
        var crumbMaterial = FoodModels.Toon(new Color(0.88f, 0.64f, 0.36f), false);
        _crumbs = Crumbs(crumbMesh, crumbMaterial, 18, false);
        _burst = Crumbs(crumbMesh, crumbMaterial, 24, true);
        AddChild(_crumbs);
        AddChild(_burst);

        _healed = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = 72,
            PixelSize = 0.004f,
            OutlineSize = 18,
            Modulate = new Color(1f, 0.4f, 0.55f),
            OutlineModulate = new Color(1f, 0.97f, 0.9f),
            Visible = false,
        };
        AddChild(_healed);
    }

    public override void _ExitTree() => FoodModels.Release();

    public override void _Process(double delta)
    {
        if (_food == null)
            return;
        float dt = (float)delta;
        _time += dt;
        var feet = _food.PlayerPosition;
        var at = new Vector3(feet.X, feet.Y, feet.Z);
        float facing = _food.PlayerFacing;
        var forward = new Vector3(-MathF.Sin(facing), 0f, -MathF.Cos(facing));

        // The food held up in front of the face, shrinking bite by bite (a nibble every 0.25 s).
        var mouth = at + forward * 0.32f + Vector3.Up * 0.95f;
        for (int kind = 0; kind < FoodKinds.Count; kind++)
        {
            var node = _held[kind];
            if (node == null)
                continue;
            bool eating = _food.Eating && _food.EatingKind == kind;
            node.Visible = eating;
            if (!eating)
                continue;
            float progress = _food.EatProgress;
            float bites = MathF.Floor(progress * 4f) / 4f;
            float scale = 1.5f * (1f - 0.55f * bites);
            float nibble = 0.03f * MathF.Max(0f, MathF.Sin(_time * 25f));
            node.Transform = new Transform3D(new Basis(Vector3.Up, facing).Scaled(new Vector3(scale, scale, scale)),
                mouth + Vector3.Up * nibble);
        }
        _crumbs.GlobalPosition = mouth + Vector3.Up * 0.12f;
        _crumbs.Emitting = _food.Eating;

        if (_food.Meals != _shownMeals)
        {
            _shownMeals = _food.Meals;
            _healTime = 0f;
            _healed.Text = FoodText.Healed(_food.LastHealed);
            _burst.GlobalPosition = mouth;
            _burst.Restart();
        }
        _healTime += dt;
        _healed.Visible = _healTime < HealSeconds;
        if (_healed.Visible)
        {
            float t = _healTime / HealSeconds;
            _healed.Position = at + Vector3.Up * (1.9f + 0.6f * t);
            float fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            _healed.Modulate = new Color(1f, 0.4f, 0.55f, fade);
            _healed.OutlineModulate = new Color(1f, 0.97f, 0.9f, fade);
            _healed.Scale = Vector3.One * (t < 0.15f ? 0.6f + 0.4f * t / 0.15f : 1f);
        }

        _panels.Refresh(_food);
    }

    /// <summary>A picture of <paramref name="kind"/>: its model in a small off-screen viewport of its own, turned a little toward the camera.</summary>
    private Texture2D? Picture(int kind)
    {
        var model = FoodModels.Instance(FoodModels.Foods, FoodModels.Names[kind]);
        if (model == null)
            return null;
        var viewport = new SubViewport
        {
            Size = new Vector2I(160, 160),
            OwnWorld3D = true,
            TransparentBg = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        AddChild(viewport);
        viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(1f, 0.92f, 0.9f),
                AmbientLightEnergy = 0.6f,
            },
        });
        viewport.AddChild(new DirectionalLight3D
        {
            LightColor = new Color(1f, 0.95f, 0.88f),
            LightEnergy = 1.1f,
            RotationDegrees = new Vector3(-50f, -30f, 0f),
        });
        model.Rotation = new Vector3(0f, -0.4f, 0f);
        viewport.AddChild(model);
        var camera = new Camera3D { Fov = 30f, Current = true };
        viewport.AddChild(camera);
        camera.LookAtFromPosition(new Vector3(0f, 0.52f, 0.4f), new Vector3(0f, 0.07f, 0f), Vector3.Up);
        return viewport.GetTexture();
    }

    private static MeshInstance3D? FindMesh(Node? node)
    {
        if (node == null)
            return null;
        if (node is MeshInstance3D mesh)
            return mesh;
        foreach (var child in node.GetChildren())
            if (FindMesh(child) is { } found)
                return found;
        return null;
    }

    /// <summary>Crumbs falling: a steady trickle while eating, or a one-shot burst when the meal is done.</summary>
    private static CpuParticles3D Crumbs(Mesh mesh, Material material, int amount, bool burst)
    {
        var particles = new CpuParticles3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            Amount = amount,
            Lifetime = burst ? 0.9f : 0.7f,
            OneShot = burst,
            Explosiveness = burst ? 1f : 0f,
            Emitting = false,
            LocalCoords = false,
            Direction = Vector3.Up,
            Spread = burst ? 70f : 45f,
            InitialVelocityMin = burst ? 0.8f : 0.3f,
            InitialVelocityMax = burst ? 1.6f : 0.8f,
            Gravity = new Vector3(0f, -6f, 0f),
            AngularVelocityMin = -360f,
            AngularVelocityMax = 360f,
            ScaleAmountMin = 1.2f,
            ScaleAmountMax = 2.2f,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.06f,
        };
        return particles;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_food == null || !_food.MenuOpen)
            return;
        // E / X come through the Input module as Interact; Up / Down come as MenuNav (Core).
        if (Pressed(e, Key.Q, JoyButton.B) || Pressed(e, Key.Escape, JoyButton.B))
            _food.Back();
        else if (Pressed(e, Key.Enter, JoyButton.A) || Pressed(e, Key.KpEnter, JoyButton.A))
            _food.Confirm();
    }

    private static bool Pressed(InputEvent e, Key key, JoyButton button) => e switch
    {
        InputEventKey k => k.Pressed && !k.Echo && (k.PhysicalKeycode == key || k.Keycode == key),
        InputEventJoypadButton j => j.Pressed && j.ButtonIndex == button,
        _ => false,
    };
}
