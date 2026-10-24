using System.Collections.Generic;
using Fungtopia.Input;
using Godot;

namespace Fungtopia.Food.View;

/// <summary>
/// The Food module's 2D panels (a <see cref="CanvasLayer"/> on layer 11), in the game's panel
/// style (rounded cream panels with an ink outline and a pink title tab): the stall's Interact
/// prompt, the Mushroom Munchies menu (each food's picture, price, healing and how many you carry,
/// the wallet's coins and the answer to the last purchase) and the food bar near the bottom of the
/// screen (the three foods with their counts, the selected one highlighted; hidden when the bag
/// is empty). Texts change only when the Core's values do.
/// </summary>
public partial class FoodPanels : CanvasLayer
{
    private static readonly Color Ink = new(0.28f, 0.16f, 0.18f);
    private static readonly Color Cream = new(1f, 0.97f, 0.9f, 0.96f);
    private static readonly Color Pink = new(0.98f, 0.62f, 0.7f);
    private static readonly Color Selected = new(0.85f, 0.3f, 0.42f);
    private static readonly Color Faded = new(0.65f, 0.58f, 0.58f);
    private static readonly Color Hint = new(0.5f, 0.4f, 0.42f);

    private PanelContainer _prompt = null!;
    private RichTextLabel _promptLabel = null!;
    private PanelContainer _menu = null!;
    private Label _coins = null!;
    private readonly PanelContainer[] _menuRows = new PanelContainer[FoodKinds.Count];
    private readonly Label[] _menuLines = new Label[FoodKinds.Count];
    private Label _answer = null!;
    private RichTextLabel _menuHelp = null!;
    private PanelContainer _bar = null!;
    private readonly PanelContainer[] _slots = new PanelContainer[FoodKinds.Count];
    private readonly Label[] _slotCounts = new Label[FoodKinds.Count];
    private readonly RichTextLabel[] _slotKeys = new RichTextLabel[FoodKinds.Count];
    private RichTextLabel _barHelp = null!;
    private StyleBoxFlat _rowStyle = null!, _rowSelectedStyle = null!, _slotStyle = null!, _slotSelectedStyle = null!;

    private DeviceKind _shownDevice = (DeviceKind)(-1);
    private bool _shownPrompt;
    private int _shownCoins = -1, _shownSelection = -1, _shownAnswer = -1, _shownBag = -1;

    // The bar's hint text is 16 pixels; its icons are a little smaller than the menu's.
    private const int BarIconSize = 22;

    private static Font? s_font;

    // The resource cache holds textures weakly; this keeps the registered icons alive.
    private static readonly List<Texture2D> s_kept = new();

    private static Font Font => s_font ??= new SystemFont
    {
        FontNames = new[] { "Nunito", "Fredoka", "Baloo 2", "Varela Round", "Comic Neue", "Quicksand", "DejaVu Sans" },
        FontWeight = 800,
        Antialiasing = TextServer.FontAntialiasing.Gray,
    };

    /// <summary>The pictures of the foods (rendered from their models by the view), by <see cref="FoodKinds"/> number.</summary>
    public Texture2D?[] Pictures { get; } = new Texture2D?[FoodKinds.Count];

    public override void _Ready()
    {
        Layer = 11;
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        _rowStyle = Box(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), 0, 18, 8);
        _rowSelectedStyle = Box(new Color(1f, 0.88f, 0.9f), Selected, 3, 18, 8);
        _slotStyle = Box(new Color(1f, 0.97f, 0.9f, 0.9f), Ink, 3, 20, 6);
        _slotSelectedStyle = Box(new Color(1f, 0.86f, 0.9f, 0.98f), Selected, 5, 20, 6);

        _prompt = Panel(22);
        _promptLabel = Rich(30, Ink);
        _prompt.AddChild(_promptLabel);
        _prompt.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _prompt.GrowHorizontal = Control.GrowDirection.Both;
        _prompt.GrowVertical = Control.GrowDirection.Begin;
        _prompt.OffsetTop = _prompt.OffsetBottom = -250;
        root.AddChild(_prompt);

        _menu = Panel(26);
        _menu.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
        _menu.GrowVertical = Control.GrowDirection.Both;
        _menu.OffsetLeft = 60;
        _menu.CustomMinimumSize = new Vector2(640, 0);
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 8);
        _menu.AddChild(list);
        var titleRow = new HBoxContainer();
        titleRow.AddChild(Tab(FoodText.StallName));
        titleRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _coins = Text("", 26, new Color(0.75f, 0.5f, 0.05f));
        titleRow.AddChild(_coins);
        list.AddChild(titleRow);
        for (int kind = 0; kind < FoodKinds.Count; kind++)
        {
            var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeStyleboxOverride("panel", _rowStyle);
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 14);
            line.AddChild(Picture(kind, 64));
            _menuLines[kind] = Text(FoodText.MenuLine(kind, 0), 26, Ink);
            _menuLines[kind].SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            line.AddChild(_menuLines[kind]);
            row.AddChild(line);
            _menuRows[kind] = row;
            list.AddChild(row);
        }
        _answer = Text("", 22, new Color(0.45f, 0.3f, 0.5f));
        list.AddChild(_answer);
        _menuHelp = Rich(18, Hint);
        list.AddChild(_menuHelp);
        root.AddChild(_menu);

        _bar = Panel(10);
        _bar.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _bar.GrowHorizontal = Control.GrowDirection.Both;
        _bar.GrowVertical = Control.GrowDirection.Begin;
        _bar.OffsetTop = _bar.OffsetBottom = -24;
        var barBox = new VBoxContainer();
        barBox.AddThemeConstantOverride("separation", 2);
        _bar.AddChild(barBox);
        var slots = new HBoxContainer();
        slots.AddThemeConstantOverride("separation", 10);
        slots.Alignment = BoxContainer.AlignmentMode.Center;
        barBox.AddChild(slots);
        for (int kind = 0; kind < FoodKinds.Count; kind++)
        {
            var slot = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            slot.AddThemeStyleboxOverride("panel", _slotStyle);
            var stack = new Control { CustomMinimumSize = new Vector2(76, 76), MouseFilter = Control.MouseFilterEnum.Ignore };
            var picture = Picture(kind, 76);
            stack.AddChild(picture);
            _slotKeys[kind] = Rich(16, Ink);
            _slotKeys[kind].Position = new Vector2(-4, -4);
            stack.AddChild(_slotKeys[kind]);
            _slotCounts[kind] = Text("0", 24, Ink);
            _slotCounts[kind].AddThemeColorOverride("font_outline_color", new Color(1f, 0.97f, 0.9f));
            _slotCounts[kind].AddThemeConstantOverride("outline_size", 8);
            _slotCounts[kind].SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            _slotCounts[kind].GrowHorizontal = Control.GrowDirection.Begin;
            _slotCounts[kind].GrowVertical = Control.GrowDirection.Begin;
            _slotCounts[kind].OffsetRight = _slotCounts[kind].OffsetBottom = 4;
            stack.AddChild(_slotCounts[kind]);
            slot.AddChild(stack);
            _slots[kind] = slot;
            slots.AddChild(slot);
        }
        _barHelp = Rich(16, Hint);
        _barHelp.HorizontalAlignment = HorizontalAlignment.Center;
        _barHelp.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        barBox.AddChild(_barHelp);
        root.AddChild(_bar);

        _prompt.Visible = _menu.Visible = _bar.Visible = false;
    }

    public void Refresh(FoodModule food)
    {
        if (food.Device != _shownDevice)
        {
            _shownDevice = food.Device;
            SetRich(_promptLabel, FoodText.Prompt(food.Device));
            SetRich(_menuHelp, FoodText.MenuHelp(food.Device));
            SetRich(_barHelp, FoodText.BarHelp(food.Device, BarIconSize));
            for (int kind = 0; kind < FoodKinds.Count; kind++)
                SetRich(_slotKeys[kind], FoodText.SlotKey(kind, food.Device));
        }

        bool prompt = food.StallPrompt;
        if (prompt != _shownPrompt)
        {
            _shownPrompt = prompt;
            _prompt.Visible = prompt;
        }

        // Counts and the selection, packed into one number to notice any change.
        int bag = food.Count(0) | food.Count(1) << 4 | food.Count(2) << 8 | food.Selected << 12;
        bool bagChanged = bag != _shownBag;
        if (bagChanged)
        {
            _shownBag = bag;
            for (int kind = 0; kind < FoodKinds.Count; kind++)
            {
                int count = food.Count(kind);
                bool selected = kind == food.Selected;
                _slotCounts[kind].Text = count.ToString();
                _slots[kind].AddThemeStyleboxOverride("panel", selected ? _slotSelectedStyle : _slotStyle);
                _slots[kind].Modulate = count > 0 ? Colors.White : new Color(1f, 1f, 1f, 0.45f);
                _slots[kind].Scale = selected ? new Vector2(1.08f, 1.08f) : Vector2.One;
                _slots[kind].PivotOffset = _slots[kind].Size / 2f;
            }
        }
        _bar.Visible = !food.BagEmpty && !food.StartScreenOpen;

        _menu.Visible = food.MenuOpen;
        if (!food.MenuOpen)
            return;
        if (food.Coins != _shownCoins)
        {
            _shownCoins = food.Coins;
            _coins.Text = $"M¢ {food.Coins:N0}";
            _shownSelection = -1; // affordability colors change too
        }
        if (food.MenuSelection != _shownSelection || bagChanged)
        {
            _shownSelection = food.MenuSelection;
            for (int kind = 0; kind < FoodKinds.Count; kind++)
            {
                bool selected = kind == food.MenuSelection;
                bool affordable = FoodCatalog.Price(kind) <= food.Coins && food.Count(kind) < FoodCatalog.MaxEach;
                _menuLines[kind].Text = FoodText.MenuLine(kind, food.Count(kind));
                _menuLines[kind].AddThemeColorOverride("font_color", selected ? Selected : affordable ? Ink : Faded);
                _menuRows[kind].AddThemeStyleboxOverride("panel", selected ? _rowSelectedStyle : _rowStyle);
            }
        }
        if (food.BuyResults != _shownAnswer)
        {
            _shownAnswer = food.BuyResults;
            _answer.Text = FoodText.BuyAnswer(food.LastBuyResult, food.LastBuyKind);
        }
    }

    /// <summary>A square picture of <paramref name="kind"/> (filled in once the view's picture exists).</summary>
    private Control Picture(int kind, int size)
    {
        var rect = new TextureRect
        {
            CustomMinimumSize = new Vector2(size, size),
            Size = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rect.Ready += () => rect.Texture = Pictures[kind];
        return rect;
    }

    private static StyleBoxFlat Box(Color fill, Color border, int borderWidth, int radius, int padding)
    {
        var style = new StyleBoxFlat { BgColor = fill, BorderColor = border };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(radius);
        style.SetContentMarginAll(padding);
        return style;
    }

    private static PanelContainer Panel(int padding)
    {
        var style = new StyleBoxFlat
        {
            BgColor = Cream,
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
            GD.PushWarning($"Fungtopia: Food: cannot read the icon {path}.");
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
