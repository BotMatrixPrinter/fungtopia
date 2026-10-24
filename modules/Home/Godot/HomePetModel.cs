using System;
using Fungtopia.Pet;
using Godot;

namespace Fungtopia.Home.View;

/// <summary>
/// A pet at home: the Pet module's model (cat.glb, dog.glb or crow.glb), toon-shaded, with a
/// simple procedural animation: a bouncy walk, hops while playing, and curled up asleep with
/// little floating "z"s while napping.
/// </summary>
public partial class HomePetModel : Node3D
{
    private readonly Node3D _rig = new() { Name = "Rig" };
    private Label3D _snore = null!;
    private PetKind _kind;
    private float _phase;
    private float _sleep;
    private float _time;
    private float _yaw;

    public static HomePetModel Create(PetKind kind)
    {
        var model = new HomePetModel { Name = "Home" + kind, _kind = kind };
        model.AddChild(model._rig);
        var scene = HomeModels.Instance(HomeModels.PetModels + kind switch
        {
            PetKind.Dog => "dog.glb",
            PetKind.Crow => "crow.glb",
            _ => "cat.glb",
        });
        if (scene != null)
            model._rig.AddChild(scene);
        model._snore = new Label3D
        {
            Text = "z z",
            FontSize = 48,
            OutlineSize = 10,
            Modulate = new Color(0.55f, 0.6f, 0.95f),
            OutlineModulate = new Color(1f, 1f, 1f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.004f,
            Position = new Vector3(0.15f, 0.7f, 0f),
            Visible = false,
        };
        model.AddChild(model._snore);
        return model;
    }

    public void Show(in HomePet pet, float delta)
    {
        _time += delta;
        var target = new Vector3(pet.Position.X, pet.Position.Y, pet.Position.Z);
        // Glide toward the Core's position (it moves at most a few centimeters a tick).
        Position = Position.DistanceTo(target) > 1f ? target : Position.Lerp(target, Math.Min(1f, delta * 20f));
        if (pet.Speed > 0.05f)
            _yaw = pet.Facing;
        Rotation = new Vector3(0f, Mathf.LerpAngle(Rotation.Y, _yaw, Math.Min(1f, delta * 10f)), 0f);

        _sleep = Mathf.MoveToward(_sleep, pet.Asleep ? 1f : 0f, delta * 2f);
        float moving = Math.Clamp(pet.Speed / 1f, 0f, 1f);
        bool playing = pet.Activity == PetActivity.Play;
        _phase += delta * (playing ? 14f : 10f) * moving;
        float hop = MathF.Abs(MathF.Sin(_phase)) * (playing ? 0.12f : 0.04f) * moving;
        float breathe = 1f + MathF.Sin(_time * (pet.Asleep ? 1.6f : 2.6f)) * (pet.Asleep ? 0.04f : 0.015f);
        // Asleep: curled up (low, round and a little turned), breathing slowly.
        float curl = Mathf.SmoothStep(0f, 1f, _sleep);
        float bedLift = pet.OnBed ? 0.05f : 0f;
        _rig.Transform = new Transform3D(
            Basis.FromEuler(new Vector3(0.15f * curl, 0.8f * curl, 0f))
                .Scaled(new Vector3(1f + 0.12f * curl, (1f - 0.35f * curl) * breathe, 1f + 0.08f * curl)),
            new Vector3(0f, hop + bedLift, 0f));
        _snore.Visible = pet.Asleep;
        if (pet.Asleep)
        {
            float rise = (_time * 0.4f) % 1f;
            _snore.Position = new Vector3(0.15f + 0.1f * rise, 0.55f + 0.35f * rise, 0f);
            _snore.Modulate = new Color(0.55f, 0.6f, 0.95f, 1f - rise);
        }
    }
}
