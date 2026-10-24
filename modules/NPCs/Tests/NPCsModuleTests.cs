using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
}
