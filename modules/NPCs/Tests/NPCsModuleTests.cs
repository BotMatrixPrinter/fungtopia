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
    [InlineData(DeviceKind.Xbox, "xbox_x")]
    [InlineData(DeviceKind.PlayStation, "ps_square")]
    [InlineData(DeviceKind.Nintendo, "ns_y")]
    [InlineData(DeviceKind.Generic, "xbox_x")]
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
        Assert.Equal("[img=36]res://modules/Input/Glyphs/xbox_x.svg[/img] Talk", rig.Npcs.PromptText);

        rig.Npcs.Handle(new ActiveDevice(DeviceKind.PlayStation));
        rig.Tick();
        Assert.Equal("[img=36]res://modules/Input/Glyphs/ps_square.svg[/img] Talk", rig.Npcs.PromptText);

        // At Pip's counter with the PlayStation pad: Square and "Food".
        rig.Position = Places.FoodSpot.Position;
        rig.Tick();
        Assert.Equal("[img=36]res://modules/Input/Glyphs/ps_square.svg[/img] Food", rig.Npcs.PromptText);

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

    /// <summary>Each model, how high it stands (Pip on the step stool) and the clothes that color its cone body.</summary>
    public static readonly TheoryData<string, float, string[]> NpcModels = new()
    {
        { "jeremy", 0f, new[] { "Coat" } },
        { "millie", 0f, new[] { "Dress" } },
        { "lucy", 0f, new[] { "Shirt", "Skirt" } },
        { "abby", 0f, new[] { "Dress" } },
        { "pip", 0.45f, new[] { "Shirt", "Trousers" } },
    };

    private static float Radius(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    /// <summary>The widest the vertices get round the vertical axis between heights y0 and y1.</summary>
    private static float WidestBetween(List<Vector3> vertices, float y0, float y1) =>
        vertices.Where(v => v.Y >= y0 && v.Y <= y1).Max(Radius);

    private static Vector3 Min(List<Vector3> v) => v.Aggregate(Vector3.Min);
    private static Vector3 Max(List<Vector3> v) => v.Aggregate(Vector3.Max);

    [Theory]
    [MemberData(nameof(NpcModels))]
    public void TheModelsKeepThePartsAndPivotsNpcFigureAnimates(string file, float lift, string[] cloth)
    {
        _ = cloth;
        var glb = new Glb(file);
        foreach (var part in new[] { "Body", "Head", "LegL", "LegR", "ArmL", "ArmR", "HandL", "HandR", "FootL", "FootR" })
            Assert.True(glb.Has(part), part);
        // The player characters' rig: the neck, the hip, the shoulders, the hands at the ends of 0.42 m (invisible) arms.
        Assert.True(Vector3.Distance(new Vector3(0f, 0.72f + lift, 0f), glb.Origin("Head")) < 0.001f);
        Assert.True(Vector3.Distance(new Vector3(0f, 0.3f + lift, 0f), glb.Origin("Body")) < 0.001f);
        foreach (var (arm, hand, leg, foot, side) in new[] { ("ArmL", "HandL", "LegL", "FootL", -1f), ("ArmR", "HandR", "LegR", "FootR", 1f) })
        {
            Assert.True(Vector3.Distance(new Vector3(side * 0.23f, 0.65f + lift, 0f), glb.Origin(arm)) < 0.001f, arm);
            var reach = glb.Origin(hand) - glb.Origin(arm);
            Assert.Equal(0.42f, reach.Length(), 3);
            Assert.True(reach.X * side > 0.1f && reach.Y < -0.3f, $"{hand} hangs down beside the body");
            var hip = glb.Origin(leg);
            Assert.InRange(hip.Y - lift, 0.28f, 0.36f);
            Assert.InRange(hip.X * side, 0.08f, 0.16f);
            // The feet hang from the legs' pivots, so they move with them.
            Assert.Equal(leg, glb.ParentOf(foot));
        }
        // Pip's spoon is held in the right hand, and Pip stands on the 0.45 m step stool.
        Assert.Equal(file == "pip", glb.Has("Spoon"));
        Assert.Equal(file == "pip", glb.Has("Stool"));
        if (file == "pip")
        {
            Assert.Equal("HandR", glb.ParentOf("Spoon"));
            Assert.Contains("Wood", glb.Materials("Spoon"));
            var stool = glb.Vertices("Stool");
            Assert.Equal(0f, Min(stool).Y, 2);
            Assert.Equal(0.45f, Max(stool).Y, 2);
        }
    }

    [Theory]
    [MemberData(nameof(NpcModels))]
    public void TheModelsAreARoundHeadFloatingOverATriangleBody(string file, float lift, string[] cloth)
    {
        var glb = new Glb(file);
        // The big round head, where and as big as the player characters'.
        var head = glb.Vertices("Head", "Skin");
        var min = Min(head);
        var max = Max(head);
        Assert.Equal(1.088f + lift, (min.Y + max.Y) / 2, 2);
        Assert.Equal(0f, (min.X + max.X) / 2, 2);
        Assert.Equal(0.75f, max.Y - min.Y, 2);
        Assert.Equal(0.77f, max.Z - min.Z, 2);
        Assert.Contains("Eye", glb.Materials("Head"));
        Assert.Contains("Mouth", glb.Materials("Head"));

        // A gap of a few cm between everything on the head (hair, ears, hat) and everything on the
        // body: no neck.
        float bodyTop = glb.VerticesUnder("Body").Max(v => v.Y);
        float headBottom = glb.VerticesUnder("Head").Min(v => v.Y);
        Assert.InRange(headBottom - bodyTop, 0.03f, 0.15f);

        // The clothes color a smooth cone: a triangle from the front, wide at the bottom, narrowing
        // steadily to a rounded top, round all the way round, from just over the feet up to the top.
        var body = glb.Vertices("Body", cloth);
        float bottom = body.Min(v => v.Y), top = body.Max(v => v.Y), h = top - bottom;
        Assert.InRange(bottom - lift, 0.08f, 0.2f);
        Assert.InRange(top - lift, 0.55f, 0.66f);
        float last = float.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            float widest = WidestBetween(body, bottom + h * i / 5f, bottom + h * (i + 1) / 5f);
            Assert.True(widest < last - 0.02f, $"slice {i}: {widest} not narrower than {last}");
            last = widest;
        }
        float base_ = WidestBetween(body, bottom, bottom + h * 0.2f);
        Assert.InRange(base_, 0.26f, 0.34f);
        Assert.True(WidestBetween(body, bottom + h * 0.8f, top) < 0.5f * base_);
        Assert.True(body.Count > 500, "a smooth cone");
        var rim = body.Where(v => v.Y > bottom + h * 0.3f && v.Y < bottom + h * 0.35f).Select(Radius).ToList();
        Assert.True(rim.Max() - rim.Min() < 0.02f, "round");
    }

    [Theory]
    [MemberData(nameof(NpcModels))]
    public void TheModelsHaveFloatingRoundHandsAndLittleFeetButNoArmsOrLegs(string file, float lift, string[] cloth)
    {
        var glb = new Glb(file);
        // No arm or leg meshes: the arms and legs are only pivots, and the only skin is the head and the hands.
        foreach (var limb in new[] { "ArmL", "ArmR", "LegL", "LegR" })
            Assert.Equal(-1, glb.Node(limb).Mesh);
        Assert.Equal(new[] { "HandL", "HandR", "Head" },
            glb.Nodes.Where(n => glb.Materials(n.Name).Contains("Skin")).Select(n => n.Name).OrderBy(n => n, StringComparer.Ordinal));

        var body = glb.Vertices("Body", cloth);
        float bottom = body.Min(v => v.Y), top = body.Max(v => v.Y), h = top - bottom;
        var everything = glb.Vertices("Body");
        foreach (var (hand, side) in new[] { ("HandL", -1f), ("HandR", 1f) })
        {
            // Round, beside the body a little below its middle, with air between them and the body.
            var v = glb.Vertices(hand);
            var size = Max(v) - Min(v);
            Assert.InRange(size.X, 0.12f, 0.2f);
            Assert.InRange(size.Y / size.X, 0.9f, 1.1f);
            Assert.InRange(size.Z / size.X, 0.9f, 1.1f);
            var center = glb.Origin(hand);
            Assert.True(center.X * side > 0, hand);
            Assert.InRange(center.Y, bottom + h * 0.2f, bottom + h * 0.6f);
            float inner = v.Min(p => p.X * side);
            Assert.True(inner > WidestBetween(everything.Where(p => MathF.Abs(p.Z) < 0.1f).ToList(), center.Y - 0.1f, center.Y + 0.1f) + 0.03f,
                $"{hand} touches the body");
        }
        foreach (var (foot, side) in new[] { ("FootL", -1f), ("FootR", 1f) })
        {
            // Little rounded feet on the ground (or the stool) under the triangle.
            var v = glb.Vertices(foot);
            var low = Min(v);
            var high = Max(v);
            Assert.InRange(low.Y - lift, -0.01f, 0.02f);
            Assert.True(high.Y <= bottom + 0.02f, $"{foot} reaches into the body");
            Assert.InRange(high.Y - low.Y, 0.08f, 0.16f);
            Assert.InRange(high.X - low.X, 0.12f, 0.24f);
            Assert.True((low.X + high.X) / 2 * side > 0.05f, foot);
            Assert.True(Math.Max(MathF.Abs(low.X), MathF.Abs(high.X)) < WidestBetween(body, bottom, bottom + 0.05f), $"{foot} sticks out sideways");
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
        Assert.True(shirt.Min(v => v.Y) > 0.3f, "the shirt shows only at the top");
        Assert.True(InFront(glb, "Body", "Badge") && glb.Vertices("Body", "Badge").All(v => v.X < 0f));
        // The stethoscope rings the top of the cone, its chest piece in front.
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
        // The little tail sticks out behind the cone.
        var tail = glb.Vertices("Body", "Hair");
        Assert.True(tail.All(v => v.Z > 0f));
        Assert.True(tail.Max(v => v.Z) > 0.3f);
    }

    [Fact]
    public void LucyKeepsHerFlowerAndPetStoreShirt()
    {
        var glb = new Glb("lucy");
        HasMaterials(glb, "Head", "Hair", "Lid", "Petal", "FlowerMiddle", "Eye");
        HasMaterials(glb, "Body", "Shirt", "Skirt", "Print", "Heart");
        // "Pet Store" across the front of the cone, hearts around it, the skirt a band round the bottom.
        var print = glb.Vertices("Body", "Print");
        Assert.True(InFront(glb, "Body", "Print"));
        Assert.True(Max(print).X - Min(print).X > 0.12f);
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
        Assert.True(glb.Vertices("Head", "Hair").Max(v => v.Y) > glb.Vertices("Head", "Skin").Max(v => v.Y) + 0.25f);
        // The cone is her dress (checked as a triangle above), with a white collar at the top and a white hem.
        HasMaterials(glb, "Body", "Dress", "Collar", "Hem");
        var dress = glb.Vertices("Body", "Dress");
        Assert.True(glb.Vertices("Body", "Collar").Min(v => v.Y) > dress.Max(v => v.Y) - 0.15f);
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
    }
}
