using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Pet.View;

/// <summary>
/// Draws the pet: the chosen pet's model placed between the last two fixed ticks, turned to face
/// <see cref="PetState.Facing"/>, and animated from its motion, bites, flight and sitting; the
/// model changes when another owned pet becomes active. Nothing is drawn before the game starts
/// (the starter pet comes from the start screen).
/// </summary>
public partial class PetView : Node3D, IModuleView
{
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
        _current = _previous = _pet.State;
        _seenTicks = _pet.Ticks;
    }

    private PetModel ShowModel(PetKind kind)
    {
        _model?.QueueFree();
        _model = PetModel.Create(kind);
        AddChild(_model);
        return _model;
    }

    public override void _Process(double delta)
    {
        if (_pet == null || !_pet.Started)
            return;
        if (_model == null)
        {
            // The game has just started: the starter appears where it is, no glide.
            _current = _previous = _pet.State;
            _seenTicks = _pet.Ticks;
        }
        else if (_pet.Ticks != _seenTicks)
        {
            // More than one tick since the last frame: start from the newest only.
            _previous = _pet.Ticks == _seenTicks + 1 ? _current : _pet.State;
            _current = _pet.State;
            _jumped = _pet.Teleported;
            _seenTicks = _pet.Ticks;
        }
        var model = _model == null || _model.Kind != _current.Kind ? ShowModel(_current.Kind) : _model;

        float alpha = (float)Engine.GetPhysicsInterpolationFraction();
        var from = ToGodot(_previous.Position);
        var to = ToGodot(_current.Position);
        // Appearing beside the player is a pop, not a glide across the forest.
        var position = _jumped ? to : from.Lerp(to, alpha);
        float facing = Mathf.LerpAngle(_previous.Facing, _current.Facing, alpha);
        model.Transform = new Transform3D(new Basis(Vector3.Up, facing), position);

        var velocity = _pet.Velocity;
        float speed = MathF.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z);
        model.Animate((float)delta, speed, _pet.Sitting, _pet.SinceBite, _current.Attacking, _pet.Altitude);
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
}
