using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Fungtopia.DayNight;
using Fungtopia.Dialogue;
using Fungtopia.Food;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Monsters;
using Fungtopia.Pet;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.Wallet;
using Fungtopia.Wardrobe;
using Xunit;

namespace Fungtopia.NPCs.Tests;

public class NPCsModuleTests
{
    private const double Step = 1.0 / 60;
    private const int Jeremy = NPCsModule.Jeremy, Millie = NPCsModule.Millie, Lucy = NPCsModule.Lucy, Abby = NPCsModule.Abby,
        Pip = NPCsModule.Pip;

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

    /// <summary>In front of where the player talks to <paramref name="npc"/> (Pip: in front of the food stall's counter).</summary>
    private static Vector3 Near(int npc, float distance = 1.5f)
    {
        var spot = NPCsModule.TalkSpots[npc];
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
        Assert.Equal(Places.AbbySpot.Position, NPCsModule.Position(Abby));
        Assert.Equal(5, NPCsModule.Count);
        var rig = new Rig(Far);
        rig.Wait(5);
        for (int i = 0; i < NPCsModule.Count; i++)
            Assert.Equal(NPCsModule.Spots[i].Yaw, rig.Npcs.Yaw[i], 4);
    }

    [Fact]
    public void PipStandsBehindTheFoodStallsCounterFacingIt()
    {
        var counter = Places.FoodSpot;
        var pip = NPCsModule.Spots[Pip];
        // Behind the counter spot (towards the stall), a little to the side, facing the same way: out at the customer.
        var offset = pip.Position - counter.Position;
        Assert.Equal(-NPCsModule.PipBehindCounter, Vector3.Dot(offset, counter.Facing), 3);
        var right = new Vector3(-counter.Facing.Z, 0f, counter.Facing.X);
        Assert.Equal(-NPCsModule.PipBesidePot, Vector3.Dot(offset, right), 3);
        Assert.InRange(NPCsModule.PipBehindCounter, 2.4f, 3.0f);
        Assert.InRange(NPCsModule.PipBesidePot, 0f, 0.8f);
        Assert.Equal(Ground.HeightAt(pip.Position.X, pip.Position.Z), pip.Position.Y, 4);
        Assert.Equal(counter.Yaw, pip.Yaw, 5);
        // Inside the stall's footprint (behind its counter), not out on the square.
        var stall = Buildings.First(BuildingKind.FoodStall);
        Assert.True(stall.Contains(pip.Position.X, pip.Position.Z, 0f));
        Assert.Equal(counter.Position, NPCsModule.TalkSpots[Pip].Position);
        Assert.Equal("Pip", NPCsModule.Name(Pip));
        Assert.Equal(Voices.Bubbly, NPCsModule.Voice(Pip));
    }

    [Theory]
    [InlineData(Jeremy)]
    [InlineData(Millie)]
    [InlineData(Lucy)]
    [InlineData(Abby)]
    [InlineData(Pip)]
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
        Assert.Equal(npc == Pip ? NPCsModule.PromptKind.Food : NPCsModule.PromptKind.Talk, rig.Npcs.Prompt);
        Assert.Equal($"[img=36]res://modules/Input/Glyphs/key_e.svg[/img] {(npc == Pip ? "Food" : "Talk")}", rig.Npcs.PromptText);

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
        // The first quest pays coins only.
        Assert.Empty(rig.Bus.All<GrantItem>());
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
        var thanks = rig.ReadAll();
        Assert.Equal(150, rig.Bus.All<GrantCoins>()[^1].Amount);
        // The second quest also gives the flower clip, once.
        var clip = Assert.Single(rig.Bus.All<GrantItem>());
        Assert.Equal("flower_clip", clip.ItemId);
        Assert.Equal("Millie's quest", clip.Why);
        Assert.True(Catalog.TryGet(clip.ItemId, out var clipItem));
        Assert.Contains(thanks, t => t.Contains("150 M¢") && t.Contains(clipItem.Name));
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
        Assert.Single(rig.Bus.All<GrantItem>());
        Assert.Equal("Defeat 10 Mushroom Caterpillars", rig.Quest.Title);
        Assert.Equal(300, rig.Npcs.QuestReward);
        Assert.Equal(3, rig.Npcs.QuestsDone);

        // The fourth quest gives the mushroom backpack besides 300 M¢.
        Kill(rig, MonsterKind.Caterpillar, 10);
        rig.Press();
        var texts = rig.ReadAll();
        Assert.Equal(300, rig.Bus.All<GrantCoins>()[^1].Amount);
        Assert.Equal(new[] { "flower_clip", "mushroom_backpack" }, rig.Bus.All<GrantItem>().Select(g => g.ItemId));
        Assert.Contains(texts, t => t.Contains("Mushroom Backpack"));
    }

    [Fact]
    public void QuestClothesAreCatalogItemsOnTheSecondAndFourthQuests()
    {
        Assert.Equal("", NPCsModule.QuestItem(0));
        Assert.Equal("flower_clip", NPCsModule.QuestItem(1));
        Assert.Equal("", NPCsModule.QuestItem(2));
        Assert.Equal("mushroom_backpack", NPCsModule.QuestItem(3));
        Assert.Equal("", NPCsModule.QuestItem(4));
        Assert.Equal("", NPCsModule.QuestItem(40));
        Assert.True(Catalog.IndexOf(NPCsModule.QuestItem(1)) >= 0);
        Assert.True(Catalog.IndexOf(NPCsModule.QuestItem(3)) >= 0);
    }

    [Fact]
    public void AbbyGreetsYouThenOpensHerShop()
    {
        var rig = new Rig(Near(Abby));
        rig.Tick();
        Assert.Equal(Abby, rig.Npcs.PromptNpc);
        Assert.Equal(NPCsModule.PromptKind.Talk, rig.Npcs.Prompt);

        rig.Tick(interact: true);
        var line = Assert.Single(rig.Lines);
        Assert.Equal("Abby", line.Speaker);
        Assert.Equal(NPCsModule.SpeakerId(Abby), line.SpeakerId);
        Assert.Equal(Voices.Bubbly, line.Voice);
        Assert.Equal("Ooh, you'd look so cute in something new!", line.Text);
        Assert.True(line.Last);
        // The shop waits until the greeting is dismissed.
        rig.Wait(10);
        Assert.Empty(rig.Bus.All<OpenShop>());

        rig.Press();
        Assert.False(rig.Open);
        Assert.Single(rig.Bus.All<OpenShop>());
        Assert.Equal(-1, rig.Npcs.Talking);

        // While the shop is open, no prompt and Interact does nothing.
        rig.Npcs.Handle(new WardrobeState(true, true));
        rig.Press();
        Assert.Equal(-1, rig.Npcs.PromptNpc);
        Assert.Single(rig.Lines);
        Assert.Single(rig.Bus.All<OpenShop>());

        // Closed again: she can be talked to again.
        rig.Npcs.Handle(new WardrobeState(false, false));
        rig.Tick();
        Assert.Equal(Abby, rig.Npcs.PromptNpc);
        rig.Press();
        rig.Press();
        Assert.Equal(2, rig.Lines.Count);
        Assert.Equal(2, rig.Bus.All<OpenShop>().Count);
    }

    [Fact]
    public void AbbyDoesNotOpenTheShopWhenSomeoneElseTakesTheBubble()
    {
        var rig = new Rig(Near(Abby));
        rig.Tick(interact: true);
        Assert.Equal(Abby, rig.Npcs.Talking);
        rig.Shown = new SayLine(42, "Someone", "Hi", Voices.Neutral, true);
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.Talking);
        rig.Press();
        Assert.Empty(rig.Bus.All<OpenShop>());
    }

    [Fact]
    public void PipsPromptShowsAtTheCounterNotFromBehindTheStall()
    {
        var rig = new Rig(Places.FoodSpot.Position);
        rig.Tick();
        Assert.Equal(Pip, rig.Npcs.PromptNpc);
        Assert.Equal(NPCsModule.PromptKind.Food, rig.Npcs.Prompt);

        // Off to the counter's side, 2.4 m from the spot in front of it: still showing; 2.6 m: gone.
        var side = new Vector3(Places.FoodSpot.Facing.Z, 0f, -Places.FoodSpot.Facing.X);
        rig.Position = Places.FoodSpot.Position + side * 2.4f;
        rig.Tick();
        Assert.Equal(Pip, rig.Npcs.PromptNpc);
        rig.Position = Places.FoodSpot.Position + side * 2.6f;
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.PromptNpc);

        // Behind the stall, close to Pip but on the far side of the counter spot: no prompt.
        rig.Position = NPCsModule.Spots[Pip].Position - Places.FoodSpot.Facing * 1f;
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.PromptNpc);
        // Pip still notices you there, but keeps facing the counter (stirring the pot on it).
        Assert.Equal(Pip, rig.Npcs.Noticing);
        rig.Wait(60);
        Assert.Equal(Places.FoodSpot.Yaw, rig.Npcs.Yaw[Pip], 4);
    }

    [Fact]
    public void PipNoticesYouComingNear()
    {
        var rig = new Rig(Places.FoodSpot.Position + Places.FoodSpot.Facing * 6f);
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.Noticing);
        rig.Position = Places.FoodSpot.Position;
        rig.Tick();
        Assert.Equal(Pip, rig.Npcs.Noticing);
        rig.Alive = false;
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.Noticing);
    }

    [Fact]
    public void PipGreetsYouThenOpensTheFoodMenu()
    {
        var rig = new Rig(Places.FoodSpot.Position);
        rig.Tick(interact: true);
        var line = Assert.Single(rig.Lines);
        Assert.Equal("Pip", line.Speaker);
        Assert.Equal(NPCsModule.SpeakerId(Pip), line.SpeakerId);
        Assert.Equal(Voices.Bubbly, line.Voice);
        Assert.Equal("Fresh from the oven!", line.Text);
        Assert.True(line.Last);
        Assert.Equal(Pip, rig.Npcs.Talking);
        Assert.Equal(-1, rig.Npcs.PromptNpc);
        // The menu waits until the greeting is dismissed.
        rig.Wait(10);
        Assert.Empty(rig.Bus.All<OpenFoodMenu>());

        rig.Press();
        Assert.False(rig.Open);
        Assert.Single(rig.Bus.All<OpenFoodMenu>());
        Assert.Equal(-1, rig.Npcs.Talking);
        Assert.Single(rig.Lines);

        // The Food module holds UiFocus while its menu is open: no prompt, and buying with Interact
        // does not make Pip greet again or ask for the menu again.
        const int menuOwner = 8241001;
        rig.Npcs.Handle(new UiFocus(menuOwner, true));
        rig.Press();
        rig.Press();
        Assert.Equal(-1, rig.Npcs.PromptNpc);
        Assert.Single(rig.Lines);
        Assert.Single(rig.Bus.All<OpenFoodMenu>());

        // Menu closed: Pip can be talked to again.
        rig.Npcs.Handle(new UiFocus(menuOwner, false));
        rig.Tick();
        Assert.Equal(Pip, rig.Npcs.PromptNpc);
        rig.Press();
        rig.Press();
        Assert.Equal(2, rig.Lines.Count);
        Assert.Equal(2, rig.Bus.All<OpenFoodMenu>().Count);
    }

    [Fact]
    public void PipsGreetingsTakeTurns()
    {
        var rig = new Rig(Places.FoodSpot.Position);
        var heard = new List<string>();
        for (int talk = 0; talk < 4; talk++)
        {
            rig.Press();
            heard.AddRange(rig.ReadAll());
        }
        Assert.Equal(new[]
        {
            "Fresh from the oven!",
            "Hungry from the forest? I've got you!",
            "A pie a day keeps the doctor away... sorry, Dr. Jeremy!",
            "Fresh from the oven!",
        }, heard);
        Assert.Equal(heard.Take(3), NPCsModule.PipGreetings.ToArray());
        // One short line per talk, and the menu after each.
        Assert.All(rig.Lines, l => Assert.True(l.Last));
        Assert.Equal(4, rig.Bus.All<OpenFoodMenu>().Count);
    }

    [Fact]
    public void PipDoesNotOpenTheMenuWhenSomeoneElseTakesTheBubble()
    {
        var rig = new Rig(Places.FoodSpot.Position);
        rig.Tick(interact: true);
        Assert.Equal(Pip, rig.Npcs.Talking);
        rig.Shown = new SayLine(42, "Someone", "Hi", Voices.Neutral, true);
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.Talking);
        rig.Press();
        Assert.Empty(rig.Bus.All<OpenFoodMenu>());
    }

    // Every icon family's file name prefix; Generic pads use the Xbox icons.
    private static readonly string[] IconFamilies = { "key_", "mouse_", "xbox_", "ps_", "ns_" };

    private static string OwnIconFamily(DeviceKind device) => device switch
    {
        DeviceKind.KeyboardMouse => "key_",
        DeviceKind.PlayStation => "ps_",
        DeviceKind.Nintendo => "ns_",
        _ => "xbox_",
    };

    [Theory]
    [InlineData(DeviceKind.KeyboardMouse, "key_e")]
    [InlineData(DeviceKind.Xbox, "xbox_a")]
    [InlineData(DeviceKind.PlayStation, "ps_cross")]
    [InlineData(DeviceKind.Nintendo, "ns_b")]
    [InlineData(DeviceKind.Generic, "xbox_a")]
    public void EveryPromptShowsOnlyTheActiveDevicesInteractIcon(DeviceKind device, string glyph)
    {
        string icon = $"[img={NPCsModule.PromptIconSize}]res://modules/Input/Glyphs/{glyph}.svg[/img]";
        Assert.Equal(icon, Prompts.Rich(PromptAction.Interact, device, NPCsModule.PromptIconSize));
        foreach (var (kind, word) in new[]
        {
            (NPCsModule.PromptKind.Talk, "Talk"), (NPCsModule.PromptKind.Food, "Food"),
            (NPCsModule.PromptKind.Buy, "Buy the Mushroom Crow (1,500 M¢)"),
        })
        {
            string text = NPCsModule.PromptBbcode(kind, device);
            Assert.Equal($"{icon} {word}", text);
            // Exactly one icon, of this device's family; no other device's icons and no key names as text.
            Assert.Single(text.Split("[img=")[1..]);
            foreach (string family in IconFamilies)
                if (family != OwnIconFamily(device))
                    Assert.DoesNotContain("/" + family, text);
            Assert.DoesNotContain(" / ", text);
            Assert.DoesNotContain("E:", text);
            Assert.DoesNotContain("X:", text);
        }
    }

    [Fact]
    public void ThePromptFollowsTheActiveDeviceLive()
    {
        var rig = new Rig(Near(Millie));
        Assert.Contains(typeof(ActiveDevice), rig.Bus.Subscribed);
        rig.Tick();
        Assert.Equal(DeviceKind.KeyboardMouse, rig.Npcs.Device);
        Assert.Contains("/key_e.svg", rig.Npcs.PromptText);

        rig.Npcs.Handle(new ActiveDevice(DeviceKind.Xbox));
        rig.Tick();
        Assert.Equal("[img=36]res://modules/Input/Glyphs/xbox_a.svg[/img] Talk", rig.Npcs.PromptText);

        rig.Npcs.Handle(new ActiveDevice(DeviceKind.PlayStation));
        rig.Tick();
        Assert.Equal("[img=36]res://modules/Input/Glyphs/ps_cross.svg[/img] Talk", rig.Npcs.PromptText);

        // At Pip's counter with the PlayStation pad: Cross and "Food".
        rig.Position = Places.FoodSpot.Position;
        rig.Tick();
        Assert.Equal("[img=36]res://modules/Input/Glyphs/ps_cross.svg[/img] Food", rig.Npcs.PromptText);

        rig.Npcs.Handle(new ActiveDevice(DeviceKind.KeyboardMouse));
        rig.Tick();
        Assert.Equal("[img=36]res://modules/Input/Glyphs/key_e.svg[/img] Food", rig.Npcs.PromptText);
        // Built once per prompt and device: the view redraws only when the string changes.
        Assert.Same(rig.Npcs.PromptText, NPCsModule.PromptBbcode(NPCsModule.PromptKind.Food, DeviceKind.KeyboardMouse));
    }

    [Fact]
    public void NoLineNamesAButton()
    {
        // The speech bubbles are plain text: lines never name a device's buttons (the prompt shows the icon).
        var rig = new Rig(Near(Lucy));
        HearTheOffer(rig);
        foreach (var line in rig.Lines)
            foreach (string button in new[] { "E / X", "Press", "Square", "Cross" })
                Assert.DoesNotContain(button, line.Text);
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
        Assert.Contains(texts, t => t.Contains("Mushroom Crow") && t.Contains("1,500 M¢"));
        Assert.Equal("Ask me again to buy it.", texts[^1]);
        rig.Tick();
        Assert.True(rig.Npcs.OfferingCrow);
        Assert.Equal(Lucy, rig.Npcs.PromptNpc);
        Assert.Equal(NPCsModule.PromptKind.Buy, rig.Npcs.Prompt);
        Assert.Equal("[img=36]res://modules/Input/Glyphs/key_e.svg[/img] Buy the Mushroom Crow (1,500 M¢)", rig.Npcs.PromptText);
        Assert.Equal(spends, rig.Bus.All<SpendCoins>().Count);
    }

    [Fact]
    public void BuyingTheCrowSpendsCoinsAdoptsItAndLucyCongratulates()
    {
        var rig = new Rig(Near(Lucy)) { Coins = 1700 };
        HearTheOffer(rig);

        rig.Tick(interact: true);
        var spend = Assert.Single(rig.Bus.All<SpendCoins>());
        Assert.Equal(1500, spend.Amount);
        Assert.Equal(1500, NPCsModule.CrowPrice);
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
        var rig = new Rig(Near(Lucy)) { Coins = 1499 };
        HearTheOffer(rig);
        rig.Press();
        Assert.Single(rig.Bus.All<SpendCoins>());
        Assert.Empty(rig.Bus.All<AdoptPet>());
        Assert.Equal(1499, rig.Coins);
        Assert.Equal("Come back with 1,500 M¢.", rig.Shown.Text);
        Assert.True(rig.Shown.Last);
        rig.ReadAll();
        rig.Tick();
        Assert.False(rig.Npcs.OfferingCrow);

        // Later, with the coins, the offer and the purchase work.
        rig.Coins = 1500;
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
    public void BackSaysNoToTheCrow()
    {
        var rig = new Rig(Near(Lucy)) { Coins = 5000 };
        HearTheOffer(rig);
        rig.Npcs.DeclineCrow();
        rig.Tick();
        Assert.False(rig.Npcs.OfferingCrow);
        // Back to the plain talk prompt: Interact starts her talk again, it does not buy.
        Assert.Equal(Lucy, rig.Npcs.PromptNpc);
        Assert.Equal(NPCsModule.PromptKind.Talk, rig.Npcs.Prompt);
        rig.Press();
        Assert.Empty(rig.Bus.All<SpendCoins>());
        Assert.Equal("Lucy", rig.Shown.Speaker);
        Assert.False(rig.Shown.Last);
        // Back with no offer standing changes nothing.
        rig.Npcs.DeclineCrow();
        Assert.False(rig.Npcs.OfferingCrow);
    }

    // Smart A: a showing prompt offers an interaction (the gamepad's A talks instead of jumping).

    [Theory]
    [InlineData(Jeremy)]
    [InlineData(Lucy)]
    [InlineData(Abby)]
    [InlineData(Pip)]
    public void APromptIsAvailableForInteractWhileItShows(int npc)
    {
        var rig = new Rig(Far);
        rig.Wait(2);
        Assert.Empty(rig.Bus.All<InteractAvailable>());
        rig.Position = Near(npc);
        rig.Tick();
        var on = Assert.Single(rig.Bus.All<InteractAvailable>());
        Assert.Equal(NPCsModule.InteractOwner, on.Owner);
        Assert.True(on.Available);
        rig.Wait(3);
        Assert.Single(rig.Bus.All<InteractAvailable>());
        rig.Position = Far;
        rig.Tick();
        var all = rig.Bus.All<InteractAvailable>();
        Assert.Equal(2, all.Count);
        Assert.Equal(NPCsModule.InteractOwner, all[1].Owner);
        Assert.False(all[1].Available);
    }

    [Fact]
    public void TalkingEndsTheAvailabilityAndTheNextPromptBringsItBack()
    {
        var rig = new Rig(Near(Jeremy));
        rig.Tick();
        Assert.True(rig.Bus.All<InteractAvailable>()[^1].Available);
        // The talk's speech bubbles hide the prompt (the Dialogue module offers A then).
        rig.Tick(interact: true);
        Assert.Equal(Jeremy, rig.Npcs.Talking);
        Assert.False(rig.Bus.All<InteractAvailable>()[^1].Available);
        rig.ReadAll();
        rig.Tick();
        Assert.Equal(Jeremy, rig.Npcs.PromptNpc);
        var all = rig.Bus.All<InteractAvailable>();
        Assert.Equal(3, all.Count);
        Assert.True(all[^1].Available);
    }

    [Fact]
    public void LucysBuyPromptIsAvailableForInteract()
    {
        var rig = new Rig(Near(Lucy)) { Coins = 5000 };
        HearTheOffer(rig);
        Assert.Equal(NPCsModule.PromptKind.Buy, rig.Npcs.Prompt);
        Assert.True(rig.Bus.All<InteractAvailable>()[^1].Available);
    }

    [Fact]
    public void AMenuHoldingUiFocusEndsTheAvailability()
    {
        var rig = new Rig(Near(Millie));
        rig.Tick();
        Assert.True(rig.Bus.All<InteractAvailable>()[^1].Available);
        rig.Npcs.Handle(new UiFocus(99, true));
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.PromptNpc);
        Assert.False(rig.Bus.All<InteractAvailable>()[^1].Available);
        rig.Npcs.Handle(new UiFocus(99, false));
        rig.Tick();
        Assert.True(rig.Bus.All<InteractAvailable>()[^1].Available);
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

    /// <summary>Answers a CollectSave as the Save module asks for it; returns the one "NPCs" part.</summary>
    private static SavePart Collect(Rig rig, int requestId = 42)
    {
        int before = rig.Bus.Published.Count;
        rig.Npcs.Handle(new CollectSave(requestId));
        var parts = rig.Bus.Published.Skip(before).OfType<SavePart>().ToList();
        var part = Assert.Single(parts);
        Assert.Equal(requestId, part.RequestId);
        Assert.Equal(SaveModules.NPCs, part.Module);
        return part;
    }

    /// <summary>A rig that has finished two quests (the second gave the flower clip), counts 50 / 500 M¢ on the third and bought the crow.</summary>
    private static Rig PlayedRig()
    {
        var rig = new Rig(Near(Millie)) { Coins = 1500 };
        rig.Press();
        rig.ReadAll();
        Kill(rig, MonsterKind.Caterpillar, 5);
        rig.Press();
        rig.ReadAll();
        Kill(rig, MonsterKind.Ant, 3);
        rig.Press();
        rig.ReadAll();
        rig.Npcs.Handle(new CoinsEarned(50, InForest));
        rig.Tick();
        rig.Position = Near(Lucy);
        HearTheOffer(rig);
        rig.Press();
        rig.ReadAll();
        rig.Tick();
        return rig;
    }

    private static void AssertSameSavedState(NPCsModule expected, NPCsModule actual)
    {
        Assert.Equal(expected.QuestsDone, actual.QuestsDone);
        Assert.Equal(expected.QuestActive, actual.QuestActive);
        Assert.Equal(expected.QuestComplete, actual.QuestComplete);
        Assert.Equal(expected.QuestProgress, actual.QuestProgress);
        Assert.Equal(expected.QuestGoal, actual.QuestGoal);
        Assert.Equal(expected.QuestReward, actual.QuestReward);
        Assert.Equal(expected.CurrentQuestKind, actual.CurrentQuestKind);
        Assert.Equal(expected.QuestTitle, actual.QuestTitle);
        Assert.Equal(expected.RewardsGiven, actual.RewardsGiven);
        Assert.Equal(expected.CrowBought, actual.CrowBought);
        Assert.Equal(expected.SaveJson(), actual.SaveJson());
    }

    /// <summary>The messages that would be a side effect of a restore.</summary>
    private static void AssertNoSideEffects(Rig rig)
    {
        Assert.Empty(rig.Bus.All<SayLine>());
        Assert.Empty(rig.Bus.All<GrantCoins>());
        Assert.Empty(rig.Bus.All<GrantItem>());
        Assert.Empty(rig.Bus.All<SpendCoins>());
        Assert.Empty(rig.Bus.All<AdoptPet>());
        Assert.Empty(rig.Bus.All<OpenShop>());
    }

    [Fact]
    public void CollectSaveAnswersWithCompactVersionedJson()
    {
        var rig = PlayedRig();
        var part = Collect(rig, 7);
        Assert.DoesNotContain("\n", part.Json);
        Assert.DoesNotContain(": ", part.Json);
        var data = System.Text.Json.JsonSerializer.Deserialize<NPCsModule.SaveData>(part.Json)!;
        Assert.Equal(NPCsModule.SaveVersion, data.Version);
        Assert.Equal(2, data.QuestsDone);
        Assert.True(data.QuestActive);
        Assert.Equal(50, data.QuestProgress);
        Assert.False(data.QuestComplete);
        Assert.Equal(new[] { "flower_clip" }, data.RewardsGiven);
        Assert.True(data.CrowBought);
    }

    [Fact]
    public void SaveThenRestoreGivesBackTheSameStateWithoutSideEffects()
    {
        var played = PlayedRig();
        Assert.Equal(2, played.Npcs.QuestsDone);
        Assert.Equal(50, played.Npcs.QuestProgress);
        Assert.True(played.Npcs.CrowBought);
        var part = Collect(played);

        var rig = new Rig(Far);
        rig.Npcs.Handle(new RestorePart(SaveModules.NPCs, part.Json));
        rig.Wait(3);
        AssertSameSavedState(played.Npcs, rig.Npcs);
        AssertNoSideEffects(rig);
        Assert.Equal(new QuestState(true, "Bring me... earn 500 M¢ in the forest", 50, 500, false), rig.Quest);

        // Play goes on from there: Lucy knows the crow is yours, the quest counts on, and pays once.
        rig.Position = Near(Lucy);
        rig.Press();
        Assert.Contains(rig.ReadAll(), t => t.Contains("already have the crow"));
        Assert.Empty(rig.Bus.All<SpendCoins>());
        rig.Position = Near(Millie);
        rig.Npcs.Handle(new CoinsEarned(450, InForest));
        rig.Tick();
        Assert.True(rig.Quest.Complete);
        rig.Press();
        rig.ReadAll();
        Assert.Equal(new[] { 200 }, rig.Bus.All<GrantCoins>().Select(g => g.Amount));
        Assert.Empty(rig.Bus.All<GrantItem>());
        Assert.Equal(3, rig.Npcs.QuestsDone);
    }

    [Fact]
    public void RestoringACompleteQuestOrANewGameRoundTrips()
    {
        // A finished but unpaid fourth quest: restoring does not pay it; talking to Millie does, with the backpack.
        var played = new Rig(Near(Millie));
        played.Npcs.Restore(System.Text.Json.JsonSerializer.Serialize(
            new NPCsModule.SaveData(NPCsModule.SaveVersion, 3, true, 10, true, new[] { "flower_clip" }, false)));
        played.Tick();
        Assert.True(played.Quest.Complete);
        Assert.Equal("Defeat 10 Mushroom Caterpillars", played.Quest.Title);
        AssertNoSideEffects(played);

        var rig = new Rig(Near(Millie));
        rig.Npcs.Handle(new RestorePart(SaveModules.NPCs, Collect(played).Json));
        rig.Tick();
        AssertSameSavedState(played.Npcs, rig.Npcs);
        AssertNoSideEffects(rig);
        rig.Press();
        rig.ReadAll();
        Assert.Equal(300, Assert.Single(rig.Bus.All<GrantCoins>()).Amount);
        Assert.Equal("mushroom_backpack", Assert.Single(rig.Bus.All<GrantItem>()).ItemId);
        Assert.Equal(new[] { "flower_clip", "mushroom_backpack" }, rig.Npcs.RewardsGiven);

        // A new game's state restores onto a played one as a new game.
        var fresh = new Rig(Far);
        var other = PlayedRig();
        other.Npcs.Handle(new RestorePart(SaveModules.NPCs, Collect(fresh).Json));
        AssertSameSavedState(fresh.Npcs, other.Npcs);
        Assert.False(other.Npcs.QuestActive);
    }

    [Fact]
    public void RestorePartsOfOtherModulesAreIgnored()
    {
        var rig = PlayedRig();
        string before = rig.Npcs.SaveJson();
        rig.Npcs.Handle(new RestorePart(SaveModules.Wallet, "{\"Coins\":5}"));
        rig.Npcs.Handle(new RestorePart(SaveModules.Pet, "garbage"));
        Assert.Equal(before, rig.Npcs.SaveJson());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"Version\":1,\"QuestsDone\":")]
    [InlineData("null")]
    [InlineData("[1,2,3]")]
    [InlineData("{}")]
    [InlineData("{\"Version\":2,\"QuestsDone\":4,\"QuestActive\":true,\"QuestProgress\":1,\"QuestComplete\":false,\"RewardsGiven\":[],\"CrowBought\":true}")]
    [InlineData("{\"Version\":1,\"QuestsDone\":-3,\"QuestActive\":true,\"QuestProgress\":1,\"QuestComplete\":false,\"RewardsGiven\":[],\"CrowBought\":true}")]
    [InlineData("{\"Version\":1,\"QuestsDone\":\"two\",\"CrowBought\":true}")]
    public void BadOrUnknownJsonKeepsTheDefaultsAndLogsOnce(string json)
    {
        string defaults = new NPCsModule().SaveJson();
        var rig = PlayedRig();
        int before = rig.Bus.Published.Count;
        var log = new List<string>();
        rig.Npcs.Log = log.Add;

        rig.Npcs.Handle(new RestorePart(SaveModules.NPCs, json));
        Assert.Single(log);
        Assert.Contains("NPCs", log[0]);
        Assert.Equal(before, rig.Bus.Published.Count);
        Assert.Equal(defaults, rig.Npcs.SaveJson());
        Assert.False(rig.Npcs.QuestActive);
        Assert.Equal(0, rig.Npcs.QuestsDone);
        Assert.Empty(rig.Npcs.RewardsGiven);
        Assert.False(rig.Npcs.CrowBought);
        rig.Tick();
        Assert.Equal(new QuestState(false, "", 0, 0, false), rig.Quest);
    }

    [Fact]
    public void RestoreDuringATalkOrOfferEndsThemQuietly()
    {
        var rig = new Rig(Near(Lucy)) { Coins = 5000 };
        HearTheOffer(rig);
        Assert.True(rig.Npcs.OfferingCrow);
        rig.Npcs.Restore(new NPCsModule().SaveJson());
        Assert.False(rig.Npcs.OfferingCrow);
        Assert.Equal(-1, rig.Npcs.Talking);

        rig.Position = Near(Abby);
        rig.Press();
        Assert.Equal(Abby, rig.Npcs.Talking);
        int lines = rig.Bus.All<SayLine>().Count;
        rig.Npcs.Restore(new NPCsModule().SaveJson());
        rig.Open = false;
        rig.Npcs.Handle(new DialogueClosed(NPCsModule.SpeakerId(Abby)));
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.Talking);
        Assert.Empty(rig.Bus.All<OpenShop>());
        Assert.Equal(lines, rig.Bus.All<SayLine>().Count);

        rig.Position = Places.FoodSpot.Position;
        rig.Press();
        Assert.Equal(Pip, rig.Npcs.Talking);
        rig.Npcs.Restore(new NPCsModule().SaveJson());
        rig.Open = false;
        rig.Npcs.Handle(new DialogueClosed(NPCsModule.SpeakerId(Pip)));
        rig.Tick();
        Assert.Equal(-1, rig.Npcs.Talking);
        Assert.Empty(rig.Bus.All<OpenFoodMenu>());
    }

    [Fact]
    public void SubscribesToWhatItReads()
    {
        var rig = new Rig(Far);
        foreach (var type in new[]
                 {
                     typeof(PlayerInput), typeof(PlayerState), typeof(DialogueState), typeof(DialogueAdvanced),
                     typeof(DialogueClosed), typeof(WokeUp), typeof(MonsterKilled), typeof(CoinsEarned),
                     typeof(SpendResult), typeof(OwnedPets), typeof(WardrobeState), typeof(CollectSave),
                     typeof(RestorePart), typeof(UiFocus),
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

    // The models (modules/NPCs/Models/*.glb, built by npcs.py), read back.

    /// <summary>
    /// One of the NPC models read back: each node's name, parent, mesh and world transform (glTF's
    /// frame, Godot's: X right, Y up, -Z forward), and its mesh's vertices by material.
    /// </summary>
    private sealed class Glb
    {
        public readonly Dictionary<string, int> Index = new();
        public readonly List<(string Name, int Parent, int Mesh, Matrix4x4 World)> Nodes = new();
        private readonly JsonElement _json;
        private readonly byte[] _bin;

        public Glb(string file)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "modules", "NPCs", "Models")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            var bytes = File.ReadAllBytes(Path.Combine(dir!.FullName, "modules", "NPCs", "Models", file + ".glb"));
            int jsonLength = BitConverter.ToInt32(bytes, 12);
            _json = JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes, 20, jsonLength)).RootElement;
            int binStart = 20 + jsonLength;
            _bin = bytes[(binStart + 8)..(binStart + 8 + BitConverter.ToInt32(bytes, binStart))];
            var nodes = _json.GetProperty("nodes");
            var parents = Enumerable.Repeat(-1, nodes.GetArrayLength()).ToArray();
            for (int i = 0; i < parents.Length; i++)
                if (nodes[i].TryGetProperty("children", out var children))
                    foreach (var child in children.EnumerateArray())
                        parents[child.GetInt32()] = i;
            var world = new Matrix4x4?[parents.Length];
            Matrix4x4 World(int i) => world[i] ??= Local(nodes[i]) * (parents[i] < 0 ? Matrix4x4.Identity : World(parents[i]));
            for (int i = 0; i < parents.Length; i++)
            {
                string name = nodes[i].GetProperty("name").GetString()!;
                Index[name] = i;
                Nodes.Add((name, parents[i], nodes[i].TryGetProperty("mesh", out var mesh) ? mesh.GetInt32() : -1, World(i)));
            }
        }

        private static Matrix4x4 Local(JsonElement node)
        {
            float[] Floats(string key, params float[] fallback) =>
                node.TryGetProperty(key, out var v) ? v.EnumerateArray().Select(x => x.GetSingle()).ToArray() : fallback;
            var t = Floats("translation", 0, 0, 0);
            var r = Floats("rotation", 0, 0, 0, 1);
            var s = Floats("scale", 1, 1, 1);
            return Matrix4x4.CreateScale(s[0], s[1], s[2]) * Matrix4x4.CreateFromQuaternion(new Quaternion(r[0], r[1], r[2], r[3])) *
                Matrix4x4.CreateTranslation(t[0], t[1], t[2]);
        }

        public bool Has(string name) => Index.ContainsKey(name);

        public (string Name, int Parent, int Mesh, Matrix4x4 World) Node(string name)
        {
            Assert.True(Index.ContainsKey(name), $"no node {name}");
            return Nodes[Index[name]];
        }

        public Vector3 Origin(string name) => Node(name).World.Translation;

        public string ParentOf(string name) => Node(name).Parent < 0 ? "" : Nodes[Node(name).Parent].Name;

        /// <summary>The materials of the node's own mesh.</summary>
        public IEnumerable<string> Materials(string name)
        {
            int mesh = Node(name).Mesh;
            if (mesh < 0)
                yield break;
            foreach (var primitive in _json.GetProperty("meshes")[mesh].GetProperty("primitives").EnumerateArray())
                yield return _json.GetProperty("materials")[primitive.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;
        }

        /// <summary>The base color of the material (npcs.py's swatch, the sRGB color NpcFigure draws).</summary>
        public Vector3 Color(string material)
        {
            foreach (var mat in _json.GetProperty("materials").EnumerateArray())
                if (mat.GetProperty("name").GetString() == material)
                {
                    // glTF leaves out the default, white.
                    if (!mat.TryGetProperty("pbrMetallicRoughness", out var pbr) || !pbr.TryGetProperty("baseColorFactor", out var c))
                        return Vector3.One;
                    return new Vector3(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle());
                }
            Assert.Fail($"no material {material}");
            return default;
        }

        public IEnumerable<string> AllMaterials() =>
            _json.GetProperty("materials").EnumerateArray().Select(m => m.GetProperty("name").GetString()!);

        /// <summary>The world positions of the node's own mesh's vertices (only those in <paramref name="materials"/> when given).</summary>
        public List<Vector3> Vertices(string name, params string[] materials)
        {
            var result = new List<Vector3>();
            var node = Node(name);
            if (node.Mesh < 0)
                return result;
            foreach (var primitive in _json.GetProperty("meshes")[node.Mesh].GetProperty("primitives").EnumerateArray())
            {
                string mat = _json.GetProperty("materials")[primitive.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;
                if (materials.Length > 0 && !materials.Contains(mat))
                    continue;
                var accessor = _json.GetProperty("accessors")[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
                var view = _json.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
                int start = (view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0) +
                    (accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
                int stride = view.TryGetProperty("byteStride", out var st) ? st.GetInt32() : 12;
                for (int i = 0; i < accessor.GetProperty("count").GetInt32(); i++)
                {
                    int at = start + i * stride;
                    var local = new Vector3(BitConverter.ToSingle(_bin, at), BitConverter.ToSingle(_bin, at + 4), BitConverter.ToSingle(_bin, at + 8));
                    result.Add(Vector3.Transform(local, node.World));
                }
            }
            return result;
        }

        /// <summary>The vertices of the node and every node under it.</summary>
        public List<Vector3> VerticesUnder(string name)
        {
            var result = Vertices(name);
            for (int i = 0; i < Nodes.Count; i++)
                if (Nodes[i].Parent == Index[name])
                    result.AddRange(VerticesUnder(Nodes[i].Name));
            return result;
        }
    }

    /// <summary>
    /// Each model, how high it stands (Pip on the step stool), the clothes that color its body (the
    /// last one round its base) and whether that body is a triangle (a dress) or an egg.
    /// </summary>
    public static readonly TheoryData<string, float, string[], bool> NpcModels = new()
    {
        { "jeremy", 0f, new[] { "Coat" }, false },
        { "millie", 0f, new[] { "Dress" }, true },
        { "lucy", 0f, new[] { "Shirt", "Skirt" }, false },
        { "abby", 0f, new[] { "Dress" }, true },
        { "pip", StoolHeight, new[] { "Shirt", "Trousers" }, false },
    };

    /// <summary>Pip's step stool (npcs.py's STOOL_H): Pip's shoulders as high over the counter as before the body was shortened.</summary>
    private const float StoolHeight = 0.63f;

    private static readonly string[] NpcFiles = { "jeremy", "millie", "lucy", "abby", "pip" };

    private static float Radius(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    /// <summary>The widest the vertices get round the vertical axis between heights y0 and y1.</summary>
    private static float WidestBetween(List<Vector3> vertices, float y0, float y1) =>
        vertices.Where(v => v.Y >= y0 && v.Y <= y1).Max(Radius);

    private static Vector3 Min(List<Vector3> v) => v.Aggregate(Vector3.Min);
    private static Vector3 Max(List<Vector3> v) => v.Aggregate(Vector3.Max);

    [Theory]
    [MemberData(nameof(NpcModels))]
    public void TheModelsKeepThePartsAndPivotsNpcFigureAnimates(string file, float lift, string[] cloth, bool cone)
    {
        _ = cone;
        var glb = new Glb(file);
        foreach (var part in new[] { "Body", "Head", "LegL", "LegR", "ArmL", "ArmR", "HandL", "HandR" })
            Assert.True(glb.Has(part), part);
        var body = glb.Vertices("Body", cloth);
        float bottom = body.Min(v => v.Y), top = body.Max(v => v.Y);
        // The neck just under the floating head, the hip low in the body.
        var head = glb.Vertices("Head", "Skin");
        Assert.True(Vector3.Distance(new Vector3(0f, (top + Min(head).Y) / 2, 0f), glb.Origin("Head")) < 0.03f);
        Assert.True(MathF.Abs(glb.Origin("Body").X) < 0.001f && MathF.Abs(glb.Origin("Body").Z) < 0.001f);
        Assert.InRange(glb.Origin("Body").Y, bottom, bottom + (top - bottom) * 0.25f);
        foreach (var (arm, hand, leg, side) in new[] { ("ArmL", "HandL", "LegL", -1f), ("ArmR", "HandR", "LegR", 1f) })
        {
            // The (invisible) arms from the shoulders, near the top of the body, down to the hands' centers.
            var shoulder = glb.Origin(arm);
            Assert.InRange(shoulder.X * side, 0.08f, 0.2f);
            Assert.InRange(shoulder.Y, top - 0.15f, top);
            var reach = glb.Origin(hand) - shoulder;
            Assert.True(reach.X * side > 0.1f && reach.Y < -0.2f, $"{hand} hangs down beside the body");
            // Each leg turns at the hip, up inside the body over its foot, wider apart than the old 0.08 m.
            var hip = glb.Origin(leg);
            Assert.InRange(hip.Y, bottom + 0.05f, bottom + 0.2f);
            Assert.InRange(hip.X * side, 0.105f, 0.115f);
            var legs = glb.Vertices(leg);
            Assert.InRange((Min(legs).X + Max(legs).X) / 2, hip.X - 0.01f, hip.X + 0.01f);
        }
        // Pip's spoon is held in the right hand, and Pip stands on the step stool.
        Assert.Equal(file == "pip", glb.Has("Spoon"));
        Assert.Equal(file == "pip", glb.Has("Stool"));
        if (file == "pip")
        {
            Assert.Equal("HandR", glb.ParentOf("Spoon"));
            Assert.Contains("Wood", glb.Materials("Spoon"));
            var stool = glb.Vertices("Stool");
            Assert.Equal(0f, Min(stool).Y, 2);
            Assert.Equal(StoolHeight, Max(stool).Y, 2);
        }
    }

    [Theory]
    [MemberData(nameof(NpcModels))]
    public void TheModelsHaveTheDrawingsProportions(string file, float lift, string[] cloth, bool cone)
    {
        _ = cone;
        var glb = new Glb(file);
        // The big round ball of a head, about 0.38 m across (0.42 with the little ears), moved down
        // with the shorter body: its center at about 1.11 m (was 1.28), its top at about 1.30 m.
        var head = glb.Vertices("Head", "Skin");
        var min = Min(head);
        var max = Max(head);
        Assert.InRange(max.Y - lift, 1.28f, 1.32f);
        Assert.InRange((min.Y + max.Y) / 2 - lift, 1.09f, 1.13f);
        Assert.Equal(0f, (min.X + max.X) / 2, 2);
        Assert.InRange(max.X - min.X, 0.36f, 0.43f);
        Assert.InRange(max.Z - min.Z, 0.34f, 0.4f);
        Assert.InRange(max.Y - min.Y, 0.33f, 0.4f);
        Assert.Contains("Eye", glb.Materials("Head"));

        // A gap of a few cm between everything on the head (hair, ears, hat) and everything on the
        // body: no neck. The head's skin still 4 to 6 cm over the body's top.
        float bodyTop = glb.VerticesUnder("Body").Max(v => v.Y);
        float headBottom = glb.VerticesUnder("Head").Min(v => v.Y);
        Assert.InRange(headBottom - bodyTop, 0.02f, 0.12f);
        Assert.InRange(min.Y - glb.Vertices("Body", cloth).Max(v => v.Y), 0.035f, 0.065f);

        // The body about 25% shorter than the old one (0.36 to 1.06 m): 0.36 to about 0.88 m.
        var body = glb.Vertices("Body", cloth);
        float bottom = body.Min(v => v.Y) - lift, top = body.Max(v => v.Y) - lift;
        Assert.InRange(top - bottom, 0.5f, 0.56f);
        Assert.InRange((top - bottom) / 0.7f, 0.71f, 0.79f);
        Assert.InRange(bottom, 0.34f, 0.37f);
        Assert.InRange(top, 0.87f, 0.9f);
        // The base: the widest of the body's lowest 30%.
        float baseR = WidestBetween(body, bottom + lift, bottom + lift + (top - bottom) * 0.3f);

        foreach (var leg in new[] { "LegL", "LegR" })
        {
            // A plain straight tube (0.06 m radius, as long as before) from inside the body down to a
            // rounded foot a little wider than it, on the ground (or the stool), coming out from
            // under the base inside its outline seen from the front (the leg and its foot no
            // further out than the base's widest).
            var shoe = glb.Vertices(leg, "Shoe");
            var tube = glb.Vertices(leg).Except(shoe).ToList();
            Assert.True(tube.Count > 0, $"{leg} has a tube");
            Assert.True(Max(tube).Y - lift > bottom + 0.1f, $"{leg} reaches up into the body");
            Assert.InRange(Max(tube).Y - Min(tube).Y, 0.53f, 0.57f);
            Assert.True(tube.Max(v => MathF.Abs(v.X)) < baseR, $"{leg} is not inside the base");
            Assert.True(shoe.Max(v => MathF.Abs(v.X)) < baseR, $"{leg}'s foot is not inside the base");
            var mid = tube.Where(v => v.Y - lift > 0.15f && v.Y - lift < bottom - 0.02f).ToList();
            Assert.True(mid.Count > 20, $"{leg} shows under the body");
            float width = Max(mid).X - Min(mid).X;
            Assert.InRange(width, 0.11f, 0.13f);
            Assert.InRange(Max(mid).Z - Min(mid).Z, 0.11f, 0.13f);
            var low = Min(shoe);
            var high = Max(shoe);
            Assert.InRange(low.Y - lift, -0.005f, 0.01f);
            Assert.InRange(high.Y - lift, 0.08f, 0.14f);
            Assert.InRange(high.X - low.X, width + 0.01f, width + 0.06f);
            Assert.InRange(Min(glb.Vertices(leg)).Y - lift, -0.005f, 0.01f);
        }
    }

    [Theory]
    [MemberData(nameof(NpcModels))]
    public void TheBodyIsShorterAndRoundAtTheBaseATriangleForADressAndAnEggOtherwise(string file, float lift, string[] cloth, bool cone)
    {
        _ = lift;
        var body = new Glb(file).Vertices("Body", cloth);
        float bottom = body.Min(v => v.Y), top = body.Max(v => v.Y), h = top - bottom;
        Assert.True(body.Count > 500, "a smooth body");
        var ring = body.Where(v => v.Y > bottom + h * 0.3f && v.Y < bottom + h * 0.35f).Select(Radius).ToList();
        Assert.True(ring.Max() - ring.Min() < 0.02f, "round");
        // Rounder at the base: the widest low down, then the outline curves softly round under it
        // to the middle of the bottom (no sharp rim: it never turns more than 25 degrees between
        // neighboring rings, on each piece of cloth), and up over a rounded top.
        float widest = body.Max(Radius);
        float widestY = body.First(v => Radius(v) == widest).Y;
        Assert.InRange(widestY, bottom + 0.05f, bottom + h * 0.3f);
        Assert.True(body.Where(v => v.Y < bottom + 0.01f).Max(Radius) < 0.65f * widest, "a round bottom");
        Assert.True(body.Where(v => v.Y > top - 0.01f).Max(Radius) < 0.08f, "a rounded top");
        var glb = new Glb(file);
        foreach (var piece in cloth)
        {
            var outline = Outline(glb.Vertices("Body", piece));
            for (int i = 1; i + 1 < outline.Length; i++)
            {
                var a = new Vector2(outline[i].R - outline[i - 1].R, outline[i].Y - outline[i - 1].Y);
                var b = new Vector2(outline[i + 1].R - outline[i].R, outline[i + 1].Y - outline[i].Y);
                float turn = MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(a), Vector2.Normalize(b)), -1f, 1f));
                Assert.True(turn < 25f * MathF.PI / 180f, $"{piece}: a sharp edge at {outline[i].Y}: {turn * 180f / MathF.PI} degrees");
            }
            // Above the widest it only narrows, up to the top.
            for (int i = 1; i < outline.Length; i++)
                if (outline[i - 1].Y >= widestY)
                    Assert.True(outline[i].R <= outline[i - 1].R + 1e-4f, $"{piece}: wider again at {outline[i].Y}");
        }
        if (cone)
        {
            // A triangle from the front, about 0.42 m wide low down, narrowing steadily to a narrow
            // rounded top.
            Assert.InRange(2 * widest, 0.41f, 0.43f);
            float last = float.MaxValue;
            for (int i = 1; i < 5; i++)
            {
                float slice = WidestBetween(body, bottom + h * i / 5f, bottom + h * (i + 1) / 5f);
                Assert.True(slice < last - 0.02f, $"slice {i}: {slice} not narrower than {last}");
                last = slice;
            }
            Assert.True(WidestBetween(body, bottom + h * 0.8f, top) < 0.45f * widest, "a narrow top");
        }
        else
        {
            // A soft egg: about 0.28 m wide near the top, widest (about 0.38 m; a little more over a
            // band of cloth round its base) low down.
            Assert.InRange(2 * widest, 0.37f, 0.41f);
            Assert.InRange(2 * WidestBetween(body, bottom + h * 0.78f, bottom + h * 0.82f), 0.25f, 0.31f);
            Assert.True(WidestBetween(body, bottom + h * 0.45f, bottom + h * 0.55f) < widest - 0.015f, "wider low down than in the middle");
        }
    }

    /// <summary>
    /// A body's outline seen from the front: its radius at each height (rings of the solid of
    /// revolution), bottom to top.
    /// </summary>
    private static (float Y, float R)[] Outline(List<Vector3> vertices) =>
        vertices.GroupBy(v => MathF.Round(v.Y, 4)).Select(g => (g.Key, g.Max(Radius))).OrderBy(r => r.Key).ToArray();

    [Theory]
    [MemberData(nameof(NpcModels))]
    public void TheModelsHaveBigFloatingOvalHandsWithFingerMarksButNoArms(string file, float lift, string[] cloth, bool cone)
    {
        _ = lift;
        _ = cone;
        var glb = new Glb(file);
        // No arm meshes: the arms are only pivots, and the only skin is the head and the hands.
        foreach (var arm in new[] { "ArmL", "ArmR" })
            Assert.Equal(-1, glb.Node(arm).Mesh);
        Assert.Equal(new[] { "HandL", "HandR", "Head" },
            glb.Nodes.Where(n => glb.Materials(n.Name).Contains("Skin")).Select(n => n.Name).OrderBy(n => n, StringComparer.Ordinal));

        var body = glb.Vertices("Body", cloth);
        float bottom = body.Min(v => v.Y), top = body.Max(v => v.Y), h = top - bottom;
        var everything = glb.Vertices("Body");
        float headWidth = Max(glb.Vertices("Head", "Skin")).X - Min(glb.Vertices("Head", "Skin")).X;
        foreach (var (hand, side) in new[] { ("HandL", -1f), ("HandR", 1f) })
        {
            // A big oval, about 0.18 m tall and 0.14 m wide (about half the head's width), beside the
            // body at about its middle height, well out from it.
            var v = glb.Vertices(hand, "Skin");
            var size = Max(v) - Min(v);
            Assert.InRange(size.Y, 0.16f, 0.2f);
            Assert.InRange(size.X, 0.12f, 0.16f);
            Assert.InRange(size.X / headWidth, 0.3f, 0.45f);
            Assert.InRange(size.Y / size.X, 1.15f, 1.45f);
            var center = (Min(v) + Max(v)) / 2;
            Assert.True(center.X * side > 0, hand);
            Assert.InRange(center.Y, bottom + h * 0.35f, bottom + h * 0.65f);
            float inner = v.Min(p => p.X * side);
            Assert.True(inner > WidestBetween(everything.Where(p => MathF.Abs(p.Z) < 0.1f).ToList(), center.Y - 0.1f, center.Y + 0.1f) + 0.05f,
                $"{hand} is not well out from the body");
            // Three short finger marks down the front of its lower half.
            var marks = glb.Vertices(hand, "Finger");
            Assert.True(marks.All(p => p.Y < center.Y && p.Z < center.Z), $"{hand}'s finger marks are on the front of its lower half");
            Assert.True(Max(marks).Y - Min(marks).Y < size.Y * 0.5f, "short");
            var xs = marks.Select(p => p.X).OrderBy(x => x).ToList();
            int lines = 1 + xs.Zip(xs.Skip(1), (a, b) => b - a > 0.008f ? 1 : 0).Sum();
            Assert.Equal(3, lines);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EachOneIsDrawnAbout1point25To1point35MetersTallMillieAbout1point17WithThePromptOverTheTopOfTheHead(int npc)
    {
        var glb = new Glb(NpcFiles[npc]);
        float lift = npc == NPCsModule.Pip ? StoolHeight : 0f;
        float scale = NPCsModule.FigureScales[npc];
        float drawn = (Max(glb.Vertices("Head", "Skin")).Y - lift) * scale;
        if (npc == NPCsModule.Millie)
            // Saria, 2026-10-26: about 10% shorter than the 1.30 m player, her whole figure scaled evenly.
            Assert.InRange(drawn, 1.14f, 1.20f);
        else
            Assert.InRange(drawn, 1.22f, 1.35f);
        // HeadTops is the top of everything on the head (hair, ears, the bun, Pip's hat) before the scale;
        // the view floats the prompt over HeadTops * FigureScales, just over the drawn top.
        Assert.Equal(glb.VerticesUnder("Head").Max(v => v.Y), NPCsModule.HeadTops[npc], 2);
        Assert.Equal(npc == NPCsModule.Jeremy, NPCsModule.FigureScales.Max() == scale);
    }

    [Fact]
    public void MillieIsAbout10PercentShorterThanTheOthersAndTheShortest()
    {
        Assert.Equal(0.9f, NPCsModule.FigureScales[NPCsModule.Millie], 3);
        float Drawn(int npc)
        {
            float lift = npc == NPCsModule.Pip ? StoolHeight : 0f;
            return (Max(new Glb(NpcFiles[npc]).Vertices("Head", "Skin")).Y - lift) * NPCsModule.FigureScales[npc];
        }
        float millie = Drawn(NPCsModule.Millie);
        for (int npc = 0; npc < NpcFiles.Length; npc++)
        {
            if (npc == NPCsModule.Millie)
                continue;
            float other = Drawn(npc);
            Assert.True(millie < other - 0.05f, $"Millie ({millie:0.00} m) is not clearly shorter than NPC {npc} ({other:0.00} m)");
            Assert.InRange(millie / other, 0.84f, 0.95f);
        }
    }

    /// <summary>Asserts that the node's mesh has each of the materials.</summary>
    private static void HasMaterials(Glb glb, string node, params string[] materials)
    {
        var has = glb.Materials(node).ToList();
        foreach (var material in materials)
            Assert.True(has.Contains(material), $"{node} has no {material}");
    }

    /// <summary>True when all of the material's vertices on the node are on the front (Godot -Z).</summary>
    private static bool InFront(Glb glb, string node, string material) => glb.Vertices(node, material).All(v => v.Z < 0f);

    [Fact]
    public void JeremyKeepsHisCoatBadgeAndStethoscope()
    {
        var glb = new Glb("jeremy");
        HasMaterials(glb, "Head", "Hair", "Eye", "EyeShine", "Blush");
        HasMaterials(glb, "Body", "Coat", "Shirt", "Tie", "Lapel", "Button", "Badge", "BadgeStripe", "Tube", "Metal");
        // The coat opens in a V at the top of the front over the shirt and tie; the badge is on his left.
        var shirt = glb.Vertices("Body", "Shirt");
        Assert.True(InFront(glb, "Body", "Shirt") && InFront(glb, "Body", "Tie"));
        var coat = glb.Vertices("Body", "Coat");
        Assert.True(shirt.Min(v => v.Y) > (coat.Min(v => v.Y) + coat.Max(v => v.Y)) / 2, "the shirt shows only at the top");
        Assert.True(InFront(glb, "Body", "Badge") && glb.Vertices("Body", "Badge").All(v => v.X < 0f));
        // The stethoscope rings the top of the coat, its chest piece in front.
        var tube = glb.Vertices("Body", "Tube");
        Assert.True(tube.Any(v => v.Z > 0.05f) && tube.Any(v => v.Z < -0.05f));
        Assert.True(InFront(glb, "Body", "Metal"));
    }

    [Fact]
    public void MillieKeepsHerCatEarsTailAndSpottedDress()
    {
        var glb = new Glb("millie");
        HasMaterials(glb, "Head", "Hair", "EarInner", "Eye", "Blush");
        Assert.DoesNotContain("EyeShine", glb.Materials("Head")); // happy closed eyes
        // The cat ears stand up over the head.
        Assert.True(glb.Vertices("Head", "EarInner").Max(v => v.Y) > glb.Vertices("Head", "Skin").Max(v => v.Y) + 0.05f);
        HasMaterials(glb, "Body", "Dress", "Frill", "Spots", "Bow", "Hair");
        Assert.True(InFront(glb, "Body", "Bow"));
        // The little tail sticks out behind the dress.
        var tail = glb.Vertices("Body", "Hair");
        Assert.True(tail.All(v => v.Z > 0f));
        Assert.True(tail.Max(v => v.Z) > WidestBetween(glb.Vertices("Body", "Dress"), 0.45f, 0.75f) + 0.08f);
    }

    [Fact]
    public void LucyKeepsHerFlowerAndPetStoreShirt()
    {
        var glb = new Glb("lucy");
        HasMaterials(glb, "Head", "Hair", "Lid", "Petal", "FlowerMiddle", "Eye");
        HasMaterials(glb, "Body", "Shirt", "Skirt", "Print", "Heart");
        // "Pet Store" across the front of the shirt, hearts around it, the skirt a band round the bottom.
        var print = glb.Vertices("Body", "Print");
        Assert.True(InFront(glb, "Body", "Print"));
        Assert.True(Max(print).X - Min(print).X > 0.1f);
        Assert.True(InFront(glb, "Body", "Heart"));
        var skirt = glb.Vertices("Body", "Skirt");
        Assert.True(skirt.Max(v => v.Y) < glb.Vertices("Body", "Shirt").Min(v => v.Y) + 0.06f);
    }

    [Fact]
    public void AbbyKeepsHerTopBunAndTriangleDress()
    {
        var glb = new Glb("abby");
        HasMaterials(glb, "Head", "Hair", "Band", "Eye", "EyeShine");
        // The tall top bun.
        Assert.True(glb.Vertices("Head", "Hair").Max(v => v.Y) > glb.Vertices("Head", "Skin").Max(v => v.Y) + 0.12f);
        // The cone is her dress (checked as a triangle above), with a white collar at the top and a white hem.
        HasMaterials(glb, "Body", "Dress", "Collar", "Hem");
        var dress = glb.Vertices("Body", "Dress");
        Assert.True(glb.Vertices("Body", "Collar").Min(v => v.Y) > dress.Max(v => v.Y) - 0.15f);
        Assert.True(glb.Vertices("Body", "Collar").Max(v => v.Y) > dress.Max(v => v.Y) - 0.1f);
        Assert.True(glb.Vertices("Body", "Hem").Max(v => v.Y) < dress.Min(v => v.Y) + 0.1f);
    }

    [Fact]
    public void PipKeepsTheChefHatMuffinApronSpoonAndStool()
    {
        var glb = new Glb("pip");
        HasMaterials(glb, "Head", "Hair", "Hat", "HatSpots", "Gills", "Freckle", "Eye", "EyeShine");
        HasMaterials(glb, "Body", "Shirt", "Trousers", "Apron", "Cup", "MuffinTop", "Berry", "Cherry");
        // The muffin printed on the front of the apron.
        foreach (var part in new[] { "Cup", "MuffinTop", "Berry", "Cherry" })
            Assert.True(InFront(glb, "Body", part), part);
        // The spoon hangs down from the right hand.
        var spoon = glb.Vertices("Spoon");
        Assert.True(Min(spoon).Y < glb.Origin("HandR").Y - 0.3f);
        HasMaterials(glb, "Stool", "Wood", "WoodDark");
        // Still short of the stall's counter (its top is 1.03 m up), more so with the shorter body:
        // the step stool lifts Pip's shoulders over it to stir the pot, as high as before (1.43 m),
        // the feet standing on it.
        Assert.True(glb.Origin("ArmR").Y - StoolHeight < 1.03f && glb.Origin("ArmR").Y > 1.03f + 0.35f);
        Assert.Equal(1.43f, glb.Origin("ArmR").Y, 2);
        Assert.Equal(StoolHeight, glb.VerticesUnder("LegL").Min(v => v.Y), 2);
    }

    /// <summary>HSV saturation of a color.</summary>
    private static float Saturation(Vector3 c)
    {
        float max = MathF.Max(c.X, MathF.Max(c.Y, c.Z)), min = MathF.Min(c.X, MathF.Min(c.Y, c.Z));
        return max <= 0f ? 0f : (max - min) / max;
    }

    [Fact]
    public void PipsMushroomHatIsVivid()
    {
        var glb = new Glb("pip");
        // The spots: a pure cherry red, bright and saturated (not brownish or dull pink).
        var spots = glb.Color("HatSpots");
        Assert.True(spots.X >= 0.9f && spots.Y <= 0.12f && spots.Z <= 0.15f, $"HatSpots {spots}");
        Assert.True(Saturation(spots) >= 0.88f, $"HatSpots saturation {Saturation(spots)}");
        // The puffy cap: crisp pure white.
        var hat = glb.Color("Hat");
        Assert.True(hat.X >= 0.995f && hat.Y >= 0.995f && hat.Z >= 0.995f, $"Hat {hat}");
        // The gills under its rim: a clean light cream (light, warm, only a little yellow).
        var gills = glb.Color("Gills");
        Assert.True(gills.X >= 0.99f && gills.Y >= 0.93f && gills.Z >= 0.78f && gills.Z <= 0.9f && gills.Z < gills.Y, $"Gills {gills}");
    }

    /// <summary>Pip's colors that are not mushroom parts (npcs.py's swatches): they stay as they were.</summary>
    public static readonly TheoryData<string, float, float, float> PipOtherColors = new()
    {
        { "Skin", 0.98f, 0.68f, 0.52f }, { "Blush", 1.0f, 0.45f, 0.48f }, { "Eye", 0.07f, 0.04f, 0.06f },
        { "EyeShine", 1.0f, 1.0f, 1.0f }, { "Mouth", 0.55f, 0.16f, 0.2f }, { "Shoe", 0.42f, 0.24f, 0.18f },
        { "Hair", 0.85f, 0.42f, 0.18f }, { "Freckle", 0.66f, 0.36f, 0.22f }, { "Shirt", 0.92f, 0.38f, 0.36f },
        { "Apron", 0.95f, 0.8f, 0.56f }, { "Cup", 0.97f, 0.55f, 0.68f }, { "MuffinTop", 0.72f, 0.45f, 0.25f },
        { "Berry", 0.35f, 0.38f, 0.85f }, { "Cherry", 0.9f, 0.15f, 0.2f }, { "Trousers", 0.45f, 0.35f, 0.3f },
        { "Wood", 0.8f, 0.58f, 0.36f }, { "WoodDark", 0.55f, 0.36f, 0.22f },
    };

    [Theory]
    [MemberData(nameof(PipOtherColors))]
    public void PipsOtherColorsStayAsTheyWere(string material, float r, float g, float b)
    {
        var glb = new Glb("pip");
        Assert.True(Vector3.Distance(new Vector3(r, g, b), glb.Color(material)) < 0.002f, $"{material} {glb.Color(material)}");
    }

    [Fact]
    public void OnlyMushroomCapsGetTheGlossyHighlightAndRimLight()
    {
        Assert.True(MushroomLook.IsCap("Hat") && MushroomLook.IsCap("HatSpots"));
        Assert.True(MushroomLook.Gloss("Hat") > 0f);
        // No glint inside the little spots (it would look like an eye).
        Assert.Equal(0f, MushroomLook.Gloss("HatSpots"));
        Assert.True(MushroomLook.Rim("Hat") > MushroomLook.PlainRim && MushroomLook.Rim("HatSpots") > MushroomLook.PlainRim);
        // Every other surface of every NPC keeps the plain toon look.
        foreach (var file in new[] { "jeremy", "millie", "lucy", "abby", "pip" })
            foreach (var material in new Glb(file).AllMaterials().Where(m => m is not ("Hat" or "HatSpots")))
            {
                Assert.False(MushroomLook.IsCap(material), $"{file} {material}");
                Assert.Equal(0f, MushroomLook.Gloss(material));
                Assert.Equal(MushroomLook.PlainRim, MushroomLook.Rim(material));
            }
        // The toon shader takes them (NpcFigure sets them per material).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "modules", "NPCs", "Godot", "Shaders")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var shader = File.ReadAllText(Path.Combine(dir!.FullName, "modules", "NPCs", "Godot", "Shaders", "toon.gdshader"));
        Assert.Contains("uniform float gloss", shader);
        Assert.Contains("uniform float rim", shader);
    }
}
