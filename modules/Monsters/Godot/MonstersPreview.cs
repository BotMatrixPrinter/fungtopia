using System;
using Godot;

namespace Fungtopia.Monsters.View;

/// <summary>
/// A stand-alone scene showing the monsters on a patch of grass, for screenshots:
/// <c>./screenshot.sh modules/Monsters/Godot/Preview.tscn</c>. From left to right: a caterpillar
/// crawling, an ant scurrying, a hurt and poisoned caterpillar (green, bubbles, health bar), a
/// hurt ant, and an ant popping into its defeat poof. The environment variable MONSTERS_PREVIEW
/// picks the view: "all" (default) or "close" (a caterpillar and an ant up close).
/// </summary>
public partial class MonstersPreview : Node3D
{
    private MonsterModel[] _models = Array.Empty<MonsterModel>();
    private float _time;
    private bool _close;

    public override void _Ready()
    {
        _close = OS.GetEnvironment("MONSTERS_PREVIEW").Equals("close", StringComparison.OrdinalIgnoreCase);
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
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 7f, BottomRadius = 7f, Height = 0.1f },
            Position = new Vector3(0f, -0.05f, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.74f, 0.33f) },
        });

        var kinds = _close
            ? new[] { MonsterKind.Caterpillar, MonsterKind.Ant }
            : new[] { MonsterKind.Caterpillar, MonsterKind.Ant, MonsterKind.Caterpillar, MonsterKind.Ant, MonsterKind.Ant };
        _models = new MonsterModel[kinds.Length];
        float spacing = _close ? 1.6f : 1.7f;
        for (int i = 0; i < kinds.Length; i++)
        {
            _models[i] = MonsterModel.Create(kinds[i]);
            AddChild(_models[i]);
            float x = (i - (kinds.Length - 1) * 0.5f) * spacing;
            // Three-quarter views facing the camera (+Z).
            float yaw = Mathf.Pi + (i % 2 == 0 ? -0.6f : 0.6f);
            _models[i].Transform = new Transform3D(new Basis(Vector3.Up, yaw), new Vector3(x, 0f, 0f));
        }

        var camera = new Camera3D { Fov = 40f, Current = true };
        AddChild(camera);
        if (_close)
            camera.LookAtFromPosition(new Vector3(0f, 1.3f, 3.6f), new Vector3(0f, 0.45f, 0f), Vector3.Up);
        else
            camera.LookAtFromPosition(new Vector3(0f, 2.0f, 7.4f), new Vector3(0f, 0.5f, 0f), Vector3.Up);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        for (int i = 0; i < _models.Length; i++)
        {
            var model = _models[i];
            switch (i)
            {
                case 0:
                    model.Animate(dt, 0.5f, 100f, false, 100f, true, 0f, 1f);
                    break;
                case 1:
                    model.Animate(dt, 0.9f, 100f, false, 100f, true, 0f, 1f);
                    break;
                case 2:
                    model.Animate(dt, 0.3f, 100f, true, 100f, true, 0f, 5f / 8f);
                    break;
                case 3:
                    // Biting now and then.
                    model.Animate(dt, 0f, 100f, false, _time % 1.2f, true, 0f, 3f / 5f);
                    break;
                default:
                    // Defeated 1.25 s in, so the default 90-frame screenshot catches the poof.
                    float dead = _time - 1.25f;
                    model.Animate(dt, 0f, dead < 0f ? 100f : 0f, false, 100f, dead < 0f, MathF.Max(0f, dead),
                        dead < 0f ? 1f / 5f : 0f);
                    break;
            }
        }
    }
}
