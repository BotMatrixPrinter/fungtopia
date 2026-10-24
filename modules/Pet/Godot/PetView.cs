using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Pet.View;

/// <summary>
/// Draws the pet: the chosen pet's model placed between the last two fixed ticks, turned to face
/// <see cref="PetState.Facing"/>, and animated from its motion, bites and sitting. The pet setting
/// is <see cref="Pet"/> ("Cat" or "Dog"), which the command-line user argument <c>--pet=dog</c>
/// (after <c>--</c>) overrides.
/// </summary>
public partial class PetView : Node3D, IModuleView
{
    /// <summary>The pet setting: "Cat" (default) or "Dog".</summary>
    [Export]
    public string Pet { get; set; } = "Cat";

    private PetModule? _pet;
    private PetModel? _model;
    private PetState _previous;
    private PetState _current;
    private long _seenTicks = -1;
    private bool _jumped;

    public void Bind(IModule module) => _pet = (PetModule)module;

    public override void _Ready()
    {
        if (_pet == null)
            return;
        _pet.Kind = ChosenPet();
        _current = _previous = _pet.State;
        _seenTicks = _pet.Ticks;
        ShowModel(_pet.Kind);
    }

    private PetKind ChosenPet()
    {
        string setting = Pet;
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--pet=", StringComparison.OrdinalIgnoreCase))
                setting = arg.Substring("--pet=".Length);
        return setting.Equals("dog", StringComparison.OrdinalIgnoreCase) ? PetKind.Dog : PetKind.Cat;
    }

    private void ShowModel(PetKind kind)
    {
        _model?.QueueFree();
        _model = PetModel.Create(kind);
        AddChild(_model);
    }

    public override void _Process(double delta)
    {
        if (_pet == null || _model == null)
            return;
        if (_pet.Ticks != _seenTicks)
        {
            // More than one tick since the last frame: start from the newest only.
            _previous = _pet.Ticks == _seenTicks + 1 ? _current : _pet.State;
            _current = _pet.State;
            _jumped = _pet.Teleported;
            _seenTicks = _pet.Ticks;
        }
        if (_current.Kind != _model.Kind)
            ShowModel(_current.Kind);

        float alpha = (float)Engine.GetPhysicsInterpolationFraction();
        var from = ToGodot(_previous.Position);
        var to = ToGodot(_current.Position);
        // Appearing beside the player is a pop, not a glide across the forest.
        var position = _jumped ? to : from.Lerp(to, alpha);
        float facing = Mathf.LerpAngle(_previous.Facing, _current.Facing, alpha);
        _model.Transform = new Transform3D(new Basis(Vector3.Up, facing), position);

        var velocity = _pet.Velocity;
        float speed = MathF.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z);
        _model.Animate((float)delta, speed, _pet.Sitting, _pet.SinceBite, _current.Attacking);
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
}
