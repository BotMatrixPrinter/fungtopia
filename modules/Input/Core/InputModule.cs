using System;
using System.Numerics;
using Fungtopia.Shell;

namespace Fungtopia.Input;

/// <summary>
/// Keeps the latest device state reported by its Godot view and publishes one
/// <see cref="PlayerInput"/> per tick. Mouse look and jump, interact and map presses that arrive between ticks
/// are gathered and handed out once, on the next tick.
/// </summary>
public sealed class InputModule : IModule
{
    /// <summary>Stick deflection below this is ignored; the rest is rescaled to 0..1.</summary>
    public float StickDeadZone { get; set; } = 0.2f;

    /// <summary>Right-stick look speed at full deflection, radians per second.</summary>
    public float StickLookRadiansPerSecond { get; set; } = 3.0f;

    /// <summary>Mouse look, radians per pixel of mouse motion.</summary>
    public float MouseRadiansPerPixel { get; set; } = 0.003f;

    /// <summary>Right trigger value (0..1) at and above which it counts as firing.</summary>
    public float TriggerThreshold { get; set; } = 0.5f;

    private IMessageBus? _bus;
    private Vector2 _keyboardMove;
    private Vector2 _moveStick;
    private Vector2 _lookStick;
    private Vector2 _mouseLook;
    private bool _jumpPressed;
    private bool _interactPressed;
    private bool _openMapPressed;
    private bool _fireButton;
    private float _fireTrigger;
    private bool _hasAim;
    private Vector3 _aim;
    private Vector3 _aimOrigin;

    /// <summary>The input published on the latest tick.</summary>
    public PlayerInput Latest { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
    }

    /// <summary>Keyboard move: x right, y forward, each -1, 0 or 1 (for example from WASD).</summary>
    public void SetKeyboardMove(Vector2 move) => _keyboardMove = Finite(move);

    /// <summary>Left stick: x right, y forward (already flipped from screen-down), each -1..1.</summary>
    public void SetMoveStick(Vector2 stick) => _moveStick = Finite(stick);

    /// <summary>Right stick: x right, y up (already flipped from screen-down), each -1..1.</summary>
    public void SetLookStick(Vector2 stick) => _lookStick = Finite(stick);

    /// <summary>Adds mouse motion in pixels, x right and y down as screens count them.</summary>
    public void AddMouseMotion(Vector2 pixels)
    {
        pixels = Finite(pixels);
        _mouseLook += new Vector2(pixels.X, -pixels.Y) * MouseRadiansPerPixel;
    }

    /// <summary>A jump button went down; reported as Jump on the next tick only.</summary>
    public void PressJump() => _jumpPressed = true;

    /// <summary>An interact button went down; reported as Interact on the next tick only.</summary>
    public void PressInteract() => _interactPressed = true;

    /// <summary>A map button went down; reported as OpenMap on the next tick only.</summary>
    public void PressOpenMap() => _openMapPressed = true;

    /// <summary>Whether the fire button (left mouse) is held.</summary>
    public void SetFireButton(bool held) => _fireButton = held;

    /// <summary>The fire trigger's value, 0..1.</summary>
    public void SetFireTrigger(float value) => _fireTrigger = float.IsFinite(value) ? value : 0f;

    /// <summary>
    /// The ray from the camera through the screen center: <paramref name="origin"/> is the camera's
    /// world position, <paramref name="direction"/> any length (normalized here).
    /// </summary>
    public void SetAim(Vector3 origin, Vector3 direction)
    {
        float length = direction.Length();
        if (!float.IsFinite(length) || length < 1e-6f
            || !float.IsFinite(origin.X) || !float.IsFinite(origin.Y) || !float.IsFinite(origin.Z))
        {
            ClearAim();
            return;
        }
        _hasAim = true;
        _aim = direction / length;
        _aimOrigin = origin;
    }

    /// <summary>No camera: HasAim is false until the next <see cref="SetAim"/>.</summary>
    public void ClearAim()
    {
        _hasAim = false;
        _aim = Vector3.Zero;
        _aimOrigin = Vector3.Zero;
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("InputModule.Tick before Start.");

        Vector2 move = _keyboardMove != Vector2.Zero ? _keyboardMove : ApplyDeadZone(_moveStick, StickDeadZone);
        move = ClampLength(move);
        Vector2 look = _mouseLook + ApplyDeadZone(_lookStick, StickDeadZone) * (StickLookRadiansPerSecond * (float)context.StepSeconds);
        bool fire = _fireButton || _fireTrigger >= TriggerThreshold;

        Latest = new PlayerInput(move, look, _jumpPressed, fire, _hasAim, _aim, _interactPressed, _openMapPressed, _aimOrigin);
        _mouseLook = Vector2.Zero;
        _jumpPressed = false;
        _interactPressed = false;
        _openMapPressed = false;
        _bus.Publish(Latest);
    }

    /// <summary>
    /// Radial dead zone: a stick shorter than <paramref name="deadZone"/> reads zero, longer ones
    /// are rescaled so the edge of the dead zone is 0 and full deflection is 1 (clamped).
    /// </summary>
    public static Vector2 ApplyDeadZone(Vector2 stick, float deadZone)
    {
        float length = stick.Length();
        if (!(length > deadZone) || deadZone >= 1f)
            return Vector2.Zero;
        float scaled = MathF.Min(1f, (length - deadZone) / (1f - deadZone));
        return stick * (scaled / length);
    }

    /// <summary>Shortens vectors longer than 1 to length 1 (keeps shorter ones as they are).</summary>
    public static Vector2 ClampLength(Vector2 v)
    {
        float length = v.Length();
        return length > 1f ? v / length : v;
    }

    private static Vector2 Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) ? v : Vector2.Zero;
}
