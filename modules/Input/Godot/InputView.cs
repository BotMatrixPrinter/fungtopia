using Fungtopia.Shell;
using Godot;
using GodotInput = Godot.Input;

namespace Fungtopia.Input.View;

/// <summary>
/// Reads mouse, keyboard and gamepad into the Input module's Core, finds the aim ray from the
/// current 3D camera through the screen center, and draws the crosshair dot there.
/// </summary>
public partial class InputView : Control, IModuleView
{
    private const int Pad = 0;
    private static readonly Color DotFill = new(1f, 1f, 1f, 0.9f);
    private static readonly Color DotInk = new(0.12f, 0.07f, 0.05f, 0.9f);

    [Export] public float DotRadius { get; set; } = 3.5f;

    private InputModule? _input;
    private bool _ignoreFireUntilRelease;

    public void Bind(IModule module) => _input = (InputModule)module;

    public override void _Ready()
    {
        // Run before the Bootstrap ticks the host, so a tick sees this frame's devices.
        ProcessPhysicsPriority = -100;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        GodotInput.MouseMode = GodotInput.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent e)
    {
        if (_input == null)
            return;
        bool captured = GodotInput.MouseMode == GodotInput.MouseModeEnum.Captured;
        switch (e)
        {
            case InputEventMouseMotion motion when captured:
                _input.AddMouseMotion(new System.Numerics.Vector2(motion.Relative.X, motion.Relative.Y));
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } when !captured:
                // This click only recaptures the mouse; it does not fire.
                GodotInput.MouseMode = GodotInput.MouseModeEnum.Captured;
                GetViewport().SetInputAsHandled();
                _ignoreFireUntilRelease = true;
                break;
            case InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false }:
                GodotInput.MouseMode = GodotInput.MouseModeEnum.Visible;
                break;
            case InputEventKey { PhysicalKeycode: Key.Space, Pressed: true, Echo: false }:
                _input.PressJump();
                break;
            case InputEventJoypadButton { ButtonIndex: JoyButton.A, Pressed: true }:
                _input.PressJump();
                break;
            case InputEventKey { PhysicalKeycode: Key.E, Pressed: true, Echo: false }:
            case InputEventJoypadButton { ButtonIndex: JoyButton.X, Pressed: true }:
                _input.PressInteract();
                break;
            case InputEventKey { PhysicalKeycode: Key.M, Pressed: true, Echo: false }:
            case InputEventJoypadButton { ButtonIndex: JoyButton.Back or JoyButton.Touchpad, Pressed: true }:
                _input.PressOpenMap();
                break;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_input == null)
            return;
        bool captured = GodotInput.MouseMode == GodotInput.MouseModeEnum.Captured;

        float x = Held(Godot.Key.D) - Held(Godot.Key.A);
        float y = Held(Godot.Key.W) - Held(Godot.Key.S);
        _input.SetKeyboardMove(new System.Numerics.Vector2(x, y));
        // Godot's stick y grows downward; ours is forward / up.
        _input.SetMoveStick(new System.Numerics.Vector2(
            GodotInput.GetJoyAxis(Pad, JoyAxis.LeftX), -GodotInput.GetJoyAxis(Pad, JoyAxis.LeftY)));
        _input.SetLookStick(new System.Numerics.Vector2(
            GodotInput.GetJoyAxis(Pad, JoyAxis.RightX), -GodotInput.GetJoyAxis(Pad, JoyAxis.RightY)));
        // The recapture click stays held for a few ticks; ignore it until it is released.
        bool held = GodotInput.IsMouseButtonPressed(MouseButton.Left);
        if (!held)
            _ignoreFireUntilRelease = false;
        _input.SetFireButton(captured && held && !_ignoreFireUntilRelease);
        _input.SetFireTrigger(GodotInput.GetJoyAxis(Pad, JoyAxis.TriggerRight));

        var camera = GetViewport().GetCamera3D();
        if (camera == null)
        {
            _input.ClearAim();
            return;
        }
        var center = GetViewport().GetVisibleRect().Size * 0.5f;
        var ray = camera.ProjectRayNormal(center);
        _input.SetAim(new System.Numerics.Vector3(ray.X, ray.Y, ray.Z));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        DrawCircle(center, DotRadius + 1.5f, DotInk);
        DrawCircle(center, DotRadius, DotFill);
    }

    private static float Held(Key key) => GodotInput.IsPhysicalKeyPressed(key) ? 1f : 0f;
}
