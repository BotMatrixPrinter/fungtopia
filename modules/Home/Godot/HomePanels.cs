using System.Collections.Generic;
using Fungtopia.Input;
using Godot;

namespace Fungtopia.Home.View;

/// <summary>
/// The Home module's 2D panels (a <see cref="CanvasLayer"/> on layer 11): the Interact prompt,
/// the furniture shop's catalog (prices, the wallet's coins, the answer to the last purchase), the
/// decorating picker (the unplaced furniture with counts, and "Move placed items") and placement
/// mode's title and help. Rounded cream panels with an ink outline and a
/// pink title tab; texts change only when the Core's values do. Prompts and hint lines are
/// RichTextLabels with the icons of the active device's buttons only, rebuilt when it changes.
/// </summary>
public partial class HomePanels : CanvasLayer
{
    private static readonly Color Ink = new(0.28f, 0.16f, 0.18f);
    private static readonly Color Cream = new(1f, 0.97f, 0.9f, 0.96f);
    private static readonly Color Pink = new(0.98f, 0.62f, 0.7f);

    private PanelContainer _prompt = null!;
    private RichTextLabel _promptLabel = null!;
    private PanelContainer _shop = null!;
    private Label _coins = null!;
    private readonly Label[] _lines = new Label[Furniture.KindCount];
    private Label _answer = null!;
    private PanelContainer _decorate = null!;
    private RichTextLabel _decorateTitle = null!;
    private RichTextLabel _shopHelp = null!;
    private RichTextLabel _decorateHelp = null!;
    private PanelContainer _picker = null!;
    private readonly Label[] _pickerLines = new Label[Furniture.KindCount + 1];
    private Label _pickerEmpty = null!;
    private RichTextLabel _pickerHelp = null!;

    private HomePrompt _shownPrompt = (HomePrompt)(-1);
    private DeviceKind _shownDevice = (DeviceKind)(-1);
    private bool _shownMouse;
    private int _shownCoins = -1, _shownSelection = -1, _shownAnswer = -1, _shownHeld = -2, _shownItems = -1;
    private int _shownHighlight = -2, _shownPicker = -1;

    private static Font? s_font;

    // The resource cache holds textures weakly; this keeps the registered icons alive.
    private static readonly List<Texture2D> s_kept = new();

    private static Font Font => s_font ??= new SystemFont
    {
        FontNames = new[] { "Nunito", "Fredoka", "Baloo 2", "Varela Round", "Comic Neue", "Quicksand", "DejaVu Sans" },
        FontWeight = 800,
        Antialiasing = TextServer.FontAntialiasing.Gray,
    };

    public override void _Ready()
    {
        Layer = 11;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _prompt = Panel(Cream, 22);
        _promptLabel = Rich(30, Ink);
        _prompt.AddChild(_promptLabel);
        _prompt.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _prompt.GrowHorizontal = Control.GrowDirection.Both;
        _prompt.GrowVertical = Control.GrowDirection.Begin;
        _prompt.OffsetTop = _prompt.OffsetBottom = -250;
        root.AddChild(_prompt);

        _shop = Panel(Cream, 26);
        _shop.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
        _shop.GrowVertical = Control.GrowDirection.Both;
        _shop.OffsetLeft = 60;
        _shop.CustomMinimumSize = new Vector2(560, 0);
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 6);
        _shop.AddChild(list);
        var titleRow = new HBoxContainer();
        titleRow.AddChild(Tab("Furniture Shop"));
        titleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _coins = Text("", 26, new Color(0.75f, 0.5f, 0.05f));
        titleRow.AddChild(_coins);
        list.AddChild(titleRow);
        var forSale = Furniture.ForSale;
        for (int i = 0; i < forSale.Length; i++)
        {
            _lines[i] = Text(HomeText.CatalogLine(forSale[i]), 26, Ink);
            list.AddChild(_lines[i]);
        }
        _answer = Text("", 22, new Color(0.45f, 0.3f, 0.5f));
        _answer.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _answer.CustomMinimumSize = new Vector2(500, 0);
        list.AddChild(_answer);
        _shopHelp = Rich(18, new Color(0.5f, 0.4f, 0.42f));
        list.AddChild(_shopHelp);
        root.AddChild(_shop);

        _decorate = Panel(Cream, 20);
        _decorate.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _decorate.GrowHorizontal = Control.GrowDirection.Both;
        _decorate.OffsetTop = 24;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        _decorate.AddChild(box);
        _decorateTitle = Rich(28, Ink);
        _decorateTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _decorateTitle.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        box.AddChild(_decorateTitle);
        _decorateHelp = Rich(18, new Color(0.5f, 0.4f, 0.42f));
        _decorateHelp.HorizontalAlignment = HorizontalAlignment.Center;
        _decorateHelp.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        box.AddChild(_decorateHelp);
        root.AddChild(_decorate);

        _picker = Panel(Cream, 26);
        _picker.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
        _picker.GrowVertical = Control.GrowDirection.Both;
        _picker.OffsetLeft = 60;
        _picker.CustomMinimumSize = new Vector2(480, 0);
        var pickerList = new VBoxContainer();
        pickerList.AddThemeConstantOverride("separation", 6);
        _picker.AddChild(pickerList);
        var pickerTitle = new HBoxContainer();
        pickerTitle.AddChild(Tab("Decorate"));
        pickerList.AddChild(pickerTitle);
        _pickerEmpty = Text(HomeText.EverythingPlaced, 22, new Color(0.45f, 0.3f, 0.5f));
        pickerList.AddChild(_pickerEmpty);
        for (int i = 0; i < _pickerLines.Length; i++)
        {
            _pickerLines[i] = Text("", 26, Ink);
            pickerList.AddChild(_pickerLines[i]);
        }
        _pickerHelp = Rich(18, new Color(0.5f, 0.4f, 0.42f));
        pickerList.AddChild(_pickerHelp);
        root.AddChild(_picker);

        _prompt.Visible = _shop.Visible = _decorate.Visible = _picker.Visible = false;
    }

    public void Refresh(HomeModule home)
    {
        if (home.Device != _shownDevice || home.MouseHints != _shownMouse)
        {
            _shownDevice = home.Device;
            _shownMouse = home.MouseHints;
            SetRich(_shopHelp, HomeText.ShopHelp(home.Device));
            SetRich(_pickerHelp, home.MouseHints ? HomeText.PickerMouseHelp() : HomeText.PickerHelp(home.Device));
            _shownPrompt = (HomePrompt)(-1);
            _shownHeld = -2;
        }

        if (home.Prompt != _shownPrompt)
        {
            _shownPrompt = home.Prompt;
            _prompt.Visible = home.Prompt != HomePrompt.None;
            SetRich(_promptLabel, HomeText.Prompt(home.Prompt, home.Device));
        }

        _shop.Visible = home.ShopOpen;
        if (home.ShopOpen)
        {
            if (home.Coins != _shownCoins)
            {
                _shownCoins = home.Coins;
                _coins.Text = $"M¢ {home.Coins:N0}";
                _shownSelection = -1; // affordability colors change too
            }
            if (home.ShopSelection != _shownSelection)
            {
                _shownSelection = home.ShopSelection;
                var forSale = Furniture.ForSale;
                for (int i = 0; i < forSale.Length; i++)
                {
                    bool selected = i == home.ShopSelection;
                    bool affordable = Furniture.Price(forSale[i]) <= home.Coins;
                    _lines[i].Text = (selected ? "▶ " : "   ") + HomeText.CatalogLine(forSale[i]);
                    _lines[i].AddThemeColorOverride("font_color",
                        selected ? new Color(0.85f, 0.3f, 0.42f) : affordable ? Ink : new Color(0.65f, 0.58f, 0.58f));
                }
            }
            if (home.ShopResults != _shownAnswer)
            {
                _shownAnswer = home.ShopResults;
                _answer.Text = HomeText.ShopAnswer(home.LastShopResult, home.LastShopKind);
            }
        }

        _picker.Visible = home.Decorating && home.Picking;
        if (_picker.Visible)
        {
            // The picker's lines change when the highlight, the furniture owned or what stands do.
            int key = home.PickerSelection + 100 * (home.PickerCount + 100 * (home.ItemCount + 100 * home.ItemsPlaced));
            if (key != _shownPicker)
            {
                _shownPicker = key;
                var entries = home.PickerEntries;
                _pickerEmpty.Visible = entries.Length == 0;
                for (int i = 0; i < _pickerLines.Length; i++)
                {
                    var line = _pickerLines[i];
                    line.Visible = i <= entries.Length;
                    if (!line.Visible)
                        continue;
                    bool selected = i == home.PickerSelection;
                    line.Text = (selected ? "▶ " : "   ") + (i < entries.Length ? HomeText.PickerLine(entries[i]) : HomeText.MovePlacedLine);
                    line.AddThemeColorOverride("font_color", selected ? new Color(0.85f, 0.3f, 0.42f) : Ink);
                }
            }
        }
        else
        {
            _shownPicker = -1;
        }

        _decorate.Visible = home.Decorating && !home.Picking;
        if (_decorate.Visible && (home.Held != _shownHeld || home.Highlighted != _shownHighlight || home.ItemCount != _shownItems))
        {
            _shownHeld = home.Held;
            _shownHighlight = home.Highlighted;
            _shownItems = home.ItemCount;
            var items = home.Items;
            SetRich(_decorateTitle, home.Held >= 0
                ? $"Decorating: {Furniture.Name(items[home.Held].Kind)}"
                : home.Highlighted >= 0 ? $"Decorating: {Furniture.Name(items[home.Highlighted].Kind)}" : "Decorating: move placed items");
            SetRich(_decorateHelp, home.MouseHints ? HomeText.MouseHelp(home.Held >= 0, home.Highlighted >= 0)
                : home.Held >= 0 ? HomeText.DecorateHelp(home.Device) : HomeText.MoveHelp(home.Device, home.Highlighted >= 0));
        }
        else if (!_decorate.Visible)
        {
            _shownHeld = -2;
        }
    }

    /// <summary>The picker line under the mouse (an index as in <see cref="HomeModule.PickerSelection"/>), or -1.</summary>
    public int PickerLineAtMouse()
    {
        if (!_picker.Visible)
            return -1;
        for (int i = 0; i < _pickerLines.Length; i++)
        {
            var line = _pickerLines[i];
            if (line.Visible && line.GetGlobalRect().HasPoint(line.GetGlobalMousePosition()))
                return i;
        }
        return -1;
    }

    private static PanelContainer Panel(Color fill, int padding)
    {
        var style = new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = Ink,
            ShadowColor = new Color(0f, 0f, 0f, 0.25f),
            ShadowSize = 8,
            ShadowOffset = new Vector2(0, 4),
        };
        style.SetBorderWidthAll(4);
        style.SetCornerRadiusAll(26);
        style.SetContentMarginAll(padding);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static PanelContainer Tab(string title)
    {
        var style = new StyleBoxFlat { BgColor = Pink, BorderColor = Ink };
        style.SetBorderWidthAll(3);
        style.SetCornerRadiusAll(18);
        style.ContentMarginLeft = style.ContentMarginRight = 18;
        style.ContentMarginTop = style.ContentMarginBottom = 4;
        var tab = new PanelContainer();
        tab.AddThemeStyleboxOverride("panel", style);
        tab.AddChild(Text(title, 28, new Color(1f, 1f, 1f)));
        return tab;
    }

    /// <summary>A BBCode label sized to its text and icons (one line; it grows with the icons).</summary>
    private static RichTextLabel Rich(int size, Color color)
    {
        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            AutowrapMode = TextServer.AutowrapMode.Off,
            ScrollActive = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontOverride("normal_font", Font);
        label.AddThemeFontSizeOverride("normal_font_size", size);
        label.AddThemeColorOverride("default_color", color);
        return label;
    }

    /// <summary>Shows BBCode, first making each icon in it loadable (also without the Input module's view, as in the preview).</summary>
    private static void SetRich(RichTextLabel label, string bbcode)
    {
        for (int at = bbcode.IndexOf("[img="); at >= 0; at = bbcode.IndexOf("[img=", at + 1))
        {
            int start = bbcode.IndexOf(']', at) + 1, end = bbcode.IndexOf("[/img]", start);
            if (start > 0 && end > start)
                RegisterIcon(bbcode[start..end]);
        }
        label.Text = bbcode;
    }

    /// <summary>Makes an SVG icon loadable by its res:// path when Godot has not imported it (a fresh checkout).</summary>
    private static void RegisterIcon(string path)
    {
        if (ResourceLoader.HasCached(path) || ResourceLoader.Exists(path))
            return;
        var image = new Image();
        if (image.LoadSvgFromString(FileAccess.GetFileAsString(path), 2f) != Error.Ok)
        {
            GD.PushWarning($"Fungtopia: Home: cannot read the icon {path}.");
            return;
        }
        var texture = ImageTexture.CreateFromImage(image);
        texture.TakeOverPath(path);
        s_kept.Add(texture);
    }

    private static Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", Font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
