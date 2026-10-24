using Fungtopia.Shell;
using Godot;

namespace Fungtopia.HUD.View;

/// <summary>
/// Draws the HUD Core on top of the game: hearts in a mushroom-cap panel at the top left (shaking
/// on a hit), the Mushroom Coins panel at the top right (popping, with floating "+50"s), the bow
/// draw ring around the center crosshair, and the soft "You fainted..." fade. It reads the Core
/// between the last two ticks (<c>Engine.GetPhysicsInterpolationFraction</c>) and sets texts only
/// when they change.
/// </summary>
public partial class HudView : CanvasLayer, IModuleView
{
    private const float Margin = 28f;
    private const float PanelHeight = 124f;
    private const float CoinsWidth = 330f;

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
    private int _shownCoins = -1;

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

        _crosshair = new Crosshair();
        _crosshair.SetAnchorsPreset(Control.LayoutPreset.Center);
        _root.AddChild(_crosshair);

        _fade = new ColorRect { Color = new Color(HudStyle.Dusk, 0f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
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

        _crosshair.Position = viewport * 0.5f;
        _crosshair.Show(_hud.DrawRing(f), _hud.DrawRingVisible);

        float fade = _hud.FaintFade(f);
        _fade.Visible = _fainted.Visible = fade > 0f;
        _fade.Color = new Color(HudStyle.Dusk, fade);
        _fainted.Modulate = new Color(1, 1, 1, _hud.FaintTextAlpha(f));
    }
}
