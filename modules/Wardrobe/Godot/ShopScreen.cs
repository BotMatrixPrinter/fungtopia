using System.Collections.Generic;
using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// Abby's clothing shop in Mushroom Town: every shop item that fits the character, with its icon
/// and price in Mushroom Coins (M¢); owned ones are marked. Picking a card selects it, Buy pays
/// through the wallet; "Not enough M¢" when the wallet says no.
/// </summary>
public partial class ShopScreen : ScreenPanel
{
    private readonly Label _coins;
    private readonly GridContainer _grid = new() { Columns = 5 };
    private readonly List<Button> _cards = new();
    private readonly List<int> _cardItems = new();
    private readonly Label _selected;
    private readonly Label _status;
    private readonly Button _buy = new() { Text = "Buy", CustomMinimumSize = new Vector2(200, 64), FocusMode = FocusModeEnum.All };
    private int _selection = -1;
    private string _shownKey = "";

    public ShopScreen(WardrobeModule core, IconStudio icons)
        : base(core, icons, "Enter  A: pick, then Buy    Esc  B: done")
    {
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 18);
        top.AddChild(new AbbyPortrait());
        var titles = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = WardrobeStyle.Label("Abby's Clothes", 46, Colors.White, 10);
        title.HorizontalAlignment = HorizontalAlignment.Left;
        titles.AddChild(title);
        var hello = WardrobeStyle.Label("\"Hi hi! Find something cute!\"", 22, WardrobeStyle.Cream, 6);
        hello.HorizontalAlignment = HorizontalAlignment.Left;
        titles.AddChild(hello);
        top.AddChild(titles);
        var purse = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        purse.AddThemeStyleboxOverride("panel", WardrobeStyle.Box(WardrobeStyle.Gold, 26, 4, null, 14));
        _coins = WardrobeStyle.Label("M¢ 0", 30, WardrobeStyle.Ink);
        purse.AddChild(_coins);
        top.AddChild(purse);
        Header.AddChild(top);

        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 12);
        _grid.SizeFlagsVertical = SizeFlags.ExpandFill;
        Body.AddChild(_grid);

        var texts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        _selected = WardrobeStyle.Label("Pick something!", 26, WardrobeStyle.Ink);
        _selected.HorizontalAlignment = HorizontalAlignment.Left;
        _status = WardrobeStyle.Label("", 24, WardrobeStyle.Warn);
        _status.HorizontalAlignment = HorizontalAlignment.Left;
        texts.AddChild(_selected);
        texts.AddChild(_status);
        // The footer: what is picked and the answer, the hint below them, Buy and Done.
        Footer.RemoveChild(Hint);
        Hint.AddThemeConstantOverride("line_spacing", 0);
        texts.AddChild(Hint);
        Footer.AddChild(texts);
        Footer.MoveChild(texts, 0);
        WardrobeStyle.Style(_buy, WardrobeStyle.Gold, 28, 30);
        _buy.Pressed += Buy;
        Footer.AddChild(_buy);
        Footer.MoveChild(_buy, 1);
    }

    public override Control FirstFocus() => _cards.Count > 0 ? _cards[0] : DoneButton;

    /// <summary>Selects the card of item <paramref name="index"/> (an index in <see cref="Catalog.Items"/>).</summary>
    public void Select(int index)
    {
        _selection = index;
        _shownKey = "";
        Refresh();
    }

    public void Buy()
    {
        if (_selection >= 0)
            Core.Buy(Catalog.Items[_selection].Id);
    }

    public override void Refresh()
    {
        _coins.Text = WardrobeStyle.Coins(Core.Coins);
        string key = $"{Core.ReceivedCount}|{Core.Character}|{_selection}";
        if (key != _shownKey)
        {
            _shownKey = key;
            var focus = GetViewport()?.GuiGetFocusOwner();
            int focused = focus is Button b ? _cards.IndexOf(b) : -1;
            Rebuild();
            if (focused >= 0)
                FocusAt(_cards, focused);
        }
        if (_selection >= 0)
        {
            var item = Catalog.Items[_selection];
            bool owned = Core.Owns(_selection);
            _selected.Text = owned ? $"{item.Name}  (owned)" : $"{item.Name}  {item.Price} M¢";
            _buy.Disabled = !Core.CanBuy(_selection) || Core.LastBuy == BuyResult.Pending;
        }
        else
        {
            _selected.Text = "Pick something!";
            _buy.Disabled = true;
        }
        _status.Text = Core.LastBuy switch
        {
            BuyResult.NotEnough => "Not enough M¢",
            BuyResult.Bought => "Thank you! It's in your wardrobe.",
            BuyResult.Pending => "...",
            _ => "",
        };
        _status.LabelSettings.FontColor = Core.LastBuy == BuyResult.Bought ? new Color(0.25f, 0.6f, 0.4f) : WardrobeStyle.Warn;
    }

    private void Rebuild()
    {
        Clear(_grid);
        _cards.Clear();
        _cardItems.Clear();
        var items = Catalog.Items;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].Source != Sources.Shop || !Core.FitsCharacter(i))
                continue;
            int index = i;
            bool owned = Core.Owns(i);
            var fill = i == _selection ? WardrobeStyle.Mint : owned ? new Color(0.9f, 0.88f, 0.86f) : WardrobeStyle.Card;
            var card = CardButton(Icons.Icon(items[i].ModelPath), items[i].Name, null, fill, new Vector2(156, 196),
                owned ? "Owned ✓" : $"{items[i].Price} M¢", owned ? WardrobeStyle.Soft : new Color(0.8f, 0.5f, 0.1f), 86f);
            card.Pressed += () =>
            {
                Select(index);
                if (Core.CanBuy(index))
                    _buy.GrabFocus();
            };
            _cards.Add(card);
            _cardItems.Add(i);
            _grid.AddChild(card);
        }
    }
}
