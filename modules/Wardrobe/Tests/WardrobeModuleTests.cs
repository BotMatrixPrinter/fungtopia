using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Fungtopia.Map;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.Wallet;
using Xunit;

namespace Fungtopia.Wardrobe.Tests;

public class CatalogTests
{
    /// <summary>The repository root, found from the test assembly's folder.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string FileOf(string resPath)
    {
        Assert.StartsWith("res://", resPath);
        return Path.Combine(RepoRoot(), resPath.Substring("res://".Length));
    }

    [Fact]
    public void IdsAreUniqueAndItemsWellFormed()
    {
        var ids = new HashSet<string>();
        foreach (var item in Catalog.Items)
        {
            Assert.Matches("^[a-z][a-z0-9_]*$", item.Id);
            Assert.True(ids.Add(item.Id), $"duplicate id {item.Id}");
            Assert.False(string.IsNullOrWhiteSpace(item.Name));
            Assert.True(Enum.IsDefined(item.Slot), $"{item.Id}: slot {item.Slot}");
            Assert.InRange(item.Fit, Fits.Both, Fits.Boy);
            Assert.Contains(item.Source, new[] { Sources.Starter, Sources.Shop, Sources.Reward });
            if (item.Source == Sources.Shop)
                Assert.InRange(item.Price, 100, 600);
            else
                Assert.Equal(0, item.Price);
            if (item.Dress)
                Assert.Equal(Slot.Top, item.Slot);
            Assert.Equal(item.Id, Catalog.Items[Catalog.IndexOf(item.Id)].Id);
        }
        Assert.Equal(Catalog.Count, ids.Count);
        Assert.Equal(-1, Catalog.IndexOf("no_such_item"));
        Assert.Equal(-1, Catalog.IndexOf(""));
    }

    [Fact]
    public void EveryModelFileAndItsScriptExist()
    {
        foreach (var item in Catalog.Items)
        {
            string glb = FileOf(item.ModelPath);
            Assert.EndsWith(item.Id + ".glb", glb);
            Assert.True(File.Exists(glb), $"missing model {glb}");
            Assert.True(new FileInfo(glb).Length > 1000, $"empty model {glb}");
            Assert.True(File.Exists(Path.ChangeExtension(glb, ".py")), $"missing model script for {item.Id}");
        }
        foreach (var hair in Looks.HairStyles)
        {
            string glb = FileOf(hair.ModelPath);
            Assert.True(File.Exists(glb), $"missing hair model {glb}");
            Assert.True(File.Exists(Path.ChangeExtension(glb, ".py")), $"missing hair script for {hair.Id}");
        }
    }

    [Fact]
    public void TheStarterCatalogHasEveryKindOfPiece()
    {
        int Count(Slot slot, bool dress = false)
        {
            int n = 0;
            foreach (var item in Catalog.Items)
                if (item.Slot == slot && item.Dress == dress && item.Source != Sources.Reward)
                    n++;
            return n;
        }
        Assert.Equal(4, Count(Slot.Top));
        Assert.Equal(3, Count(Slot.Top, dress: true));
        Assert.Equal(4, Count(Slot.Bottom));
        Assert.Equal(3, Count(Slot.Shoes));
        Assert.Equal(4, Count(Slot.Head));
        Assert.Equal(2, Count(Slot.Face));
        Assert.Equal(2, Count(Slot.Back));
        foreach (var item in Catalog.Items)
            if (item.Dress)
                Assert.NotEqual(Fits.Boy, item.Fit);
        Assert.True(Catalog.TryGet(Catalog.SpiderSilkScarf, out var scarf));
        Assert.Equal(Sources.Reward, scarf.Source);
        Assert.True(Catalog.TryGet(Catalog.LadybugBackpack, out var backpack));
        Assert.Equal(Sources.Reward, backpack.Source);
        Assert.Equal(Slot.Back, backpack.Slot);
    }

    [Fact]
    public void StarterOutfitsAreThreeOutfitsOfStarterItemsInTheirSlots()
    {
        Assert.Equal(3, Catalog.StarterOutfits.Length);
        foreach (var outfit in Catalog.StarterOutfits)
        {
            void Check(string id, Slot slot, bool required)
            {
                if (string.IsNullOrEmpty(id))
                {
                    Assert.False(required, $"{outfit.Name} has no {slot}");
                    return;
                }
                Assert.True(Catalog.TryGet(id, out var item), id);
                Assert.Equal(slot, item.Slot);
                Assert.Equal(Sources.Starter, item.Source);
            }
            Check(outfit.Top, Slot.Top, true);
            Check(outfit.Bottom, Slot.Bottom, !Catalog.Items[Catalog.IndexOf(outfit.Top)].Dress);
            Check(outfit.Shoes, Slot.Shoes, true);
            Check(outfit.Head, Slot.Head, false);
        }
        // Both characters have an outfit to start in.
        Assert.True(Fits.Matches(Catalog.StarterOutfits[Catalog.DefaultOutfit(0)].Fit, 0));
        Assert.True(Fits.Matches(Catalog.StarterOutfits[Catalog.DefaultOutfit(1)].Fit, 1));
    }

    [Fact]
    public void LooksHaveTheDesignedChoices()
    {
        Assert.Equal(6, Looks.SkinTones.Length);
        Assert.Equal(6, Looks.HairStyles.Length);
        Assert.Equal(8, Looks.HairColors.Length);
        Assert.Equal(6, Looks.EyeColors.Length);
        Assert.Equal(Looks.HairColors.Length, Looks.HairColorNames.Length);
        Assert.Equal(Looks.EyeColors.Length, Looks.EyeColorNames.Length);
        Assert.Contains("Pink", Looks.HairColorNames.ToArray());
        Assert.Contains("Mint", Looks.HairColorNames.ToArray());
        var styles = new List<string>();
        foreach (var style in Looks.HairStyles)
            styles.Add(style.Id);
        Assert.Equal(new[] { "short", "bob", "pigtails", "ponytail", "top_bun", "curly" }, styles);
        // Light to deep.
        for (int i = 1; i < Looks.SkinTones.Length; i++)
        {
            var a = Looks.SkinTones[i - 1];
            var b = Looks.SkinTones[i];
            Assert.True(a.X + a.Y + a.Z > b.X + b.Y + b.Z, $"skin tone {i} is not deeper");
        }
        foreach (var span in new[] { Looks.SkinTones.ToArray(), Looks.HairColors.ToArray(), Looks.EyeColors.ToArray() })
            foreach (var c in span)
                Assert.True(c.X is >= 0 and <= 1 && c.Y is >= 0 and <= 1 && c.Z is >= 0 and <= 1);
    }
}

public class WardrobeModuleTests
{
    /// <summary>
    /// Fake of the shell's bus: delivers messages like the real one and records what the
    /// wardrobe publishes. With <see cref="Coins"/> set it also plays the wallet, answering every
    /// SpendCoins with a SpendResult right away.
    /// </summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<AppearanceState> Appearances = new();
        public readonly List<WardrobeState> States = new();
        public readonly List<SpendCoins> Spends = new();
        public readonly List<ItemReceived> Received = new();
        public int? Coins;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is AppearanceState appearance)
                Appearances.Add(appearance);
            if (message is WardrobeState state)
                States.Add(state);
            if (message is ItemReceived received)
                Received.Add(received);
            if (message is SpendCoins spend)
            {
                Spends.Add(spend);
                if (Coins is int coins)
                {
                    bool ok = spend.Amount >= 0 && spend.Amount <= coins;
                    if (ok)
                        Coins = coins - spend.Amount;
                    Publish(new SpendResult(spend.RequestId, ok, Coins.Value));
                }
            }
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(message);
        }
    }

    private static readonly TickContext Step = new(0, 1.0 / 60);

    private static WardrobeModule Started(FakeBus bus, CharacterKind? character = null)
    {
        var wardrobe = new WardrobeModule();
        wardrobe.Start(bus);
        if (character is CharacterKind kind)
            bus.Publish(Player(kind));
        return wardrobe;
    }

    private static PlayerState Player(CharacterKind kind) =>
        new(Vector3.Zero, 0f, Vector3.Zero, true, 10, 10, true, kind, Area.Home);

    private static MonsterKilled Kill(int id, MonsterKind kind) =>
        new(id, kind, MonsterRewards.For(kind), Vector3.Zero, 0);

    [Fact]
    public void StartersAreOwnedAndNothingElse()
    {
        var wardrobe = Started(new FakeBus());
        for (int i = 0; i < Catalog.Count; i++)
            Assert.Equal(Catalog.Items[i].Source == Sources.Starter, wardrobe.Owns(i));
        // Every starter outfit can be put on.
        foreach (var outfit in Catalog.StarterOutfits)
            foreach (var id in new[] { outfit.Top, outfit.Bottom, outfit.Shoes, outfit.Head })
                if (id != "")
                    Assert.True(wardrobe.Owns(id), id);
    }

    [Fact]
    public void PublishesAppearanceAndWardrobeStateEveryTick()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        for (int i = 0; i < 3; i++)
            wardrobe.Tick(Step);
        Assert.Equal(3, bus.Appearances.Count);
        Assert.Equal(3, bus.States.Count);
        Assert.All(bus.States, s => Assert.False(s.Open));
        // The boy starts in the first starter outfit.
        var look = bus.Appearances[^1];
        Assert.Equal("striped_tee", look.Top);
        Assert.Equal("shorts", look.Bottom);
        Assert.Equal("sneakers", look.Shoes);
        Assert.Equal("", look.Face);
        Assert.Equal("", look.Back);
    }

    [Fact]
    public void TheGirlStartsInTheMushroomDressWithNoBottomShown()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        wardrobe.Tick(Step);
        var look = bus.Appearances[^1];
        Assert.Equal("spotted_dress", look.Top);
        Assert.Equal("", look.Bottom);
        Assert.Equal("mary_janes", look.Shoes);
        Assert.Equal("big_bow", look.Head);
    }

    [Fact]
    public void EquipAndUnequip()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        bus.Publish(new Equip("rain_boots"));
        bus.Publish(new Equip("beanie"));
        wardrobe.Tick(Step);
        Assert.Equal("rain_boots", bus.Appearances[^1].Shoes);
        Assert.Equal("beanie", bus.Appearances[^1].Head);

        bus.Publish(Equip.Unequip(Slot.Head));
        bus.Publish(new Equip("", Slot.Shoes));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Head);
        Assert.Equal("", bus.Appearances[^1].Shoes);
        Assert.Equal("striped_tee", bus.Appearances[^1].Top);
        Assert.Equal(-1, wardrobe.Worn(Slot.Head));
    }

    [Fact]
    public void ItemsNotOwnedOrNotFittingOrUnknownCannotBePutOn()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        bus.Publish(new Equip("mushroom_hat"));      // shop item, not owned
        bus.Publish(new Equip("spotted_dress"));     // owned starter, but a girl's dress
        bus.Publish(new Equip("no_such_item"));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Head);
        Assert.Equal("striped_tee", bus.Appearances[^1].Top);
    }

    [Fact]
    public void ADressHidesTheBottomAndTakingItOffShowsItAgain()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new Equip("striped_tee"));
        bus.Publish(new Equip("overalls"));
        wardrobe.Tick(Step);
        Assert.Equal("overalls", bus.Appearances[^1].Bottom);

        bus.Publish(new Equip("spotted_dress"));
        wardrobe.Tick(Step);
        Assert.Equal("spotted_dress", bus.Appearances[^1].Top);
        Assert.Equal("", bus.Appearances[^1].Bottom);
        Assert.True(wardrobe.BottomHidden);

        // A bottom put on under the dress stays hidden until the dress comes off.
        bus.Publish(new Equip("shorts"));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Bottom);

        bus.Publish(new Equip("overalls_top"));
        wardrobe.Tick(Step);
        Assert.Equal("overalls_top", bus.Appearances[^1].Top);
        Assert.Equal("shorts", bus.Appearances[^1].Bottom);
        Assert.False(wardrobe.BottomHidden);
    }

    [Fact]
    public void SetLookChangesTheLookOnlyWhenInRange()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new SetLook(5, 4, 6, 2));
        wardrobe.Tick(Step);
        var look = bus.Appearances[^1];
        Assert.Equal((5, 4, 6, 2), (look.Skin, look.HairStyle, look.HairColor, look.EyeColor));

        foreach (var bad in new[]
                 {
                     new SetLook(6, 0, 0, 0), new SetLook(0, 6, 0, 0), new SetLook(0, 0, 8, 0),
                     new SetLook(0, 0, 0, 6), new SetLook(-1, 0, 0, 0),
                 })
            bus.Publish(bad);
        wardrobe.Tick(Step);
        look = bus.Appearances[^1];
        Assert.Equal((5, 4, 6, 2), (look.Skin, look.HairStyle, look.HairColor, look.EyeColor));
        Assert.True(Looks.Valid(look.Skin, look.HairStyle, look.HairColor, look.EyeColor));
    }

    [Fact]
    public void OpenWardrobeAndShopAndClose()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        bus.Publish(new OpenWardrobe());
        wardrobe.Tick(Step);
        Assert.Equal(new WardrobeState(true, false), bus.States[^1]);
        bus.Publish(new OpenShop());
        wardrobe.Tick(Step);
        Assert.Equal(new WardrobeState(true, true), bus.States[^1]);
        wardrobe.Close();
        wardrobe.Tick(Step);
        Assert.Equal(new WardrobeState(false, false), bus.States[^1]);
    }

    [Fact]
    public void BuyingWithEnoughCoinsAddsTheItem()
    {
        var bus = new FakeBus { Coins = 1000 };
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new WalletState(1000));
        bus.Publish(new OpenShop());
        wardrobe.Buy("mushroom_hat");
        Assert.Empty(bus.Spends);                 // sent on the next tick
        wardrobe.Tick(Step);

        var spend = Assert.Single(bus.Spends);
        Assert.Equal(450, spend.Amount);
        Assert.Equal("Red Mushroom-Cap Hat", spend.What);
        Assert.True(wardrobe.Owns("mushroom_hat"));
        Assert.Equal(BuyResult.Bought, wardrobe.LastBuy);
        Assert.Equal(550, wardrobe.Coins);
        var received = Assert.Single(bus.Received);
        Assert.Equal("mushroom_hat", received.ItemId);
        Assert.Equal(WardrobeModule.ShopReason, received.Why);

        // Bought items can be worn, and are not sold twice.
        bus.Publish(new Equip("mushroom_hat"));
        wardrobe.Tick(Step);
        Assert.Equal("mushroom_hat", bus.Appearances[^1].Head);
        Assert.False(wardrobe.CanBuy(Catalog.IndexOf("mushroom_hat")));
        wardrobe.Buy("mushroom_hat");
        wardrobe.Tick(Step);
        Assert.Single(bus.Spends);
    }

    [Fact]
    public void BuyingWithTooFewCoinsAddsNothing()
    {
        var bus = new FakeBus { Coins = 100 };
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new OpenShop());
        wardrobe.Buy("party_dress");
        wardrobe.Tick(Step);
        Assert.Single(bus.Spends);
        Assert.False(wardrobe.Owns("party_dress"));
        Assert.Equal(BuyResult.NotEnough, wardrobe.LastBuy);
        Assert.Empty(bus.Received);
        Assert.Equal(100, bus.Coins);
    }

    [Fact]
    public void OnlyTheOpenShopSellsAndOnlyShopItemsThatFit()
    {
        var bus = new FakeBus { Coins = 5000 };
        var wardrobe = Started(bus, CharacterKind.Boy);
        wardrobe.Buy("jeans");                     // shop not open
        bus.Publish(new OpenWardrobe());
        wardrobe.Buy("jeans");                     // the wardrobe is not the shop
        bus.Publish(new OpenShop());
        wardrobe.Buy(Catalog.SpiderSilkScarf);     // rewards are not for sale
        wardrobe.Buy("flower_dress");              // does not fit the boy
        wardrobe.Buy("sneakers");                  // already owned
        wardrobe.Tick(Step);
        Assert.Empty(bus.Spends);
        Assert.False(wardrobe.Owns("jeans"));
    }

    [Fact]
    public void ASpendResultForSomeoneElseIsIgnored()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new SpendResult(7, true, 0));
        bus.Publish(new SpendResult(WardrobeModule.RequestIdBase + 1, true, 0));
        wardrobe.Tick(Step);
        Assert.Empty(bus.Received);
    }

    [Fact]
    public void FirstTarantulaAndFirstLadybugGiveTheirRewardOnce()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        bus.Publish(Kill(1, MonsterKind.Caterpillar));
        bus.Publish(Kill(2, MonsterKind.Ant));
        Assert.Empty(bus.Received);

        bus.Publish(Kill(3, MonsterKind.Tarantula));
        bus.Publish(Kill(4, MonsterKind.Tarantula));
        Assert.True(wardrobe.Owns(Catalog.SpiderSilkScarf));
        Assert.False(wardrobe.Owns(Catalog.LadybugBackpack));
        var scarf = Assert.Single(bus.Received);
        Assert.Equal(Catalog.SpiderSilkScarf, scarf.ItemId);
        Assert.Equal(WardrobeModule.TarantulaReason, scarf.Why);

        bus.Publish(Kill(5, MonsterKind.Ladybug));
        bus.Publish(Kill(6, MonsterKind.Ladybug));
        Assert.Equal(2, bus.Received.Count);
        Assert.Equal(Catalog.LadybugBackpack, bus.Received[1].ItemId);
        Assert.True(wardrobe.TarantulaRewarded && wardrobe.LadybugRewarded);

        bus.Publish(new Equip(Catalog.LadybugBackpack));
        wardrobe.Tick(Step);
        Assert.Equal(Catalog.LadybugBackpack, bus.Appearances[^1].Back);
    }

    [Fact]
    public void GrantItemGivesOnceAndIgnoresUnknownItems()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new GrantItem("round_glasses", "Millie's quest"));
        bus.Publish(new GrantItem("round_glasses", "Millie's quest"));
        bus.Publish(new GrantItem("sneakers", "already owned"));
        bus.Publish(new GrantItem("no_such_item", "?"));
        var received = Assert.Single(bus.Received);
        Assert.Equal("round_glasses", received.ItemId);
        Assert.Equal("Millie's quest", received.Why);
        Assert.True(wardrobe.Owns("round_glasses"));
    }

    [Fact]
    public void SwitchingCharacterTakesOffWhatDoesNotFit()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new Equip("shorts"));
        bus.Publish(new Equip("spotted_dress"));
        bus.Publish(Player(CharacterKind.Boy));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Top);
        Assert.Equal("shorts", bus.Appearances[^1].Bottom);
    }

    [Fact]
    public void WearOutfitPutsOnAStarterOutfit()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        wardrobe.WearOutfit(2);
        wardrobe.Tick(Step);
        var look = bus.Appearances[^1];
        Assert.Equal(("overalls_top", "overalls", "rain_boots", "beanie"), (look.Top, look.Bottom, look.Shoes, look.Head));
        wardrobe.WearOutfit(1); // the dress outfit does not fit the boy
        wardrobe.Tick(Step);
        Assert.Equal("overalls_top", bus.Appearances[^1].Top);
    }

    [Fact]
    public void TicksDoNotAllocate()
    {
        var bus = new CountingBus();
        var wardrobe = new WardrobeModule();
        wardrobe.Start(bus);
        for (int i = 0; i < 100; i++)
            wardrobe.Tick(Step);
        var equip = new Equip("beanie");
        var kill = Kill(1, MonsterKind.Caterpillar);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            wardrobe.Handle(equip);
            wardrobe.Handle(kill);
            wardrobe.Tick(Step);
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(2200, bus.Published);
    }

    /// <summary>A bus that only counts what is published (no allocation).</summary>
    private sealed class CountingBus : IMessageBus
    {
        public int Published;
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct => Published++;
    }
}
