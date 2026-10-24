using System;
using Godot;

namespace Fungtopia.NPCs.View;

/// <summary>
/// A stand-alone scene for screenshots: <c>./screenshot.sh modules/NPCs/Godot/Preview.tscn</c>.
/// Dr. Jeremy, Millie, Lucy and Abby side by side on a patch of grass, Millie with her "E / X: Talk"
/// prompt. <c>NPC_PREVIEW=jeremy</c>, <c>millie</c>, <c>lucy</c> or <c>abby</c> shows only that one, closer.
/// </summary>
public partial class NpcPreview : Node3D
{
    private Camera3D _camera = null!;
    private NpcFigure? _prompted;
    private PanelContainer _prompt = null!;

    public override void _Ready()
    {
        string only = (OS.GetEnvironment("NPC_PREVIEW") ?? "").ToLowerInvariant();
        int single = only switch { "jeremy" => NPCsModule.Jeremy, "millie" => NPCsModule.Millie, "lucy" => NPCsModule.Lucy, "abby" => NPCsModule.Abby, _ => -1 };
        BuildStage();
        _camera = new Camera3D { Fov = 40f };
        AddChild(_camera);
        if (single >= 0)
        {
            var figure = NpcFigure.Create(single);
            figure.Rotation = new Vector3(0f, MathF.PI - 0.25f, 0f);
            AddChild(figure);
            _camera.LookAtFromPosition(new Vector3(0.6f, 1.25f, 3.0f), new Vector3(0f, 0.85f, 0f));
        }
        else
        {
            for (int i = 0; i < NPCsModule.Count; i++)
            {
                var figure = NpcFigure.Create(i);
                figure.Position = new Vector3((i - 1.5f) * 1.35f, 0f, 0f);
                figure.Rotation = new Vector3(0f, MathF.PI + (1.5f - i) * 0.2f, 0f);
                AddChild(figure);
                if (i == NPCsModule.Millie)
                    _prompted = figure;
            }
            _camera.LookAtFromPosition(new Vector3(0f, 1.5f, 6.4f), new Vector3(0f, 0.85f, 0f));
            var layer = new CanvasLayer { Layer = 11 };
            AddChild(layer);
            (_prompt, _) = NPCsView.BuildPrompt();
            layer.AddChild(_prompt);
        }
        _camera.Current = true;
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
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(40f, 40f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.76f, 0.36f), Roughness = 1f },
        });
    }
}
