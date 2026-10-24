using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Pet;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Fungtopia.Wallet;
using Fungtopia.Wardrobe;
using Xunit;

namespace Fungtopia.Home.Tests;

public class HomeModuleTests
{
    /// <summary>
    /// Fake of the shell's bus: delivers messages like the real one and records what the Home
    /// module publishes. With <see cref="Coins"/> set it also answers SpendCoins like the wallet.
    /// </summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<TeleportPlayer> Teleports = new();
        public readonly List<SpendCoins> Spends = new();
        public readonly List<HomeState> States = new();
        public int OpenWardrobes;
        public int? Coins;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is TeleportPlayer teleport)
                Teleports.Add(teleport);
            if (message is HomeState state)
                States.Add(state);
            if (message is OpenWardrobe)
                OpenWardrobes++;
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(message);
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
        }
    }

    private static readonly TickContext Step = new(0, 1.0 / 60);

    private static (HomeModule home, FakeBus bus) Started(int? coins = null)
    {
        var bus = new FakeBus { Coins = coins };
        var home = new HomeModule();
        home.Start(bus);
        return (home, bus);
    }

    private static void StandAt(FakeBus bus, Vector3 position, bool alive = true) =>
        bus.Publish(new PlayerState(position, 0f, Vector3.Zero, true, alive ? 10 : 0, 10, alive, CharacterKind.Girl));

    private static void PressInteract(FakeBus bus) =>
        bus.Publish(new PlayerInput(Vector2.Zero, Vector2.Zero, false, false, false, Vector3.Zero, true, false));

    /// <summary>Stands the player at a spot, ticks once to see the prompt, presses Interact and ticks again.</summary>
    private static void Interact(HomeModule home, FakeBus bus, Vector3 at)
    {
        StandAt(bus, at);
        home.Tick(Step);
        PressInteract(bus);
        home.Tick(Step);
    }

    private static void Inside(HomeModule home, FakeBus bus)
    {
        StandAt(bus, Places.HomeInterior.Position);
        home.Tick(Step);
    }

    // The door

    [Fact]
    public void AtTheHomeDoorThePromptIsEnterAndInteractTeleportsInside()
    {
        var (home, bus) = Started();
        StandAt(bus, Places.HomeDoor.Position);
        home.Tick(Step);
        Assert.Equal(HomePrompt.Enter, home.Prompt);
        Assert.Equal("E / X: Enter", HomeText.Prompt(HomePrompt.Enter));

        PressInteract(bus);
        home.Tick(Step);
        var teleport = Assert.Single(bus.Teleports);
        Assert.Equal(Places.HomeInterior.Position, teleport.Position);
        Assert.Equal(Places.HomeInterior.Yaw, teleport.Facing);
        Assert.Equal(Area.Home, Areas.At(teleport.Position.X, teleport.Position.Z));
    }

    [Fact]
    public void InsideAtTheDoorThePromptIsLeaveAndInteractTeleportsOutsideTheHouse()
    {
        var (home, bus) = Started();
        StandAt(bus, Places.HomeExit.Position);
        home.Tick(Step);
        Assert.Equal(HomePrompt.Leave, home.Prompt);
        Assert.Equal("E / X: Leave", HomeText.Prompt(HomePrompt.Leave));

        PressInteract(bus);
        home.Tick(Step);
        var teleport = Assert.Single(bus.Teleports);
        Assert.Equal(Places.HomeDoor.Position, teleport.Position);
        Assert.Equal(Places.HomeDoor.Yaw, teleport.Facing);
    }

    [Fact]
    public void EnteringThenLeavingTeleportsOnceEachWay()
    {
        var (home, bus) = Started();
        Interact(home, bus, Places.HomeDoor.Position);
        // A second press before the player's next state does not teleport again.
        PressInteract(bus);
        home.Tick(Step);
        Assert.Single(bus.Teleports);

        Interact(home, bus, Places.HomeExit.Position);
        Assert.Equal(2, bus.Teleports.Count);
        Assert.Equal(Places.HomeDoor.Position, bus.Teleports[1].Position);
    }

    [Fact]
    public void AwayFromTheDoorOrWhileDefeatedInteractDoesNothing()
    {
        var (home, bus) = Started();
        Interact(home, bus, Places.HomeDoor.Position + new Vector3(4f, 0f, 0f));
        Assert.Equal(HomePrompt.None, home.Prompt);

        StandAt(bus, Places.HomeDoor.Position, alive: false);
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        PressInteract(bus);
        home.Tick(Step);
        Assert.Empty(bus.Teleports);
    }

    // The furniture shop

    [Fact]
    public void TheCatalogHasTheCuteFurnitureWithItsPrices()
    {
        Assert.Equal(10, Furniture.ForSale.Length);
        Assert.Equal(300, Furniture.Price(FurnitureKind.MushroomBed));
        Assert.Equal(120, Furniture.Price(FurnitureKind.Rug));
        Assert.Equal(150, Furniture.Price(FurnitureKind.Lamp));
        Assert.Equal(200, Furniture.Price(FurnitureKind.Table));
        Assert.Equal(100, Furniture.Price(FurnitureKind.Chair));
        Assert.Equal(80, Furniture.Price(FurnitureKind.PottedMushroom));
        Assert.Equal(250, Furniture.Price(FurnitureKind.Painting));
        Assert.Equal(180, Furniture.Price(FurnitureKind.PetBed));
        Assert.Equal(220, Furniture.Price(FurnitureKind.Bookshelf));
        Assert.Equal(90, Furniture.Price(FurnitureKind.ToadstoolStool));
        Assert.Equal("Toadstool Stool", Furniture.Name(FurnitureKind.ToadstoolStool));
        Assert.Equal("Lamp  150 M¢", HomeText.CatalogLine(FurnitureKind.Lamp));
    }

    [Fact]
    public void AtTheFurnitureShopInteractOpensTheCatalog()
    {
        var (home, bus) = Started(coins: 1000);
        StandAt(bus, Places.FurnitureShopSpot.Position);
        home.Tick(Step);
        Assert.Equal(HomePrompt.Shop, home.Prompt);
        Assert.Equal("E / X: Shop", HomeText.Prompt(HomePrompt.Shop));
        PressInteract(bus);
        home.Tick(Step);
        Assert.True(home.ShopOpen);
        Assert.Equal(HomePrompt.None, home.Prompt);

        // Walking away closes it.
        StandAt(bus, Places.FurnitureShopSpot.Position + new Vector3(0f, 0f, 5f));
        home.Tick(Step);
        Assert.False(home.ShopOpen);
    }

    [Fact]
    public void BuyingWithEnoughCoinsSpendsThePriceAndPutsTheItemInTheInventory()
    {
        var (home, bus) = Started(coins: 400);
        Interact(home, bus, Places.FurnitureShopSpot.Position);
        while (Furniture.ForSale[home.ShopSelection] != FurnitureKind.Lamp)
            home.MoveShopSelection(1);
        // Interact in the open catalog buys the highlighted item.
        PressInteract(bus);
        home.Tick(Step);

        var spend = Assert.Single(bus.Spends);
        Assert.Equal(150, spend.Amount);
        Assert.Equal("Lamp", spend.What);
        Assert.Equal(250, bus.Coins);
        Assert.Equal(ShopResult.Bought, home.LastShopResult);
        Assert.Equal(2, home.ItemCount);
        Assert.Equal(FurnitureKind.Lamp, home.Items[1].Kind);
        Assert.False(home.Items[1].Placed);
        Assert.Equal(new HomeState(2, 1, false), bus.States[^1]);
    }

    [Fact]
    public void BuyingWithTooFewCoinsAddsNothing()
    {
        var (home, bus) = Started(coins: 299);
        home.OpenShop();
        home.Buy(FurnitureKind.MushroomBed);
        Assert.Single(bus.Spends);
        Assert.Equal(299, bus.Coins);
        Assert.Equal(ShopResult.TooFewCoins, home.LastShopResult);
        Assert.Equal(1, home.ItemCount);

        // Exactly enough is fine.
        bus.Coins = 300;
        home.Buy(FurnitureKind.MushroomBed);
        Assert.Equal(ShopResult.Bought, home.LastShopResult);
        Assert.Equal(0, bus.Coins);
        Assert.Equal(2, home.ItemCount);
    }

    [Fact]
    public void AnAnswerToSomeoneElsesRequestIsIgnored()
    {
        var (home, bus) = Started();
        home.Buy(FurnitureKind.Chair);
        bus.Publish(new SpendResult(12345, true, 0));
        Assert.Equal(1, home.ItemCount);
        bus.Publish(new SpendResult(bus.Spends[0].RequestId, true, 0));
        Assert.Equal(2, home.ItemCount);
    }

    // The house and placing furniture

    [Fact]
    public void TheHouseStartsWithABasicBedPlaced()
    {
        var (home, bus) = Started();
        home.Tick(Step);
        var bed = Assert.Single(home.Items.ToArray());
        Assert.Equal(FurnitureKind.BasicBed, bed.Kind);
        Assert.True(bed.Placed);
        Assert.Equal(new HomeState(1, 1, false), bus.States[^1]);
    }

    [Fact]
    public void TheCatalogBookInsideOpensPlacementMode()
    {
        var (home, bus) = Started(coins: 1000);
        StandAt(bus, HomeRoom.BookPosition + new Vector3(0f, 0f, -0.8f));
        home.Tick(Step);
        Assert.Equal(HomePrompt.Decorate, home.Prompt);
        Assert.Equal("E / X: Decorate", HomeText.Prompt(HomePrompt.Decorate));
        PressInteract(bus);
        home.Tick(Step);
        Assert.True(home.Decorating);
        Assert.True(bus.States[^1].Decorating);

        // Leaving the house ends it.
        StandAt(bus, Places.HomeDoor.Position);
        home.Tick(Step);
        Assert.False(home.Decorating);
    }

    [Fact]
    public void PlacingPutsTheItemOnTheHalfMeterGridAndPicksItBackUp()
    {
        var (home, bus) = Started(coins: 1000);
        home.Buy(FurnitureKind.Table);
        Inside(home, bus);
        home.StartDecorating();
        Assert.Equal(1, home.Held);

        home.MoveCursor(-100, -100);
        home.MoveCursor(6, 1);
        Assert.Equal((6, 1), (home.CursorColumn, home.CursorRow));
        Assert.True(home.PlaceOrPickUp());
        var table = home.Items[1];
        Assert.True(table.Placed);
        Assert.Equal(-1, home.Held);
        // A 1 m table on cells 6..7 x 1..2: centered on the 0.5 m grid.
        Assert.Equal(Areas.Home.MinX + 3.5f, table.Center.X, 4);
        Assert.Equal(Areas.Home.MinZ + 1f, table.Center.Z, 4);
        Assert.Equal(Areas.HomeFloorY, table.Center.Y);
        home.Tick(Step);
        Assert.Equal(new HomeState(2, 2, true), bus.States[^1]);

        // Nothing held: the cursor on the table picks it back up.
        home.MoveCursor(1, 1);
        Assert.True(home.PlaceOrPickUp());
        Assert.Equal(1, home.Held);
        Assert.False(home.Items[1].Placed);
        Assert.Equal(1, home.ItemsPlaced);
    }

    [Fact]
    public void RotatingTurnsTheFootprintInQuarterTurns()
    {
        var (home, bus) = Started(coins: 1000);
        home.Buy(FurnitureKind.Bookshelf);
        Inside(home, bus);
        home.StartDecorating();
        home.MoveCursor(-100, -100);
        home.MoveCursor(0, 2);
        home.Rotate();
        Assert.Equal(1, home.CursorRotation);
        Assert.True(home.PlaceOrPickUp());
        var shelf = home.Items[1];
        Assert.Equal((1, 3), (shelf.Width, shelf.Depth));
        Assert.Equal(MathF.PI / 2f, shelf.Yaw, 5);
        for (int i = 0; i < 3; i++)
            Assert.True(shelf.Covers(0, 2 + i));
        Assert.False(shelf.Covers(1, 2));
    }

    [Fact]
    public void FurnitureCannotOverlap()
    {
        var (home, bus) = Started(coins: 1000);
        home.Buy(FurnitureKind.Rug);
        home.Buy(FurnitureKind.Chair);
        Inside(home, bus);
        home.StartDecorating();
        home.MoveCursor(-100, -100);
        home.MoveCursor(8, 2);
        Assert.True(home.PlaceOrPickUp()); // the rug on cells 8..11 x 2..4

        home.NextItem(1);
        Assert.Equal(FurnitureKind.Chair, home.Items[home.Held].Kind);
        home.MoveCursor(-100, -100);
        home.MoveCursor(10, 3);
        Assert.False(home.CanPlaceHeld);
        Assert.False(home.PlaceOrPickUp());
        Assert.False(home.Items[2].Placed);
        // Over the starting bed neither.
        Assert.False(home.CanPlace(FurnitureKind.Chair, 2, 1, 0));

        home.MoveCursor(2, 0); // cell 12: just past the rug
        Assert.True(home.PlaceOrPickUp());
        Assert.Equal(3, home.ItemsPlaced);
    }

    [Fact]
    public void FurnitureCannotBlockTheDoorOrLeaveTheRoom()
    {
        var (home, _) = Started();
        int doorColumn = HomeRoom.ColumnAt(Places.HomeExit.Position.X);
        int doorRow = HomeRoom.RowAt(Places.HomeExit.Position.Z);
        int enterRow = HomeRoom.RowAt(Places.HomeInterior.Position.Z);
        Assert.False(home.CanPlace(FurnitureKind.Chair, doorColumn, doorRow, 0));
        Assert.False(home.CanPlace(FurnitureKind.Chair, doorColumn, HomeRoom.Rows - 1, 0));
        Assert.False(home.CanPlace(FurnitureKind.Chair, doorColumn, enterRow, 0));
        Assert.False(home.CanPlace(FurnitureKind.Chair, doorColumn - 1, enterRow, 0));
        // A rug reaching into the doorway from the side is refused too.
        Assert.False(home.CanPlace(FurnitureKind.Rug, doorColumn - 4, HomeRoom.Rows - 3, 0));
        Assert.True(home.CanPlace(FurnitureKind.Rug, doorColumn - 6, HomeRoom.Rows - 3, 0));
        // Not on the catalog book's stand, not past the walls.
        Assert.False(home.CanPlace(FurnitureKind.Chair, HomeRoom.BookColumn, HomeRoom.BookRow, 0));
        Assert.False(home.CanPlace(FurnitureKind.Chair, -1, 5, 0));
        Assert.False(home.CanPlace(FurnitureKind.Bookshelf, HomeRoom.Columns - 2, 5, 0));
        Assert.True(home.CanPlace(FurnitureKind.Bookshelf, HomeRoom.Columns - 2, 5, 1));
        // The cursor stays inside the room.
        home.StartDecorating();
        home.MoveCursor(1000, 1000);
        Assert.Equal((HomeRoom.Columns - 1, HomeRoom.Rows - 1), (home.CursorColumn, home.CursorRow));
    }

    [Fact]
    public void InteractWhileDecoratingPlacesTheHeldItem()
    {
        var (home, bus) = Started(coins: 1000);
        home.Buy(FurnitureKind.PottedMushroom);
        StandAt(bus, HomeRoom.BookPosition + new Vector3(0f, 0f, -0.8f));
        home.Tick(Step);
        PressInteract(bus);
        home.Tick(Step);
        Assert.True(home.Decorating);
        home.MoveCursor(-100, -100);
        PressInteract(bus);
        home.Tick(Step);
        Assert.True(home.Items[1].Placed);
        Assert.Equal((0, 0), (home.Items[1].Column, home.Items[1].Row));
        home.Back();
        Assert.False(home.Decorating);
    }

    // The wardrobe

    private static void WardrobeScreen(FakeBus bus, bool open) => bus.Publish(new WardrobeState(open, false));

    [Fact]
    public void TheWardrobeStandsInTheHouseFromTheStartAndIsNotFurniture()
    {
        var (home, bus) = Started();
        home.Tick(Step);
        // Not one of the owned pieces: the house still holds just the basic bed.
        Assert.Equal(1, home.ItemCount);
        Assert.Equal(new HomeState(1, 1, false), bus.States[^1]);
        // Against the north wall, inside the room, clear of the bed, the door's way in and the book.
        var at = HomeRoom.WardrobePosition;
        Assert.True(Areas.Home.Contains(at.X, at.Z));
        Assert.Equal(0, HomeRoom.WardrobeRow);
        for (int c = HomeRoom.WardrobeColumn; c < HomeRoom.WardrobeColumn + HomeRoom.WardrobeWidth; c++)
            for (int r = HomeRoom.WardrobeRow; r < HomeRoom.WardrobeRow + HomeRoom.WardrobeDepth; r++)
            {
                Assert.True(HomeRoom.Inside(c, r));
                Assert.True(HomeRoom.UnderWardrobe(c, r));
                Assert.True(HomeRoom.KeptClear(c, r));
                Assert.Equal(-1, home.ItemAt(c, r));
                Assert.False(home.CanPlace(FurnitureKind.Chair, c, r, 0));
            }
        Assert.False(HomeRoom.UnderWardrobe(HomeRoom.BookColumn, HomeRoom.BookRow));
        Assert.False(HomeRoom.UnderWardrobe(HomeRoom.ColumnAt(Places.HomeInterior.Position.X), HomeRoom.RowAt(Places.HomeInterior.Position.Z)));
        Assert.False(HomeRoom.UnderWardrobe(2, 0)); // the bed's corner
        // Nothing reaches over it, not even partly (a rug from the side, a bookshelf turned along it).
        Assert.False(home.CanPlace(FurnitureKind.Rug, HomeRoom.WardrobeColumn - 2, 0, 0));
        Assert.False(home.CanPlace(FurnitureKind.Bookshelf, HomeRoom.WardrobeColumn + 2, 1, 1));
        Assert.True(home.CanPlace(FurnitureKind.Chair, HomeRoom.WardrobeColumn, HomeRoom.WardrobeRow + HomeRoom.WardrobeDepth, 0));
        Assert.True(home.CanPlace(FurnitureKind.Chair, HomeRoom.WardrobeColumn - 1, 0, 0));
    }

    [Fact]
    public void TheWardrobeCannotBePickedUpOrCoveredWhileDecorating()
    {
        var (home, bus) = Started(coins: 1000);
        home.Buy(FurnitureKind.Chair);
        Inside(home, bus);
        home.StartDecorating();
        Assert.Equal(1, home.Held); // the chair
        home.MoveCursor(HomeRoom.WardrobeColumn + 1 - home.CursorColumn, HomeRoom.WardrobeRow - home.CursorRow);
        Assert.False(home.CanPlaceHeld);
        Assert.False(home.PlaceOrPickUp());
        Assert.False(home.Items[1].Placed);
        // With nothing held, the cursor over the wardrobe picks nothing up.
        home.MoveCursor(0, HomeRoom.WardrobeDepth);
        Assert.True(home.PlaceOrPickUp());
        Assert.Equal(-1, home.Held);
        home.MoveCursor(0, -HomeRoom.WardrobeDepth);
        Assert.False(home.PlaceOrPickUp());
        Assert.Equal(-1, home.Held);
        Assert.Equal(2, home.ItemsPlaced);
    }

    [Fact]
    public void NearTheWardrobeThePromptIsWardrobeAndInteractOpensIt()
    {
        var (home, bus) = Started();
        var spot = HomeRoom.WardrobeFront + new Vector3(0f, 0f, 0.6f);
        StandAt(bus, spot);
        home.Tick(Step);
        Assert.Equal(HomePrompt.Wardrobe, home.Prompt);
        Assert.Equal("E / X: Wardrobe", HomeText.Prompt(HomePrompt.Wardrobe));
        PressInteract(bus);
        home.Tick(Step);
        Assert.Equal(1, bus.OpenWardrobes);
        Assert.Empty(bus.Teleports);
        Assert.False(home.Decorating);

        // Too far away, or fainted: no wardrobe.
        StandAt(bus, HomeRoom.WardrobeFront + new Vector3(0f, 0f, HomeModule.IndoorReach + 0.3f));
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        StandAt(bus, spot, alive: false);
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        PressInteract(bus);
        home.Tick(Step);
        Assert.Equal(1, bus.OpenWardrobes);
    }

    [Fact]
    public void WhileTheWardrobeIsOpenDecoratingAndLeavingAreDisabled()
    {
        var (home, bus) = Started(coins: 1000);
        WardrobeScreen(bus, open: true);
        Assert.True(home.WardrobeOpen);

        // By the door inside: no Leave, and Interact does not teleport.
        StandAt(bus, Places.HomeExit.Position);
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        PressInteract(bus);
        home.Tick(Step);
        Assert.Empty(bus.Teleports);

        // By the catalog book: no Decorate, and placement mode does not open.
        StandAt(bus, HomeRoom.BookPosition + new Vector3(0f, 0f, -0.8f));
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        PressInteract(bus);
        home.Tick(Step);
        home.StartDecorating();
        home.Tick(Step);
        Assert.False(home.Decorating);
        Assert.False(bus.States[^1].Decorating);

        // By the wardrobe itself: no second open.
        StandAt(bus, HomeRoom.WardrobeFront + new Vector3(0f, 0f, 0.5f));
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        PressInteract(bus);
        home.Tick(Step);
        Assert.Equal(0, bus.OpenWardrobes);

        // Closed again: a press made while it was open is not acted on later, and all works again.
        WardrobeScreen(bus, open: false);
        StandAt(bus, Places.HomeExit.Position);
        home.Tick(Step);
        Assert.Equal(HomePrompt.Leave, home.Prompt);
        Assert.Empty(bus.Teleports);
        PressInteract(bus);
        home.Tick(Step);
        Assert.Single(bus.Teleports);
    }

    [Fact]
    public void OpeningTheWardrobeEndsDecorating()
    {
        var (home, bus) = Started(coins: 1000);
        home.Buy(FurnitureKind.Lamp);
        Inside(home, bus);
        home.StartDecorating();
        Assert.Equal(1, home.Held);
        WardrobeScreen(bus, open: true);
        home.Tick(Step);
        Assert.False(home.Decorating);
        Assert.Equal(-1, home.Held);
        Assert.False(home.Items[1].Placed); // the held lamp went back to the inventory
        Assert.False(bus.States[^1].Decorating);
    }

    // The start screen

    private static void StartScreen(FakeBus bus, bool open) => bus.Publish(new StartScreenState(open));

    [Fact]
    public void WhileTheStartScreenIsOpenThereAreNoPromptsAndInteractIsIgnored()
    {
        var (home, bus) = Started(coins: 1000);
        StartScreen(bus, open: true);
        Assert.True(home.StartScreenOpen);

        // At the house door (the start screen's backdrop): no "Enter", and Interact does not teleport.
        StandAt(bus, Places.HomeDoor.Position);
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        PressInteract(bus);
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        Assert.Empty(bus.Teleports);

        // At the furniture shop: no "Shop", the catalog does not open and nothing is bought.
        StandAt(bus, Places.FurnitureShopSpot.Position);
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        PressInteract(bus);
        home.Tick(Step);
        Assert.False(home.ShopOpen);
        Assert.Empty(bus.Spends);

        // By the door inside, the catalog book and the wardrobe: no Leave, no Decorate, no Wardrobe.
        StandAt(bus, Places.HomeExit.Position);
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        PressInteract(bus);
        home.Tick(Step);
        Assert.Empty(bus.Teleports);
        StandAt(bus, HomeRoom.BookPosition + new Vector3(0f, 0f, -0.8f));
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        PressInteract(bus);
        home.Tick(Step);
        home.StartDecorating();
        home.Tick(Step);
        Assert.False(home.Decorating);
        Assert.False(bus.States[^1].Decorating);
        StandAt(bus, HomeRoom.WardrobeFront + new Vector3(0f, 0f, 0.5f));
        home.Tick(Step);
        Assert.Equal(HomePrompt.None, home.Prompt);
        PressInteract(bus);
        home.Tick(Step);
        Assert.Equal(0, bus.OpenWardrobes);

        // "Start!" pressed: a press made on the start screen is not acted on later, and all works again.
        StandAt(bus, Places.HomeDoor.Position);
        PressInteract(bus);
        home.Tick(Step);
        StartScreen(bus, open: false);
        home.Tick(Step);
        Assert.False(home.StartScreenOpen);
        Assert.Equal(HomePrompt.Enter, home.Prompt);
        Assert.Empty(bus.Teleports);
        PressInteract(bus);
        home.Tick(Step);
        Assert.Single(bus.Teleports);
    }

    [Fact]
    public void TheStartScreenClosesTheShopAndEndsDecorating()
    {
        var (home, bus) = Started(coins: 1000);
        StandAt(bus, Places.FurnitureShopSpot.Position);
        home.Tick(Step);
        home.OpenShop();
        StartScreen(bus, open: true);
        home.Tick(Step);
        Assert.False(home.ShopOpen);

        StartScreen(bus, open: false);
        home.Buy(FurnitureKind.Lamp);
        Inside(home, bus);
        home.StartDecorating();
        Assert.Equal(1, home.Held);
        StartScreen(bus, open: true);
        home.Tick(Step);
        Assert.False(home.Decorating);
        Assert.Equal(-1, home.Held);
        Assert.False(home.Items[1].Placed);
        Assert.False(bus.States[^1].Decorating);
    }

    // Pets at home

    private static void Owned(FakeBus bus, bool cat, bool dog, bool crow, PetKind active) =>
        bus.Publish(new OwnedPets(cat, dog, crow, active));

    [Fact]
    public void OwnedPetsThatAreNotActiveAreShownAtHome()
    {
        var (home, bus) = Started();
        Owned(bus, cat: true, dog: true, crow: false, active: PetKind.Cat);
        home.Tick(Step);
        Assert.False(home.Pets[(int)PetKind.Cat].Present);
        Assert.True(home.Pets[(int)PetKind.Dog].Present);
        Assert.False(home.Pets[(int)PetKind.Crow].Present);
        var dog = home.Pets[(int)PetKind.Dog].Position;
        Assert.True(Areas.Home.Contains(dog.X, dog.Z));
        Assert.Equal(Areas.HomeFloorY, dog.Y);

        // Taking the dog out and leaving the cat home swaps them.
        Owned(bus, cat: true, dog: true, crow: false, active: PetKind.Dog);
        home.Tick(Step);
        Assert.True(home.Pets[(int)PetKind.Cat].Present);
        Assert.False(home.Pets[(int)PetKind.Dog].Present);
    }

    [Fact]
    public void PetsAtHomeWanderAndStayInsideOffTheFurniture()
    {
        var (home, bus) = Started();
        Owned(bus, cat: true, dog: true, crow: true, active: PetKind.Crow);
        var start = home.Pets[(int)PetKind.Cat].Position;
        bool moved = false, played = false;
        for (int t = 0; t < 60 * 60; t++)
        {
            Owned(bus, true, true, true, PetKind.Crow);
            home.Tick(Step);
            foreach (var pet in home.Pets)
            {
                if (!pet.Present)
                    continue;
                Assert.True(Areas.Home.Contains(pet.Position.X, pet.Position.Z));
                int under = home.ItemAt(HomeRoom.ColumnAt(pet.Position.X), HomeRoom.RowAt(pet.Position.Z));
                Assert.Equal(-1, under); // never on the bed
                Assert.False(HomeRoom.UnderWardrobe(HomeRoom.ColumnAt(pet.Position.X), HomeRoom.RowAt(pet.Position.Z)));
                played |= pet.Activity == PetActivity.Play && pet.Speed > PetWalkSpeedLimit;
            }
            moved |= Vector3.Distance(home.Pets[(int)PetKind.Cat].Position, start) > 1f;
        }
        Assert.True(moved);
        Assert.True(played);
    }

    private const float PetWalkSpeedLimit = HomeModule.PetWalkSpeed + 0.01f;

    [Fact]
    public void PetsNapOnAPlacedPetBed()
    {
        var (home, bus) = Started(coins: 1000);
        home.Buy(FurnitureKind.PetBed);
        Inside(home, bus);
        home.StartDecorating();
        home.MoveCursor(-100, -100);
        home.MoveCursor(16, 4);
        Assert.True(home.PlaceOrPickUp());
        var bed = home.Items[1];

        Owned(bus, true, true, false, PetKind.Cat);
        bool onBed = false;
        for (int t = 0; t < 60 * 120 && !onBed; t++)
        {
            home.Tick(Step);
            var dog = home.Pets[(int)PetKind.Dog];
            if (dog.OnBed)
            {
                onBed = true;
                Assert.Equal(PetActivity.Nap, dog.Activity);
                Assert.Equal(1, dog.BedItem);
                Assert.True(Vector2.Distance(new Vector2(dog.Position.X, dog.Position.Z), new Vector2(bed.Center.X, bed.Center.Z)) < 0.01f);
            }
        }
        Assert.True(onBed);
    }

    [Fact]
    public void TheHomeIsTheSameRunAfterRun()
    {
        static Vector3 Run()
        {
            var (home, bus) = Started();
            Owned(bus, true, true, false, PetKind.Cat);
            for (int t = 0; t < 600; t++)
                home.Tick(Step);
            return home.Pets[(int)PetKind.Dog].Position;
        }
        Assert.Equal(Run(), Run());
    }
}
