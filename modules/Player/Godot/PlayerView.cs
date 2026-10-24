using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Player.View;

/// <summary>
/// Draws the player: the chosen character's model placed between the last two fixed ticks,
/// turned to face <see cref="PlayerModule.ShownFacing"/> (the facing, turned for viewing while the
/// wardrobe or the shop is open), and animated from the player's motion and its
/// archery pose (<see cref="PlayerModule.Archery"/>, aimed along the input's aim).
/// The character setting is <see cref="Character"/> ("Girl" or "Boy"), which the command-line
/// user argument <c>--character=boy</c> (after <c>--</c>) overrides, and the start screen's
/// choice replaces once the game starts. Hidden until the start screen opens, then shown standing
/// idle at the home door for character creation (<see cref="PlayerModule.Visible"/>).
/// Dressed by <see cref="PlayerModule.Look"/> (the Wardrobe's <c>AppearanceState</c>), redressed
/// the frame it changes, so the wardrobe screen shows each pick at once.
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
    private ArcheryState _previousArchery = ArcheryState.Relaxed;
    private ArcheryState _currentArchery = ArcheryState.Relaxed;
    private float _previousFacing;
    private float _currentFacing;
    private long _seenTicks = -1;
    private int _dressedVersion = -1;

    public void Bind(IModule module) => _player = (PlayerModule)module;

    public override void _Ready()
    {
        if (_player == null)
            return;
        _player.Character = ChosenCharacter();
        _current = _previous = _player.State;
        _currentArchery = _previousArchery = _player.Archery;
        _currentFacing = _previousFacing = _player.ShownFacing;
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
        _dressedVersion = -1;
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
            _previousArchery = _player.Ticks == _seenTicks + 1 ? _currentArchery : _player.Archery;
            _currentArchery = _player.Archery;
            _previousFacing = _player.Ticks == _seenTicks + 1 ? _currentFacing : _player.ShownFacing;
            _currentFacing = _player.ShownFacing;
            _seenTicks = _player.Ticks;
        }
        if (_current.Character != _model.Kind)
            ShowModel(_current.Character);
        if (_player.AppearanceVersion != _dressedVersion)
        {
            var look = _player.HasAppearance
                ? CharacterLook.From(_player.Appearance, _model.Kind)
                : CharacterLook.Default(_model.Kind);
            _model.Apply(look);
            _dressedVersion = _player.AppearanceVersion;
        }
        // Hidden only before the start screen opens; on it the character stands idle for creation.
        _model.Visible = _player.Visible;
        if (!_model.Visible)
            return;

        float alpha = (float)Engine.GetPhysicsInterpolationFraction();
        var from = ToGodot(_previous.Position);
        var to = ToGodot(_current.Position);
        // A teleport (fast travel, the clinic, the house) is a jump, not a glide across the world.
        var position = from.DistanceTo(to) > 2f ? to : from.Lerp(to, alpha);
        float facing = Mathf.LerpAngle(_previousFacing, _currentFacing, alpha);
        _model.Transform = new Transform3D(new Basis(Vector3.Up, facing), position);

        var velocity = _current.Velocity;
        float speed = MathF.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z);
        float flash = 0f;
        if (_player.InvulnerableSeconds > 0f && _current.Alive)
            flash = (0.5f + 0.5f * MathF.Cos((float)Time.GetTicksMsec() * 0.03f)) * Math.Min(1f, _player.InvulnerableSeconds / 0.35f);
        // The pose's blend weight between the ticks, so it glides at any frame rate.
        var archery = _currentArchery;
        archery.Weight = Mathf.Lerp(_previousArchery.Weight, _currentArchery.Weight, alpha);
        var (aimYaw, aimPitch) = ArcheryPose.AimAngles(_player.AimDirection, _player.HasAim, facing);
        _model.Animate((float)delta, speed, _current.Grounded, velocity.Y, flash, _current.Alive, archery, aimYaw, aimPitch);
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
}
