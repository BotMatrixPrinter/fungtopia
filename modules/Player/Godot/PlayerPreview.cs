using System;
using Godot;

namespace Fungtopia.Player.View;

/// <summary>
/// A stand-alone scene showing the Girl and the Boy side by side on a patch of grass, for
/// screenshots: <c>./screenshot.sh modules/Player/Godot/Preview.tscn</c>. The environment
/// variable PLAYER_PREVIEW picks the pose: "front" (default), "back" (shows the bow on the
/// back), "walk", "jump" or "hit".
/// </summary>
public partial class PlayerPreview : Node3D
{
    private CharacterModel[] _models = Array.Empty<CharacterModel>();
    private string _pose = "front";
    private float _time;

    public override void _Ready()
    {
        string pose = OS.GetEnvironment("PLAYER_PREVIEW");
        if (!string.IsNullOrEmpty(pose))
            _pose = pose.ToLowerInvariant();

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
            RotationDegrees = new Vector3(-40f, -30f, 0f),
        });

        var ground = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 4f, BottomRadius = 4f, Height = 0.1f },
            Position = new Vector3(0f, -0.05f, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.74f, 0.33f) },
        };
        AddChild(ground);

        _models = new[] { CharacterModel.Create(CharacterKind.Girl), CharacterModel.Create(CharacterKind.Boy) };
        bool back = _pose == "back";
        for (int i = 0; i < _models.Length; i++)
        {
            AddChild(_models[i]);
            float yaw = (back ? 0f : Mathf.Pi) + (i == 0 ? -0.35f : 0.35f) * (back ? -1.5f : 1f);
            _models[i].Transform = new Transform3D(new Basis(Vector3.Up, yaw), new Vector3(i == 0 ? -0.75f : 0.75f, 0f, 0f));
        }

        var camera = new Camera3D { Fov = 40f, Current = true };
        AddChild(camera);
        camera.LookAtFromPosition(new Vector3(0f, 1.3f, 4.6f), new Vector3(0f, 0.68f, 0f), Vector3.Up);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        foreach (var model in _models)
        {
            switch (_pose)
            {
                case "walk":
                    model.Animate(dt, 5f, true, 0f, 0f, true);
                    break;
                case "jump":
                    model.Animate(dt, 0f, false, 3f, 0f, true);
                    break;
                case "hit":
                    model.Animate(dt, 0f, true, 0f, 0.8f, true);
                    break;
                default:
                    model.Animate(dt, 0f, true, 0f, 0f, true);
                    break;
            }
        }
    }
}
