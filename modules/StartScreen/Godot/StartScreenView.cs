using Fungtopia.Shell;
using Godot;
using GodotInput = Godot.Input;

namespace Fungtopia.StartScreen.View;

/// <summary>
/// Draws the start screen over everything (a <c>CanvasLayer</c> on layer 50) and reports mouse,
/// keyboard and gamepad presses to its Core: the "Fungtopia" logo over a soft blurred view of
/// Mushroom Town, then the Girl and the Boy, then the Mushroom Cat and the Mushroom Dog, then
/// "Start!". It takes every input event while open (so a press here does not also jump or talk
/// in the game), keeps the mouse visible, fades out after "Start!" and captures the mouse again.
/// </summary>
public partial class StartScreenView : CanvasLayer, IModuleView
{
    private const float FadeSeconds = 0.6f;
    private const float StickOn = 0.6f;
    private const float StickOff = 0.3f;

    private StartScreenModule? _screen;
    private Control _root = null!;
    private PreviewStage _town = null!;
    private MushroomLogo _logo = null!;
    private Control _content = null!;
    private Label _heading = null!;
    private Label _hint = null!;
    private Label _prompt = null!;
    private readonly ChoiceCard[] _cards = new ChoiceCard[2];
    private readonly ChoiceCard[] _picks = new ChoiceCard[2];
    private CapButton _start = null!;
    private CapButton _back = null!;
    private readonly PreviewStage[] _stages = new PreviewStage[4];
    private StartStep _shownStep = (StartStep)(-1);
    private int _hovered = -1;
    private int _stick;
    private float _closing;
    private float _time;

    private static readonly string[] Models = { "girl", "boy", "cat", "dog" };
    private static readonly string[] Names = { "Girl", "Boy", "Mushroom Cat", "Mushroom Dog" };
    private static readonly Color[] CardCaps = { StartStyle.Pink, StartStyle.Blue, StartStyle.Gold, StartStyle.Red };

    public void Bind(IModule module) => _screen = (StartScreenModule)module;

    public override void _Ready()
    {
        Layer = 50;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        // The blurred town: rendered small, stretched over the screen with a gaussian blur.
        _town = new PreviewStage { Model = "town", Size = new Vector2I(480, 270) };
        AddChild(_town);
        var blur = new ShaderMaterial { Shader = GD.Load<Shader>("res://modules/StartScreen/Godot/Shaders/blur.gdshader") };
        blur.SetShaderParameter("town", _town.GetTexture());
        var backdrop = new TextureRect
        {
            Texture = _town.GetTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = blur,
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(backdrop);

        for (int i = 0; i < Models.Length; i++)
        {
            _stages[i] = new PreviewStage { Model = Models[i], Size = new Vector2I(512, 512) };
            AddChild(_stages[i]);
        }

        _logo = new MushroomLogo();
        _root.AddChild(_logo);
        _content = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_content);

        _heading = NewLabel(64, StartStyle.Cream, 18);
        _prompt = NewLabel(44, StartStyle.Cream, 14);
        _hint = NewLabel(28, StartStyle.Cream, 10);
        for (int i = 0; i < 2; i++)
        {
            _cards[i] = new ChoiceCard();
            _picks[i] = new ChoiceCard();
            _content.AddChild(_cards[i]);
            _content.AddChild(_picks[i]);
        }
        _start = new CapButton { Text = "Start!", FontSize = 72, CapColor = StartStyle.Red };
        _back = new CapButton { Text = "Back", FontSize = 30, CapColor = StartStyle.Lilac };
        _content.AddChild(_start);
        _content.AddChild(_back);
    }

    private Label NewLabel(int size, Color color, int outline)
    {
        var label = new Label
        {
            LabelSettings = StartStyle.Text(size, color, outline),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _content.AddChild(label);
        return label;
    }

    public override void _Process(double delta)
    {
        if (_screen == null)
            return;
        float dt = (float)delta;
        _time += dt;
        if (!_screen.Open)
        {
            Close(dt);
            return;
        }
        if (GodotInput.MouseMode != GodotInput.MouseModeEnum.Visible)
            GodotInput.MouseMode = GodotInput.MouseModeEnum.Visible;
        var step = _screen.Step;
        if (step != _shownStep)
            ShowStep(step);
        Layout(step);

        // Each new step slides up and fades in over a fifth of a second.
        float appear = Mathf.Clamp((_screen.StepTicks + (float)Engine.GetPhysicsInterpolationFraction()) / 12f, 0f, 1f);
        float ease = 1f - (1f - appear) * (1f - appear);
        _content.Modulate = new Color(1f, 1f, 1f, ease);
        _content.Position = new Vector2(0f, (1f - ease) * 40f);

        int chosen = step == StartStep.Character ? _screen.Character : _screen.StarterPet;
        for (int i = 0; i < 2; i++)
        {
            _cards[i].Highlighted = chosen == i;
            _stages[step == StartStep.Pet ? 2 + i : i].Spinning = chosen == i;
        }
        _start.Highlighted = step == StartStep.Ready;
        _prompt.Modulate = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Cos(_time * 3.5f));
    }

    /// <summary>Sets up what a step shows and which previews render.</summary>
    private void ShowStep(StartStep step)
    {
        _shownStep = step;
        _hovered = -1;
        bool title = step == StartStep.Title;
        bool choosing = step is StartStep.Character or StartStep.Pet;
        bool ready = step == StartStep.Ready;
        int first = step == StartStep.Pet ? 2 : 0;
        for (int i = 0; i < 2; i++)
        {
            _cards[i].Visible = choosing;
            _cards[i].Preview = _stages[first + i].GetTexture();
            _cards[i].Title = Names[first + i];
            _cards[i].CapColor = CardCaps[first + i];
            _picks[i].Visible = ready;
        }
        int character = _screen!.Character, pet = 2 + _screen.StarterPet;
        _picks[0].Preview = _stages[character].GetTexture();
        _picks[0].Title = Names[character];
        _picks[0].CapColor = CardCaps[character];
        _picks[1].Preview = _stages[pet].GetTexture();
        _picks[1].Title = Names[pet];
        _picks[1].CapColor = CardCaps[pet];
        for (int i = 0; i < 4; i++)
        {
            bool shown = (choosing && i >= first && i < first + 2) || (ready && (i == character || i == pet));
            _stages[i].RenderTargetUpdateMode = shown ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
            _stages[i].Spinning = ready && shown;
        }
        _start.Visible = ready;
        _back.Visible = !title;
        _prompt.Visible = title;
        _heading.Visible = !title;
        _heading.Text = step switch
        {
            StartStep.Character => "Choose your character",
            StartStep.Pet => "Choose your starter pet",
            _ => "Ready for adventure!",
        };
        _prompt.Text = "Press Enter  /  E  /  (A)  or click to begin";
        _hint.Text = step switch
        {
            StartStep.Title => "",
            StartStep.Ready => "Enter / E / (A): Start!     Esc / (B): back",
            _ => "Arrows / stick / D-pad: choose     Enter / E / (A): OK     Esc / (B): back",
        };
    }

    /// <summary>Places everything for the current window size.</summary>
    private void Layout(StartStep step)
    {
        var view = _root.Size;
        float u = Mathf.Min(view.X / 1920f, view.Y / 1080f);
        bool title = step == StartStep.Title;
        // The logo: big in the middle on the title, small at the top afterwards.
        float logoScale = (title ? 1f : 0.42f) * u;
        float logoY = title ? view.Y * 0.4f : 92f * u;
        _logo.Position = _logo.Position.Lerp(new Vector2(view.X * 0.5f, logoY), title && _time < 0.1f ? 1f : 0.2f);
        _logo.Scale = _logo.Scale.Lerp(new Vector2(logoScale, logoScale), title && _time < 0.1f ? 1f : 0.2f);

        Place(_heading, new Vector2(view.X * 0.5f, 210f * u), new Vector2(1400f, 90f) * u);
        SetFontSize(_heading, Mathf.RoundToInt(64 * u));
        Place(_prompt, new Vector2(view.X * 0.5f, view.Y * 0.72f), new Vector2(1400f, 70f) * u);
        SetFontSize(_prompt, Mathf.RoundToInt(44 * u));
        Place(_hint, new Vector2(view.X * 0.5f, view.Y - 46f * u), new Vector2(1800f, 50f) * u);
        SetFontSize(_hint, Mathf.RoundToInt(28 * u));

        var card = new Vector2(500f, 640f) * u;
        for (int i = 0; i < 2; i++)
        {
            float x = view.X * 0.5f + (i == 0 ? -1f : 1f) * 320f * u;
            Place(_cards[i], new Vector2(x, view.Y * 0.6f), card);
            Place(_picks[i], new Vector2(view.X * 0.5f + (i == 0 ? -1f : 1f) * 230f * u, view.Y * 0.5f), card * 0.75f);
        }
        Place(_start, new Vector2(view.X * 0.5f, view.Y * 0.5f + 360f * u), new Vector2(380f, 190f) * u);
        _start.FontSize = Mathf.RoundToInt(72 * u);
        Place(_back, new Vector2(120f * u, 90f * u), new Vector2(170f, 100f) * u);
        _back.FontSize = Mathf.RoundToInt(30 * u);
    }

    private static void SetFontSize(Label label, int size)
    {
        if (label.LabelSettings.FontSize != size)
            label.LabelSettings.FontSize = size;
    }

    private static void Place(Control control, Vector2 center, Vector2 size)
    {
        control.Size = size;
        control.Position = center - size * 0.5f;
    }

    private void Close(float dt)
    {
        if (!Visible)
            return;
        if (_closing == 0f)
        {
            GodotInput.MouseMode = GodotInput.MouseModeEnum.Captured;
            foreach (var stage in _stages)
                stage.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        }
        _closing += dt;
        _root.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp(_closing / FadeSeconds, 0f, 1f));
        _root.Scale = Vector2.One * (1f + _closing * 0.1f);
        _root.PivotOffset = _root.Size * 0.5f;
        if (_closing >= FadeSeconds)
        {
            Visible = false;
            _town.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            SetProcess(false);
        }
    }

    public override void _Input(InputEvent e)
    {
        if (_screen == null || !_screen.Open)
            return;
        switch (e)
        {
            case InputEventKey { Pressed: true, Echo: false } key:
                var code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
                if (code == Key.Left)
                    _screen.Press(StartButton.Left);
                else if (code == Key.Right)
                    _screen.Press(StartButton.Right);
                else if (code is Key.Enter or Key.KpEnter or Key.E)
                    _screen.Press(StartButton.Confirm);
                else if (code is Key.Escape or Key.Backspace)
                    _screen.Press(StartButton.Back);
                break;
            case InputEventJoypadButton { Pressed: true } button:
                if (button.ButtonIndex == JoyButton.DpadLeft)
                    _screen.Press(StartButton.Left);
                else if (button.ButtonIndex == JoyButton.DpadRight)
                    _screen.Press(StartButton.Right);
                else if (button.ButtonIndex == JoyButton.A)
                    _screen.Press(StartButton.Confirm);
                else if (button.ButtonIndex == JoyButton.B)
                    _screen.Press(StartButton.Back);
                break;
            case InputEventJoypadMotion { Axis: JoyAxis.LeftX } motion:
                // One press each time the stick is pushed past StickOn; it re-arms near the middle.
                if (motion.AxisValue <= -StickOn && _stick != -1)
                {
                    _stick = -1;
                    _screen.Press(StartButton.Left);
                }
                else if (motion.AxisValue >= StickOn && _stick != 1)
                {
                    _stick = 1;
                    _screen.Press(StartButton.Right);
                }
                else if (Mathf.Abs(motion.AxisValue) < StickOff)
                    _stick = 0;
                break;
            case InputEventMouseMotion motion:
                int over = CardAt(motion.Position);
                if (over != _hovered && over >= 0)
                    _screen.Point(over);
                _hovered = over;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click:
                Click(click.Position);
                break;
        }
        GetViewport().SetInputAsHandled();
    }

    private void Click(Vector2 at)
    {
        var step = _screen!.Step;
        if (step != StartStep.Title && Hit(_back, at))
            _screen.Press(StartButton.Back);
        else if (step == StartStep.Title)
            _screen.Press(StartButton.Confirm);
        else if (step == StartStep.Ready && Hit(_start, at))
            _screen.Press(StartButton.Confirm);
        else if (CardAt(at) is int card and >= 0)
        {
            _screen.Point(card);
            _screen.Press(StartButton.Confirm);
        }
    }

    /// <summary>The choice card under a point (0 or 1), or -1.</summary>
    private int CardAt(Vector2 at)
    {
        if (_shownStep is not (StartStep.Character or StartStep.Pet))
            return -1;
        for (int i = 0; i < 2; i++)
            if (Hit(_cards[i], at))
                return i;
        return -1;
    }

    private static bool Hit(Control control, Vector2 at) => control.Visible && control.GetGlobalRect().HasPoint(at);
}
