using System.Collections.Generic;
using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// The wardrobe screen (the wardrobe in the house, free): tabs Look / Top / Bottom / Shoes / Head
/// / Face / Back. Look picks the skin tone, hair style, hair color and eye color; each slot tab
/// shows the owned items that fit, with little icons, and "None". Every pick changes the
/// character at once (the Player module draws it live behind the panel).
/// </summary>
public partial class WardrobeScreen : ScreenPanel
{
    public static readonly string[] TabNames = { "Look", "Top", "Bottom", "Shoes", "Head", "Face", "Back" };

    private readonly Button[] _tabs = new Button[TabNames.Length];
    private readonly VBoxContainer _content = new();
    private readonly List<Button> _cards = new();
    private int _tab;
    private string _shownKey = "";

    public WardrobeScreen(WardrobeModule core, IconStudio icons)
        : base(core, icons, "Q / E  LB / RB: tabs    Enter  A: choose    Esc  B: done")
    {
        Header.AddChild(WardrobeStyle.Label("My Wardrobe", 44, Colors.White, 10));
        var tabs = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        tabs.AddThemeConstantOverride("separation", 8);
        for (int i = 0; i < TabNames.Length; i++)
        {
            int tab = i;
            var button = new Button { Text = TabNames[i], CustomMinimumSize = new Vector2(112, 58), FocusMode = FocusModeEnum.All };
            button.Pressed += () => SelectTab(tab);
            _tabs[i] = button;
            tabs.AddChild(button);
        }
        Body.AddChild(tabs);
        _content.AddThemeConstantOverride("separation", 12);
        _content.SizeFlagsVertical = SizeFlags.ExpandFill;
        Body.AddChild(_content);
    }

    public int Tab => _tab;

    public void SelectTab(int tab)
    {
        _tab = Mathf.PosMod(tab, TabNames.Length);
        Refresh();
        FocusAt(_cards, 0);
    }

    public override void StepTab(int direction) => SelectTab(_tab + direction);

    public override Control FirstFocus() => _cards.Count > 0 ? _cards[0] : _tabs[_tab];

    public override void Refresh()
    {
        var look = Core.Appearance;
        string key = $"{_tab}|{Core.ReceivedCount}|{Core.Character}|{look.Skin},{look.HairStyle},{look.HairColor},{look.EyeColor}|" +
                     $"{Core.Worn(Slot.Top)},{Core.Worn(Slot.Bottom)},{Core.Worn(Slot.Shoes)},{Core.Worn(Slot.Head)},{Core.Worn(Slot.Face)},{Core.Worn(Slot.Back)}";
        if (key == _shownKey)
            return;
        _shownKey = key;
        for (int i = 0; i < _tabs.Length; i++)
            WardrobeStyle.Style(_tabs[i], i == _tab ? WardrobeStyle.Gold : WardrobeStyle.Card, 20, 22);
        var focus = GetViewport()?.GuiGetFocusOwner();
        int focused = focus is Button b ? _cards.IndexOf(b) : -1;
        Clear(_content);
        _cards.Clear();
        if (_tab == 0)
            BuildLook();
        else
            BuildSlot((Slot)(_tab - 1));
        if (focused >= 0)
            FocusAt(_cards, focused);
    }

    private void BuildLook()
    {
        var look = Core.Appearance;
        Section("Skin", Looks.SkinTones.Length, i => Swatch(WardrobeStyle.ToColor(Looks.SkinTones[i]), i == look.Skin,
            () => Set(i, look.HairStyle, look.HairColor, look.EyeColor)));
        var hairColor = WardrobeStyle.ToColor(Looks.HairColors[look.HairColor]);
        Section("Hair", Looks.HairStyles.Length, i =>
        {
            var style = Looks.HairStyles[i];
            var card = CardButton(Icons.Icon(style.ModelPath, hairColor), style.Name, null,
                i == look.HairStyle ? WardrobeStyle.Mint : WardrobeStyle.Card, new Vector2(124, 150));
            card.Pressed += () => Set(look.Skin, i, look.HairColor, look.EyeColor);
            return card;
        });
        Section("Hair Color", Looks.HairColors.Length, i => Swatch(WardrobeStyle.ToColor(Looks.HairColors[i]), i == look.HairColor,
            () => Set(look.Skin, look.HairStyle, i, look.EyeColor)));
        Section("Eyes", Looks.EyeColors.Length, i => Swatch(WardrobeStyle.ToColor(Looks.EyeColors[i]), i == look.EyeColor,
            () => Set(look.Skin, look.HairStyle, look.HairColor, i)));
    }

    private void Set(int skin, int hairStyle, int hairColor, int eyeColor) =>
        Core.Handle(new SetLook(skin, hairStyle, hairColor, eyeColor));

    private void Section(string title, int count, System.Func<int, Button> make)
    {
        var label = WardrobeStyle.Label(title, 24, WardrobeStyle.Ink);
        label.HorizontalAlignment = HorizontalAlignment.Left;
        _content.AddChild(label);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        for (int i = 0; i < count; i++)
        {
            var button = make(i);
            _cards.Add(button);
            row.AddChild(button);
        }
        _content.AddChild(row);
    }

    private Button Swatch(Color color, bool chosen, System.Action pick)
    {
        var button = new Button { CustomMinimumSize = new Vector2(70, 70), FocusMode = FocusModeEnum.All };
        WardrobeStyle.Style(button, color, 35);
        if (chosen)
            button.AddThemeStyleboxOverride("normal", WardrobeStyle.Box(color, 35, 8, WardrobeStyle.Gold));
        button.Pressed += pick;
        return button;
    }

    private void BuildSlot(Slot slot)
    {
        if (slot == Slot.Bottom && Core.BottomHidden)
            _content.AddChild(WardrobeStyle.Label("Your dress covers the bottom: it shows again with another top.", 20, WardrobeStyle.Soft));
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        _content.AddChild(grid);
        int worn = Core.Worn(slot);
        var none = CardButton(null, "None", "-", worn < 0 ? WardrobeStyle.Mint : WardrobeStyle.Card, new Vector2(156, 204));
        none.Pressed += () => Core.Handle(Equip.Unequip(slot));
        _cards.Add(none);
        grid.AddChild(none);
        var items = Catalog.Items;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].Slot != slot || !Core.Owns(i) || !Core.FitsCharacter(i))
                continue;
            string id = items[i].Id;
            var card = CardButton(Icons.Icon(items[i].ModelPath), items[i].Name, null,
                i == worn ? WardrobeStyle.Mint : WardrobeStyle.Card, new Vector2(156, 204));
            card.Pressed += () => Core.Handle(new Equip(id));
            _cards.Add(card);
            grid.AddChild(card);
        }
    }
}
