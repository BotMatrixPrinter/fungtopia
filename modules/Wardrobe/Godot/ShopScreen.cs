using System.Collections.Generic;
using Fungtopia.Input;
using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// Abby's clothing shop in Mushroom Town: every shop item that fits the character, with its icon
/// and price in Mushroom Coins (M¢); owned ones are marked. The highlighted card's item (else the
/// picked one) is tried on: the character on the left shows it until the highlight moves on or
/// the shop closes, and keeps it once bought. Picking a card selects it, Buy pays through the
/// wallet; "Not enough M¢" when the wallet says no.
/// </summary>
public partial class ShopScreen : ScreenPanel
{
    private readonly Label _coins;
    private readonly GridContainer _grid = new() { Columns = 5 };
    private readonly List<Button> _cards = new();
    private readonly List<int> _cardItems = new();
    private readonly Label _selected;
    private readonly Label _status;
    private readonly CapButton _buy = new("Buy", WardrobeStyle.Gold, new Vector2(240, 104), 40);
    private int _selection = -1;
    private string _shownKey = "";

    public ShopScreen(WardrobeModule core, IconStudio icons)
        : base(core, icons, "Abby's Clothes")
    {
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 18);
        // Abby drawn smaller, so the three rows of cards fit.
        var abby = new Control { CustomMinimumSize = new Vector2(88, 108), MouseFilter = MouseFilterEnum.Ignore };
        abby.AddChild(new AbbyPortrait { Scale = new Vector2(0.72f, 0.72f) });
        top.AddChild(abby);
        var hello = WardrobeStyle.Label("\"Hi hi! Try things on and find something cute!\"", 24, WardrobeStyle.Ink);
        hello.HorizontalAlignment = HorizontalAlignment.Left;
        hello.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hello.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(hello);
        var purse = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        purse.AddThemeStyleboxOverride("panel", WardrobeStyle.Box(WardrobeStyle.Gold, 26, 4, null, 14));
        _coins = WardrobeStyle.Label("M¢ 0", 30, WardrobeStyle.Ink);
        purse.AddChild(_coins);
        top.AddChild(purse);
        Header.AddChild(top);

        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 10);
        _grid.SizeFlagsVertical = SizeFlags.ExpandFill;
        Body.AddChild(_grid);

        var texts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        _selected = WardrobeStyle.Label("Pick something!", 24, WardrobeStyle.Ink);
        _selected.HorizontalAlignment = HorizontalAlignment.Left;
        _status = WardrobeStyle.Label("", 22, WardrobeStyle.Warn);
        _status.HorizontalAlignment = HorizontalAlignment.Left;
        texts.AddChild(_selected);
        texts.AddChild(_status);
        // The footer: what is picked and the answer, the hint below them; Buy beside Done.
        Footer.RemoveChild(Hint);
        texts.AddChild(Hint);
        Footer.AddChild(texts);
        _buy.Pressed += Buy;
        Buttons.AddChild(_buy);
        Buttons.MoveChild(_buy, 0);
    }

    /// <summary>The controls, shown at the bottom of the panel with the device's button icons.</summary>
    public override string HintFor(DeviceKind device) => WardrobePrompts.Shop(device);

    /// <summary>The item the highlighted card shows (an index in <see cref="Catalog.Items"/>), else the picked one, else -1.</summary>
    public int Highlighted
    {
        get
        {
            var focus = GetViewport()?.GuiGetFocusOwner();
            int card = focus is Button b ? _cards.IndexOf(b) : -1;
            return card >= 0 ? _cardItems[card] : _selection;
        }
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
        int tried = Highlighted;
        Core.TryOn(tried >= 0 ? Catalog.Items[tried].Id : "");
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
            var card = CardButton(Icons.Icon(items[i].ModelPath), items[i].Name, null, fill, new Vector2(136, 166),
                owned ? "Owned ✓" : $"{items[i].Price} M¢", owned ? WardrobeStyle.Soft : new Color(0.8f, 0.5f, 0.1f), 58f);
            // Hovering a card highlights it (and tries it on), like moving the focus to it.
            card.MouseEntered += () => card.GrabFocus();
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
