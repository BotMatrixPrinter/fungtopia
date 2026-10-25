using System;
using Godot;

namespace Fungtopia.Pet.View;

/// <summary>
/// A stand-alone scene showing the Mushroom Cat, the Mushroom Dog and the Mushroom Crow (hovering
/// above them) on a patch of grass, for screenshots: <c>./screenshot.sh modules/Pet/Godot/Preview.tscn</c>.
/// The environment variable PET_PREVIEW picks the pose: "front" (default), "side", "trot", "sit",
/// "pounce" (the hop of a bite), "dash" (galloping to an aimed monster, the Crow swooping),
/// "crouch" and "leap" (the start and the middle of the pounce onto it); "crow" shows the Crow
/// alone close up, flying, "crow-peck" pecking and "crow-dash" swooping.
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

        var camera = new Camera3D { Fov = 40f, Current = true };
        AddChild(camera);
        if (_pose.StartsWith("crow", StringComparison.Ordinal))
        {
            _models = new[] { PetModel.Create(PetKind.Crow) };
            AddChild(_models[0]);
            _models[0].Transform = new Transform3D(new Basis(Vector3.Up, Mathf.Pi - 0.45f), new Vector3(0f, 0.55f, 0f));
            camera.LookAtFromPosition(new Vector3(0f, 1.25f, 2.0f), new Vector3(0f, 0.8f, 0f), Vector3.Up);
            return;
        }

        _models = new[] { PetModel.Create(PetKind.Cat), PetModel.Create(PetKind.Dog), PetModel.Create(PetKind.Crow) };
        bool side = _pose is "side" or "trot" or "pounce" or "dash" or "crouch" or "leap";
        for (int i = 0; i < _models.Length; i++)
        {
            AddChild(_models[i]);
            // Front: turned toward the camera; side: facing right, showing their profiles. The
            // crow hovers between and above the other two.
            float yaw = side ? -Mathf.Pi * 0.5f - 0.35f : Mathf.Pi + (i == 0 ? -0.4f : i == 1 ? 0.4f : 0f);
            var at = i == 2 ? new Vector3(0f, 0.95f, -0.35f) : new Vector3(i == 0 ? -0.55f : 0.55f, _pose == "leap" ? 0.5f : 0f, 0f);
            _models[i].Transform = new Transform3D(new Basis(Vector3.Up, yaw), at);
        }
        camera.LookAtFromPosition(new Vector3(0f, 1.1f, 3.2f), new Vector3(0f, 0.5f, 0f), Vector3.Up);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        foreach (var model in _models)
        {
            // The crow flies in every pose but sitting.
            float altitude = model.Kind == PetKind.Crow && _pose != "sit" ? 2f : 0f;
            if (model.Kind == PetKind.Crow && _pose == "crow-peck")
            {
                model.Animate(dt, 0f, false, 0.12f, true, altitude);
                continue;
            }
            if (model.Kind == PetKind.Crow && _pose is "crow-dash" or "dash")
            {
                model.Animate(dt, 12f, false, 100f, true, altitude, dashing: true);
                continue;
            }
            if (model.Kind == PetKind.Crow)
            {
                model.Animate(dt, _pose == "trot" ? 4f : 0f, false, _pose == "pounce" ? 0.12f : 100f, _pose == "pounce", altitude);
                continue;
            }
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
                case "dash":
                    model.Animate(dt, 14f, false, 100f, true, dashing: true);
                    break;
                case "crouch":
                    model.Animate(dt, 0f, false, 100f, true, pouncing: true, pounceProgress: 0.06f);
                    break;
                case "leap":
                    // The middle of the leap, lifted to its height on the arc (0.5 m at the top).
                    model.Animate(dt, 0f, false, 100f, true, pouncing: true, pounceProgress: 0.45f);
                    break;
                default:
                    model.Animate(dt, 0f, false, 100f, false);
                    break;
            }
        }
    }
}
