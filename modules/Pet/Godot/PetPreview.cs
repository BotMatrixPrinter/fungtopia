using System;
using Godot;

namespace Fungtopia.Pet.View;

/// <summary>
/// A stand-alone scene showing the Mushroom Cat and the Mushroom Dog side by side on a patch of
/// grass, for screenshots: <c>./screenshot.sh modules/Pet/Godot/Preview.tscn</c>. The environment
/// variable PET_PREVIEW picks the pose: "front" (default), "side", "trot", "sit" or "pounce".
/// </summary>
public partial class PetPreview : Node3D
{
    private PetModel[] _models = Array.Empty<PetModel>();
    private string _pose = "front";
    private float _time;

    public override void _Ready()
    {
        string pose = OS.GetEnvironment("PET_PREVIEW");
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

        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 3f, BottomRadius = 3f, Height = 0.1f },
            Position = new Vector3(0f, -0.05f, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.74f, 0.33f) },
        });

        _models = new[] { PetModel.Create(PetKind.Cat), PetModel.Create(PetKind.Dog) };
        bool side = _pose is "side" or "trot" or "pounce";
        for (int i = 0; i < _models.Length; i++)
        {
            AddChild(_models[i]);
            // Front: both turned toward the camera; side: both facing right, showing their profiles.
            float yaw = side ? -Mathf.Pi * 0.5f - 0.35f : Mathf.Pi + (i == 0 ? -0.4f : 0.4f);
            _models[i].Transform = new Transform3D(new Basis(Vector3.Up, yaw), new Vector3(i == 0 ? -0.55f : 0.55f, 0f, 0f));
        }

        var camera = new Camera3D { Fov = 40f, Current = true };
        AddChild(camera);
        camera.LookAtFromPosition(new Vector3(0f, 1.0f, 2.9f), new Vector3(0f, 0.38f, 0f), Vector3.Up);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        foreach (var model in _models)
        {
            switch (_pose)
            {
                case "trot":
                    model.Animate(dt, 4f, false, 100f, false);
                    break;
                case "sit":
                    model.Animate(dt, 0f, true, 100f, false);
                    break;
                case "pounce":
                    // Caught near the top of a pounce.
                    model.Animate(dt, 0f, false, 0.16f, true);
                    break;
                default:
                    model.Animate(dt, 0f, false, 100f, false);
                    break;
            }
        }
    }
}
