using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Player.View;

/// <summary>
/// Draws the player: the chosen character's model placed between the last two fixed ticks,
/// turned to face <see cref="PlayerState.Facing"/>, and animated from the player's motion.
/// The character setting is <see cref="Character"/> ("Girl" or "Boy"), which the command-line
/// user argument <c>--character=boy</c> (after <c>--</c>) overrides.
/// </summary>
public partial class PlayerView : Node3D, IModuleView
{
    /// <summary>The character setting: "Girl" (default) or "Boy".</summary>
    [Export]
    public string Character { get; set; } = "Girl";

    private PlayerModule? _player;
    private CharacterModel? _model;
    private PlayerState _previous;
    private PlayerState _current;
    private long _seenTicks = -1;

    public void Bind(IModule module) => _player = (PlayerModule)module;

    public override void _Ready()
    {
        if (_player == null)
            return;
        _player.Character = ChosenCharacter();
        _current = _previous = _player.State;
        _seenTicks = _player.Ticks;
        ShowModel(_player.Character);
    }

    private CharacterKind ChosenCharacter()
    {
        string setting = Character;
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--character=", StringComparison.OrdinalIgnoreCase))
                setting = arg.Substring("--character=".Length);
        return setting.Equals("boy", StringComparison.OrdinalIgnoreCase) ? CharacterKind.Boy : CharacterKind.Girl;
    }

    private void ShowModel(CharacterKind kind)
    {
        _model?.QueueFree();
        _model = CharacterModel.Create(kind);
        AddChild(_model);
    }

    public override void _Process(double delta)
    {
        if (_player == null || _model == null)
            return;
        if (_player.Ticks != _seenTicks)
        {
            // More than one tick since the last frame: start from the newest only.
            _previous = _player.Ticks == _seenTicks + 1 ? _current : _player.State;
            _current = _player.State;
            _seenTicks = _player.Ticks;
        }
        if (_current.Character != _model.Kind)
            ShowModel(_current.Character);

        float alpha = (float)Engine.GetPhysicsInterpolationFraction();
        var from = ToGodot(_previous.Position);
        var to = ToGodot(_current.Position);
        // A teleport (fast travel, the clinic, the house) is a jump, not a glide across the world.
        var position = from.DistanceTo(to) > 2f ? to : from.Lerp(to, alpha);
        float facing = Mathf.LerpAngle(_previous.Facing, _current.Facing, alpha);
        _model.Transform = new Transform3D(new Basis(Vector3.Up, facing), position);

        var velocity = _current.Velocity;
        float speed = MathF.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z);
        float flash = 0f;
        if (_player.InvulnerableSeconds > 0f && _current.Alive)
            flash = (0.5f + 0.5f * MathF.Cos((float)Time.GetTicksMsec() * 0.03f)) * Math.Min(1f, _player.InvulnerableSeconds / 0.35f);
        _model.Animate((float)delta, speed, _current.Grounded, velocity.Y, flash, _current.Alive);
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
}
