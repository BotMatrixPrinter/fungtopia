using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Input;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.Dialogue.Tests;

public class DialogueModuleTests
{
    private const double Step = 1.0 / 60;
    private const int Millie = 7;

    /// <summary>Fake of the shell's bus: delivers published messages to subscribers and records them.</summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<object> Published = new();

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
                foreach (var handler in list.ToArray())
                    ((IMessageHandler<T>)handler).Handle(message);
        }

        public bool Has<T>() where T : struct => _handlers.ContainsKey(typeof(T));

        public List<T> All<T>() where T : struct
        {
            var found = new List<T>();
            foreach (var message in Published)
                if (message is T typed)
                    found.Add(typed);
            return found;
        }
    }

    /// <summary>A fake NPC: says its lines one at a time, the next one on each DialogueAdvanced for it.</summary>
    private sealed class FakeNpc : IMessageHandler<DialogueAdvanced>
    {
        private readonly FakeBus _bus;
        private readonly int _id;
        private readonly string[] _lines;
        private int _next;

        public FakeNpc(FakeBus bus, int id, params string[] lines)
        {
            _bus = bus;
            _id = id;
            _lines = lines;
            bus.Subscribe(this);
        }

        public void Talk() => SayNext();

        public void Handle(in DialogueAdvanced message)
        {
            if (message.SpeakerId == _id && _next < _lines.Length)
                SayNext();
        }

        private void SayNext()
        {
            _bus.Publish(new SayLine(_id, "Millie", _lines[_next], Voices.Bubbly, _next == _lines.Length - 1));
            _next++;
        }
    }

    private readonly FakeBus _bus = new();
    private readonly DialogueModule _dialogue = new();
    private long _tick;

    public DialogueModuleTests() => _dialogue.Start(_bus);

    private void Tick(int count = 1)
    {
        for (int i = 0; i < count; i++)
            _dialogue.Tick(new TickContext(_tick++, Step));
    }

    private void Press() =>
        _bus.Publish(new PlayerInput(Vector2.Zero, Vector2.Zero, false, false, false, Vector3.Zero, true, false));

    private void Say(string text, bool last = false, int speaker = Millie, int voice = Voices.Bubbly) =>
        _bus.Publish(new SayLine(speaker, "Millie", text, voice, last));

    private static int TicksToType(string text) => (int)Math.Ceiling(text.Length / DialogueModule.LettersPerSecond * 60) + 1;

    private DialogueState LastState => _bus.All<DialogueState>()[^1];

    [Fact]
    public void ListensToLinesAndInput()
    {
        Assert.True(_bus.Has<SayLine>());
        Assert.True(_bus.Has<PlayerInput>());
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new DialogueModule().Tick(new TickContext(0, Step)));
    }

    // States

    [Fact]
    public void PublishesAClosedStateEveryTickWhenNobodyTalks()
    {
        Tick(5);
        var states = _bus.All<DialogueState>();
        Assert.Equal(5, states.Count);
        Assert.All(states, s => Assert.False(s.Open));
        Assert.All(states, s => Assert.Equal(-1, s.SpeakerId));
        Assert.False(_dialogue.Open);
    }

    [Fact]
    public void PublishesAnOpenStateWithTheSpeakerEveryTickWhileALineIsShown()
    {
        Say("Hi!");
        Tick(3);
        var states = _bus.All<DialogueState>();
        Assert.Equal(3, states.Count);
        Assert.All(states, s => Assert.True(s.Open));
        Assert.All(states, s => Assert.Equal(Millie, s.SpeakerId));
    }

    // Smart A: an open dialogue is an interaction, so the gamepad's A advances it instead of jumping.

    [Fact]
    public void OpenDialogueIsAvailableForInteractUntilItCloses()
    {
        var npc = new FakeNpc(_bus, Millie, "Hi!", "Bye!");
        Tick(3);
        Assert.Empty(_bus.All<InteractAvailable>());
        npc.Talk();
        Tick();
        var available = Assert.Single(_bus.All<InteractAvailable>());
        Assert.Equal(DialogueModule.InteractOwner, available.Owner);
        Assert.True(available.Available);
        // Typing out, waiting for the next line and the next line all stay available, sent only once.
        Tick(TicksToType("Hi!"));
        Press();
        Tick(TicksToType("Bye!"));
        Assert.True(_dialogue.Open);
        Assert.Single(_bus.All<InteractAvailable>());
        Press();
        Tick();
        Assert.False(_dialogue.Open);
        var all = _bus.All<InteractAvailable>();
        Assert.Equal(2, all.Count);
        Assert.Equal(DialogueModule.InteractOwner, all[1].Owner);
        Assert.False(all[1].Available);
        Tick(3);
        Assert.Equal(2, _bus.All<InteractAvailable>().Count);
    }

    [Fact]
    public void InteractWhileClosedDoesNothing()
    {
        Tick();
        Press();
        Tick();
        Assert.Empty(_bus.All<DialogueAdvanced>());
        Assert.Empty(_bus.All<DialogueClosed>());
        Assert.False(LastState.Open);
    }

    // Line flow

    [Fact]
    public void ShowsTheLineAndTypesItOutLetterByLetter()
    {
        Say("Hello, neighbor!", voice: Voices.Kind);
        Assert.True(_dialogue.Open);
        Assert.Equal("Millie", _dialogue.Speaker);
        Assert.Equal("Hello, neighbor!", _dialogue.Text);
        Assert.Equal(Voices.Kind, _dialogue.Voice);
        Assert.Equal(0, _dialogue.VisibleLetters);
        Assert.True(_dialogue.Typing);
        Assert.False(_dialogue.CanContinue);

        int shown = 0;
        for (int i = 0; i < TicksToType("Hello, neighbor!"); i++)
        {
            Tick();
            Assert.True(_dialogue.VisibleLetters >= shown);
            Assert.True(_dialogue.VisibleLetters - shown <= 1);
            shown = _dialogue.VisibleLetters;
        }
        Assert.Equal(16, _dialogue.VisibleLetters);
        Assert.False(_dialogue.Typing);
        Assert.True(_dialogue.CanContinue);
    }

    [Fact]
    public void TypingTakesTheTextLengthOverTheLetterRate()
    {
        Say("0123456789");
        Tick(14);
        Assert.Equal(9, _dialogue.VisibleLetters);
        Tick();
        Assert.Equal(10, _dialogue.VisibleLetters);
    }

    [Fact]
    public void ANewLineReplacesTheShownOneAndStartsTypingAgain()
    {
        Say("First.");
        Tick(TicksToType("First."));
        Say("Second line.", speaker: 9, voice: Voices.Deadpan);
        Assert.Equal("Second line.", _dialogue.Text);
        Assert.Equal(9, _dialogue.SpeakerId);
        Assert.Equal(Voices.Deadpan, _dialogue.Voice);
        Assert.Equal(0, _dialogue.VisibleLetters);
        Tick();
        Assert.Equal(9, LastState.SpeakerId);
    }

    [Fact]
    public void AnUnknownVoiceIsNeutral()
    {
        Say("Hm.", voice: 12);
        Assert.Equal(Voices.Neutral, _dialogue.Voice);
    }

    [Fact]
    public void WalksThroughAnNpcsLinesOneAtATimeAndThenCloses()
    {
        var npc = new FakeNpc(_bus, Millie, "Good morning!", "Dr. Jeremy is open today.", "See you!");
        npc.Talk();
        var shown = new List<string>();
        for (int line = 0; line < 3; line++)
        {
            Tick(TicksToType(_dialogue.Text));
            shown.Add(_dialogue.Text);
            Assert.True(_dialogue.CanContinue);
            Press();
            Tick();
        }
        Assert.Equal(new[] { "Good morning!", "Dr. Jeremy is open today.", "See you!" }, shown);
        Assert.Equal(3, _bus.All<DialogueAdvanced>().Count);
        Assert.All(_bus.All<DialogueAdvanced>(), a => Assert.Equal(Millie, a.SpeakerId));
        var closed = Assert.Single(_bus.All<DialogueClosed>());
        Assert.Equal(Millie, closed.SpeakerId);
        Assert.False(_dialogue.Open);
        Assert.False(LastState.Open);
        Tick(10);
        Assert.Single(_bus.All<DialogueClosed>());
    }

    [Fact]
    public void ThePressThatStartsATalkDoesNotSkipTheFirstLine()
    {
        Press();
        Say("Hi!");
        Tick();
        Assert.True(_dialogue.Typing);
        Assert.Empty(_bus.All<DialogueAdvanced>());
    }

    // Typing complete vs advance

    [Fact]
    public void InteractWhileTypingCompletesTheLineWithoutAdvancing()
    {
        Say("A rather long line about mushrooms.");
        Tick(3);
        Assert.True(_dialogue.Typing);
        Press();
        Tick();
        Assert.False(_dialogue.Typing);
        Assert.Equal(_dialogue.Text.Length, _dialogue.VisibleLetters);
        Assert.True(_dialogue.CanContinue);
        Assert.Empty(_bus.All<DialogueAdvanced>());
        Assert.True(LastState.Open);
    }

    [Fact]
    public void InteractWhileTypingTheLastLineCompletesItAndDoesNotClose()
    {
        Say("Bye bye now!", last: true);
        Tick(2);
        Press();
        Tick();
        Assert.True(_dialogue.Open);
        Assert.Empty(_bus.All<DialogueClosed>());
        Press();
        Tick();
        Assert.Single(_bus.All<DialogueAdvanced>());
        Assert.Single(_bus.All<DialogueClosed>());
        Assert.False(_dialogue.Open);
    }

    [Fact]
    public void InteractOnAFullyShownLineAdvancesAndWaitsForTheNextLine()
    {
        Say("Wait here.");
        Tick(TicksToType("Wait here."));
        Press();
        Tick();
        var advanced = Assert.Single(_bus.All<DialogueAdvanced>());
        Assert.Equal(Millie, advanced.SpeakerId);
        Assert.Empty(_bus.All<DialogueClosed>());
        Assert.True(_dialogue.Open);
        Assert.True(_dialogue.Waiting);
        Assert.False(_dialogue.CanContinue);

        Press();
        Tick();
        Assert.Single(_bus.All<DialogueAdvanced>());

        Say("Next!");
        Assert.False(_dialogue.Waiting);
        Assert.Equal("Next!", _dialogue.Text);
    }

    [Fact]
    public void OnePressDoesOneThing()
    {
        Say("Short.");
        Tick(2);
        Press();
        Tick(TicksToType("Short.") + 5);
        Assert.True(_dialogue.CanContinue);
        Assert.Empty(_bus.All<DialogueAdvanced>());
    }

    // Last line closes

    [Fact]
    public void DismissingTheLastLineClosesAndPublishesClosedOnce()
    {
        Say("That's all!", last: true);
        Tick(TicksToType("That's all!"));
        Press();
        Tick();
        Assert.Single(_bus.All<DialogueAdvanced>());
        var closed = Assert.Single(_bus.All<DialogueClosed>());
        Assert.Equal(Millie, closed.SpeakerId);
        Assert.False(_dialogue.Open);
        Assert.Equal(-1, _dialogue.SpeakerId);
        Assert.Equal("", _dialogue.Text);
        Assert.False(LastState.Open);
        Assert.Equal(-1, LastState.SpeakerId);

        int advancedIndex = _bus.Published.FindIndex(m => m is DialogueAdvanced);
        int closedIndex = _bus.Published.FindIndex(m => m is DialogueClosed);
        Assert.True(advancedIndex < closedIndex);

        Press();
        Tick(5);
        Assert.Single(_bus.All<DialogueClosed>());
    }

    [Fact]
    public void ANewTalkAfterClosingOpensAgain()
    {
        Say("Bye!", last: true);
        Tick(TicksToType("Bye!"));
        Press();
        Tick();
        Say("Oh, one more thing!", speaker: 3);
        Tick();
        Assert.True(LastState.Open);
        Assert.Equal(3, LastState.SpeakerId);
    }
}
