using System;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.NPCs.View;

/// <summary>
/// A stand-alone scene for screenshots: <c>./screenshot.sh modules/NPCs/Godot/Preview.tscn</c>.
/// Dr. Jeremy, Millie, Lucy, Abby and Pip side by side on a patch of grass, Millie with her "E / X: Talk"
/// prompt. <c>NPC_PREVIEW=jeremy</c>, <c>millie</c>, <c>lucy</c>, <c>abby</c> or <c>pip</c> shows only that
/// one, closer. <c>NPC_PREVIEW=stall</c> shows Pip behind the Mushroom Munchies counter in Mushroom Town
/// (the Map and Town modules' scenes, loaded read-only), seen by a player standing at
/// <see cref="Places.FoodSpot"/>, with the "E / X: Food" prompt.
/// </summary>
public partial class NpcPreview : Node3D
{
    private sealed class SilentBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }

    private Camera3D _camera = null!;
    private NPCsModule? _npcs;
    private long _tick;
    private NpcFigure? _prompted;
    private PanelContainer _prompt = null!;

    public override void _Ready()
    {
        string only = (OS.GetEnvironment("NPC_PREVIEW") ?? "").ToLowerInvariant();
        int single = only switch
        {
            "jeremy" => NPCsModule.Jeremy, "millie" => NPCsModule.Millie, "lucy" => NPCsModule.Lucy,
            "abby" => NPCsModule.Abby, "pip" => NPCsModule.Pip, _ => -1,
        };
        _camera = new Camera3D { Fov = 40f };
        AddChild(_camera);
        if (only == "stall")
        {
            BuildStall();
            _camera.Current = true;
            return;
        }
        BuildStage();
        if (single >= 0)
        {
            var figure = NpcFigure.Create(single);
            figure.Rotation = new Vector3(0f, MathF.PI - 0.25f, 0f);
            AddChild(figure);
            float tall = single == NPCsModule.Pip ? 0.2f : 0f;
            _camera.LookAtFromPosition(new Vector3(0.6f, 1.25f + tall, 3.0f + tall), new Vector3(0f, 0.85f + tall, 0f));
        }
        else
        {
            for (int i = 0; i < NPCsModule.Count; i++)
            {
                var figure = NpcFigure.Create(i);
                float at = i - (NPCsModule.Count - 1) * 0.5f;
                figure.Position = new Vector3(at * 1.35f, 0f, 0f);
                figure.Rotation = new Vector3(0f, MathF.PI - at * 0.2f, 0f);
                AddChild(figure);
                if (i == NPCsModule.Millie)
                    _prompted = figure;
            }
            _camera.LookAtFromPosition(new Vector3(0f, 1.5f, 7.6f), new Vector3(0f, 0.85f, 0f));
            var layer = new CanvasLayer { Layer = 11 };
            AddChild(layer);
            (_prompt, _) = NPCsView.BuildPrompt();
            layer.AddChild(_prompt);
        }
        _camera.Current = true;
    }

    /// <summary>The town around the food stall, the NPCs' own view bound to a Core whose player stands at the counter.</summary>
    private void BuildStall()
    {
        BuildLight();
        foreach (var path in new[] { "res://modules/Map/Godot/Main.tscn", "res://modules/Town/Godot/Main.tscn" })
            if (GD.Load<PackedScene>(path) is { } scene)
                AddChild(scene.Instantiate());
        _npcs = new NPCsModule();
        _npcs.Start(new SilentBus());
        var view = (NPCsView)GD.Load<PackedScene>("res://modules/NPCs/Godot/Main.tscn").Instantiate();
        view.Bind(_npcs);
        AddChild(view);
        // The camera over the shoulder of a player at the counter, a little to the side.
        var spot = Places.FoodSpot;
        var side = new System.Numerics.Vector3(spot.Facing.Z, 0f, -spot.Facing.X);
        var eye = spot.Position + spot.Facing * 2.6f + side * 1.3f + new System.Numerics.Vector3(0f, 1.9f, 0f);
        var pip = NPCsModule.Position(NPCsModule.Pip);
        _camera.LookAtFromPosition(new Vector3(eye.X, eye.Y, eye.Z), new Vector3(pip.X, pip.Y + 1.2f, pip.Z));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_npcs == null)
            return;
        // The player walks up to the counter after a second (so Pip waves), and stays.
        var spot = Places.FoodSpot;
        var at = _tick < 30 ? spot.Position + spot.Facing * 7f : spot.Position;
        _npcs.Handle(new PlayerState(at, 0f, System.Numerics.Vector3.Zero, true, 10, 10, true, CharacterKind.Girl));
        _npcs.Tick(new TickContext(_tick++, 1.0 / 60));
    }

    public override void _Process(double delta)
    {
        if (_prompted == null)
            return;
        var size = _prompt.GetCombinedMinimumSize();
        var top = _prompted.Position + new Vector3(0f, _prompted.Height + 0.25f, 0f);
        _prompt.Position = _camera.UnprojectPosition(top) - new Vector2(size.X * 0.5f, size.Y);
        _prompt.Visible = true;
    }

    private void BuildStage()
    {
        BuildLight();
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(40f, 40f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.76f, 0.36f), Roughness = 1f },
        });
    }

    private void BuildLight()
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
            RotationDegrees = new Vector3(-45f, -30f, 0f),
            DirectionalShadowMaxDistance = 60f,
        });
    }
}
