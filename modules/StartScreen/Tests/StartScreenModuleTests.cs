using System;
using System.Collections.Generic;
using Fungtopia.Shell;
using Fungtopia.Wardrobe;
using Xunit;

namespace Fungtopia.StartScreen.Tests;

public class StartScreenModuleTests
{
    /// <summary>Fake of the shell's bus: records the start screen's messages in order.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public readonly List<GameStart> Starts = new();
        public readonly List<StartScreenState> States = new();
        public readonly List<SetLook> SetLooks = new();
        public readonly List<Equip> Equips = new();
        public readonly List<string> Order = new();
        public int OtherMessages;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }

        public void Publish<T>(in T message) where T : struct
        {
            if (message is GameStart start)
            {
                Starts.Add(start);
                Order.Add("GameStart");
            }
            else if (message is StartScreenState state)
            {
                States.Add(state);
                Order.Add("StartScreenState");
            }
            else if (message is SetLook look)
            {
                SetLooks.Add(look);
                Order.Add("SetLook");
            }
            else if (message is Equip equip)
            {
                Equips.Add(equip);
                Order.Add("Equip");
            }
            else
                OtherMessages++;
        }

        public void ClearLookMessages()
        {
            SetLooks.Clear();
            Equips.Clear();
            Order.Clear();
        }
    }

    /// <summary>A bus that only counts, so measuring allocations sees the module's alone.</summary>
    private sealed class CountingBus : IMessageBus
    {
        public int Count;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }

        public void Publish<T>(in T message) where T : struct => Count++;
    }

    private long _tick;

    private static StartScreenModule Started(FakeBus bus)
    {
        var screen = new StartScreenModule();
        screen.Start(bus);
        return screen;
    }

    private void Tick(StartScreenModule screen) => screen.Tick(new TickContext(_tick++, 1.0 / 60));

    private void PressAndTick(StartScreenModule screen, params StartButton[] buttons)
    {
        foreach (var button in buttons)
            screen.Press(button);
        Tick(screen);
    }

    [Fact]
    public void BeginsOnTheTitleWithGirlAndCatHighlighted()
    {
        var screen = Started(new FakeBus());
        Assert.Equal(StartStep.Title, screen.Step);
        Assert.True(screen.Open);
        Assert.Equal(GameStart.Girl, screen.Character);
        Assert.Equal(GameStart.Cat, screen.StarterPet);
    }

    [Fact]
    public void ContractValuesAreGirlZeroBoyOneCatZeroDogOne()
    {
        Assert.Equal(0, GameStart.Girl);
        Assert.Equal(1, GameStart.Boy);
        Assert.Equal(0, GameStart.Cat);
        Assert.Equal(1, GameStart.Dog);
        var start = new GameStart(1, 0);
        Assert.Equal(1, start.Character);
        Assert.Equal(0, start.StarterPet);
        Assert.True(new StartScreenState(true).Open);
        Assert.False(new StartScreenState(false).Open);
    }

    [Fact]
    public void ConfirmWalksThroughTheStepsInOrder()
    {
        var screen = Started(new FakeBus());
        var seen = new List<StartStep> { screen.Step };
        for (int i = 0; i < 5; i++)
        {
            PressAndTick(screen, StartButton.Confirm);
            seen.Add(screen.Step);
        }
        Assert.Equal(new[] { StartStep.Title, StartStep.Character, StartStep.Look, StartStep.Pet, StartStep.Ready, StartStep.Started }, seen);
        Assert.False(screen.Open);
    }

    [Fact]
    public void PublishesOpenEveryTickUntilStartThenClosed()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        for (int i = 0; i < 10; i++)
            Tick(screen);
        Assert.Equal(10, bus.States.Count);
        Assert.All(bus.States, s => Assert.True(s.Open));
        Assert.Empty(bus.Starts);

        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
        Tick(screen);
        Tick(screen);
        Assert.Equal(13, bus.States.Count);
        Assert.False(bus.States[10].Open);
        Assert.False(bus.States[12].Open);
        Assert.Equal(0, bus.OtherMessages);
    }

    [Fact]
    public void NothingIsPublishedBeforeTheFirstTick()
    {
        var bus = new FakeBus();
        Started(bus).Press(StartButton.Confirm);
        Assert.Empty(bus.States);
        Assert.Empty(bus.Starts);
    }

    [Fact]
    public void DefaultsStartAsGirlWithCat()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
        var start = Assert.Single(bus.Starts);
        Assert.Equal(GameStart.Girl, start.Character);
        Assert.Equal(GameStart.Cat, start.StarterPet);
    }

    [Theory]
    [InlineData(StartButton.Left, StartButton.Left, 0, 0)]
    [InlineData(StartButton.Right, StartButton.Left, 1, 0)]
    [InlineData(StartButton.Left, StartButton.Right, 0, 1)]
    [InlineData(StartButton.Right, StartButton.Right, 1, 1)]
    public void ArrowsChooseCharacterAndPet(StartButton character, StartButton pet, int wantCharacter, int wantPet)
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        PressAndTick(screen, StartButton.Confirm);
        PressAndTick(screen, StartButton.Right, character);
        Assert.Equal(wantCharacter, screen.Character);
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm);   // through the look step
        Assert.Equal(StartStep.Pet, screen.Step);
        PressAndTick(screen, StartButton.Right, pet);
        Assert.Equal(wantPet, screen.StarterPet);
        PressAndTick(screen, StartButton.Confirm);
        Assert.Empty(bus.Starts);
        PressAndTick(screen, StartButton.Confirm);

        var start = Assert.Single(bus.Starts);
        Assert.Equal(wantCharacter, start.Character);
        Assert.Equal(wantPet, start.StarterPet);
    }

    [Fact]
    public void ArrowsOnTitleAndReadyChangeNothing()
    {
        var screen = Started(new FakeBus());
        PressAndTick(screen, StartButton.Right);
        Assert.Equal(StartStep.Title, screen.Step);
        Assert.Equal(GameStart.Girl, screen.Character);
        Assert.Equal(GameStart.Cat, screen.StarterPet);
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
        Assert.Equal(StartStep.Ready, screen.Step);
        PressAndTick(screen, StartButton.Right, StartButton.Left, StartButton.Right, StartButton.Up, StartButton.Down);
        Assert.Equal(StartStep.Ready, screen.Step);
        Assert.Equal(GameStart.Girl, screen.Character);
        Assert.Equal(GameStart.Cat, screen.StarterPet);
    }

    [Fact]
    public void MouseClicksChooseBoyAndDog()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        screen.Press(StartButton.Confirm);           // click on the title
        Tick(screen);
        screen.Point(1);                             // hover the Boy
        Tick(screen);
        Assert.Equal(StartStep.Character, screen.Step);
        Assert.Equal(GameStart.Boy, screen.Character);
        screen.Point(1);                             // click the Boy
        screen.Press(StartButton.Confirm);
        screen.Press(StartButton.Confirm);           // click "Next" on the look step
        screen.Point(1);                             // click the Dog in the same frame
        screen.Press(StartButton.Confirm);
        Tick(screen);
        Assert.Equal(StartStep.Ready, screen.Step);
        screen.Press(StartButton.Confirm);           // click "Start!"
        Tick(screen);

        var start = Assert.Single(bus.Starts);
        Assert.Equal(GameStart.Boy, start.Character);
        Assert.Equal(GameStart.Dog, start.StarterPet);
    }

    [Fact]
    public void PointingOutsideTheTwoChoicesIsIgnored()
    {
        var screen = Started(new FakeBus());
        PressAndTick(screen, StartButton.Confirm);
        screen.Point(2);
        screen.Point(-1);
        Tick(screen);
        Assert.Equal(GameStart.Girl, screen.Character);
        Assert.Equal(StartStep.Character, screen.Step);
    }

    [Fact]
    public void BackGoesOneStepBackAndKeepsTheChoices()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Title, screen.Step);
        PressAndTick(screen, StartButton.Confirm, StartButton.Right, StartButton.Confirm, StartButton.Right,
                     StartButton.Confirm, StartButton.Right, StartButton.Confirm);
        Assert.Equal(StartStep.Ready, screen.Step);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Pet, screen.Step);
        Assert.Equal(GameStart.Dog, screen.StarterPet);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Look, screen.Step);
        Assert.Equal(1, screen.Skin);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Character, screen.Step);
        Assert.Equal(GameStart.Boy, screen.Character);
        PressAndTick(screen, StartButton.Left, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);

        var start = Assert.Single(bus.Starts);
        Assert.Equal(GameStart.Girl, start.Character);
        Assert.Equal(GameStart.Dog, start.StarterPet);
    }

    [Fact]
    public void GameStartIsPublishedOnceBeforeTheClosedState()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
        bus.Order.Clear();
        PressAndTick(screen, StartButton.Confirm);
        Assert.Equal(new[] { "GameStart", "StartScreenState" }, bus.Order);
        Assert.False(bus.States[^1].Open);
    }

    [Fact]
    public void PressesAfterStartAreIgnored()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        // Extra presses in the same frame as "Start!" and in later frames change nothing.
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm,
                     StartButton.Confirm, StartButton.Back, StartButton.Confirm);
        bus.ClearLookMessages();
        for (int i = 0; i < 5; i++)
            PressAndTick(screen, StartButton.Right, StartButton.Back, StartButton.Confirm);
        screen.Point(1);
        screen.Pick(LookRow.Skin, 3);
        Tick(screen);
        Assert.Empty(bus.SetLooks);
        Assert.Empty(bus.Equips);
        Assert.Equal(0, screen.Skin);
        Assert.Single(bus.Starts);
        Assert.Equal(StartStep.Started, screen.Step);
        Assert.Equal(GameStart.Girl, screen.Character);
        Assert.Equal(GameStart.Cat, screen.StarterPet);
        Assert.All(bus.States.GetRange(1, bus.States.Count - 1), s => Assert.False(s.Open));
    }

    [Fact]
    public void PressesWaitForTheNextTick()
    {
        var screen = Started(new FakeBus());
        screen.Press(StartButton.Confirm);
        Assert.Equal(StartStep.Title, screen.Step);
        Tick(screen);
        Assert.Equal(StartStep.Character, screen.Step);
    }

    [Fact]
    public void StepTicksCountsFromEachNewStep()
    {
        var screen = Started(new FakeBus());
        for (int i = 0; i < 5; i++)
            Tick(screen);
        Assert.Equal(4, screen.StepTicks);
        PressAndTick(screen, StartButton.Confirm);
        Assert.Equal(0, screen.StepTicks);
        Tick(screen);
        Tick(screen);
        Assert.Equal(2, screen.StepTicks);
        Assert.Equal(8, screen.Ticks);
    }

    [Fact]
    public void TooManyPressesInOneFrameAreDropped()
    {
        var screen = Started(new FakeBus());
        for (int i = 0; i < StartScreenModule.MaxQueued + 10; i++)
            screen.Press(i < StartScreenModule.MaxQueued ? StartButton.Left : StartButton.Confirm);
        Tick(screen);
        Assert.Equal(StartStep.Title, screen.Step);
    }

    [Fact]
    public void TickDoesNotAllocate()
    {
        var bus = new CountingBus();
        var screen = new StartScreenModule();
        screen.Start(bus);
        screen.Press(StartButton.Confirm);
        void Run(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                screen.Press(i % 2 == 0 ? StartButton.Left : StartButton.Right);
                screen.Point(i % 2);
                screen.Tick(new TickContext(i, 1.0 / 60));
            }
        }
        Run(100);   // warm up
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(100);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Equal(200, bus.Count);
    }

    /// <summary>On the look step as the Girl (or the Boy with <paramref name="boy"/>), with the messages of getting there cleared.</summary>
    private StartScreenModule OnLookStep(FakeBus bus, bool boy = false)
    {
        var screen = Started(bus);
        PressAndTick(screen, StartButton.Confirm);
        PressAndTick(screen, boy ? StartButton.Right : StartButton.Left, StartButton.Confirm);
        Assert.Equal(StartStep.Look, screen.Step);
        bus.ClearLookMessages();
        return screen;
    }

    private static (string Item, Slot Slot)[] Equipped(FakeBus bus)
    {
        var all = new (string, Slot)[bus.Equips.Count];
        for (int i = 0; i < all.Length; i++)
            all[i] = (bus.Equips[i].ItemId, string.IsNullOrEmpty(bus.Equips[i].ItemId) ? bus.Equips[i].EquipSlot : Catalog.Items[Catalog.IndexOf(bus.Equips[i].ItemId)].Slot);
        return all;
    }

    private static void AssertLook(SetLook look, int skin, int hairStyle, int hairColor, int eyeColor)
    {
        Assert.Equal(skin, look.Skin);
        Assert.Equal(hairStyle, look.HairStyle);
        Assert.Equal(hairColor, look.HairColor);
        Assert.Equal(eyeColor, look.EyeColor);
    }

    [Fact]
    public void ReachingTheLookStepAsTheGirlSendsTheWholeLookWithTheMushroomDress()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        PressAndTick(screen, StartButton.Confirm);
        Assert.Empty(bus.SetLooks);
        Assert.Empty(bus.Equips);
        bus.Order.Clear();
        PressAndTick(screen, StartButton.Confirm);

        Assert.Equal(StartStep.Look, screen.Step);
        Assert.Equal(LookRow.Skin, screen.Row);
        Assert.Equal(1, screen.Outfit);
        Assert.Equal("Mushroom Dress", Catalog.StarterOutfits[screen.Outfit].Name);
        AssertLook(Assert.Single(bus.SetLooks), 0, 0, 0, 0);
        Assert.Equal(new[] { ("spotted_dress", Slot.Top), ("", Slot.Bottom), ("mary_janes", Slot.Shoes), ("big_bow", Slot.Head) },
            Equipped(bus));
        Assert.Equal(new[] { "SetLook", "Equip", "Equip", "Equip", "Equip", "StartScreenState" }, bus.Order);
        Assert.True(bus.States[^1].Open);
    }

    [Fact]
    public void TheBoyStartsInSunnyStripes()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        PressAndTick(screen, StartButton.Confirm);
        PressAndTick(screen, StartButton.Right, StartButton.Confirm);
        Assert.Equal(0, screen.Outfit);
        Assert.Single(bus.SetLooks);
        Assert.Equal(new[] { ("striped_tee", Slot.Top), ("shorts", Slot.Bottom), ("sneakers", Slot.Shoes), ("", Slot.Head) },
            Equipped(bus));
    }

    [Fact]
    public void ArrowsChangeEachRowAndSendSetLookAtOnce()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);

        PressAndTick(screen, StartButton.Right);                      // skin 1
        AssertLook(Assert.Single(bus.SetLooks), 1, 0, 0, 0);
        Assert.Empty(bus.Equips);
        Assert.Equal(new[] { "SetLook", "StartScreenState" }, bus.Order);

        bus.ClearLookMessages();
        PressAndTick(screen, StartButton.Down, StartButton.Right, StartButton.Right);   // hair style 2
        Assert.Equal(LookRow.HairStyle, screen.Row);
        AssertLook(Assert.Single(bus.SetLooks), 1, 2, 0, 0);

        bus.ClearLookMessages();
        PressAndTick(screen, StartButton.Down, StartButton.Left);    // hair color wraps to the last
        Assert.Equal(LookRow.HairColor, screen.Row);
        AssertLook(Assert.Single(bus.SetLooks), 1, 2, Looks.HairColors.Length - 1, 0);

        bus.ClearLookMessages();
        PressAndTick(screen, StartButton.Down, StartButton.Right, StartButton.Right, StartButton.Right);
        Assert.Equal(LookRow.EyeColor, screen.Row);
        AssertLook(Assert.Single(bus.SetLooks), 1, 2, Looks.HairColors.Length - 1, 3);
        Assert.Empty(bus.Equips);

        Assert.Equal(1, screen.Skin);
        Assert.Equal(2, screen.HairStyle);
        Assert.Equal(Looks.HairColors.Length - 1, screen.HairColor);
        Assert.Equal(3, screen.EyeColor);
        Assert.True(Looks.Valid(screen.Skin, screen.HairStyle, screen.HairColor, screen.EyeColor));
    }

    [Fact]
    public void EveryValueOfARowCanBeReachedAndItWrapsRound()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        PressAndTick(screen, StartButton.Down);                      // hair style
        for (int i = 1; i <= Looks.HairStyles.Length; i++)
        {
            PressAndTick(screen, StartButton.Right);
            Assert.Equal(i % Looks.HairStyles.Length, screen.HairStyle);
            Assert.Equal(i % Looks.HairStyles.Length, bus.SetLooks[^1].HairStyle);
        }
        Assert.Equal(Looks.HairStyles.Length, bus.SetLooks.Count);
    }

    [Fact]
    public void UpAndDownStopAtTheFirstAndLastRows()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        PressAndTick(screen, StartButton.Up);
        Assert.Equal(LookRow.Skin, screen.Row);
        for (int i = 0; i < 8; i++)
            PressAndTick(screen, StartButton.Down);
        Assert.Equal(LookRow.Outfit, screen.Row);
        PressAndTick(screen, StartButton.Up);
        Assert.Equal(LookRow.EyeColor, screen.Row);
        Assert.Empty(bus.SetLooks);
        Assert.Empty(bus.Equips);
    }

    [Fact]
    public void ChangingTheOutfitEquipsAllItsSlotsAtOnce()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        PressAndTick(screen, StartButton.Down, StartButton.Down, StartButton.Down, StartButton.Down);
        Assert.Equal(LookRow.Outfit, screen.Row);
        PressAndTick(screen, StartButton.Right);                     // Mushroom Dress -> Cozy Overalls
        Assert.Equal(2, screen.Outfit);
        Assert.Equal(new[] { ("overalls_top", Slot.Top), ("overalls", Slot.Bottom), ("rain_boots", Slot.Shoes), ("beanie", Slot.Head) },
            Equipped(bus));
        Assert.Empty(bus.SetLooks);

        bus.ClearLookMessages();
        PressAndTick(screen, StartButton.Right);                     // wraps to Sunny Stripes
        Assert.Equal(0, screen.Outfit);
        Assert.Equal(new[] { ("striped_tee", Slot.Top), ("shorts", Slot.Bottom), ("sneakers", Slot.Shoes), ("", Slot.Head) },
            Equipped(bus));
    }

    [Fact]
    public void TheBoySkipsTheDress()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus, boy: true);
        Assert.False(screen.OutfitFits(1));
        Assert.True(screen.OutfitFits(0));
        Assert.True(screen.OutfitFits(2));
        PressAndTick(screen, StartButton.Down, StartButton.Down, StartButton.Down, StartButton.Down);
        var seen = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            PressAndTick(screen, StartButton.Right);
            seen.Add(screen.Outfit);
        }
        Assert.Equal(new[] { 2, 0, 2, 0 }, seen);
        PressAndTick(screen, StartButton.Left);
        Assert.Equal(2, screen.Outfit);
        Assert.DoesNotContain(bus.Equips, e => e.ItemId == "spotted_dress");

        bus.ClearLookMessages();
        screen.Pick(LookRow.Outfit, 1);                              // clicking the dress does nothing
        Tick(screen);
        Assert.Equal(2, screen.Outfit);
        Assert.Empty(bus.Equips);
    }

    [Fact]
    public void ClickingAValuePicksItsRowAndSendsAtOnce()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        screen.Pick(LookRow.EyeColor, 2);
        Tick(screen);
        Assert.Equal(LookRow.EyeColor, screen.Row);
        AssertLook(Assert.Single(bus.SetLooks), 0, 0, 0, 2);

        bus.ClearLookMessages();
        screen.Pick(LookRow.Skin, 5);
        screen.Pick(LookRow.HairColor, 6);                           // two clicks in one frame: one SetLook
        Tick(screen);
        Assert.Equal(LookRow.HairColor, screen.Row);
        AssertLook(Assert.Single(bus.SetLooks), 5, 0, 6, 2);

        bus.ClearLookMessages();
        screen.Pick(LookRow.Outfit, 0);
        Tick(screen);
        Assert.Equal(0, screen.Outfit);
        Assert.Equal(4, bus.Equips.Count);
        Assert.Empty(bus.SetLooks);

        bus.ClearLookMessages();
        screen.Pick(LookRow.HairColor, 6);                           // the same value again: nothing sent
        Tick(screen);
        Assert.Empty(bus.SetLooks);
        Assert.Empty(bus.Equips);
    }

    [Fact]
    public void PicksOutOfRangeOrOffTheLookStepAreIgnored()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        PressAndTick(screen, StartButton.Confirm);
        screen.Pick(LookRow.Skin, 2);                                // on the character step
        Tick(screen);
        Assert.Equal(0, screen.Skin);
        PressAndTick(screen, StartButton.Confirm);
        bus.ClearLookMessages();
        screen.Pick(LookRow.Skin, Looks.SkinTones.Length);
        screen.Pick(LookRow.HairStyle, -1);
        screen.Pick(LookRow.Outfit, Catalog.StarterOutfits.Length);
        screen.Pick((LookRow)9, 0);
        Tick(screen);
        Assert.Equal(LookRow.Skin, screen.Row);
        Assert.Empty(bus.SetLooks);
        Assert.Empty(bus.Equips);
    }

    [Fact]
    public void ArrowsOnTheLookStepDoNotChangeCharacterOrPet()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        PressAndTick(screen, StartButton.Right, StartButton.Right);
        screen.Point(1);
        Tick(screen);
        Assert.Equal(GameStart.Girl, screen.Character);
        Assert.Equal(GameStart.Cat, screen.StarterPet);
        Assert.Equal(2, screen.Skin);
    }

    [Fact]
    public void BackToTheCharacterKeepsTheLookAndANewCharacterGetsItsOutfit()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        PressAndTick(screen, StartButton.Right, StartButton.Down, StartButton.Right);   // skin 1, hair style 1
        screen.Pick(LookRow.Outfit, 2);
        Tick(screen);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Character, screen.Step);

        // Same character again: the chosen outfit stays, and the whole look is sent again.
        bus.ClearLookMessages();
        PressAndTick(screen, StartButton.Confirm);
        Assert.Equal(StartStep.Look, screen.Step);
        Assert.Equal(2, screen.Outfit);
        Assert.Equal(LookRow.Outfit, screen.Row);
        AssertLook(Assert.Single(bus.SetLooks), 1, 1, 0, 0);
        Assert.Equal("overalls_top", bus.Equips[0].ItemId);

        // The Boy now: the look stays, the outfit becomes his default.
        PressAndTick(screen, StartButton.Back, StartButton.Right);
        bus.ClearLookMessages();
        PressAndTick(screen, StartButton.Confirm);
        Assert.Equal(0, screen.Outfit);
        Assert.Equal(LookRow.Skin, screen.Row);
        AssertLook(Assert.Single(bus.SetLooks), 1, 1, 0, 0);
        Assert.Equal("striped_tee", bus.Equips[0].ItemId);

        // Back from the pet step to the look step sends nothing.
        PressAndTick(screen, StartButton.Confirm);
        bus.ClearLookMessages();
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Look, screen.Step);
        Assert.Empty(bus.SetLooks);
        Assert.Empty(bus.Equips);
    }

    [Fact]
    public void ThePetStepAndStartComeAfterTheLook()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus, boy: true);
        PressAndTick(screen, StartButton.Right, StartButton.Confirm);
        Assert.Equal(StartStep.Pet, screen.Step);
        PressAndTick(screen, StartButton.Right, StartButton.Confirm);
        Assert.Equal(StartStep.Ready, screen.Step);
        bus.ClearLookMessages();
        PressAndTick(screen, StartButton.Confirm);

        var start = Assert.Single(bus.Starts);
        Assert.Equal(GameStart.Boy, start.Character);
        Assert.Equal(GameStart.Dog, start.StarterPet);
        Assert.Equal(new[] { "GameStart", "StartScreenState" }, bus.Order);
        Assert.Equal(1, screen.Skin);
    }

    [Fact]
    public void LookTicksDoNotAllocate()
    {
        var bus = new CountingBus();
        var screen = new StartScreenModule();
        screen.Start(bus);
        screen.Press(StartButton.Confirm);
        screen.Press(StartButton.Confirm);
        screen.Tick(new TickContext(0, 1.0 / 60));
        Assert.Equal(StartStep.Look, screen.Step);
        void Run(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                screen.Press(i % 3 == 0 ? StartButton.Down : StartButton.Right);
                if (i % 7 == 0)
                    screen.Press(StartButton.Up);
                screen.Pick(LookRow.Outfit, i % 3);
                screen.Tick(new TickContext(i + 1, 1.0 / 60));
            }
        }
        Run(100);   // warm up
        int sentBefore = bus.Count;
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(100);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.True(bus.Count > sentBefore + 100);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new StartScreenModule().Tick(new TickContext(0, 1.0 / 60)));
    }
}
