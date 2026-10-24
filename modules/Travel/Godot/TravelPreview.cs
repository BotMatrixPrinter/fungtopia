using System;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Travel.View;

/// <summary>
/// A stand-alone scene for screenshots: <c>./screenshot.sh modules/Travel/Godot/Preview.tscn</c>.
/// It runs a real <see cref="TravelModule"/> with a <see cref="TravelView"/> by the Town signpost
/// on a patch of the Map's ground. <c>TRAVEL_PREVIEW=map</c> (default) opens the map with Mushroom
/// Forest selected; <c>signpost</c> leaves it closed and shows the signpost and its prompt.
/// <c>TRAVEL_DEVICE=keyboard</c> (default), <c>xbox</c>, <c>playstation</c> or <c>nintendo</c> picks
/// the active device the prompts are shown for.
/// </summary>
public partial class TravelPreview : Node3D
{
    private sealed class SilentBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }

    private readonly TravelModule _travel = new();
    private readonly System.Collections.Generic.List<Texture2D> _icons = new();
    private System.Numerics.Vector3 _feet;
    private bool _map;
    private DeviceKind _device;
    private long _tick;

    public override void _Ready()
    {
        _map = (OS.GetEnvironment("TRAVEL_PREVIEW") ?? "") != "signpost";
        _device = (OS.GetEnvironment("TRAVEL_DEVICE") ?? "") switch
        {
            "xbox" => DeviceKind.Xbox,
            "playstation" => DeviceKind.PlayStation,
            "nintendo" => DeviceKind.Nintendo,
            _ => DeviceKind.KeyboardMouse,
        };
        var post = Places.SignpostTown;
        _feet = post.Position + new System.Numerics.Vector3(-1.4f, 0f, 1.2f);
        _travel.Start(new SilentBus());
        LoadIcons();
        BuildStage(post);

        var view = (TravelView)GD.Load<PackedScene>("res://modules/Travel/Godot/Main.tscn").Instantiate();
        view.Bind(_travel);
        AddChild(view);

        Step(openMap: _map);
        if (_map)
            _travel.Step(1);
    }

    public override void _PhysicsProcess(double delta) => Step(false);

    private void Step(bool openMap)
    {
        _travel.Handle(new ActiveDevice(_device));
        _travel.Handle(new PlayerInput(System.Numerics.Vector2.Zero, System.Numerics.Vector2.Zero, false, false, false,
            System.Numerics.Vector3.Zero, false, openMap));
        _travel.Handle(new PlayerState(_feet, 0f, System.Numerics.Vector3.Zero, true, 10, 10, true, CharacterKind.Girl));
        _travel.Tick(new TickContext(_tick++, 1.0 / 60));
    }

    private void BuildStage(Place post)
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky
            {
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = new Color(0.45f, 0.65f, 0.95f),
                    SkyHorizonColor = new Color(0.85f, 0.9f, 1.0f),
                    GroundBottomColor = new Color(0.3f, 0.32f, 0.25f),
                    GroundHorizonColor = new Color(0.8f, 0.85f, 0.75f),
                },
            },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.62f, 0.58f, 0.72f),
            AmbientLightEnergy = 0.9f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapWhite = 6f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        AddChild(new DirectionalLight3D
        {
            LightColor = new Color(1.0f, 0.92f, 0.78f),
            LightEnergy = 1.3f,
            ShadowEnabled = true,
            RotationDegrees = new Vector3(-45f, -35f, 0f),
        });

        var center = new Vector3(post.Position.X, 0f, post.Position.Z);
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int half = 25;
        for (int i = -half; i < half; i++)
        for (int j = -half; j < half; j++)
        {
            float x0 = center.X + i, z0 = center.Z + j, x1 = x0 + 1f, z1 = z0 + 1f;
            Vector3 a = Ground(x0, z0), b = Ground(x1, z0), c = Ground(x1, z1), d = Ground(x0, z1);
            st.AddVertex(a); st.AddVertex(b); st.AddVertex(c);
            st.AddVertex(a); st.AddVertex(c); st.AddVertex(d);
        }
        st.GenerateNormals();
        AddChild(new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.76f, 0.36f), Roughness = 1f },
        });

        var camera = new Camera3D { Fov = 50f };
        AddChild(camera);
        var p = new Vector3(post.Position.X, post.Position.Y, post.Position.Z);
        camera.LookAtFromPosition(p + new Vector3(-3.2f, 2.2f, 4.2f), p + new Vector3(0.2f, 1.3f, 0f));
        camera.Current = true;
    }

    /// <summary>
    /// In the game the Input view makes the prompt icons loadable (rasterizing the SVGs Godot has not
    /// imported); the preview runs without it, so it does the same for itself.
    /// </summary>
    private void LoadIcons()
    {
        foreach (var file in DirAccess.GetFilesAt(Prompts.GlyphDirectory))
        {
            string path = Prompts.GlyphDirectory + file;
            if (!file.EndsWith(".svg") || ResourceLoader.Exists(path))
                continue;
            var image = new Image();
            if (image.LoadSvgFromString(FileAccess.GetFileAsString(path), 2f) != Error.Ok)
                continue;
            var texture = ImageTexture.CreateFromImage(image);
            texture.TakeOverPath(path);
            _icons.Add(texture);
        }
    }

    private static Vector3 Ground(float x, float z) => new(x, Fungtopia.Map.Ground.HeightAt(x, z), z);
}
