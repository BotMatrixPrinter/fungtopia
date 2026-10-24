using System;
using Fungtopia.Forest;
using Fungtopia.Input;
using Fungtopia.Player;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Bow.View;

/// <summary>
/// A stand-alone scene showing the bow, for screenshots:
/// <c>./screenshot.sh modules/Bow/Godot/Preview.tscn</c>. It runs a real <see cref="BowModule"/>
/// with scripted input and a <see cref="BowView"/> on a patch of forest ground, with the Girl
/// model (from the Player module's Models, for the look only) standing in for the player. A few
/// arrows are already stuck in the ground. The environment variable BOW_PREVIEW picks the
/// moment: "shot" (default: an arrow just flew off with its spore trail and the next one is
/// being drawn) or "draw" (holding a full draw).
/// </summary>
public partial class BowPreview : Node3D
{
    private static readonly System.Numerics.Vector3 Feet = new(0f, Terrain.HeightAt(0f, 10f), 10f);

    private sealed class SilentBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }

    private readonly BowModule _bow = new();
    private PlayerState _player;
    private bool _fullDraw;
    private long _tick;
    private int _frame;

    public override void _Ready()
    {
        _fullDraw = OS.GetEnvironment("BOW_PREVIEW").Equals("draw", StringComparison.OrdinalIgnoreCase);
        BuildStage();
        _bow.Start(new SilentBus());
        _player = new PlayerState(Feet, 0f, System.Numerics.Vector3.Zero, true, 10, 10, true, CharacterKind.Girl);

        // Earlier shots, now stuck in the ground ahead.
        float[] yaws = { -0.35f, 0.2f, 0.45f, -0.1f };
        float[] downs = { -0.12f, -0.09f, -0.16f, -0.07f };
        for (int i = 0; i < yaws.Length; i++)
        {
            var aim = new System.Numerics.Vector3(MathF.Sin(yaws[i]), downs[i], -MathF.Cos(yaws[i]));
            for (int k = 0; k < 12; k++)
                Step(true, aim);
            for (int k = 0; k < 40; k++)
                Step(false, aim);
        }

        var view = (BowView)GD.Load<PackedScene>("res://modules/Bow/Godot/Main.tscn").Instantiate();
        view.Bind(_bow);
        AddChild(view);
    }

    private void Step(bool fire, System.Numerics.Vector3 aim)
    {
        _bow.Handle(new PlayerInput(System.Numerics.Vector2.Zero, System.Numerics.Vector2.Zero, false, fire, true,
            System.Numerics.Vector3.Normalize(aim)));
        _bow.Handle(_player);
        _bow.Tick(new TickContext(_tick++, 1.0 / 60));
    }

    public override void _PhysicsProcess(double delta)
    {
        // Frames since the scene started: a shot leaves at frame 77, then the next draw begins.
        int frame = _frame++ % 600;
        var aim = new System.Numerics.Vector3(0.02f, 0.06f, -1f);
        bool fire = _fullDraw || frame < 77 || frame > 79;
        Step(fire, aim);
    }

    private void BuildStage()
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky
            {
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = new Color(0.5f, 0.62f, 0.9f),
                    SkyHorizonColor = new Color(1.0f, 0.78f, 0.58f),
                    GroundBottomColor = new Color(0.3f, 0.32f, 0.25f),
                    GroundHorizonColor = new Color(0.95f, 0.78f, 0.62f),
                },
            },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.62f, 0.55f, 0.72f),
            AmbientLightEnergy = 0.9f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapWhite = 6f,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.15f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        AddChild(new DirectionalLight3D
        {
            LightColor = new Color(1.0f, 0.88f, 0.7f),
            LightEnergy = 1.3f,
            ShadowEnabled = true,
            RotationDegrees = new Vector3(-40f, 30f, 0f),
        });

        // The forest ground around the player, from the Forest's Terrain.
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int half = 20;
        for (int i = -half; i < half; i++)
        for (int j = -half; j < half; j++)
        {
            float x0 = Feet.X + i, z0 = Feet.Z + j - 10f, x1 = x0 + 1f, z1 = z0 + 1f;
            Vector3 a = Ground(x0, z0), b = Ground(x1, z0), c = Ground(x1, z1), d = Ground(x0, z1);
            st.AddVertex(a); st.AddVertex(b); st.AddVertex(c);
            st.AddVertex(a); st.AddVertex(c); st.AddVertex(d);
        }
        st.GenerateNormals();
        AddChild(new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.74f, 0.33f), Roughness = 1f },
        });

        var girl = ToonModels.Load("res://modules/Player/Models/girl.glb");
        girl.Position = new Vector3(Feet.X, Feet.Y, Feet.Z);
        AddChild(girl);

        var camera = new Camera3D { Fov = 50f };
        AddChild(camera);
        var feet = new Vector3(Feet.X, Feet.Y, Feet.Z);
        camera.LookAtFromPosition(feet + new Vector3(2.3f, 1.2f, 0.9f), feet + new Vector3(-0.6f, 0.5f, -2.6f));
        camera.Current = true;
    }

    private static Vector3 Ground(float x, float z) => new(x, Terrain.HeightAt(x, z), z);
}
