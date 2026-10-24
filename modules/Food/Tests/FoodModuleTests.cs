using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Fungtopia.Food;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Fungtopia.Wallet;
using Xunit;

namespace Fungtopia.Food.Tests;

/// <summary>A synchronous bus that records every message and answers <see cref="SpendCoins"/> like the wallet.</summary>
internal sealed class FakeBus : IMessageBus
{
    private readonly Dictionary<Type, List<object>> _handlers = new();
    public readonly List<object> Published = new();
    public int Coins;

    public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
    {
        if (!_handlers.TryGetValue(typeof(T), out var list))
            _handlers[typeof(T)] = list = new List<object>();
        list.Add(handler);
    }

    public void Publish<T>(in T message) where T : struct
    {
        Published.Add(message);
        if (_handlers.TryGetValue(typeof(T), out var list))
            foreach (var handler in list)
                ((IMessageHandler<T>)handler).Handle(message);
        if (message is SpendCoins spend)
        {
            bool ok = spend.Amount >= 0 && spend.Amount <= Coins;
            if (ok)
                Coins -= spend.Amount;
            Publish(new SpendResult(spend.RequestId, ok, Coins));
        }
    }

    public IEnumerable<T> All<T>() => Published.OfType<T>();

    public T Last<T>() => Published.OfType<T>().Last();
}

public class FoodModuleTests
{
    private readonly FakeBus _bus = new();
    private readonly FoodModule _food = new();
    private readonly List<string> _log = new();
    private long _tick;

    public FoodModuleTests()
    {
        _food.Log = _log.Add;
        _food.Start(_bus);
    }

    private static readonly Vector3 Away = new(0f, 0f, 150f);

    private void Player(int health, bool alive = true, int max = 10, Vector3? at = null) =>
        _bus.Publish(new PlayerState(at ?? Away, 0f, Vector3.Zero, true, health, max, alive, CharacterKind.Girl));

    private void Tick(int count = 1)
    {
        for (int i = 0; i < count; i++)
            _food.Tick(new TickContext(_tick++, 1.0 / 60));
    }

    private static PlayerInput Input(bool interact = false, bool eat = false, int select = -1, int cycle = 0) =>
        new(Vector2.Zero, Vector2.Zero, false, false, false, Vector3.Zero, interact, false, Vector3.Zero, eat, select, cycle);

    private void Press(bool interact = false, bool eat = false, int select = -1, int cycle = 0)
    {
        _bus.Publish(Input(interact, eat, select, cycle));
        Tick();
    }

    /// <summary>Fills the bag through the menu with plenty of coins (published messages cleared after).</summary>
    private void Stock(int muffins, int soups, int pies)
    {
        _bus.Coins += 1000;
        _food.OpenMenu();
        int[] wanted = { muffins, soups, pies };
        for (int kind = 0; kind < 3; kind++)
            for (int i = 0; i < wanted[kind]; i++)
                _food.Buy(kind);
        _food.Back();
        Tick();
        _bus.Published.Clear();
    }

    private FoodBag Bag => _bus.Last<FoodBag>();

    // Catalog

    [Fact]
    public void CatalogHasThePricesAndHealingOfTheFeature()
    {
        Assert.Equal("Berry Muffin", FoodCatalog.Name(FoodKinds.Muffin));
        Assert.Equal("Mushroom Soup", FoodCatalog.Name(FoodKinds.Soup));
        Assert.Equal("Spore Pie", FoodCatalog.Name(FoodKinds.Pie));
        Assert.Equal(5, FoodCatalog.Price(FoodKinds.Muffin));
        Assert.Equal(12, FoodCatalog.Price(FoodKinds.Soup));
        Assert.Equal(35, FoodCatalog.Price(FoodKinds.Pie));
        // 1 heart is 2 health (5 hearts for 10); the pie heals fully.
        Assert.Equal(2, FoodCatalog.HealAmount(FoodKinds.Muffin));
        Assert.Equal(6, FoodCatalog.HealAmount(FoodKinds.Soup));
        Assert.Equal(HealPlayer.Full, FoodCatalog.HealAmount(FoodKinds.Pie));
        Assert.Equal(999, FoodCatalog.HealAmount(FoodKinds.Pie));
        Assert.Equal(9, FoodCatalog.MaxEach);
        // Cheap on purpose: one 50 M¢ monster buys about 10 muffins, 4 soups or a pie.
        Assert.Equal(10, 50 / FoodCatalog.Price(FoodKinds.Muffin));
        Assert.Equal(4, 50 / FoodCatalog.Price(FoodKinds.Soup));
        Assert.Equal(1, 50 / FoodCatalog.Price(FoodKinds.Pie));
    }

    [Fact]
    public void FoodBagIsPublishedEveryTick()
    {
        Tick(3);
        Assert.Equal(3, _bus.All<FoodBag>().Count());
        var bag = Bag;
        Assert.Equal(0, bag.Muffins + bag.Soups + bag.Pies);
        Assert.True(bag.Empty);
        Assert.Equal(FoodKinds.Muffin, bag.Selected);
        Assert.False(bag.Eating);
        Assert.Equal(0f, bag.EatProgress);
    }

    // Buying

    [Fact]
    public void BuyingWithEnoughCoinsSpendsThePriceAndPutsOneInTheBag()
    {
        _bus.Coins = 20;
        _food.OpenMenu();
        _food.MoveMenuSelection(1); // Mushroom Soup
        _food.Buy();
        var spend = _bus.Last<SpendCoins>();
        Assert.Equal(12, spend.Amount);
        Assert.Equal("Mushroom Soup", spend.What);
        Assert.Equal(8, _bus.Coins);
        Assert.Equal(1, _food.Count(FoodKinds.Soup));
        Assert.Equal(BuyResult.Bought, _food.LastBuyResult);
        Assert.Equal(8, _food.Coins);
        Tick();
        Assert.Equal(1, Bag.Soups);
        Assert.Equal(0, Bag.Muffins);
    }

    [Fact]
    public void BuyingWithTooFewCoinsChangesNothingAndSaysNotEnough()
    {
        _bus.Coins = 34;
        _food.OpenMenu();
        _food.Buy(FoodKinds.Pie);
        Assert.Single(_bus.All<SpendCoins>());
        Assert.Equal(34, _bus.Coins);
        Assert.Equal(0, _food.Count(FoodKinds.Pie));
        Assert.Equal(BuyResult.TooFewCoins, _food.LastBuyResult);
        Assert.Equal("Not enough M¢", FoodText.BuyAnswer(_food.LastBuyResult, _food.LastBuyKind));
        // Exactly enough is enough.
        _bus.Coins = 35;
        _food.Buy(FoodKinds.Pie);
        Assert.Equal(1, _food.Count(FoodKinds.Pie));
        Assert.Equal(0, _bus.Coins);
    }

    [Fact]
    public void YouCarryAtMostNineOfEachAndTheTenthIsNotCharged()
    {
        _bus.Coins = 1000;
        _food.OpenMenu();
        for (int i = 0; i < 9; i++)
            _food.Buy(FoodKinds.Muffin);
        Assert.Equal(9, _food.Count(FoodKinds.Muffin));
        Assert.Equal(1000 - 45, _bus.Coins);
        _food.Buy(FoodKinds.Muffin);
        Assert.Equal(9, _food.Count(FoodKinds.Muffin));
        Assert.Equal(9, _bus.All<SpendCoins>().Count());
        Assert.Equal(1000 - 45, _bus.Coins);
        Assert.Equal(BuyResult.Full, _food.LastBuyResult);
        // Other kinds have their own nine.
        _food.Buy(FoodKinds.Soup);
        Assert.Equal(1, _food.Count(FoodKinds.Soup));
    }

    [Fact]
    public void InteractInTheOpenMenuBuysTheHighlightedFood()
    {
        Player(10);
        _bus.Coins = 50;
        _bus.Publish(new OpenFoodMenu());
        Tick();
        Assert.True(_food.MenuOpen);
        Assert.Empty(_bus.All<SpendCoins>()); // the press that opened it buys nothing
        _food.MoveMenuSelection(-1); // wraps up to the Spore Pie
        Assert.Equal(FoodKinds.Pie, _food.MenuSelection);
        Press(interact: true);
        Assert.Equal(1, _food.Count(FoodKinds.Pie));
        Assert.Equal(15, _bus.Coins);
        _food.MoveMenuSelection(1); // wraps down to the muffin
        _food.Confirm(); // Enter / gamepad A
        Tick();
        Assert.Equal(1, _food.Count(FoodKinds.Muffin));
        Assert.Equal(10, _bus.Coins);
    }

    [Fact]
    public void ConfirmDoesNothingWithTheMenuClosed()
    {
        Player(10);
        _bus.Coins = 50;
        _food.Confirm();
        Tick();
        Assert.Empty(_bus.All<SpendCoins>());
    }

    [Fact]
    public void NavigateStepsTheHighlightOnceThenRepeatsWhileHeld()
    {
        _food.OpenMenu();
        _food.Navigate(1, 0.016f);
        Assert.Equal(1, _food.MenuSelection);
        _food.Navigate(1, 0.1f); // still within the repeat delay
        Assert.Equal(1, _food.MenuSelection);
        _food.Navigate(1, 0.3f);
        Assert.Equal(2, _food.MenuSelection);
        _food.Navigate(0, 0.016f);
        _food.Navigate(-1, 0.016f);
        Assert.Equal(1, _food.MenuSelection);
    }

    // The menu: Pip, the stall fallback, UiFocus

    [Fact]
    public void OpenFoodMenuOpensTheMenuWithUiFocusAndBackClosesIt()
    {
        Player(10);
        _bus.Publish(new OpenFoodMenu());
        Tick();
        Assert.True(_food.MenuOpen);
        var open = _bus.Last<UiFocus>();
        Assert.Equal(FoodModule.MenuFocusOwner, open.Owner);
        Assert.True(open.Open);
        Tick();
        Assert.Single(_bus.All<UiFocus>()); // once per change, not every tick
        _food.Back();
        Tick();
        Assert.False(_food.MenuOpen);
        var close = _bus.Last<UiFocus>();
        Assert.Equal(FoodModule.MenuFocusOwner, close.Owner);
        Assert.False(close.Open);
    }

    [Fact]
    public void UntilPipExistsInteractAtTheStallOpensTheMenu()
    {
        Player(10, at: Places.FoodSpot.Position);
        Tick();
        Assert.True(_food.StallPrompt);
        Press(interact: true);
        Assert.True(_food.MenuOpen);
        Assert.False(_food.StallPrompt);
        Assert.True(_bus.Last<UiFocus>().Open);
    }

    // Each device's icon files start with these; Generic pads show the Xbox buttons.
    private static string[] OwnIcons(DeviceKind device) => device switch
    {
        DeviceKind.KeyboardMouse => new[] { "key_", "mouse_" },
        DeviceKind.PlayStation => new[] { "ps_" },
        DeviceKind.Nintendo => new[] { "ns_" },
        _ => new[] { "xbox_" },
    };

    /// <summary>The icons' file names in <paramref name="bbcode"/>, and its text with the icons taken out.</summary>
    private static (List<string> Icons, string Words) Split(string bbcode)
    {
        var icons = new List<string>();
        var words = new System.Text.StringBuilder();
        int at = 0;
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(bbcode, @"\[img=\d+\]res://modules/Input/Glyphs/([a-z0-9_]+)\.svg\[/img\]"))
        {
            words.Append(bbcode, at, m.Index - at);
            icons.Add(m.Groups[1].Value);
            at = m.Index + m.Length;
        }
        words.Append(bbcode, at, bbcode.Length - at);
        return (icons, words.ToString());
    }

    private static string[] AllPromptTexts(DeviceKind device) => new[]
    {
        FoodText.Prompt(device), FoodText.MenuHelp(device), FoodText.BarHelp(device),
        FoodText.SlotKey(0, device), FoodText.SlotKey(1, device), FoodText.SlotKey(2, device),
    };

    [Theory]
    [InlineData(DeviceKind.KeyboardMouse)]
    [InlineData(DeviceKind.Xbox)]
    [InlineData(DeviceKind.PlayStation)]
    [InlineData(DeviceKind.Nintendo)]
    [InlineData(DeviceKind.Generic)]
    public void PromptsShowOnlyTheActiveDevicesIconsAndNoKeyNames(DeviceKind device)
    {
        var own = OwnIcons(device);
        foreach (string text in AllPromptTexts(device))
        {
            var (icons, words) = Split(text);
            foreach (string icon in icons)
                Assert.True(own.Any(icon.StartsWith), $"{device} shows {icon}");
            // Only icons and words: no "E / X: Talk", "Esc/(B)" or "[img" left as text.
            foreach (string word in words.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                Assert.Contains(word, new[] { "Food", "choose", "buy", "close", "eat" });
        }
    }

    [Theory]
    [InlineData(DeviceKind.KeyboardMouse, "key_e", "key_up key_down", "key_e", "key_esc", "key_f", "key_1 key_2 key_3")]
    [InlineData(DeviceKind.Xbox, "xbox_x", "xbox_dpad_up xbox_dpad_down", "xbox_a", "xbox_b", "xbox_dpad_up", "xbox_dpad_left xbox_dpad_right")]
    [InlineData(DeviceKind.Generic, "xbox_x", "xbox_dpad_up xbox_dpad_down", "xbox_a", "xbox_b", "xbox_dpad_up", "xbox_dpad_left xbox_dpad_right")]
    [InlineData(DeviceKind.PlayStation, "ps_square", "ps_dpad_up ps_dpad_down", "ps_cross", "ps_circle", "ps_dpad_up", "ps_dpad_left ps_dpad_right")]
    [InlineData(DeviceKind.Nintendo, "ns_y", "ns_dpad_up ns_dpad_down", "ns_b", "ns_a", "ns_dpad_up", "ns_dpad_left ns_dpad_right")]
    public void PromptsUseTheDevicesButtons(DeviceKind device, string interact, string choose, string buy, string close, string eat, string pick)
    {
        var prompt = Split(FoodText.Prompt(device));
        Assert.Equal(new[] { interact }, prompt.Icons);
        Assert.Equal(" Food", prompt.Words);
        var menu = Split(FoodText.MenuHelp(device));
        Assert.Equal($"{choose} {buy} {close}".Split(' '), menu.Icons);
        Assert.Equal("choose buy close", string.Join(' ', menu.Words.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
        var bar = Split(FoodText.BarHelp(device));
        Assert.Equal($"{eat} {pick}".Split(' '), bar.Icons);
        Assert.Equal("eat choose", string.Join(' ', bar.Words.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
        var slots = Enumerable.Range(0, FoodKinds.Count).SelectMany(kind => Split(FoodText.SlotKey(kind, device)).Icons);
        Assert.Equal(device == DeviceKind.KeyboardMouse ? pick.Split(' ') : Array.Empty<string>(), slots);
    }

    [Theory]
    [InlineData(28)]
    [InlineData(64)]
    public void PromptIconsTakeTheAskedSize(int size)
    {
        foreach (string text in new[] { FoodText.Prompt(DeviceKind.PlayStation, size), FoodText.MenuHelp(DeviceKind.Xbox, size), FoodText.BarHelp(DeviceKind.KeyboardMouse, size), FoodText.SlotKey(0, DeviceKind.KeyboardMouse, size) })
        {
            Assert.NotEmpty(Split(text).Icons);
            Assert.Equal(Split(text).Icons.Count, System.Text.RegularExpressions.Regex.Matches(text, $@"\[img={size}\]").Count);
        }
    }

    [Fact]
    public void TheShownDeviceFollowsActiveDeviceEveryChange()
    {
        Assert.Equal(DeviceKind.KeyboardMouse, _food.Device);
        foreach (var device in new[] { DeviceKind.Xbox, DeviceKind.PlayStation, DeviceKind.KeyboardMouse, DeviceKind.Nintendo })
        {
            _bus.Publish(new ActiveDevice(device));
            Tick();
            Assert.Equal(device, _food.Device);
        }
        // Also while the menu is open (the Input module keeps publishing it then).
        Player(10);
        _bus.Publish(new OpenFoodMenu());
        Tick();
        Assert.True(_food.MenuOpen);
        _bus.Publish(new ActiveDevice(DeviceKind.PlayStation));
        Tick();
        Assert.Equal(DeviceKind.PlayStation, _food.Device);
    }

    [Fact]
    public void InteractAwayFromTheStallOpensNothing()
    {
        Player(10, at: Places.FoodSpot.Position + new Vector3(3f, 0f, 0f));
        Press(interact: true);
        Assert.False(_food.StallPrompt);
        Assert.False(_food.MenuOpen);
    }

    [Fact]
    public void OnceAnOpenFoodMenuHasArrivedTheStallNoLongerOpensItOnItsOwn()
    {
        Player(10, at: Places.FoodSpot.Position);
        _bus.Publish(new OpenFoodMenu());
        Tick();
        _food.Back();
        Tick();
        Assert.True(_food.CookPresent);
        Assert.False(_food.StallPrompt);
        Press(interact: true);
        Assert.False(_food.MenuOpen);
    }

    [Fact]
    public void TheMenuClosesWhenThePlayerIsDefeated()
    {
        Player(10);
        _bus.Publish(new OpenFoodMenu());
        Tick();
        Player(0, alive: false);
        Tick();
        Assert.False(_food.MenuOpen);
        Assert.False(_bus.Last<UiFocus>().Open);
        // And a defeated player cannot open it.
        _bus.Publish(new OpenFoodMenu());
        Tick();
        Assert.False(_food.MenuOpen);
    }

    [Fact]
    public void NoMenuOrPromptWhileTheStartScreenIsOpen()
    {
        Player(10, at: Places.FoodSpot.Position);
        _bus.Publish(new StartScreenState(true));
        Press(interact: true);
        Assert.False(_food.StallPrompt);
        Assert.False(_food.MenuOpen);
    }

    // Eating

    [Fact]
    public void EatingTakesASecondThenHealsAndTakesOneFromTheBag()
    {
        Player(4);
        Stock(2, 0, 0);
        Press(eat: true);
        Assert.True(_food.Eating);
        Assert.True(Bag.Eating);
        Assert.True(Bag.EatProgress > 0f && Bag.EatProgress < 0.1f);
        var focus = _bus.Last<UiFocus>();
        Assert.Equal(FoodModule.EatFocusOwner, focus.Owner); // you stop to eat
        Assert.True(focus.Open);
        Tick(58); // 59 ticks of the 60 in one second
        Assert.True(Bag.Eating);
        Assert.Empty(_bus.All<HealPlayer>());
        Assert.Equal(2, Bag.Muffins);
        Tick();
        Assert.False(_food.Eating);
        var heal = Assert.Single(_bus.All<HealPlayer>());
        Assert.Equal(2, heal.Amount);
        var eaten = Assert.Single(_bus.All<FoodEaten>());
        Assert.Equal(FoodKinds.Muffin, eaten.Kind);
        Assert.Equal(2, eaten.Healed);
        Assert.Equal(1, Bag.Muffins);
        Assert.False(Bag.Eating);
        Assert.Equal(0f, Bag.EatProgress);
        Assert.False(_bus.Last<UiFocus>().Open);
        Tick(120);
        Assert.Single(_bus.All<FoodEaten>()); // once per meal
    }

    [Theory]
    [InlineData(FoodKinds.Muffin, 2, 2)]
    [InlineData(FoodKinds.Soup, 6, 6)]
    [InlineData(FoodKinds.Pie, 999, 7)]
    public void EachFoodHealsItsAmount(int kind, int amount, int healed)
    {
        Player(3);
        Stock(1, 1, 1);
        Press(select: kind);
        Press(eat: true);
        Tick(60);
        Assert.Equal(amount, Assert.Single(_bus.All<HealPlayer>()).Amount);
        var eaten = Assert.Single(_bus.All<FoodEaten>());
        Assert.Equal(kind, eaten.Kind);
        Assert.Equal(healed, eaten.Healed);
        Assert.Equal(0, _food.Count(kind));
    }

    [Fact]
    public void HealedIsAtMostTheHealthMissing()
    {
        Player(9);
        Stock(0, 1, 0);
        Press(select: FoodKinds.Soup);
        Press(eat: true);
        Tick(60);
        Assert.Equal(6, _bus.Last<HealPlayer>().Amount);
        Assert.Equal(1, _bus.Last<FoodEaten>().Healed);
        Assert.Equal("+½ ♥", FoodText.Healed(1));
        Assert.Equal("+3 ♥", FoodText.Healed(6));
    }

    [Fact]
    public void NoEatingAtFullHealth()
    {
        Player(10);
        Stock(3, 3, 3);
        Press(eat: true);
        Tick(90);
        Assert.False(_food.Eating);
        Assert.Empty(_bus.All<HealPlayer>());
        Assert.Empty(_bus.All<FoodEaten>());
        Assert.Empty(_bus.All<UiFocus>());
        Assert.Equal(3, _food.Count(FoodKinds.Muffin));
    }

    [Fact]
    public void NoEatingWhileDefeated()
    {
        Player(0, alive: false);
        Stock(3, 0, 0);
        Press(eat: true);
        Tick(90);
        Assert.False(_food.Eating);
        Assert.Empty(_bus.All<HealPlayer>());
        Assert.Equal(3, _food.Count(FoodKinds.Muffin));
    }

    [Fact]
    public void NoEatingWithTheSelectedFoodNotCarried()
    {
        Player(2);
        Stock(0, 2, 0); // muffin selected, none carried
        Press(eat: true);
        Tick(90);
        Assert.False(_food.Eating);
        Assert.Empty(_bus.All<HealPlayer>());
        Assert.Equal(2, _food.Count(FoodKinds.Soup));
    }

    [Fact]
    public void NoEatingWithAnEmptyBag()
    {
        Player(2);
        Press(eat: true);
        Tick(90);
        Assert.False(_food.Eating);
        Assert.Empty(_bus.All<HealPlayer>());
        Assert.Empty(_bus.All<FoodEaten>());
    }

    [Fact]
    public void FaintingWhileEatingStopsTheMealAndKeepsTheFood()
    {
        Player(2);
        Stock(1, 0, 0);
        Press(eat: true);
        Tick(20);
        Player(0, alive: false);
        Tick(60);
        Assert.False(_food.Eating);
        Assert.Empty(_bus.All<HealPlayer>());
        Assert.Equal(1, _food.Count(FoodKinds.Muffin));
        Assert.False(_bus.Last<UiFocus>().Open);
    }

    [Fact]
    public void EatPressedWhileEatingDoesNotStartASecondMeal()
    {
        Player(2);
        Stock(3, 0, 0);
        Press(eat: true);
        Tick(10);
        Press(eat: true);
        Tick(60);
        Assert.Single(_bus.All<FoodEaten>());
        Assert.Equal(2, _food.Count(FoodKinds.Muffin));
    }

    [Fact]
    public void NoEatingWhileTheMenuIsOpen()
    {
        Player(2);
        Stock(1, 0, 0);
        _food.OpenMenu();
        Press(eat: true);
        Assert.False(_food.Eating);
    }

    // Selection

    [Fact]
    public void FoodSelectPicksAKindDirectly()
    {
        Press(select: 2);
        Assert.Equal(FoodKinds.Pie, Bag.Selected);
        Press(select: 1);
        Assert.Equal(FoodKinds.Soup, Bag.Selected);
        Press(select: 0);
        Assert.Equal(FoodKinds.Muffin, Bag.Selected);
    }

    [Fact]
    public void FoodCycleSkipsEmptyKindsAndWraps()
    {
        Stock(2, 0, 1); // no soup
        Assert.Equal(FoodKinds.Muffin, _food.Selected);
        Press(cycle: 1);
        Assert.Equal(FoodKinds.Pie, Bag.Selected);
        Press(cycle: 1);
        Assert.Equal(FoodKinds.Muffin, Bag.Selected);
        Press(cycle: -1);
        Assert.Equal(FoodKinds.Pie, Bag.Selected);
        Press(cycle: -1);
        Assert.Equal(FoodKinds.Muffin, Bag.Selected);
    }

    [Fact]
    public void FoodCycleWithNothingElseCarriedKeepsTheSelection()
    {
        Press(select: 1);
        Press(cycle: 1);
        Assert.Equal(FoodKinds.Soup, Bag.Selected);
        Stock(0, 0, 4);
        Press(select: 2);
        Press(cycle: -1);
        Assert.Equal(FoodKinds.Pie, Bag.Selected);
    }

    [Fact]
    public void SelectingThenEatingEatsTheSelectedKind()
    {
        Player(1);
        Stock(1, 1, 0);
        Press(cycle: 1);
        Press(eat: true);
        Tick(60);
        Assert.Equal(FoodKinds.Soup, _bus.Last<FoodEaten>().Kind);
        Assert.Equal(1, _food.Count(FoodKinds.Muffin));
        Assert.Equal(0, _food.Count(FoodKinds.Soup));
    }

    // Saving

    [Fact]
    public void SaveRoundTripRestoresTheBagAndSelection()
    {
        Stock(3, 1, 2);
        Press(select: 2);
        _bus.Publish(new CollectSave(77));
        var part = Assert.Single(_bus.All<SavePart>());
        Assert.Equal(77, part.RequestId);
        Assert.Equal("Food", part.Module);
        Assert.Equal("{\"v\":1,\"muffins\":3,\"soups\":1,\"pies\":2,\"selected\":2}", part.Json);

        var other = new FakeBus();
        var loaded = new FoodModule { Log = _log.Add };
        loaded.Start(other);
        other.Publish(new RestorePart("Food", part.Json));
        Assert.Equal(3, loaded.Count(FoodKinds.Muffin));
        Assert.Equal(1, loaded.Count(FoodKinds.Soup));
        Assert.Equal(2, loaded.Count(FoodKinds.Pie));
        Assert.Equal(FoodKinds.Pie, loaded.Selected);
        Assert.Empty(_log);
    }

    [Fact]
    public void RestoringHasNoSideEffects()
    {
        Player(3);
        _bus.Coins = 100;
        Stock(1, 0, 0);
        _bus.Coins = 100;
        Press(eat: true);
        Tick(10);
        _food.OpenMenu();
        _bus.Published.Clear();
        _bus.Publish(new RestorePart("Food", "{\"v\":1,\"muffins\":0,\"soups\":5,\"pies\":0,\"selected\":1}"));
        Assert.Single(_bus.Published); // only the RestorePart itself
        Assert.Equal(100, _bus.Coins);
        Assert.False(_food.Eating);
        Assert.False(_food.MenuOpen);
        Tick(90);
        Assert.Empty(_bus.All<HealPlayer>());
        Assert.Empty(_bus.All<FoodEaten>());
        Assert.Empty(_bus.All<SpendCoins>());
        Assert.Equal(5, Bag.Soups);
        Assert.Equal(0, Bag.Muffins);
        Assert.Equal(FoodKinds.Soup, Bag.Selected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"v\":2,\"muffins\":0,\"soups\":0,\"pies\":0,\"selected\":0}")]
    [InlineData("{\"v\":1,\"muffins\":10,\"soups\":0,\"pies\":0,\"selected\":0}")]
    [InlineData("{\"v\":1,\"muffins\":-1,\"soups\":0,\"pies\":0,\"selected\":0}")]
    [InlineData("{\"v\":1,\"muffins\":0,\"soups\":0,\"pies\":0,\"selected\":3}")]
    [InlineData("{\"v\":1,\"muffins\":0,\"pies\":0,\"selected\":0}")]
    public void UnreadableSavesKeepTheBagAndLogOnce(string json)
    {
        Stock(2, 0, 0);
        _bus.Publish(new RestorePart("Food", json));
        Assert.Equal(2, _food.Count(FoodKinds.Muffin));
        Assert.Single(_log);
    }

    [Fact]
    public void OtherModulesPartsAreIgnored()
    {
        Stock(2, 0, 0);
        _bus.Publish(new RestorePart("Home", "{\"v\":1,\"muffins\":0,\"soups\":0,\"pies\":0,\"selected\":0}"));
        Assert.Equal(2, _food.Count(FoodKinds.Muffin));
        Assert.Empty(_log);
    }

    // Texts

    [Fact]
    public void MenuLinesShowPriceHealingAndHowManyYouCarry()
    {
        Assert.Equal("Berry Muffin   5 M¢   +1 ♥   0/9", FoodText.MenuLine(FoodKinds.Muffin, 0));
        Assert.Equal("Mushroom Soup   12 M¢   +3 ♥   4/9", FoodText.MenuLine(FoodKinds.Soup, 4));
        Assert.Equal("Spore Pie   35 M¢   all ♥   9/9", FoodText.MenuLine(FoodKinds.Pie, 9));
    }
}
