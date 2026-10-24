using Fungtopia.Shell;
using Godot;

namespace Fungtopia.HUD.View;

/// <summary>
/// Draws the HUD Core on top of the game: hearts in a mushroom-cap panel at the top left (shaking
/// on a hit), the Mushroom Coins panel at the top right (popping, with floating "+50"s), the bow
/// draw ring around the center crosshair, the clock and below it the quest tracker under the hearts, the place-name banner sliding down at the top center, and the fade to black with "You
/// fainted..." and then the wake-up text. It reads the Core
/// between the last two ticks (<c>Engine.GetPhysicsInterpolationFraction</c>) and sets texts only
/// when they change.
/// </summary>
public partial class HudView : CanvasLayer, IModuleView
{
    private const float Margin = 28f;
    private const float PanelHeight = 124f;
    private const float CoinsWidth = 330f;
    private const float PillHeight = 56f;
    private const float BannerWidth = 560f;
    private static readonly Color Black = new(0.05f, 0.03f, 0.07f);

    private HudModule? _hud;
    private Control _root = null!;
    private MushroomPanel _heartsPanel = null!;
    private HeartRow _hearts = null!;
    private MushroomPanel _coinsPanel = null!;
    private Control _coinsContent = null!;
    private Label _coins = null!;
    private readonly Label[] _floaters = new Label[HudModule.FloaterCapacity];
    private readonly int[] _floaterAmounts = new int[HudModule.FloaterCapacity];
    private Crosshair _crosshair = null!;
    private ColorRect _fade = null!;
    private Label _fainted = null!;
    private Label _wake = null!;
    private int _shownCoins = -1;
    private int _shownWakeDay = -1;

    private MushroomPanel _banner = null!;
    private Label _bannerLabel = null!;
    private int _shownBanner = -1;

    private PanelContainer _clockPill = null!;
    private SkyIcon _sky = null!;
    private Label _clock = null!;
    private int _shownDay = -1, _shownMinute = -1;

    private PanelContainer _questPill = null!;
    private QuestIcon _questIcon = null!;
    private Label _quest = null!;
    private int _shownQuest = -1;

    public void Bind(IModule module) => _hud = (HudModule)module;

    public override void _Ready()
    {
        Layer = 10;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _heartsPanel = new MushroomPanel { Position = new Vector2(Margin, Margin), Size = new Vector2(320f, PanelHeight) };
        _root.AddChild(_heartsPanel);
        _hearts = new HeartRow { Position = new Vector2(30f, PanelHeight - 54f) };
        _heartsPanel.AddChild(_hearts);

        _coinsPanel = new MushroomPanel { Size = new Vector2(CoinsWidth, PanelHeight) };
        _coinsPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _coinsPanel.Position = new Vector2(-CoinsWidth - Margin, Margin);
        _root.AddChild(_coinsPanel);
        _coinsContent = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(CoinsWidth, 56f), Position = new Vector2(0, PanelHeight - 60f) };
        _coinsContent.PivotOffset = _coinsContent.Size * 0.5f;
        _coinsPanel.AddChild(_coinsContent);
        _coinsContent.AddChild(new CoinIcon { Position = new Vector2(22f, 2f), Size = new Vector2(52f, 52f) });
        _coins = new Label
        {
            LabelSettings = HudStyle.Text(36, HudStyle.Ink, 0),
            Position = new Vector2(82f, 0f),
            Size = new Vector2(CoinsWidth - 100f, 56f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = HudText.Coins(0),
        };
        _coinsContent.AddChild(_coins);

        for (int i = 0; i < _floaters.Length; i++)
        {
            _floaters[i] = new Label
            {
                LabelSettings = HudStyle.Text(40, HudStyle.Gold, 12),
                HorizontalAlignment = HorizontalAlignment.Center,
                Size = new Vector2(160f, 56f),
                Visible = false,
            };
            _floaters[i].SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _root.AddChild(_floaters[i]);
        }

        _questPill = Pill();
        _questPill.Position = new Vector2(Margin, Margin + PanelHeight + 14f + PillHeight + 12f);
        _root.AddChild(_questPill);
        var questRow = (HBoxContainer)_questPill.GetChild(0);
        _questIcon = new QuestIcon { CustomMinimumSize = new Vector2(40f, 40f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        questRow.AddChild(_questIcon);
        _quest = new Label { LabelSettings = HudStyle.Text(28, HudStyle.Ink, 0), VerticalAlignment = VerticalAlignment.Center };
        questRow.AddChild(_quest);

        _clockPill = Pill();
        _clockPill.Position = new Vector2(Margin, Margin + PanelHeight + 14f);
        _root.AddChild(_clockPill);
        var clockRow = (HBoxContainer)_clockPill.GetChild(0);
        _sky = new SkyIcon { CustomMinimumSize = new Vector2(44f, 44f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        clockRow.AddChild(_sky);
        _clock = new Label
        {
            LabelSettings = HudStyle.Text(26, HudStyle.Ink, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        clockRow.AddChild(_clock);

        _banner = new MushroomPanel { Size = new Vector2(BannerWidth, PanelHeight), Visible = false };
        _root.AddChild(_banner);
        _bannerLabel = new Label
        {
            LabelSettings = HudStyle.Text(40, HudStyle.Ink, 0),
            Position = new Vector2(0f, PanelHeight - 60f),
            Size = new Vector2(BannerWidth, 56f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _banner.AddChild(_bannerLabel);

        _crosshair = new Crosshair();
        _crosshair.SetAnchorsPreset(Control.LayoutPreset.Center);
        _root.AddChild(_crosshair);

        _fade = new ColorRect { Color = new Color(Black, 0f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_fade);
        _fainted = new Label
        {
            LabelSettings = HudStyle.Text(84, HudStyle.Cream, 18),
            Text = HudText.Fainted,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
        };
        _fainted.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_fainted);
        _wake = new Label
        {
            LabelSettings = HudStyle.Text(64, HudStyle.Cream, 16),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
        };
        _wake.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_wake);
    }

    /// <summary>A cream rounded pill with an ink border holding a row (icon, text).</summary>
    private static PanelContainer Pill()
    {
        var box = new StyleBoxFlat
        {
            BgColor = HudStyle.Cream,
            BorderColor = HudStyle.Ink,
            CornerDetail = 10,
            AntiAliasing = true,
            ContentMarginLeft = 10f,
            ContentMarginRight = 22f,
            ContentMarginTop = 4f,
            ContentMarginBottom = 4f,
        };
        box.SetBorderWidthAll(3);
        box.SetCornerRadiusAll((int)(PillHeight * 0.5f));
        var pill = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0f, PillHeight) };
        pill.AddThemeStyleboxOverride("panel", box);
        pill.AddChild(new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center });
        ((HBoxContainer)pill.GetChild(0)).AddThemeConstantOverride("separation", 10);
        return pill;
    }

    public override void _Process(double delta)
    {
        if (_hud == null)
            return;
        float f = (float)Engine.GetPhysicsInterpolationFraction();

        _heartsPanel.Visible = _hud.HasPlayer;
        _hearts.Show(_hud);
        _heartsPanel.Size = new Vector2(_hearts.CustomMinimumSize.X + 60f, PanelHeight);
        _heartsPanel.Position = new Vector2(Margin + _hud.HeartsShake(f), Margin);

        if (_hud.Coins != _shownCoins)
        {
            _shownCoins = _hud.Coins;
            _coins.Text = HudText.Coins(_shownCoins);
        }
        float pop = _hud.CoinsPopScale(f);
        _coinsContent.Scale = new Vector2(pop, pop);

        var viewport = _root.Size;
        for (int i = 0; i < _floaters.Length; i++)
        {
            var floater = _hud.Floater(i);
            var label = _floaters[i];
            label.Visible = floater.Active;
            if (!floater.Active)
                continue;
            if (floater.Amount != _floaterAmounts[i])
            {
                _floaterAmounts[i] = floater.Amount;
                label.Text = HudText.Earned(floater.Amount);
            }
            label.Position = new Vector2(viewport.X - Margin - CoinsWidth * 0.5f - 40f, Margin + PanelHeight + 60f - floater.Rise(f));
            label.Modulate = new Color(1, 1, 1, floater.Alpha(f));
        }

        _questPill.Visible = _hud.QuestActive;
        if (_hud.QuestVersion != _shownQuest)
        {
            _shownQuest = _hud.QuestVersion;
            _quest.Text = HudText.Quest(_hud.QuestTitle, _hud.QuestProgress, _hud.QuestGoal);
            _questIcon.Show(_hud.QuestComplete);
            _questPill.ResetSize();
        }
        float questPop = _hud.QuestPopScale(f);
        _questPill.PivotOffset = new Vector2(0f, _questPill.Size.Y * 0.5f);
        _questPill.Scale = new Vector2(questPop, questPop);

        _clockPill.Visible = _hud.HasClock;
        if (_hud.Day != _shownDay || _hud.ClockMinute != _shownMinute)
        {
            _shownDay = _hud.Day;
            _shownMinute = _hud.ClockMinute;
            _clock.Text = HudText.Day(_shownDay) + "  " + HudText.Time(_shownMinute);
            _clockPill.ResetSize();
        }
        _sky.Show(_hud.IsNight);

        if (_hud.BannerCount != _shownBanner)
        {
            _shownBanner = _hud.BannerCount;
            _bannerLabel.Text = HudText.AreaName(_hud.BannerArea);
        }
        float slide = _hud.BannerSlide(f);
        _banner.Visible = slide > 0f;
        _banner.Position = new Vector2((viewport.X - BannerWidth) * 0.5f, Mathf.Lerp(-PanelHeight - 16f, Margin, slide));

        _crosshair.Position = viewport * 0.5f;
        _crosshair.Show(_hud.DrawRing(f), _hud.DrawRingVisible);

        float fade = _hud.FaintFade(f);
        _fade.Visible = fade > 0f;
        _crosshair.Visible = fade <= 0f && !_hud.Waking;
        _fade.Color = new Color(Black, fade);
        float faintText = _hud.FaintTextAlpha(f);
        _fainted.Visible = faintText > 0f;
        _fainted.Modulate = new Color(1, 1, 1, faintText);

        float wake = _hud.WakeTextAlpha(f);
        _wake.Visible = wake > 0f;
        if (_wake.Visible && _hud.WakeDay != _shownWakeDay)
        {
            _shownWakeDay = _hud.WakeDay;
            _wake.Text = HudText.WokeUp(_shownWakeDay);
        }
        _wake.Modulate = new Color(1, 1, 1, wake);
    }
}
