using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Fungtopia.DayNight;
using Fungtopia.Dialogue;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Monsters;
using Fungtopia.Pet;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.Wallet;
using Xunit;

namespace Fungtopia.NPCs.Tests;

public class NPCsModuleTests
{
    private const double Step = 1.0 / 60;
    private const int Jeremy = NPCsModule.Jeremy, Millie = NPCsModule.Millie, Lucy = NPCsModule.Lucy;

    /// <summary>Fake of the shell's bus: records what NPCs publishes; a fake wallet answers purchases.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public readonly List<Type> Subscribed = new();
        public readonly List<object> Published = new();
        public Action<SpendCoins>? OnSpend;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct => Subscribed.Add(typeof(T));

        public void Publish<T>(in T message) where T : struct
        {
            Published.Add(message);
            if (message is SpendCoins spend)
                OnSpend?.Invoke(spend);
        }

        public List<T> All<T>() => Published.OfType<T>().ToList();
    }

    /// <summary>
    /// The NPCs module in a tiny world: a fake Dialogue (shows the last SayLine; Interact advances or
    /// closes it, as the real one does at its tick before NPCs'), a fake player and a fake wallet.
    /// </summary>
    private sealed class Rig
    {
        public readonly FakeBus Bus = new();
        public readonly NPCsModule Npcs = new();
        public Vector3 Position;
        public bool Alive = true;
        public int Coins;
        private long _tick;

        public bool Open;
        public SayLine Shown;

        public Rig(Vector3 position)
        {
            Position = position;
            Npcs.Start(Bus);
            Bus.OnSpend = spend =>
            {
                bool ok = Coins >= spend.Amount;
                if (ok)
                    Coins -= spend.Amount;
                Npcs.Handle(new SpendResult(spend.RequestId, ok, Coins));
            };
        }

        public void Tick(bool interact = false)
        {
            // Modules tick in name order: Dialogue and Input before NPCs, Player after (its last state).
            if (interact && Open)
            {
                Npcs.Handle(new DialogueAdvanced(Shown.SpeakerId));
                if (Shown.Last)
                {
                    Open = false;
                    Npcs.Handle(new DialogueClosed(Shown.SpeakerId));
                }
            }
            Npcs.Handle(new DialogueState(Open, Open ? Shown.SpeakerId : -1));
            Npcs.Handle(new PlayerInput(Vector2.Zero, Vector2.Zero, false, false, false, Vector3.Zero, interact, false));
            Npcs.Handle(new PlayerState(Position, 0f, Vector3.Zero, true, 10, 10, Alive, CharacterKind.Girl));
            int before = Bus.Published.Count;
            Npcs.Tick(new TickContext(_tick++, Step));
            for (int i = before; i < Bus.Published.Count; i++)
                if (Bus.Published[i] is SayLine line)
                {
                    Shown = line;
                    Open = true;
                }
        }

        public void Wait(int ticks)
        {
            for (int i = 0; i < ticks; i++)
                Tick();
        }

        /// <summary>Presses Interact, then lets a tick pass (the next line comes on NPCs' tick).</summary>
        public void Press()
        {
            Tick(interact: true);
            Tick();
        }

        /// <summary>Presses through every line of the open conversation; returns their texts.</summary>
        public List<string> ReadAll()
        {
            var texts = new List<string>();
            for (int guard = 0; Open && guard < 10; guard++)
            {
                texts.Add(Shown.Text);
                Press();
            }
            return texts;
        }

        public List<SayLine> Lines => Bus.All<SayLine>();
        public QuestState Quest => Bus.All<QuestState>()[^1];
    }

    private static Vector3 Near(int npc, float distance = 1.5f)
    {
        var spot = NPCsModule.Spots[npc];
        return spot.Position + spot.Facing * distance;
    }

    private static readonly Vector3 Far = new(0f, 0f, 0f);
    private static readonly Vector3 InForest = new(10f, 0f, 10f);
    private static readonly Vector3 InTown = new(0f, 0f, 230f);

    private static void Kill(Rig rig, MonsterKind kind, int count)
    {
        for (int i = 0; i < count; i++)
            rig.Npcs.Handle(new MonsterKilled(i, kind, MonsterRewards.For(kind), InForest, 0));
        rig.Tick();
    }

    [Fact]
    public void StandsAtThePlacesSpots()
    {
        Assert.Equal(Places.ClinicDoor.Position, NPCsModule.Position(Jeremy));
        Assert.Equal(Places.MillieSpot.Position, NPCsModule.Position(Millie));
        Assert.Equal(Places.LucySpot.Position, NPCsModule.Position(Lucy));
        var rig = new Rig(Far);
        rig.Wait(5);
        for (int i = 0; i < NPCsModule.Count; i++)
            Assert.Equal(NPCsModule.Spots[i].Yaw, rig.Npcs.Yaw[i], 4);
    }

    [Theory]
    [InlineData(Jeremy)]
    [InlineData(Millie)]
    [InlineData(Lucy)]
    public void PromptShowsWithinTwoAndAHalfMeters(int npc)
    {
        var rig = new Rig(Far);
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.PromptNpc);

        rig.Position = Near(npc, 2.6f);
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.PromptNpc);

        rig.Position = Near(npc, 2.4f);
        rig.Tick();
        Assert.Equal(npc, rig.Npcs.PromptNpc);
        Assert.Equal("E / X: Talk", rig.Npcs.PromptText);

        rig.Alive = false;
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.PromptNpc);
    }

    [Fact]
    public void PromptHidesWhileTalking()
    {
        var rig = new Rig(Near(Jeremy));
        rig.Press();
        Assert.True(rig.Open);
        Assert.Equal(-1, rig.Npcs.PromptNpc);
        rig.ReadAll();
        rig.Tick();
        Assert.Equal(Jeremy, rig.Npcs.PromptNpc);
    }

    [Fact]
    public void TurnsToFaceANearPlayerAndBack()
    {
        var spot = NPCsModule.Spots[Millie];
        // Off to the NPC's side, 3 m away: facing it needs a quarter turn.
        var side = new Vector3(spot.Facing.Z, 0f, -spot.Facing.X);
        var rig = new Rig(spot.Position + side * 3f);
        rig.Tick();
        float firstStep = MathF.Abs(rig.Npcs.Yaw[Millie] - spot.Yaw);
        Assert.InRange(firstStep, 0.01f, NPCsModule.TurnSpeed / 60f + 1e-4f);
        rig.Wait(60);
        float yaw = rig.Npcs.Yaw[Millie];
        var facing = new Vector3(-MathF.Sin(yaw), 0f, -MathF.Cos(yaw));
        Assert.True(Vector3.Dot(facing, side) > 0.999f);

        rig.Position = Far;
        rig.Wait(60);
        Assert.Equal(spot.Yaw, rig.Npcs.Yaw[Millie], 3);
    }

    [Fact]
    public void InteractFarAwayOrWhileAnotherBubbleIsOpenDoesNothing()
    {
        var rig = new Rig(Far);
        rig.Press();
        Assert.Empty(rig.Lines);

        rig.Position = Near(Jeremy);
        rig.Open = true;
        rig.Shown = new SayLine(42, "Someone", "Hi", Voices.Neutral, false);
        rig.Tick(interact: true);
        rig.Tick();
        Assert.Empty(rig.Lines);
        Assert.Equal(-1, rig.Npcs.PromptNpc);
    }

    [Fact]
    public void ConversationGoesLineByLineAndTheClosingPressDoesNotRestartIt()
    {
        var rig = new Rig(Near(Jeremy));
        rig.Tick(interact: true);
        var first = Assert.Single(rig.Lines);
        Assert.Equal(NPCsModule.SpeakerId(Jeremy), first.SpeakerId);
        Assert.Equal("Dr. Jeremy", first.Speaker);
        Assert.Equal(Voices.Kind, first.Voice);
        Assert.False(first.Last);
        Assert.Equal(Jeremy, rig.Npcs.Talking);

        // Nothing more until the player advances.
        rig.Wait(30);
        Assert.Single(rig.Lines);

        rig.Press();
        Assert.Equal(2, rig.Lines.Count);
        Assert.True(rig.Lines[^1].Last);

        // Dismissing the last line closes it; that same press does not start a new talk.
        rig.Press();
        Assert.False(rig.Open);
        Assert.Equal(2, rig.Lines.Count);
        Assert.Equal(-1, rig.Npcs.Talking);

        // A new press talks again, with another health tip.
        rig.Press();
        Assert.Equal(3, rig.Lines.Count);
        rig.Press();
        Assert.NotEqual(rig.Lines[1].Text, rig.Lines[^1].Text);
    }

    [Fact]
    public void JeremyGreetsYouOnWakingUpWithoutInteract()
    {
        var rig = new Rig(Far);
        rig.Wait(3);
        Assert.Empty(rig.Lines);
        rig.Position = Places.ClinicDoor.Position;
        rig.Npcs.Handle(new WokeUp(2));
        rig.Tick();
        var line = Assert.Single(rig.Lines);
        Assert.Equal("Good morning! You fainted in the forest, but you're all patched up now.", line.Text);
        Assert.Equal(NPCsModule.SpeakerId(Jeremy), line.SpeakerId);
        Assert.Equal(Voices.Kind, line.Voice);
        var texts = rig.ReadAll();
        Assert.Equal(2, texts.Count);
        Assert.False(rig.Open);
        // Only once per WokeUp.
        rig.Wait(10);
        Assert.Equal(2, rig.Lines.Count);
    }

    [Fact]
    public void MillieGivesTheFirstQuestAndItsProgressCounts()
    {
        var rig = new Rig(Near(Millie));
        rig.Tick();
        Assert.False(rig.Quest.Active);
        // Kills before the quest is given do not count.
        Kill(rig, MonsterKind.Caterpillar, 2);

        rig.Press();
        Assert.Equal("Millie", rig.Lines[0].Speaker);
        Assert.Equal(Voices.Bubbly, rig.Lines[0].Voice);
        var texts = rig.ReadAll();
        Assert.Contains(texts, t => t.Contains("Defeat 5 Mushroom Caterpillars"));
        Assert.Contains(texts, t => t.StartsWith("Tip!"));
        var quest = rig.Quest;
        Assert.True(quest.Active);
        Assert.Equal("Defeat 5 Mushroom Caterpillars", quest.Title);
        Assert.Equal(0, quest.Progress);
        Assert.Equal(5, quest.Goal);
        Assert.False(quest.Complete);
        Assert.Equal(150, rig.Npcs.QuestReward);

        Kill(rig, MonsterKind.Caterpillar, 3);
        Kill(rig, MonsterKind.Ant, 4);
        Assert.Equal(3, rig.Quest.Progress);
        Assert.False(rig.Quest.Complete);

        // Talking before it is done pays nothing and tells the progress.
        rig.Press();
        Assert.Contains("3 / 5", rig.Shown.Text);
        rig.ReadAll();
        Assert.Empty(rig.Bus.All<GrantCoins>());

        Kill(rig, MonsterKind.Caterpillar, 4);
        Assert.Equal(5, rig.Quest.Progress);
        Assert.True(rig.Quest.Complete);
        Assert.Empty(rig.Bus.All<GrantCoins>());
    }

    [Fact]
    public void QuestsPayWhenDoneAndComeOneAtATimeWithBiggerNumbers()
    {
        var rig = new Rig(Near(Millie));
        rig.Press();
        rig.ReadAll();

        Kill(rig, MonsterKind.Caterpillar, 5);
        rig.Press();
        var grant = Assert.Single(rig.Bus.All<GrantCoins>());
        Assert.Equal(150, grant.Amount);
        Assert.Contains(rig.ReadAll(), t => t.Contains("Defeat 3 Mushroom Ants"));
        Assert.Equal("Defeat 3 Mushroom Ants", rig.Quest.Title);
        Assert.Equal(0, rig.Quest.Progress);
        Assert.False(rig.Quest.Complete);

        // Talking again right away does not pay twice.
        rig.Press();
        rig.ReadAll();
        Assert.Single(rig.Bus.All<GrantCoins>());

        Kill(rig, MonsterKind.Caterpillar, 3);
        Kill(rig, MonsterKind.Ant, 3);
        Assert.True(rig.Quest.Complete);
        rig.Press();
        rig.ReadAll();
        Assert.Equal(150, rig.Bus.All<GrantCoins>()[^1].Amount);
        Assert.Equal(500, rig.Quest.Goal);
        Assert.Contains("earn 500 M¢ in the forest", rig.Quest.Title);

        // Coins earned outside the forest (Millie's own reward in town) do not count.
        rig.Npcs.Handle(new CoinsEarned(150, InTown));
        rig.Npcs.Handle(new CoinsEarned(MonsterRewards.Normal, InForest));
        rig.Tick();
        Assert.Equal(50, rig.Quest.Progress);
        for (int i = 0; i < 9; i++)
            rig.Npcs.Handle(new CoinsEarned(MonsterRewards.Normal, InForest));
        rig.Npcs.Handle(new CoinsEarned(MonsterRewards.Hard, new Vector3(-200f, 0f, 0f)));
        rig.Tick();
        Assert.Equal(500, rig.Quest.Progress);
        Assert.True(rig.Quest.Complete);

        rig.Press();
        rig.ReadAll();
        Assert.Equal(new[] { 150, 150, 200 }, rig.Bus.All<GrantCoins>().Select(g => g.Amount));
        Assert.Equal("Defeat 10 Mushroom Caterpillars", rig.Quest.Title);
        Assert.Equal(300, rig.Npcs.QuestReward);
        Assert.Equal(3, rig.Npcs.QuestsDone);
    }

    [Fact]
    public void QuestTableRepeatsWithBiggerNumbers()
    {
        Assert.Equal((NPCsModule.QuestKind.Caterpillars, 5, 150), NPCsModule.Quest(0));
        Assert.Equal((NPCsModule.QuestKind.Ants, 3, 150), NPCsModule.Quest(1));
        Assert.Equal((NPCsModule.QuestKind.ForestCoins, 500, 200), NPCsModule.Quest(2));
        Assert.Equal((NPCsModule.QuestKind.Caterpillars, 10, 300), NPCsModule.Quest(3));
        Assert.Equal((NPCsModule.QuestKind.Ants, 9, 450), NPCsModule.Quest(7));
    }

    [Fact]
    public void QuestStateIsPublishedEveryTick()
    {
        var rig = new Rig(Far);
        rig.Wait(7);
        Assert.Equal(7, rig.Bus.All<QuestState>().Count);
        Assert.Equal("", rig.Quest.Title);
        Assert.Equal(0, rig.Quest.Goal);
    }

    /// <summary>Talks to Lucy through her offer; afterwards she offers the crow.</summary>
    private static void HearTheOffer(Rig rig)
    {
        int spends = rig.Bus.All<SpendCoins>().Count;
        rig.Press();
        Assert.Equal("Lucy", rig.Shown.Speaker);
        Assert.Equal(Voices.Deadpan, rig.Shown.Voice);
        var texts = rig.ReadAll();
        Assert.Contains(texts, t => t.Contains("Mushroom Crow") && t.Contains("3,000 M¢"));
        Assert.Equal("Press E / X again to buy.", texts[^1]);
        rig.Tick();
        Assert.True(rig.Npcs.OfferingCrow);
        Assert.Equal(Lucy, rig.Npcs.PromptNpc);
        Assert.Equal(NPCsModule.BuyPrompt, rig.Npcs.PromptText);
        Assert.Equal(spends, rig.Bus.All<SpendCoins>().Count);
    }

    [Fact]
    public void BuyingTheCrowSpendsCoinsAdoptsItAndLucyCongratulates()
    {
        var rig = new Rig(Near(Lucy)) { Coins = 3200 };
        HearTheOffer(rig);

        rig.Tick(interact: true);
        var spend = Assert.Single(rig.Bus.All<SpendCoins>());
        Assert.Equal(3000, spend.Amount);
        rig.Tick();
        Assert.Equal(200, rig.Coins);
        var adopt = Assert.Single(rig.Bus.All<AdoptPet>());
        Assert.Equal(PetKind.Crow, adopt.Kind);
        Assert.Equal("Congrats. It bites.", rig.Shown.Text);
        Assert.True(rig.Shown.Last);
        Assert.Equal(Voices.Deadpan, rig.Shown.Voice);
        rig.ReadAll();

        // Owned now: no second offer.
        rig.Press();
        rig.ReadAll();
        rig.Tick();
        Assert.False(rig.Npcs.OfferingCrow);
        rig.Press();
        rig.ReadAll();
        Assert.Single(rig.Bus.All<SpendCoins>());
        Assert.Single(rig.Bus.All<AdoptPet>());
    }

    [Fact]
    public void NotEnoughCoinsMeansComeBackLater()
    {
        var rig = new Rig(Near(Lucy)) { Coins = 2999 };
        HearTheOffer(rig);
        rig.Press();
        Assert.Single(rig.Bus.All<SpendCoins>());
        Assert.Empty(rig.Bus.All<AdoptPet>());
        Assert.Equal(2999, rig.Coins);
        Assert.Equal("Come back with 3,000 M¢.", rig.Shown.Text);
        Assert.True(rig.Shown.Last);
        rig.ReadAll();
        rig.Tick();
        Assert.False(rig.Npcs.OfferingCrow);

        // Later, with the coins, the offer and the purchase work.
        rig.Coins = 3000;
        HearTheOffer(rig);
        rig.Press();
        Assert.Single(rig.Bus.All<AdoptPet>());
        Assert.Equal(0, rig.Coins);
    }

    [Fact]
    public void WalkingAwaySaysNoToTheCrow()
    {
        var rig = new Rig(Near(Lucy)) { Coins = 5000 };
        HearTheOffer(rig);
        rig.Position = Far;
        rig.Tick();
        Assert.False(rig.Npcs.OfferingCrow);
        rig.Position = Near(Lucy);
        rig.Press();
        Assert.Empty(rig.Bus.All<SpendCoins>());
        Assert.Equal("Lucy", rig.Shown.Speaker);
        Assert.False(rig.Shown.Last);
    }

    [Fact]
    public void LucyDoesNotSellACrowYouOwn()
    {
        var rig = new Rig(Near(Lucy)) { Coins = 5000 };
        rig.Npcs.Handle(new OwnedPets(true, false, true, PetKind.Cat));
        rig.Press();
        var texts = rig.ReadAll();
        Assert.Contains(texts, t => t.Contains("already have the crow"));
        rig.Tick();
        Assert.False(rig.Npcs.OfferingCrow);
        rig.Press();
        rig.ReadAll();
        Assert.Empty(rig.Bus.All<SpendCoins>());
    }

    [Fact]
    public void SubscribesToWhatItReads()
    {
        var rig = new Rig(Far);
        foreach (var type in new[]
                 {
                     typeof(PlayerInput), typeof(PlayerState), typeof(DialogueState), typeof(DialogueAdvanced),
                     typeof(DialogueClosed), typeof(WokeUp), typeof(MonsterKilled), typeof(CoinsEarned),
                     typeof(SpendResult), typeof(OwnedPets),
                 })
            Assert.Contains(type, rig.Bus.Subscribed);
    }

    [Fact]
    public void TurnTowardTakesTheShortWayRound()
    {
        Assert.Equal(3.0f, NPCsModule.TurnToward(3.0f, 3.0f, 0.1f), 5);
        float turned = NPCsModule.TurnToward(3.0f, -3.0f, 0.1f);
        Assert.True(turned > 3.0f || turned < -3.0f);
        Assert.Equal(0.5f, NPCsModule.TurnToward(0f, 1f, 0.5f), 5);
    }
}
