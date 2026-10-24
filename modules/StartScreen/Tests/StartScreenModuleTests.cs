using System;
using System.Collections.Generic;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.StartScreen.Tests;

public class StartScreenModuleTests
{
    /// <summary>Fake of the shell's bus: records the start screen's messages in order.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public readonly List<GameStart> Starts = new();
        public readonly List<StartScreenState> States = new();
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
            else
                OtherMessages++;
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
        for (int i = 0; i < 4; i++)
        {
            PressAndTick(screen, StartButton.Confirm);
            seen.Add(screen.Step);
        }
        Assert.Equal(new[] { StartStep.Title, StartStep.Character, StartStep.Pet, StartStep.Ready, StartStep.Started }, seen);
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

        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
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
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
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
        PressAndTick(screen, StartButton.Confirm);
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
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
        Assert.Equal(StartStep.Ready, screen.Step);
        PressAndTick(screen, StartButton.Right, StartButton.Left, StartButton.Right);
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
        PressAndTick(screen, StartButton.Confirm, StartButton.Right, StartButton.Confirm, StartButton.Right, StartButton.Confirm);
        Assert.Equal(StartStep.Ready, screen.Step);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Pet, screen.Step);
        Assert.Equal(GameStart.Dog, screen.StarterPet);
        PressAndTick(screen, StartButton.Back);
        Assert.Equal(StartStep.Character, screen.Step);
        Assert.Equal(GameStart.Boy, screen.Character);
        PressAndTick(screen, StartButton.Left, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);

        var start = Assert.Single(bus.Starts);
        Assert.Equal(GameStart.Girl, start.Character);
        Assert.Equal(GameStart.Dog, start.StarterPet);
    }

    [Fact]
    public void GameStartIsPublishedOnceBeforeTheClosedState()
    {
        var bus = new FakeBus();
        var screen = Started(bus);
        PressAndTick(screen, StartButton.Confirm, StartButton.Confirm, StartButton.Confirm);
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
                     StartButton.Back, StartButton.Confirm);
        for (int i = 0; i < 5; i++)
            PressAndTick(screen, StartButton.Right, StartButton.Back, StartButton.Confirm);
        screen.Point(1);
        Tick(screen);
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

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new StartScreenModule().Tick(new TickContext(0, 1.0 / 60)));
    }
}
