using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Dialogue.View;

/// <summary>
/// Draws the Dialogue Core: a cute rounded speech bubble at the bottom of the screen with the
/// speaker's name on a mushroom-cap tab, the text typing out letter by letter with a short blip
/// per letter in the speaker's voice, and a small bouncing arrow when Interact would continue.
/// Texts are set only when a new line arrives.
/// </summary>
public partial class DialogueView : CanvasLayer, IModuleView
{
    private const float Width = 1240f;
    private const float Height = 280f;
    private const float Bottom = 40f;
    private const float PopSeconds = 0.18f;
    private const int NameSize = 34;
    private const int TextSize = 38;

    private DialogueModule? _dialogue;
    private Control _root = null!;
    private SpeechBubble _bubble = null!;
    private Label _name = null!;
    private Label _text = null!;
    private AudioStreamPlayer _blip = null!;
    private AudioStreamWav[] _voices = Array.Empty<AudioStreamWav>();
    private long _shownLine = -1;
    private int _shownLetters;
    private float _openSeconds;
    private float _clock;

    public void Bind(IModule module) => _dialogue = (DialogueModule)module;

    public override void _Ready()
    {
        Layer = 12;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _bubble = new SpeechBubble { Size = new Vector2(Width, Height), Visible = false };
        _root.AddChild(_bubble);
        _name = new Label
        {
            LabelSettings = DialogueStyle.Label(NameSize, DialogueStyle.Spot, 10),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _bubble.AddChild(_name);
        _text = new Label
        {
            LabelSettings = DialogueStyle.Label(TextSize, DialogueStyle.Text, 0),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Top,
            VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsBeforeShaping,
        };
        _bubble.AddChild(_text);

        _voices = Blips.Make();
        _blip = new AudioStreamPlayer { VolumeDb = -6f, MaxPolyphony = 2 };
        AddChild(_blip);
    }

    public override void _Process(double delta)
    {
        if (_dialogue == null)
            return;
        float dt = (float)delta;
        _clock += dt;
        bool open = _dialogue.Open;
        _bubble.Visible = open;
        if (!open)
        {
            _shownLine = -1;
            _openSeconds = 0f;
            return;
        }

        if (_dialogue.LineCount != _shownLine)
            ShowLine();

        int letters = _dialogue.VisibleLetters;
        if (letters != _shownLetters)
        {
            // One blip per newly typed letter; a line completed by a press stays quiet.
            if (letters > _shownLetters && letters - _shownLetters <= 2)
                Blip(_dialogue.Text[letters - 1]);
            _shownLetters = letters;
            _text.VisibleCharacters = letters;
        }

        var viewport = _root.Size;
        float width = MathF.Min(Width, viewport.X - 80f);
        if (_bubble.Size.X != width)
            Layout(width);
        _openSeconds += dt;
        float pop = MathF.Min(1f, _openSeconds / PopSeconds);
        float scale = 0.85f + 0.15f * (1f - (1f - pop) * (1f - pop)) + 0.04f * MathF.Sin(MathF.PI * pop);
        _bubble.PivotOffset = new Vector2(width * 0.5f, Height);
        _bubble.Scale = new Vector2(scale, scale);
        _bubble.Modulate = new Color(1, 1, 1, pop);
        _bubble.Position = new Vector2((viewport.X - width) * 0.5f, viewport.Y - Height - Bottom);

        bool arrow = _dialogue.CanContinue;
        float bounce = MathF.Abs(MathF.Sin(_clock * 5f));
        if (arrow != _bubble.ArrowVisible || (arrow && bounce != _bubble.ArrowBounce))
        {
            _bubble.ArrowVisible = arrow;
            _bubble.ArrowBounce = bounce;
            _bubble.QueueRedraw();
        }
    }

    private void ShowLine()
    {
        _shownLine = _dialogue!.LineCount;
        _name.Text = _dialogue.Speaker;
        _name.Visible = _dialogue.Speaker.Length > 0;
        _text.Text = _dialogue.Text;
        _shownLetters = _dialogue.VisibleLetters;
        _text.VisibleCharacters = _shownLetters;
        float nameWidth = DialogueStyle.Font.GetStringSize(_dialogue.Speaker, HorizontalAlignment.Left, -1, NameSize).X;
        _bubble.TabWidth = MathF.Max(230f, nameWidth + 130f);
        Layout(_bubble.Size.X);
    }

    private void Layout(float width)
    {
        _bubble.Size = new Vector2(width, Height);
        var tab = _bubble.TabBand;
        _name.Position = tab.Position + new Vector2(0f, tab.Size.Y * 0.4f);
        _name.Size = new Vector2(tab.Size.X, tab.Size.Y * 0.5f);
        var body = _bubble.Body;
        _text.Position = body.Position + new Vector2(56f, 58f);
        _text.Size = new Vector2(body.Size.X - 160f, body.Size.Y - 86f);
        _bubble.QueueRedraw();
    }

    private void Blip(char letter)
    {
        if (!Blips.Sounds(letter))
            return;
        int voice = Math.Clamp(_dialogue!.Voice, 0, _voices.Length - 1);
        _blip.Stream = _voices[voice];
        _blip.PitchScale = Blips.Pitch(voice, letter);
        _blip.Play();
    }
}
