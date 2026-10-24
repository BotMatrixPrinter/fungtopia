using Fungtopia.Shell;
using Godot;
using GodotInput = Godot.Input;

namespace Fungtopia.Save.View;

/// <summary>
/// The pause menu and the "Saved!" toast. Esc or Start opens the menu while playing (the Core
/// says when: not on the start screen nor in another module's menu); its Core publishes
/// <c>UiFocus</c> while it is open, so the player stands still. The game keeps ticking behind it.
/// Also points the Core at Godot's user://saves folder.
/// </summary>
public partial class SaveView : CanvasLayer, IModuleView
{
    private const float StickOn = 0.6f;
    private const float StickOff = 0.3f;

    private SaveModule? _save;
    private PauseMenu _menu = null!;
    private SaveToast _toast = null!;
    private bool _shown;
    private float _appear;
    private int _seenSaves;
    private int _seenLoads;
    private int _stick;

    /// <summary>The slot the Choice page shows.</summary>
    public int ChosenSlot
    {
        get => _menu.ChosenSlot;
        set => _menu.ChosenSlot = value;
    }

    /// <summary>For the preview: opening the menu keeps the page set by <see cref="Show"/>, and hovering highlights nothing.</summary>
    public bool KeepPage { get; set; }

    public void Bind(IModule module)
    {
        _save = (SaveModule)module;
        _save.SaveFolder = ProjectSettings.GlobalizePath("user://saves");
    }

    public override void _Ready()
    {
        // Over the HUD and the start screen.
        Layer = 60;
        _menu = new PauseMenu { Visible = false };
        AddChild(_menu);
        _toast = new SaveToast();
        AddChild(_toast);
    }

    public override void _Process(double delta)
    {
        if (_save == null)
            return;
        if (_save.SaveCount != _seenSaves)
        {
            _seenSaves = _save.SaveCount;
            if (!_save.LastSaveOk)
                _toast.Show("Could not save", PauseMenu.Red);
            else if (_save.LastSaveAuto)
                _toast.Show("Autosaved", PauseMenu.Blue);
            else
                _toast.Show("Saved!", PauseMenu.Green);
        }
        if (_save.LoadCount != _seenLoads)
        {
            _seenLoads = _save.LoadCount;
            _toast.Show(_save.LastLoadOk ? "Loaded!" : "Could not load", _save.LastLoadOk ? PauseMenu.Lilac : PauseMenu.Red);
        }

        if (_save.MenuOpen != _shown)
        {
            _shown = _save.MenuOpen;
            _menu.Visible = _shown;
            _appear = 0f;
            if (_shown && !KeepPage)
            {
                _menu.Page = MenuPage.Main;
                _menu.Highlight = 0;
            }
            else
                GodotInput.MouseMode = GodotInput.MouseModeEnum.Captured;
        }
        if (!_shown)
            return;
        if (GodotInput.MouseMode != GodotInput.MouseModeEnum.Visible)
            GodotInput.MouseMode = GodotInput.MouseModeEnum.Visible;
        _appear = Mathf.Min(1f, _appear + (float)delta / 0.18f);
        _menu.Appear = _appear;
        _menu.Slots = _save.Slots;
        _menu.CurrentSlot = _save.Current;
    }

    public override void _Input(InputEvent e)
    {
        if (_save == null)
            return;
        if (!_save.MenuOpen)
        {
            if (IsPause(e) && _save.OpenMenu())
                GodotInput.MouseMode = GodotInput.MouseModeEnum.Visible;
            return;
        }

        switch (e)
        {
            case InputEventMouseMotion motion:
                if (KeepPage)
                    return;
                int hover = _menu.ItemAt(motion.Position);
                if (hover >= 0)
                    _menu.Highlight = hover;
                return;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                // Keep the click from the game (the Input view would recapture the mouse on it).
                GetViewport().SetInputAsHandled();
                if (!button.Pressed)
                    return;
                int item = _menu.ItemAt(button.Position);
                if (item >= 0)
                {
                    _menu.Highlight = item;
                    Confirm();
                }
                return;
        }

        if (IsPause(e))
        {
            if (_menu.Page == MenuPage.Main)
                Resume();
            else
                Back();
        }
        else if (Pressed(e, Key.Up, JoyButton.DpadUp) || Pressed(e, Key.Left, JoyButton.DpadLeft))
            Move(-1);
        else if (Pressed(e, Key.Down, JoyButton.DpadDown) || Pressed(e, Key.Right, JoyButton.DpadRight))
            Move(1);
        else if (Pressed(e, Key.Enter, JoyButton.A) || Pressed(e, Key.KpEnter, JoyButton.A) || Pressed(e, Key.Space, JoyButton.A))
            Confirm();
        else if (Pressed(e, Key.Backspace, JoyButton.B))
            Back();
        else if (e is InputEventJoypadMotion { Axis: JoyAxis.LeftY or JoyAxis.LeftX } stick)
        {
            // One step each time the stick is pushed past StickOn; it re-arms near the middle.
            if (stick.AxisValue <= -StickOn && _stick != -1)
            {
                _stick = -1;
                Move(-1);
            }
            else if (stick.AxisValue >= StickOn && _stick != 1)
            {
                _stick = 1;
                Move(1);
            }
            else if (Mathf.Abs(stick.AxisValue) < StickOff)
                _stick = 0;
        }
    }

    private static bool IsPause(InputEvent e) => Pressed(e, Key.Escape, JoyButton.Start);

    private static bool Pressed(InputEvent e, Key key, JoyButton button) => e switch
    {
        InputEventKey k => k.Pressed && !k.Echo && (k.PhysicalKeycode == key || k.Keycode == key),
        InputEventJoypadButton j => j.Pressed && j.ButtonIndex == button,
        _ => false,
    };

    private void Move(int by)
    {
        int count = PauseMenu.ItemCount(_menu.Page);
        _menu.Highlight = (_menu.Highlight + by + count) % count;
    }

    private void Confirm()
    {
        if (_save == null)
            return;
        int item = _menu.Highlight;
        switch (_menu.Page)
        {
            case MenuPage.Main:
                if (item == 0)
                    Resume();
                else if (item == 1)
                    _save.RequestSave(0, false);
                else if (item == 2)
                    Show(MenuPage.Slots, _save.Current >= 1 ? _save.Current - 1 : 0);
                else
                    QuitToTitle();
                break;
            case MenuPage.Slots:
                if (item == SaveSlots.Count)
                    Back();
                else if (_save.Slots.Get(item + 1).Exists)
                {
                    var slot = _save.Slots.Get(item + 1);
                    _menu.ChosenSlot = item + 1;
                    Show(MenuPage.Choice, slot.NewestIsAuto ? 1 : 0);
                }
                break;
            default:
                if (item == 2)
                {
                    Back();
                    break;
                }
                bool auto = item == 1;
                var info = _save.Slots.Get(_menu.ChosenSlot);
                if ((auto ? info.Auto : info.Manual).Ok)
                {
                    _save.RequestLoad(_menu.ChosenSlot, auto);
                    Resume();
                }
                break;
        }
    }

    private void Back()
    {
        if (_menu.Page == MenuPage.Choice)
            Show(MenuPage.Slots, _menu.ChosenSlot - 1);
        else
            Show(MenuPage.Main, 2);
    }

    /// <summary>Shows a page with an item highlighted (the preview uses it too).</summary>
    public void Show(MenuPage page, int highlight)
    {
        if (page == MenuPage.Choice && _menu.ChosenSlot < 1)
            _menu.ChosenSlot = 1;
        _menu.Page = page;
        _menu.Highlight = highlight;
        _appear = 0.6f;
    }

    private void Resume()
    {
        _save?.CloseMenu();
        GodotInput.MouseMode = GodotInput.MouseModeEnum.Captured;
    }

    /// <summary>Back to the start screen: the whole game scene is loaded again (unsaved play is lost).</summary>
    private void QuitToTitle()
    {
        _save?.CloseMenu();
        Callable.From(() => GetTree().ReloadCurrentScene()).CallDeferred();
    }
}
