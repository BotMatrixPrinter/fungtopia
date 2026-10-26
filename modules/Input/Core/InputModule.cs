using System;
using System.Numerics;
using Fungtopia.Shell;

namespace Fungtopia.Input;

/// <summary>
/// Keeps the latest device state reported by its Godot view and publishes one
/// <see cref="PlayerInput"/> and one <see cref="ActiveDevice"/> per tick. Mouse look and jump, interact, map, eat and food presses that arrive between ticks
/// are gathered and handed out once, on the next tick. The gamepad's bottom face button
/// (<see cref="PressGamepadA"/>) gives Interact while an <see cref="InteractAvailable"/> owner is
/// available or a menu is open, else Jump.
/// </summary>
public sealed class InputModule : IModule, IMessageHandler<UiFocus>, IMessageHandler<InteractAvailable>
{
    /// <summary>Stick deflection below this is ignored; the rest is rescaled to 0..1.</summary>
    public float StickDeadZone { get; set; } = 0.2f;

    /// <summary>Right-stick look speed at full deflection, radians per second.</summary>
    public float StickLookRadiansPerSecond { get; set; } = 3.0f;

    /// <summary>Mouse look, radians per pixel of mouse motion.</summary>
    public float MouseRadiansPerPixel { get; set; } = 0.003f;

    /// <summary>Trigger value (0..1) at and above which it counts as held: the right one fires, the left one runs.</summary>
    public float TriggerThreshold { get; set; } = 0.5f;

    /// <summary>Mouse motion of at least this many pixels in one event switches to keyboard and mouse.</summary>
    public float MouseSwitchPixels { get; set; } = 2f;

    /// <summary>Left-stick deflection (on its larger axis) past which it counts as a menu push.</summary>
    public float MenuStickThreshold { get; set; } = 0.5f;

    /// <summary>Shortest time between two <see cref="MenuNav"/> steps, seconds (also the hold repeat).</summary>
    public double MenuRepeatSeconds { get; set; } = 0.2;

    private IMessageBus? _bus;
    private Vector2 _keyboardMove;
    private Vector2 _moveStick;
    private Vector2 _lookStick;
    private Vector2 _mouseLook;
    private bool _jumpPressed;
    private bool _interactPressed;
    private bool _gamepadAPressed;
    private bool _openMapPressed;
    private bool _eatPressed;
    private int _foodSelectPressed = -1;
    private int _foodCyclePressed;
    private bool _fireButton;
    private float _fireTrigger;
    private bool _runKey;
    private float _runTrigger;
    private bool _hasAim;
    private Vector3 _aim;
    private Vector3 _aimOrigin;
    private int[] _openOwners = new int[8];
    private int _openCount;
    private int[] _availableOwners = new int[8];
    private int _availableCount;
    private int _menuButtonsVertical;
    private int _menuButtonsHorizontal;
    private double _sinceMenuStep = double.PositiveInfinity;

    /// <summary>The menu step published on the latest tick.</summary>
    public MenuNav LatestMenuNav { get; private set; }

    /// <summary>The input published on the latest tick.</summary>
    public PlayerInput Latest { get; private set; }

    /// <summary>The device used last (keyboard and mouse until a gamepad is used); published every tick.</summary>
    public ActiveDevice Device { get; private set; } = new(DeviceKind.KeyboardMouse);

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<UiFocus>(this);
        bus.Subscribe<InteractAvailable>(this);
    }

    /// <summary>True while at least one <see cref="UiFocus"/> owner is open.</summary>
    public bool MenuOpen => _openCount > 0;

    /// <summary>True while at least one <see cref="InteractAvailable"/> owner is available.</summary>
    public bool InteractOffered => _availableCount > 0;

    /// <summary>
    /// Tracks open menus per owner: opening an owner already open, or closing one not open, changes nothing.
    /// </summary>
    public void Handle(in UiFocus message) => Track(ref _openOwners, ref _openCount, message.Owner, message.Open);

    /// <summary>Tracks offered interactions per owner, the same way as <see cref="UiFocus"/>.</summary>
    public void Handle(in InteractAvailable message) =>
        Track(ref _availableOwners, ref _availableCount, message.Owner, message.Available);

    private static void Track(ref int[] owners, ref int count, int owner, bool on)
    {
        int index = Array.IndexOf(owners, owner, 0, count);
        if (on)
        {
            if (index >= 0)
                return;
            if (count == owners.Length)
                Array.Resize(ref owners, owners.Length * 2);
            owners[count++] = owner;
        }
        else if (index >= 0)
        {
            owners[index] = owners[--count];
        }
    }

    /// <summary>Keyboard move: x right, y forward, each -1, 0 or 1 (for example from WASD).</summary>
    public void SetKeyboardMove(Vector2 move) => _keyboardMove = Finite(move);

    /// <summary>Left stick: x right, y forward (already flipped from screen-down), each -1..1.</summary>
    public void SetMoveStick(Vector2 stick) => _moveStick = Finite(stick);

    /// <summary>Right stick: x right, y up (already flipped from screen-down), each -1..1.</summary>
    public void SetLookStick(Vector2 stick) => _lookStick = Finite(stick);

    /// <summary>
    /// Digital menu directions held now (D-pad, arrow keys, W A S D): <paramref name="vertical"/> -1 up,
    /// +1 down; <paramref name="horizontal"/> -1 left, +1 right; 0 for none or both.
    /// </summary>
    public void SetMenuButtons(int vertical, int horizontal)
    {
        _menuButtonsVertical = Math.Sign(vertical);
        _menuButtonsHorizontal = Math.Sign(horizontal);
    }

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

    /// <summary>
    /// The gamepad's bottom face button (Xbox A, PlayStation Cross, Nintendo B) went down: on the next
    /// tick it is Interact (and no Jump) while <see cref="InteractOffered"/> or <see cref="MenuOpen"/>,
    /// else Jump.
    /// </summary>
    public void PressGamepadA() => _gamepadAPressed = true;

    /// <summary>A map button went down; reported as OpenMap on the next tick only.</summary>
    public void PressOpenMap() => _openMapPressed = true;

    /// <summary>An eat button went down; reported as Eat on the next tick only.</summary>
    public void PressEat() => _eatPressed = true;

    /// <summary>
    /// A food slot key went down (0, 1 or 2; others are ignored); reported as FoodSelect on the
    /// next tick only. The last slot pressed between ticks wins.
    /// </summary>
    public void PressFoodSelect(int slot)
    {
        if (slot is >= 0 and <= 2)
            _foodSelectPressed = slot;
    }

    /// <summary>
    /// A food cycle button went down (-1 previous, +1 next); reported as FoodCycle on the next tick
    /// only. Presses between ticks add up and the sign is reported (left then right is 0).
    /// </summary>
    public void PressFoodCycle(int direction) => _foodCyclePressed += Math.Sign(direction);

    /// <summary>A key, mouse button or mouse wheel was pressed: keyboard and mouse become the active device.</summary>
    public void NoteKeyboardMouse() => Device = new ActiveDevice(DeviceKind.KeyboardMouse);

    /// <summary>The mouse moved (pixels, any mode): switches to keyboard and mouse when it moved at least <see cref="MouseSwitchPixels"/>.</summary>
    public void NoteMouseMotion(Vector2 pixels)
    {
        if (pixels.Length() >= MouseSwitchPixels)
            NoteKeyboardMouse();
    }

    /// <summary>
    /// A gamepad button was pressed: the gamepad of <paramref name="family"/> becomes the active device
    /// (<see cref="DeviceKind.KeyboardMouse"/> is taken as Generic).
    /// </summary>
    public void NoteGamepadButton(DeviceKind family) => Device = new ActiveDevice(Gamepad(family));

    /// <summary>A gamepad stick or trigger moved to <paramref name="value"/>: switches only past <see cref="StickDeadZone"/>.</summary>
    public void NoteGamepadAxis(DeviceKind family, float value)
    {
        if (MathF.Abs(value) > StickDeadZone)
            NoteGamepadButton(family);
    }

    /// <summary>Whether the fire button (left mouse) is held.</summary>
    public void SetFireButton(bool held) => _fireButton = held;

    /// <summary>The fire trigger's value, 0..1.</summary>
    public void SetFireTrigger(float value) => _fireTrigger = float.IsFinite(value) ? value : 0f;

    /// <summary>Whether the run key (Shift) is held.</summary>
    public void SetRunKey(bool held) => _runKey = held;

    /// <summary>The run trigger's (left trigger's) value, 0..1.</summary>
    public void SetRunTrigger(float value) => _runTrigger = float.IsFinite(value) ? value : 0f;

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
        bool run = _runKey || _runTrigger >= TriggerThreshold;
        bool jump = _jumpPressed;
        bool interact = _interactPressed;
        if (_gamepadAPressed)
        {
            if (InteractOffered || MenuOpen)
                interact = true;
            else
                jump = true;
        }
        bool eat = _eatPressed;
        int foodSelect = _foodSelectPressed;
        int foodCycle = _foodCyclePressed;
        if (MenuOpen)
        {
            move = Vector2.Zero;
            look = Vector2.Zero;
            jump = false;
            fire = false;
            run = false;
            eat = false;
            foodSelect = -1;
            foodCycle = 0;
        }

        Latest = new PlayerInput(move, look, jump, fire, _hasAim, _aim, interact, _openMapPressed, _aimOrigin,
            eat, foodSelect, foodCycle, run);
        _mouseLook = Vector2.Zero;
        _jumpPressed = false;
        _interactPressed = false;
        _gamepadAPressed = false;
        _openMapPressed = false;
        _eatPressed = false;
        _foodSelectPressed = -1;
        _foodCyclePressed = 0;
        _bus.Publish(Latest);
        var device = Device;
        _bus.Publish(device);
        LatestMenuNav = StepMenu(context.StepSeconds);
        var nav = LatestMenuNav;
        _bus.Publish(nav);
    }

    /// <summary>
    /// The direction pushed now, on one axis only: the digital buttons when any is held, else the
    /// left stick's larger axis when it is past <see cref="MenuStickThreshold"/>.
    /// </summary>
    private (int Vertical, int Horizontal) MenuDirection()
    {
        if (_menuButtonsVertical != 0)
            return (_menuButtonsVertical, 0);
        if (_menuButtonsHorizontal != 0)
            return (0, _menuButtonsHorizontal);
        float x = _moveStick.X, y = _moveStick.Y;
        // The stick's y is forward / up; menus count down as +1.
        if (MathF.Abs(y) >= MathF.Abs(x))
            return MathF.Abs(y) > MenuStickThreshold ? (y > 0 ? -1 : 1, 0) : (0, 0);
        return MathF.Abs(x) > MenuStickThreshold ? (0, x > 0 ? 1 : -1) : (0, 0);
    }

    /// <summary>
    /// A step fires when a direction is pushed and at least <see cref="MenuRepeatSeconds"/> have
    /// passed since the last step: at once on a fresh push after a pause, then once per interval
    /// while held, and never more often however the stick is flicked.
    /// </summary>
    private MenuNav StepMenu(double stepSeconds)
    {
        if (double.IsFinite(_sinceMenuStep))
            _sinceMenuStep += stepSeconds;
        var (vertical, horizontal) = MenuDirection();
        // A small tolerance, so twelve 1/60 s steps count as a full 0.2 s.
        if ((vertical == 0 && horizontal == 0) || _sinceMenuStep + 1e-9 < MenuRepeatSeconds)
            return default;
        _sinceMenuStep = 0;
        return new MenuNav(vertical, horizontal);
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

    private static DeviceKind Gamepad(DeviceKind family) =>
        family is DeviceKind.Xbox or DeviceKind.PlayStation or DeviceKind.Nintendo ? family : DeviceKind.Generic;

    private static Vector2 Finite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) ? v : Vector2.Zero;
}
