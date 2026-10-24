using System.Collections.Generic;
using Fungtopia.Input;
using Fungtopia.Shell;
using Godot;
using GodotInput = Godot.Input;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// Shows the Wardrobe Core's screens: the <see cref="WardrobeScreen"/> while the wardrobe is
/// open, Abby's <see cref="ShopScreen"/> while the shop is, on a canvas layer over the right half
/// of the game (the Player module draws the character on the left, the camera frames it). While
/// a screen is open the mouse is free and this view takes the input before the Input module:
/// mouse clicks (hit-tested here, so they are not taken to recapture the mouse), a mouse drag on
/// the left half, Q / E, LB / RB and the right stick to turn the character (the Core's
/// <see cref="PreviewYaw"/>), arrows / W A S D / d-pad / left stick to move the focus, Enter /
/// Space / A to choose, Page Up / Page Down / LT / RT for tabs, Esc / B for Done. As the Input
/// module does not see that input, this view tells the Core which device it came from, so the
/// screens' hints show that device's buttons (<see cref="WardrobeModule.Device"/>).
/// </summary>
public partial class WardrobeView : Node, IModuleView
{
    private WardrobeModule? _wardrobe;
    private CanvasLayer? _layer;
    private IconStudio? _icons;
    private WardrobeScreen? _wardrobeScreen;
    private ShopScreen? _shopScreen;
    private ScreenPanel? _shown;
    private const int Pad = 0;
    private const float TriggerThreshold = 0.5f;

    private Vector2 _stick;
    private float _triggerLeft;
    private float _triggerRight;
    private bool _dragging;
    private readonly Dictionary<int, DeviceKind> _families = new();

    public void Bind(IModule module) => _wardrobe = (WardrobeModule)module;

    /// <summary>The screen shown now, or null.</summary>
    public ScreenPanel? Shown => _shown;

    public WardrobeScreen? Wardrobe => _wardrobeScreen;
    public ShopScreen? Shop => _shopScreen;

    public override void _Process(double delta)
    {
        if (_wardrobe == null)
            return;
        ScreenPanel? want = null;
        if (_wardrobe.Open)
        {
            Build();
            want = _wardrobe.Shop ? _shopScreen : _wardrobeScreen;
        }
        if (want != _shown)
        {
            if (_shown != null)
                _shown.Visible = false;
            bool wasOpen = _shown != null;
            _shown = want;
            if (_shown != null)
            {
                _families.Clear(); // pads may have been swapped since
                _shown.Visible = true;
                _shown.Refresh();
                _shown.FirstFocus().GrabFocus();
            }
            // The screens need the mouse pointer; walking needs it captured again.
            if (wasOpen != (_shown != null))
                GodotInput.MouseMode = _shown != null ? GodotInput.MouseModeEnum.Visible : GodotInput.MouseModeEnum.Captured;
        }
        if (_shown != null)
        {
            _shown.Place(GetViewport().GetVisibleRect().Size);
            _shown.RefreshHint();
            _shown.Refresh();
            // Held turning: Q / LB to the left, E / RB to the right, or the right stick.
            _wardrobe.SetTurnButtons(
                GodotInput.IsPhysicalKeyPressed(Key.Q) || GodotInput.IsJoyButtonPressed(Pad, JoyButton.LeftShoulder),
                GodotInput.IsPhysicalKeyPressed(Key.E) || GodotInput.IsJoyButtonPressed(Pad, JoyButton.RightShoulder));
            _wardrobe.SetTurnStick(GodotInput.GetJoyAxis(Pad, JoyAxis.RightX));
        }
        else
        {
            _dragging = false;
            _wardrobe.SetTurnButtons(false, false);
            _wardrobe.SetTurnStick(0f);
        }
    }

    /// <summary>Makes the screens on first use (nothing is built while they stay closed).</summary>
    private void Build()
    {
        if (_layer != null || _wardrobe == null)
            return;
        WardrobeGlyphs.Register();
        _icons = new IconStudio { Name = "Icons" };
        AddChild(_icons);
        _layer = new CanvasLayer { Layer = 20 };
        AddChild(_layer);
        _wardrobeScreen = new WardrobeScreen(_wardrobe, _icons) { Visible = false };
        _shopScreen = new ShopScreen(_wardrobe, _icons) { Visible = false };
        _layer.AddChild(_wardrobeScreen);
        _layer.AddChild(_shopScreen);
    }

    public override void _Input(InputEvent e)
    {
        if (_shown == null || _wardrobe == null || !_wardrobe.Open)
            return;
        NoteDevice(e);
        switch (e)
        {
            case InputEventMouseMotion motion:
                if (_dragging)
                    _wardrobe.Drag(motion.Relative.X);
                return; // hover highlights through the GUI
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click:
                if (ButtonAt(_shown, click.Position) is Button hit)
                {
                    hit.GrabFocus();
                    hit.EmitSignal(BaseButton.SignalName.Pressed);
                }
                else if (click.Position.X < GetViewport().GetVisibleRect().Size.X * 0.5f)
                    _dragging = true; // the left half: drag to turn the character
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                _dragging = false;
                break;
            case InputEventKey { Pressed: true } key:
                switch (key.PhysicalKeycode)
                {
                    case Key.Escape: _wardrobe.Close(); break;
                    case Key.Pageup: _shown.StepTab(-1); break;
                    case Key.Pagedown: _shown.StepTab(1); break;
                    case Key.Enter or Key.KpEnter or Key.Space: Press(); break;
                    case Key.Up or Key.W: Move(Side.Top); break;
                    case Key.Down or Key.S: Move(Side.Bottom); break;
                    case Key.Left or Key.A: Move(Side.Left); break;
                    case Key.Right or Key.D: Move(Side.Right); break;
                    case Key.Tab: Move(key.ShiftPressed ? Side.Left : Side.Right); break;
                }
                break;
            case InputEventJoypadButton { Pressed: true } pad:
                switch (pad.ButtonIndex)
                {
                    case JoyButton.A: Press(); break;
                    case JoyButton.B: _wardrobe.Close(); break;
                    case JoyButton.DpadUp: Move(Side.Top); break;
                    case JoyButton.DpadDown: Move(Side.Bottom); break;
                    case JoyButton.DpadLeft: Move(Side.Left); break;
                    case JoyButton.DpadRight: Move(Side.Right); break;
                }
                break;
            case InputEventJoypadMotion { Axis: JoyAxis.LeftX or JoyAxis.LeftY } motion:
                Stick(motion);
                break;
            case InputEventJoypadMotion { Axis: JoyAxis.TriggerLeft or JoyAxis.TriggerRight } trigger:
                Trigger(trigger);
                break;
        }
        // Nothing reaches the game (walking, jumping, the map) while a screen is open.
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Tells the Core which device real input comes from (the same rules as the Input module's).</summary>
    private void NoteDevice(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey { Pressed: true, Echo: false }:
            case InputEventMouseButton { Pressed: true }:
            case InputEventMouseMotion motion when motion.Relative.Length() >= MouseSwitchPixels:
                _wardrobe!.NoteDevice(DeviceKind.KeyboardMouse);
                break;
            case InputEventJoypadButton { Pressed: true } button:
                _wardrobe!.NoteDevice(Family(button.Device));
                break;
            case InputEventJoypadMotion stick when Mathf.Abs(stick.AxisValue) > WardrobeModule.StickDeadZone:
                _wardrobe!.NoteDevice(Family(stick.Device));
                break;
        }
    }

    private const float MouseSwitchPixels = 2f;

    private DeviceKind Family(int device)
    {
        if (!_families.TryGetValue(device, out var family))
            _families[device] = family = WardrobePrompts.PadFamily(GodotInput.GetJoyName(device), GodotInput.GetJoyGuid(device));
        return family;
    }

    private void Stick(InputEventJoypadMotion motion)
    {
        var old = _stick;
        if (motion.Axis == JoyAxis.LeftX)
            _stick.X = motion.AxisValue;
        else
            _stick.Y = motion.AxisValue;
        // One step per push past the threshold.
        if (Mathf.Abs(old.X) < 0.6f && Mathf.Abs(_stick.X) >= 0.6f)
            Move(_stick.X > 0 ? Side.Right : Side.Left);
        if (Mathf.Abs(old.Y) < 0.6f && Mathf.Abs(_stick.Y) >= 0.6f)
            Move(_stick.Y > 0 ? Side.Bottom : Side.Top);
    }

    /// <summary>LT / RT: one tab back or on per pull past the threshold.</summary>
    private void Trigger(InputEventJoypadMotion motion)
    {
        bool left = motion.Axis == JoyAxis.TriggerLeft;
        float old = left ? _triggerLeft : _triggerRight;
        if (left)
            _triggerLeft = motion.AxisValue;
        else
            _triggerRight = motion.AxisValue;
        if (old < TriggerThreshold && motion.AxisValue >= TriggerThreshold)
            _shown!.StepTab(left ? -1 : 1);
    }

    private void Press()
    {
        if (GetViewport().GuiGetFocusOwner() is Button { Disabled: false } button && _shown!.IsAncestorOf(button))
            button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private void Move(Side side)
    {
        var focus = GetViewport().GuiGetFocusOwner();
        if (focus == null || !_shown!.IsAncestorOf(focus))
        {
            _shown!.FirstFocus().GrabFocus();
            return;
        }
        focus.FindValidFocusNeighbor(side)?.GrabFocus();
    }

    /// <summary>The enabled, visible button under <paramref name="position"/>, the innermost one.</summary>
    private static Button? ButtonAt(Node node, Vector2 position)
    {
        Button? found = null;
        foreach (var child in node.GetChildren())
        {
            if (child is CanvasItem { Visible: false })
                continue;
            if (ButtonAt(child, position) is Button inner)
                found = inner;
        }
        if (found == null && node is Button { Disabled: false } button && button.GetGlobalRect().HasPoint(position))
            found = button;
        return found;
    }
}
