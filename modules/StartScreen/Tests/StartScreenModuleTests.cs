using System;
using System.Collections.Generic;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Save;
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
        public readonly List<PreviewYaw> Yaws = new();
        public readonly List<NewGameInSlot> NewGames = new();
        public readonly List<LoadGame> Loads = new();
        public readonly List<UiFocus> Focus = new();
        public readonly List<string> Order = new();
        public int OtherMessages;

        public readonly List<Type> Subscribed = new();

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct => Subscribed.Add(typeof(T));

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
            else if (message is PreviewYaw yaw)
            {
                Yaws.Add(yaw);
                Order.Add("PreviewYaw");
            }
            else if (message is NewGameInSlot newGame)
            {
                NewGames.Add(newGame);
                Order.Add("NewGameInSlot");
            }
            else if (message is LoadGame load)
            {
                Loads.Add(load);
                Order.Add("LoadGame");
            }
            else if (message is UiFocus focus)
            {
                Focus.Add(focus);
                Order.Add("UiFocus");
            }
            else
                OtherMessages++;
        }

        public void ClearLookMessages()
        {
            SetLooks.Clear();
            Equips.Clear();
            Yaws.Clear();
            Order.Clear();
        }
    }

    private long _tick;

    private static StartScreenModule Started(FakeBus bus)
    {
        var screen = new StartScreenModule();
        screen.Start(bus);
        return screen;
    }

    private void Tick(StartScreenModule screen) => screen.Tick(new TickContext(_tick++, 1.0 / 60));

    /// <summary>From the title with no saves: New Game, then the empty slot 1, in one tick.</summary>
    private void ToCharacter(StartScreenModule screen)
    {
        Assert.Equal(StartStep.Title, screen.Step);
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm);
        Assert.Equal(StartStep.Character, screen.Step);
    }

    /// <summary>
    /// Reports <paramref name="buttons"/> in order, then ticks: Confirm and Back as presses, the
    /// directions as one MenuNav step each (the only way they reach the screen).
    /// </summary>
    private void PressAndTick(StartScreenModule screen, params StartButton[] buttons)
    {
        foreach (var button in buttons)
        {
            if (button is StartButton.Confirm or StartButton.Back)
                screen.Press(button);
            else
                screen.Handle(Nav(button));
        }
        Tick(screen);
    }

    private static MenuNav Nav(StartButton direction) => direction switch
    {
        StartButton.Up => new MenuNav(-1, 0),
        StartButton.Down => new MenuNav(1, 0),
        StartButton.Left => new MenuNav(0, -1),
        StartButton.Right => new MenuNav(0, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    /// <summary>Ticks per MenuNav repeat while a direction is held: 0.2 s at 60 ticks a second.</summary>
    private const int RepeatTicks = 12;

    /// <summary>
    /// The MenuNav the Input module publishes on tick <paramref name="tick"/> (from 0) of a push in
    /// <paramref name="nav"/>'s direction held for <paramref name="heldTicks"/> ticks: the step on the
    /// first tick, then one every 0.2 s while held, nothing in between or after.
    /// </summary>
    private static MenuNav HeldNav(MenuNav nav, int tick, int heldTicks) =>
        tick < heldTicks && tick % RepeatTicks == 0 ? nav : default;

    /// <summary>Publishes <paramref name="nav"/> to the screen, as the Input module does every tick, then ticks.</summary>
    private void NavAndTick(StartScreenModule screen, MenuNav nav)
    {
        screen.Handle(nav);
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
        for (int i = 0; i < 6; i++)
        {
            PressAndTick(screen, StartButton.Confirm);
            seen.Add(screen.Step);
        }
        Assert.Equal(new[] { StartStep.Title, StartStep.NewSlot, StartStep.Character, StartStep.Look, StartStep.Pet, StartStep.Ready, StartStep.Started }, seen);
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

        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
        Tick(screen);
        Tick(screen);
        Assert.Equal(13, bus.States.Count);
        Assert.False(bus.States[10].Open);
        Assert.False(bus.States[12].Open);
        Assert.Equal(0, bus.OtherMessages);
        Assert.Equal(new[] { 1 }, bus.NewGames.ConvertAll(n => n.Slot));
        Assert.Empty(bus.Loads);
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
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
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
        ToCharacter(screen);
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
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
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
        screen.Point((int)TitleItem.NewGame);        // click "New Game" on the title
        screen.Press(StartButton.Confirm);
        Tick(screen);
        screen.Point(0);                             // click slot 1
        screen.Press(StartButton.Confirm);
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
        ToCharacter(screen);
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
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Right, StartButton.Confirm, StartButton.Right,
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
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
        bus.Order.Clear();
        PressAndTick(screen, StartButton.Confirm);
        Assert.Equal(new[] { "GameStart", "UiFocus", "StartScreenState" }, bus.Order);
        Assert.False(bus.States[^1].Open);
    }

    [Fact]
    public void PressesAfterStartAreIgnored()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        // Extra presses in the same frame as "Start!" and in later frames change nothing.
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm,
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
        Assert.Equal(StartStep.NewSlot, screen.Step);
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
    public void StartListensForMenuNav()
    {
        var bus = new FakeBus();
        Started(bus);
        Assert.Contains(typeof(MenuNav), bus.Subscribed);
    }

    [Fact]
    public void DirectionPressesAreIgnoredOnlyMenuNavMoves()
    {
        var screen = Started(new FakeBus());
        ToCharacter(screen);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.NewSlot, screen.Step);
        Assert.Equal(0, screen.Highlight);
        foreach (var direction in new[] { StartButton.Down, StartButton.Right, StartButton.Up, StartButton.Left })
        {
            screen.Press(direction);
            Tick(screen);
            Assert.Equal(0, screen.Highlight);
        }

        NavAndTick(screen, new MenuNav(1, 0));
        Assert.Equal(1, screen.Highlight);
        PressAndTick(screen, StartButton.Back, StartButton.Confirm, StartButton.Confirm);
        Assert.Equal(StartStep.Character, screen.Step);
        screen.Press(StartButton.Right);
        Tick(screen);
        Assert.Equal(GameStart.Girl, screen.Character);
        NavAndTick(screen, new MenuNav(0, 1));
        Assert.Equal(GameStart.Boy, screen.Character);
    }

    [Fact]
    public void ANavWithNoStepMovesNothing()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        for (int i = 0; i < 30; i++)
            NavAndTick(screen, default);
        Assert.Equal(LookRow.Skin, screen.Row);
        Assert.Equal(0, screen.Skin);
        Assert.Empty(bus.SetLooks);
    }

    [Fact]
    public void AHeldDirectionMovesTheMenuOneItemPerFifthOfASecond()
    {
        var screen = Started(new FakeBus());
        ToCharacter(screen);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.NewSlot, screen.Step);
        Assert.Equal(0, screen.Highlight);

        // Down held for 0.5 s: steps on ticks 0, 12 and 24 (0 s, 0.2 s, 0.4 s); the menu stops at slot 3.
        var down = new MenuNav(1, 0);
        for (int tick = 0; tick < 30; tick++)
        {
            NavAndTick(screen, HeldNav(down, tick, 30));
            Assert.Equal(tick < RepeatTicks ? 1 : 2, screen.Highlight);
        }

        // Up held for 0.3 s: back one item at once, the next only 0.2 s later.
        var up = new MenuNav(-1, 0);
        for (int tick = 0; tick < 18; tick++)
        {
            NavAndTick(screen, HeldNav(up, tick, 18));
            Assert.Equal(tick < RepeatTicks ? 1 : 0, screen.Highlight);
        }
    }

    [Fact]
    public void AHeldDirectionCyclesALookValueOnePerFifthOfASecond()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        int count = StartScreenModule.LookCount(LookRow.Skin);
        Assert.True(count >= 6);

        // Right held for one second (60 ticks): five steps, at 0, 0.2, 0.4, 0.6 and 0.8 s.
        var right = new MenuNav(0, 1);
        for (int tick = 0; tick < 60; tick++)
        {
            NavAndTick(screen, HeldNav(right, tick, 60));
            Assert.Equal(tick / RepeatTicks + 1, screen.Skin);
        }
        Assert.Equal(5, bus.SetLooks.Count);
        // Released: nothing more.
        for (int tick = 0; tick < 30; tick++)
            NavAndTick(screen, default);
        Assert.Equal(5, screen.Skin);
        Assert.Equal(LookRow.Skin, screen.Row);

        // Down held for 0.5 s: the row moves three times (0, 0.2, 0.4 s; Skin to EyeColor), not once a tick.
        var down = new MenuNav(1, 0);
        for (int tick = 0; tick < 30; tick++)
            NavAndTick(screen, HeldNav(down, tick, 30));
        Assert.Equal(LookRow.EyeColor, screen.Row);
    }

    [Fact]
    public void AShortFlickMovesExactlyOneItem()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);

        // A flick of a few ticks: the Input module sends one step, then nothing.
        var right = new MenuNav(0, 1);
        for (int tick = 0; tick < 40; tick++)
            NavAndTick(screen, HeldNav(right, tick, 4));
        Assert.Equal(1, screen.Skin);
        Assert.Single(bus.SetLooks);

        var down = new MenuNav(1, 0);
        for (int tick = 0; tick < 40; tick++)
            NavAndTick(screen, HeldNav(down, tick, 3));
        Assert.Equal(LookRow.HairStyle, screen.Row);

        // On a menu too: one flick down on the slots highlights the next slot only.
        PressAndTick(screen, StartButton.Back, StartButton.Back);
        Assert.Equal(StartStep.NewSlot, screen.Step);
        Assert.Equal(0, screen.Highlight);
        for (int tick = 0; tick < 40; tick++)
            NavAndTick(screen, HeldNav(down, tick, 5));
        Assert.Equal(1, screen.Highlight);
    }

    [Fact]
    public void TooManyPressesInOneFrameAreDropped()
    {
        var screen = Started(new FakeBus());
        for (int i = 0; i < StartScreenModule.MaxQueued + 10; i++)
        {
            if (i < StartScreenModule.MaxQueued)
                screen.Handle(new MenuNav(0, -1));
            else
                screen.Press(StartButton.Confirm);
        }
        Tick(screen);
        Assert.Equal(StartStep.Title, screen.Step);
    }

    /// <summary>On the look step as the Girl (or the Boy with <paramref name="boy"/>), with the messages of getting there cleared.</summary>
    private StartScreenModule OnLookStep(FakeBus bus, bool boy = false)
    {
        var screen = Started(bus);
        ToCharacter(screen);
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
        ToCharacter(screen);
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
        Assert.Equal(new[] { "SetLook", "Equip", "Equip", "Equip", "Equip", "PreviewYaw", "UiFocus", "StartScreenState" }, bus.Order);
        Assert.True(bus.States[^1].Open);
    }

    [Fact]
    public void TheBoyStartsInSunnyStripes()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        ToCharacter(screen);
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
        Assert.Equal(new[] { "SetLook", "PreviewYaw", "UiFocus", "StartScreenState" }, bus.Order);

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
        ToCharacter(screen);
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
        Assert.Equal(new[] { "GameStart", "UiFocus", "StartScreenState" }, bus.Order);
        Assert.Equal(1, screen.Skin);
    }

    private const float Step = 1f / 60;

    [Fact]
    public void TheLookStepPublishesPreviewYawEveryTickStartingFacingTheCamera()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        ToCharacter(screen);
        Tick(screen);
        Assert.Empty(bus.Yaws);                                       // not on the title or the character step

        PressAndTick(screen, StartButton.Confirm);                    // the look step
        Assert.Equal(0f, Assert.Single(bus.Yaws).Yaw);
        for (int i = 0; i < 9; i++)
            Tick(screen);
        Assert.Equal(10, bus.Yaws.Count);
        Assert.All(bus.Yaws, y => Assert.Equal(0f, y.Yaw));
        Assert.Equal(0f, screen.Yaw);

        bus.ClearLookMessages();
        PressAndTick(screen, StartButton.Confirm);                    // on to the pet: no more
        Tick(screen);
        Assert.Empty(bus.Yaws);
    }

    [Fact]
    public void TurnSettingsMatchTheWardrobe()
    {
        Assert.Equal(3f, StartScreenModule.TurnRadiansPerSecond);
        Assert.Equal(0.01f, StartScreenModule.DragRadiansPerPixel);
        Assert.Equal(0.2f, StartScreenModule.StickDeadZone);
    }

    [Fact]
    public void HeldTurnButtonsTurnLeftAndRightAtTheTurnSpeed()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        screen.SetTurnButtons(false, true);                          // E / RB
        for (int i = 0; i < 30; i++)
            Tick(screen);
        Assert.Equal(30 * Step * StartScreenModule.TurnRadiansPerSecond, screen.Yaw, 4);
        Assert.Equal(screen.Yaw, bus.Yaws[^1].Yaw);
        Assert.True(bus.Yaws[0].Yaw > 0f && bus.Yaws[0].Yaw < bus.Yaws[^1].Yaw);

        screen.SetTurnButtons(true, false);                          // Q / LB
        for (int i = 0; i < 60; i++)
            Tick(screen);
        Assert.Equal(-30 * Step * StartScreenModule.TurnRadiansPerSecond, screen.Yaw, 4);

        screen.SetTurnButtons(true, true);                           // both: they cancel
        Tick(screen);
        Assert.Equal(-30 * Step * StartScreenModule.TurnRadiansPerSecond, screen.Yaw, 4);
        screen.SetTurnButtons(false, false);
        Tick(screen);
        Assert.Equal(-30 * Step * StartScreenModule.TurnRadiansPerSecond, screen.Yaw, 4);
    }

    [Fact]
    public void TheRightStickTurnsPastItsDeadZoneAndTheButtonsWin()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        screen.SetTurnStick(0.15f);                                  // inside the dead zone
        Tick(screen);
        Assert.Equal(0f, screen.Yaw);

        screen.SetTurnStick(1f);
        Tick(screen);
        Assert.Equal(Step * StartScreenModule.TurnRadiansPerSecond, screen.Yaw, 5);

        screen.SetTurnStick(-0.6f);                                  // half way from the dead zone to full
        Tick(screen);
        Assert.Equal(0.5f * Step * StartScreenModule.TurnRadiansPerSecond, screen.Yaw, 5);

        screen.SetTurnStick(-5f);                                    // clamped to full
        Tick(screen);
        Assert.Equal(-0.5f * Step * StartScreenModule.TurnRadiansPerSecond, screen.Yaw, 5);

        screen.SetTurnStick(float.NaN);
        Tick(screen);
        Assert.Equal(-0.5f * Step * StartScreenModule.TurnRadiansPerSecond, screen.Yaw, 5);

        screen.SetTurnStick(-1f);
        screen.SetTurnButtons(false, true);
        Tick(screen);
        Assert.Equal(0.5f * Step * StartScreenModule.TurnRadiansPerSecond, screen.Yaw, 5);
    }

    [Fact]
    public void DragsAddUpAndApplyOnTheNextTick()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        screen.Drag(30f);
        screen.Drag(20f);
        Assert.Equal(0f, screen.Yaw);
        Tick(screen);
        Assert.Equal(50f * StartScreenModule.DragRadiansPerPixel, screen.Yaw, 5);
        Assert.Equal(screen.Yaw, bus.Yaws[^1].Yaw);
        Tick(screen);
        Assert.Equal(50f * StartScreenModule.DragRadiansPerPixel, screen.Yaw, 5);

        screen.Drag(-80f);
        screen.Drag(float.PositiveInfinity);
        Tick(screen);
        Assert.Equal(-30f * StartScreenModule.DragRadiansPerPixel, screen.Yaw, 5);
    }

    [Fact]
    public void TheYawWrapsToMinusPiUpToPi()
    {
        var bus = new FakeBus();
        var screen = OnLookStep(bus);
        screen.Drag(400f);                                           // 4 radians
        Tick(screen);
        Assert.Equal(4f - MathF.Tau, screen.Yaw, 4);
        screen.Drag(-800f);
        Tick(screen);
        Assert.Equal(-4f + MathF.Tau, screen.Yaw, 4);
        screen.SetTurnButtons(false, true);
        for (int i = 0; i < 600; i++)
        {
            Tick(screen);
            Assert.InRange(bus.Yaws[^1].Yaw, -MathF.PI, MathF.PI);
            Assert.True(bus.Yaws[^1].Yaw < MathF.PI);
        }
    }

    [Fact]
    public void TurningOffTheLookStepDoesNothingAndEachVisitStartsFacingTheCamera()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        ToCharacter(screen);                    // the character step
        screen.Drag(100f);                                           // dropped: not the look step
        screen.SetTurnButtons(false, true);
        Tick(screen);
        Assert.Equal(0f, screen.Yaw);
        Assert.Empty(bus.Yaws);
        screen.SetTurnButtons(false, false);

        PressAndTick(screen, StartButton.Confirm);                    // the look step
        Assert.Equal(0f, bus.Yaws[^1].Yaw);
        screen.Drag(100f);
        Tick(screen);
        Assert.Equal(1f, screen.Yaw, 5);

        screen.Drag(50f);                                            // left before it applies: dropped
        PressAndTick(screen, StartButton.Confirm);                    // the pet step
        screen.Drag(70f);
        Tick(screen);
        bus.ClearLookMessages();
        PressAndTick(screen, StartButton.Back);                       // back to the look step
        Assert.Equal(StartStep.Look, screen.Step);
        Assert.Equal(0f, Assert.Single(bus.Yaws).Yaw);

        screen.Drag(100f);
        Tick(screen);
        PressAndTick(screen, StartButton.Back, StartButton.Confirm);  // character and back again
        Assert.Equal(0f, bus.Yaws[^1].Yaw);
    }

    // ---- Save games: Continue, Load and New Game ----

    private static readonly DateTime T0 = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A readable save made <paramref name="minutes"/> after <see cref="T0"/>.</summary>
    private static SaveInfo SaveAt(int minutes, CharacterKind character = CharacterKind.Girl, int day = 1, int coins = 0, Area area = Area.Town) =>
        new(SaveFileState.Ok, T0.AddMinutes(minutes), character.ToString(), character, day, coins, area);

    private static SlotInfo SlotOf(int slot, SaveInfo? manual = null, SaveInfo? auto = null) =>
        new(slot, manual ?? SaveInfo.Missing, auto ?? SaveInfo.Missing);

    private static SaveSlots Slots(SlotInfo? slot1 = null, SlotInfo? slot2 = null, SlotInfo? slot3 = null) =>
        new(slot1 ?? SlotOf(1), slot2 ?? SlotOf(2), slot3 ?? SlotOf(3));

    /// <summary>The start screen with <paramref name="slots"/> received (as the Save module sends them) and one tick run.</summary>
    private StartScreenModule WithSlots(FakeBus bus, SaveSlots slots)
    {
        var screen = Started(bus);
        screen.Handle(slots);
        Tick(screen);
        return screen;
    }

    [Fact]
    public void StartListensForTheSaveSlots()
    {
        var bus = new FakeBus();
        Started(bus);
        Assert.Contains(typeof(SaveSlots), bus.Subscribed);
    }

    [Fact]
    public void WithoutSavesContinueIsHiddenLoadIsGreyAndNewGameIsHighlighted()
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots());
        Assert.False(screen.CanContinue);
        Assert.Equal((int)TitleItem.NewGame, screen.Highlight);
        Assert.False(screen.ItemEnabled((int)TitleItem.Continue));
        Assert.False(screen.ItemEnabled((int)TitleItem.Load));
        Assert.True(screen.ItemEnabled((int)TitleItem.NewGame));
        PressAndTick(screen, StartButton.Up, StartButton.Left);
        screen.Point((int)TitleItem.Continue);
        screen.Point((int)TitleItem.Load);
        Tick(screen);
        Assert.Equal((int)TitleItem.NewGame, screen.Highlight);
        Assert.Equal(StartStep.Title, screen.Step);
        Assert.Empty(bus.Loads);
    }

    [Fact]
    public void ContinueIsHighlightedOnceSavesAreKnownAndLoadsTheNewestSaveOfAnySlot()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        Tick(screen);
        Assert.Equal((int)TitleItem.NewGame, screen.Highlight);         // nothing known yet
        // Slot 2's autosave (the Boy) is the newest of all, manual or auto.
        screen.Handle(Slots(SlotOf(1, manual: SaveAt(10)),
                            SlotOf(2, manual: SaveAt(20, CharacterKind.Boy), auto: SaveAt(30, CharacterKind.Boy)),
                            SlotOf(3, auto: SaveAt(25))));
        Tick(screen);
        Assert.True(screen.CanContinue);
        Assert.Equal((int)TitleItem.Continue, screen.Highlight);
        bus.Order.Clear();
        PressAndTick(screen, StartButton.Confirm);

        var load = Assert.Single(bus.Loads);
        Assert.Equal(2, load.Slot);
        Assert.True(load.Auto);
        var start = Assert.Single(bus.Starts);
        Assert.Equal(GameStart.Boy, start.Character);
        Assert.Equal(GameStart.Cat, start.StarterPet);
        Assert.Equal(new[] { "LoadGame", "GameStart", "UiFocus", "StartScreenState" }, bus.Order);
        Assert.False(bus.States[^1].Open);
        Assert.False(screen.Open);
        Assert.True(screen.Loaded);
        Assert.Equal(2, screen.GameSlot);
        // No character creation: no slot claimed for a new game, no look sent.
        Assert.Empty(bus.NewGames);
        Assert.Empty(bus.SetLooks);
        Assert.Empty(bus.Equips);
    }

    [Fact]
    public void ContinueTakesANewerManualSave()
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots(SlotOf(1, manual: SaveAt(10), auto: SaveAt(5)),
                                          slot3: SlotOf(3, manual: SaveAt(40), auto: SaveAt(35, CharacterKind.Boy))));
        PressAndTick(screen, StartButton.Confirm);
        var load = Assert.Single(bus.Loads);
        Assert.Equal(3, load.Slot);
        Assert.False(load.Auto);
        Assert.Equal(GameStart.Girl, Assert.Single(bus.Starts).Character);
    }

    [Fact]
    public void ContinueIsHiddenAgainWhenTheSavesAreGone()
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots(SlotOf(1, manual: SaveAt(10))));
        Assert.Equal((int)TitleItem.Continue, screen.Highlight);
        screen.Handle(Slots(SlotOf(1, manual: SaveInfo.Unreadable)));
        Tick(screen);
        Assert.False(screen.CanContinue);
        Assert.Equal((int)TitleItem.NewGame, screen.Highlight);
        PressAndTick(screen, StartButton.Confirm);
        Assert.Equal(StartStep.NewSlot, screen.Step);
        Assert.Empty(bus.Loads);
    }

    [Fact]
    public void AMovedTitleHighlightStaysWhenTheSlotsComeAgain()
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots(SlotOf(1, manual: SaveAt(10))));
        PressAndTick(screen, StartButton.Down, StartButton.Down);
        Assert.Equal((int)TitleItem.NewGame, screen.Highlight);
        screen.Handle(Slots(SlotOf(1, manual: SaveAt(10))));
        Tick(screen);
        Assert.Equal((int)TitleItem.NewGame, screen.Highlight);
        PressAndTick(screen, StartButton.Up);
        Assert.Equal((int)TitleItem.Load, screen.Highlight);
        PressAndTick(screen, StartButton.Up, StartButton.Up);
        Assert.Equal((int)TitleItem.Continue, screen.Highlight);         // it stops at the top
    }

    [Fact]
    public void LoadPicksASlotThenItsManualSaveOrItsAutosave()
    {
        var bus = new FakeBus();
        var slots = Slots(SlotOf(1),
                          SlotOf(2, manual: SaveAt(20, CharacterKind.Boy, day: 4, coins: 120, area: Area.Forest), auto: SaveAt(30, CharacterKind.Boy)),
                          SlotOf(3, manual: SaveInfo.Unreadable));
        var screen = WithSlots(bus, slots);
        PressAndTick(screen, StartButton.Down);
        Assert.Equal((int)TitleItem.Load, screen.Highlight);
        PressAndTick(screen, StartButton.Confirm);

        // The three slots with their info; only slot 2 can be loaded, and it is highlighted.
        Assert.Equal(StartStep.Load, screen.Step);
        Assert.Equal(3, StartScreenModule.ItemCount(StartStep.Load));
        Assert.Equal(1, screen.Highlight);
        Assert.False(screen.ItemEnabled(0));                             // empty
        Assert.True(screen.ItemEnabled(1));
        Assert.False(screen.ItemEnabled(2));                             // unreadable
        var manual = screen.Slots.Get(2).Manual;
        Assert.Equal((CharacterKind.Boy, 4, 120, Area.Forest, T0.AddMinutes(20)), (manual.Character, manual.Day, manual.Coins, manual.Area, manual.SavedAt));
        PressAndTick(screen, StartButton.Down, StartButton.Right, StartButton.Up, StartButton.Up);
        Assert.Equal(1, screen.Highlight);
        Assert.Empty(bus.Loads);

        // Slot 2: its manual save and its autosave; the newer (auto) is highlighted.
        PressAndTick(screen, StartButton.Confirm);
        Assert.Equal(StartStep.LoadSave, screen.Step);
        Assert.Equal(2, screen.GameSlot);
        Assert.Equal(1, screen.Highlight);
        Assert.True(screen.ItemEnabled(0));
        Assert.True(screen.ItemEnabled(1));
        PressAndTick(screen, StartButton.Up);
        Assert.Equal(0, screen.Highlight);
        Assert.Empty(bus.Loads);
        bus.Order.Clear();
        PressAndTick(screen, StartButton.Confirm);

        var load = Assert.Single(bus.Loads);
        Assert.Equal((2, false), (load.Slot, load.Auto));
        Assert.Equal(GameStart.Boy, Assert.Single(bus.Starts).Character);
        Assert.Equal(new[] { "LoadGame", "GameStart", "UiFocus", "StartScreenState" }, bus.Order);
        Assert.False(screen.Open);
        Assert.Empty(bus.NewGames);
    }

    [Fact]
    public void LoadingTheAutosaveByMouse()
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots(slot3: SlotOf(3, manual: SaveAt(50), auto: SaveAt(40, CharacterKind.Boy))));
        screen.Point((int)TitleItem.Load);
        screen.Press(StartButton.Confirm);
        screen.Point(2);                                                // slot 3
        screen.Press(StartButton.Confirm);
        Tick(screen);
        Assert.Equal(StartStep.LoadSave, screen.Step);
        Assert.Equal(0, screen.Highlight);                               // the manual save is newer
        screen.Point(1);                                                // click the autosave
        screen.Press(StartButton.Confirm);
        Tick(screen);
        var load = Assert.Single(bus.Loads);
        Assert.Equal((3, true), (load.Slot, load.Auto));
        Assert.Equal(GameStart.Boy, Assert.Single(bus.Starts).Character);
        Assert.True(screen.LoadedAuto);
    }

    [Fact]
    public void AMissingOrUnreadableSaveOfTheSlotCannotBeChosen()
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots(SlotOf(1, manual: SaveInfo.Unreadable, auto: SaveAt(5))));
        PressAndTick(screen, StartButton.Down, StartButton.Confirm, StartButton.Confirm);
        Assert.Equal(StartStep.LoadSave, screen.Step);
        Assert.Equal(1, screen.Highlight);
        Assert.False(screen.ItemEnabled(0));
        PressAndTick(screen, StartButton.Up, StartButton.Left);
        screen.Point(0);
        Tick(screen);
        Assert.Equal(1, screen.Highlight);
        PressAndTick(screen, StartButton.Confirm);
        Assert.Equal((1, true), (bus.Loads[0].Slot, bus.Loads[0].Auto));
    }

    [Fact]
    public void BackLeavesTheLoadMenusWithoutLoading()
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots(SlotOf(1, manual: SaveAt(1)), SlotOf(2, manual: SaveAt(2))));
        PressAndTick(screen, StartButton.Down, StartButton.Confirm, StartButton.Down, StartButton.Confirm);
        Assert.Equal(StartStep.LoadSave, screen.Step);
        Assert.Equal(2, screen.GameSlot);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Load, screen.Step);
        Assert.Equal(1, screen.Highlight);                               // still on slot 2
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Title, screen.Step);
        Assert.Equal((int)TitleItem.Load, screen.Highlight);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Title, screen.Step);
        Assert.True(screen.Open);
        Assert.Empty(bus.Loads);
        Assert.Empty(bus.Starts);
        Assert.All(bus.States, s => Assert.True(s.Open));
    }

    [Fact]
    public void NewGameInAnEmptySlotPublishesNewGameInSlotThenTheCreationSteps()
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots(SlotOf(1, manual: SaveAt(10))));
        PressAndTick(screen, StartButton.Down, StartButton.Down);
        Assert.Equal((int)TitleItem.NewGame, screen.Highlight);
        PressAndTick(screen, StartButton.Confirm);
        Assert.Equal(StartStep.NewSlot, screen.Step);
        Assert.Equal(0, screen.Highlight);
        Assert.True(screen.ItemEnabled(0) && screen.ItemEnabled(1) && screen.ItemEnabled(2));
        PressAndTick(screen, StartButton.Right, StartButton.Right, StartButton.Right);
        Assert.Equal(2, screen.Highlight);                               // it stops at slot 3
        Assert.Empty(bus.NewGames);
        bus.Order.Clear();
        PressAndTick(screen, StartButton.Confirm);

        Assert.Equal(StartStep.Character, screen.Step);
        Assert.Equal(3, Assert.Single(bus.NewGames).Slot);
        Assert.Equal(new[] { "NewGameInSlot", "UiFocus", "StartScreenState" }, bus.Order);
        PressAndTick(screen, StartButton.Right, StartButton.Confirm, StartButton.Confirm, StartButton.Right, StartButton.Confirm, StartButton.Confirm);
        var start = Assert.Single(bus.Starts);
        Assert.Equal((GameStart.Boy, GameStart.Dog), (start.Character, start.StarterPet));
        Assert.Single(bus.NewGames);
        Assert.Empty(bus.Loads);
        Assert.False(screen.Loaded);
        Assert.Equal(3, screen.GameSlot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnOccupiedSlotAsksOverwriteFirst(bool unreadable)
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots(SlotOf(1, auto: unreadable ? SaveInfo.Unreadable : SaveAt(10))));
        screen.Point((int)TitleItem.NewGame);
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm);
        Assert.Equal(StartStep.Overwrite, screen.Step);
        Assert.Equal(1, screen.Highlight);                               // "No" first
        Assert.Empty(bus.NewGames);

        PressAndTick(screen, StartButton.Confirm);                       // No
        Assert.Equal(StartStep.NewSlot, screen.Step);
        Assert.Equal(0, screen.Highlight);
        Assert.Empty(bus.NewGames);
        PressAndTick(screen, StartButton.Confirm);
        PressAndTick(screen, StartButton.Back);                          // Back is No too
        Assert.Equal(StartStep.NewSlot, screen.Step);
        PressAndTick(screen, StartButton.Confirm, StartButton.Left);
        Assert.Equal(StartStep.Overwrite, screen.Step);
        Assert.Equal(0, screen.Highlight);
        Assert.Empty(bus.NewGames);
        PressAndTick(screen, StartButton.Confirm);                       // Yes

        Assert.Equal(StartStep.Character, screen.Step);
        Assert.Equal(1, Assert.Single(bus.NewGames).Slot);
        Assert.Empty(bus.Loads);
    }

    [Fact]
    public void BackFromTheCharacterGoesToTheSlotsThenTheTitle()
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots());
        PressAndTick(screen, StartButton.Confirm, StartButton.Down, StartButton.Confirm);
        Assert.Equal(StartStep.Character, screen.Step);
        Assert.Equal(2, screen.GameSlot);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.NewSlot, screen.Step);
        Assert.Equal(1, screen.Highlight);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Title, screen.Step);
        Assert.Equal((int)TitleItem.NewGame, screen.Highlight);
        Assert.Equal(2, Assert.Single(bus.NewGames).Slot);
        Assert.Empty(bus.Starts);
    }

    [Fact]
    public void PointingBeyondAMenuIsIgnored()
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots(SlotOf(1, manual: SaveAt(1))));
        screen.Point(StartScreenModule.MaxOptions);
        screen.Point(3);
        screen.Point(-1);
        Tick(screen);
        Assert.Equal((int)TitleItem.Continue, screen.Highlight);
        PressAndTick(screen, StartButton.Down, StartButton.Confirm);    // the load step
        screen.Point(3);
        Tick(screen);
        Assert.Equal(0, screen.Highlight);
    }

    [Fact]
    public void UiFocusIsOpenEveryTickWhileOpenThenClosedOnce()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        for (int i = 0; i < 5; i++)
            Tick(screen);
        Assert.Equal(5, bus.Focus.Count);
        Assert.All(bus.Focus, f => Assert.Equal((StartScreenModule.FocusOwner, true), (f.Owner, f.Open)));
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
        Assert.Equal(6, bus.Focus.Count);
        Assert.Equal((StartScreenModule.FocusOwner, false), (bus.Focus[^1].Owner, bus.Focus[^1].Open));
        for (int i = 0; i < 5; i++)
            Tick(screen);
        Assert.Equal(6, bus.Focus.Count);
    }

    [Fact]
    public void AfterALoadPressesAreIgnoredAndNothingIsPublishedAgain()
    {
        var bus = new FakeBus();
        var screen = WithSlots(bus, Slots(SlotOf(1, manual: SaveAt(1))));
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Back);
        for (int i = 0; i < 5; i++)
            PressAndTick(screen, StartButton.Confirm, StartButton.Back);
        Assert.Single(bus.Loads);
        Assert.Single(bus.Starts);
        Assert.Equal(StartStep.Started, screen.Step);
        Assert.Single(bus.Focus.FindAll(f => !f.Open));
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new StartScreenModule().Tick(new TickContext(0, 1.0 / 60)));
    }

    // ---- Prompts: the hint under each step shows only the active device's buttons, as icons ----

    private static readonly StartStep[] ShownSteps =
    {
        StartStep.Title, StartStep.Load, StartStep.LoadSave, StartStep.NewSlot, StartStep.Overwrite,
        StartStep.Character, StartStep.Look, StartStep.Pet, StartStep.Ready,
    };

    private static readonly DeviceKind[] Devices =
    {
        DeviceKind.KeyboardMouse, DeviceKind.Xbox, DeviceKind.PlayStation, DeviceKind.Nintendo, DeviceKind.Generic,
    };

    /// <summary>The icon file name prefixes a device's prompts may use.</summary>
    private static string[] OwnPrefixes(DeviceKind device) => device switch
    {
        DeviceKind.KeyboardMouse => new[] { "key_", "mouse_" },
        DeviceKind.PlayStation => new[] { "ps_" },
        DeviceKind.Nintendo => new[] { "ns_" },
        _ => new[] { "xbox_" },
    };

    /// <summary>The glyph names of every [img] in a hint, and the hint with them removed.</summary>
    private static (List<string> Glyphs, string Words) SplitHint(string hint)
    {
        var glyphs = new List<string>();
        var matches = System.Text.RegularExpressions.Regex.Matches(hint, @"\[img=(\d+)\](.*?)\[/img\]");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            string path = match.Groups[2].Value;
            Assert.StartsWith(Prompts.GlyphDirectory, path);
            Assert.EndsWith(".svg", path);
            glyphs.Add(path.Substring(Prompts.GlyphDirectory.Length, path.Length - Prompts.GlyphDirectory.Length - 4));
        }
        return (glyphs, System.Text.RegularExpressions.Regex.Replace(hint, @"\[img=\d+\].*?\[/img\]", "#"));
    }

    [Theory]
    [InlineData(DeviceKind.KeyboardMouse)]
    [InlineData(DeviceKind.Xbox)]
    [InlineData(DeviceKind.PlayStation)]
    [InlineData(DeviceKind.Nintendo)]
    [InlineData(DeviceKind.Generic)]
    public void EveryHintUsesOnlyTheDevicesOwnIcons(DeviceKind device)
    {
        var own = OwnPrefixes(device);
        foreach (var step in ShownSteps)
        {
            var (glyphs, _) = SplitHint(StartPrompts.Hint(step, device, 28));
            Assert.NotEmpty(glyphs);
            foreach (var glyph in glyphs)
                Assert.True(Array.Exists(own, p => glyph.StartsWith(p, StringComparison.Ordinal)),
                    $"{step} on {device} shows {glyph}");
            // Confirm is on every step; Back on all but the title.
            Assert.Contains(Prompts.Glyph(PromptAction.Confirm, device), glyphs);
            if (step == StartStep.Title)
                Assert.DoesNotContain(Prompts.Glyph(PromptAction.Back, device), glyphs);
            else
                Assert.Contains(Prompts.Glyph(PromptAction.Back, device), glyphs);
        }
    }

    [Fact]
    public void HintsShowTheRightIconsForEachDevice()
    {
        string Hint(StartStep step, DeviceKind device) => StartPrompts.Hint(step, device, 28);

        Assert.Contains("[img=28]res://modules/Input/Glyphs/key_enter.svg[/img] OK", Hint(StartStep.Title, DeviceKind.KeyboardMouse));
        Assert.Contains("[img=28]res://modules/Input/Glyphs/xbox_a.svg[/img] OK", Hint(StartStep.Title, DeviceKind.Xbox));
        Assert.Contains("[img=28]res://modules/Input/Glyphs/ps_cross.svg[/img] OK", Hint(StartStep.Title, DeviceKind.PlayStation));
        Assert.Contains("[img=28]res://modules/Input/Glyphs/ns_b.svg[/img] OK", Hint(StartStep.Title, DeviceKind.Nintendo));
        Assert.Contains("xbox_b.svg[/img] Back", Hint(StartStep.Character, DeviceKind.Xbox));
        Assert.Contains("ps_circle.svg[/img] Back", Hint(StartStep.Pet, DeviceKind.PlayStation));
        Assert.Contains("key_esc.svg[/img] Back", Hint(StartStep.Load, DeviceKind.KeyboardMouse));
        Assert.Contains("xbox_a.svg[/img] Start!", Hint(StartStep.Ready, DeviceKind.Generic));

        // The look step: turning on its own line, then rows, values, next and back.
        string keys = Hint(StartStep.Look, DeviceKind.KeyboardMouse);
        Assert.Equal(new[] { "key_q", "key_e", "mouse_move", "key_up", "key_down", "key_left", "key_right", "key_enter", "key_esc" },
            SplitHint(keys).Glyphs.ToArray());
        string pad = Hint(StartStep.Look, DeviceKind.PlayStation);
        Assert.Equal(new[] { "ps_l1", "ps_r1", "ps_rs", "ps_dpad_up", "ps_dpad_down", "ps_dpad_left", "ps_dpad_right", "ps_cross", "ps_circle" },
            SplitHint(pad).Glyphs.ToArray());
        Assert.Single(pad.Split('\n'), line => line.EndsWith(" Turn", StringComparison.Ordinal));
        Assert.Equal(2, pad.Split('\n').Length);

        Assert.Equal("", Hint(StartStep.Started, DeviceKind.Xbox));
    }

    [Theory]
    [InlineData(DeviceKind.KeyboardMouse)]
    [InlineData(DeviceKind.Xbox)]
    [InlineData(DeviceKind.PlayStation)]
    [InlineData(DeviceKind.Nintendo)]
    public void HintsNameNoButtonInWords(DeviceKind device)
    {
        // No "E / X: OK" or "Esc/(B)" text: every button is an icon, the words only say what it does.
        string[] buttonWords = { "Enter", "Esc", "(A)", "(B)", "LB", "RB", "Arrows", "D-pad", "stick", "Cross", "Circle", "/", ":" };
        foreach (var step in ShownSteps)
        {
            var (_, words) = SplitHint(StartPrompts.Hint(step, device, 28));
            foreach (var word in buttonWords)
                Assert.DoesNotContain(word, words);
            Assert.DoesNotContain("[", words);
        }
    }

    [Fact]
    public void IconsTakeTheSizeAsked()
    {
        foreach (var size in new[] { 16, 28, 64 })
        {
            string hint = StartPrompts.Hint(StartStep.Look, DeviceKind.Xbox, size);
            var sizes = System.Text.RegularExpressions.Regex.Matches(hint, @"\[img=(\d+)\]");
            Assert.NotEmpty(sizes);
            foreach (System.Text.RegularExpressions.Match match in sizes)
                Assert.Equal(size.ToString(System.Globalization.CultureInfo.InvariantCulture), match.Groups[1].Value);
        }
    }

    [Fact]
    public void TheHintFollowsTheActiveDeviceAndTheStep()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        Assert.Contains(typeof(ActiveDevice), bus.Subscribed);
        Assert.Equal(DeviceKind.KeyboardMouse, screen.Device);
        Assert.Equal(StartPrompts.Hint(StartStep.Title, DeviceKind.KeyboardMouse, 30), screen.Hint(30));

        screen.Handle(new ActiveDevice(DeviceKind.PlayStation));
        Assert.Equal(DeviceKind.PlayStation, screen.Device);
        Assert.Equal(StartPrompts.Hint(StartStep.Title, DeviceKind.PlayStation, 30), screen.Hint(30));
        Assert.DoesNotContain("key_", screen.Hint(30));

        screen.Handle(new ActiveDevice(DeviceKind.Xbox));
        screen.Press(StartButton.Confirm);   // New Game
        screen.Tick(new TickContext(1, 1.0 / 60));
        Assert.Equal(StartStep.NewSlot, screen.Step);
        Assert.Equal(StartPrompts.Hint(StartStep.NewSlot, DeviceKind.Xbox, 30), screen.Hint(30));
        Assert.DoesNotContain("ps_", screen.Hint(30));

        screen.Handle(new ActiveDevice(DeviceKind.KeyboardMouse));
        Assert.Contains("key_esc", screen.Hint(30));
        Assert.DoesNotContain("xbox_", screen.Hint(30));
    }
}
