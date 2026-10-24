using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Fungtopia.DayNight;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Fungtopia.Wallet;
using Xunit;

namespace Fungtopia.Save.Tests;

/// <summary>Fake of the shell's bus: delivers synchronously like the real one and records every message.</summary>
internal sealed class FakeBus : IMessageBus
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
        if (!_handlers.TryGetValue(typeof(T), out var list))
            return;
        foreach (var handler in list.ToArray())
            ((IMessageHandler<T>)handler).Handle(in message);
    }

    public List<T> All<T>() where T : struct => Published.OfType<T>().ToList();

    public void Clear() => Published.Clear();
}

/// <summary>A fake module with saved state: answers CollectSave with its Json, remembers what it was restored with.</summary>
internal sealed class FakeSaver : IMessageHandler<CollectSave>, IMessageHandler<RestorePart>
{
    private readonly IMessageBus _bus;
    public readonly string Module;
    public string Json;
    public readonly List<string> Restored = new();

    public FakeSaver(IMessageBus bus, string module, string json)
    {
        _bus = bus;
        Module = module;
        Json = json;
        bus.Subscribe<CollectSave>(this);
        bus.Subscribe<RestorePart>(this);
    }

    public void Handle(in CollectSave message) => _bus.Publish(new SavePart(message.RequestId, Module, Json));

    public void Handle(in RestorePart message)
    {
        if (message.Module == Module)
            Restored.Add(message.Json);
    }
}

/// <summary>A bus that only counts, so measuring allocations sees the module's alone.</summary>
internal sealed class CountingBus : IMessageBus
{
    public int Count;

    public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }

    public void Publish<T>(in T message) where T : struct => Count++;
}

public sealed class SaveModuleTests : IDisposable
{
    private const double Step = 1.0 / 60.0;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "fungtopia-save-tests-" + Guid.NewGuid().ToString("N"));
    private long _tick;
    private DateTime _now = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, true);
    }

    private SaveModule NewModule(FakeBus bus)
    {
        var save = new SaveModule { SaveFolder = _folder, Now = () => _now };
        save.Start(bus);
        return save;
    }

    private void Tick(SaveModule save, int ticks = 1)
    {
        for (int i = 0; i < ticks; i++)
            save.Tick(new TickContext(_tick++, Step));
    }

    private static PlayerState Player(Area area, CharacterKind kind = CharacterKind.Girl) =>
        new(Vector3.Zero, 0f, Vector3.Zero, true, 10, 10, true, kind, area);

    /// <summary>A module in play: the start screen closed and "Start!" pressed.</summary>
    private static void StartPlaying(FakeBus bus)
    {
        bus.Publish(new StartScreenState(false));
        bus.Publish(new GameStart(GameStart.Girl, GameStart.Cat));
    }

    private string ManualPath(int slot) => Path.Combine(_folder, $"slot{slot}.json");

    private string AutoPath(int slot) => Path.Combine(_folder, $"slot{slot}-auto.json");

    // ---- Round trip ----

    [Fact]
    public void Parts_round_trip_through_a_file_in_order_then_GameLoaded()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        var player = new FakeSaver(bus, SaveModules.Player, "{\"x\":1.5,\"area\":\"Home\",\"health\":7}");
        var wallet = new FakeSaver(bus, SaveModules.Wallet, "{\"coins\":42}");
        var notJson = new FakeSaver(bus, SaveModules.NPCs, "quests: not json \"quoted\"");

        bus.Publish(new SaveGame(2, false));
        Tick(save);
        Assert.True(File.Exists(ManualPath(2)));
        Assert.False(File.Exists(AutoPath(2)));
        Assert.Equal(new GameSaved(2, false, true), bus.All<GameSaved>().Single());

        // A fresh game (new module and fakes with other state) loads it back.
        var bus2 = new FakeBus();
        var load = NewModule(bus2);
        var player2 = new FakeSaver(bus2, SaveModules.Player, "{}");
        var wallet2 = new FakeSaver(bus2, SaveModules.Wallet, "{}");
        var npcs2 = new FakeSaver(bus2, SaveModules.NPCs, "");
        Tick(load);
        Assert.Equal(0, bus2.All<CurrentSlot>().Last().Slot);
        bus2.Clear();
        bus2.Publish(new LoadGame(2, false));
        Tick(load);

        Assert.Equal(new[] { player.Json }, player2.Restored);
        Assert.Equal(new[] { wallet.Json }, wallet2.Restored);
        Assert.Equal(new[] { notJson.Json }, npcs2.Restored);
        var order = bus2.Published.Where(m => m is RestorePart or GameLoaded).ToList();
        Assert.Equal(4, order.Count);
        Assert.Equal(new[] { "Player", "Wallet", "NPCs" }, order.Take(3).Cast<RestorePart>().Select(p => p.Module));
        var loaded = Assert.IsType<GameLoaded>(order[3]);
        Assert.Equal(2, loaded.Slot);
        Assert.False(loaded.Auto);
        Assert.Equal(2, load.Current);
        Assert.Equal(2, bus2.All<CurrentSlot>().Last().Slot);
        Assert.True(load.Started);
    }

    [Fact]
    public void Each_slot_has_its_own_manual_save_and_separate_autosave()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        var player = new FakeSaver(bus, SaveModules.Player, "{\"v\":\"manual\"}");
        bus.Publish(new SaveGame(1, false));
        Tick(save);
        player.Json = "{\"v\":\"auto\"}";
        bus.Publish(new SaveGame(1, true));
        Tick(save);
        player.Json = "{\"v\":\"slot3\"}";
        bus.Publish(new SaveGame(3, false));
        Tick(save);

        bus.Publish(new LoadGame(1, false));
        Tick(save);
        bus.Publish(new LoadGame(1, true));
        Tick(save);
        bus.Publish(new LoadGame(3, false));
        Tick(save);
        Assert.Equal(new[] { "{\"v\":\"manual\"}", "{\"v\":\"auto\"}", "{\"v\":\"slot3\"}" }, player.Restored);
        Assert.Contains(new GameLoaded(1, true), bus.All<GameLoaded>());
    }

    [Fact]
    public void Collected_parts_answering_another_request_are_ignored_and_a_repeated_module_keeps_the_latest()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        new FakeSaver(bus, SaveModules.Pet, "{\"a\":1}");
        new FakeSaver(bus, SaveModules.Pet, "{\"a\":2}");
        bus.Publish(new SavePart(12345, SaveModules.Home, "{\"stray\":true}"));
        bus.Publish(new SaveGame(1, false));
        Tick(save);

        var data = save.Files!.Read(1, false)!;
        Assert.Equal(new[] { new KeyValuePair<string, string>("Pet", "{\"a\":2}") }, data.Parts);
    }

    [Fact]
    public void Missing_parts_are_not_restored()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        new FakeSaver(bus, SaveModules.Wallet, "{\"coins\":1}");
        bus.Publish(new SaveGame(1, false));
        Tick(save);

        var bus2 = new FakeBus();
        var load = NewModule(bus2);
        var home = new FakeSaver(bus2, SaveModules.Home, "{}");
        bus2.Publish(new LoadGame(1, false));
        Tick(load);
        Assert.Empty(home.Restored);
        Assert.Single(bus2.All<RestorePart>());
        Assert.Single(bus2.All<GameLoaded>());
    }

    // ---- Atomic write ----

    [Fact]
    public void Writes_go_through_a_temp_file_that_is_renamed_over_the_old_one()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        var player = new FakeSaver(bus, SaveModules.Player, "{\"n\":1}");
        Directory.CreateDirectory(_folder);
        // A temp file left by a crash mid-write does not matter.
        File.WriteAllText(ManualPath(1) + ".tmp", "{ half writ");
        bus.Publish(new SaveGame(1, false));
        Tick(save);
        player.Json = "{\"n\":2}";
        bus.Publish(new SaveGame(1, false));
        Tick(save);

        Assert.Equal(new[] { "slot1.json" }, Directory.GetFiles(_folder).Select(Path.GetFileName));
        Assert.Equal("{\"n\":2}", save.Files!.Read(1, false)!.Parts.Single().Value);
    }

    [Fact]
    public void A_failed_write_keeps_the_old_file_and_reports_not_ok()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        var player = new FakeSaver(bus, SaveModules.Player, "{\"n\":1}");
        bus.Publish(new SaveGame(1, false));
        Tick(save);
        byte[] before = File.ReadAllBytes(ManualPath(1));

        // The temp file cannot be created (a folder is in its way).
        Directory.CreateDirectory(ManualPath(1) + ".tmp");
        player.Json = "{\"n\":2}";
        bus.Clear();
        bus.Publish(new SaveGame(1, false));
        Tick(save);

        Assert.Equal(new GameSaved(1, false, false), bus.All<GameSaved>().Single());
        Assert.False(save.LastSaveOk);
        Assert.Equal(before, File.ReadAllBytes(ManualPath(1)));
        Assert.Equal("{\"n\":1}", save.Files!.Read(1, false)!.Parts.Single().Value);
    }

    // ---- Corrupt files ----

    [Theory]
    [InlineData("{ \"version\": 1, \"parts\": { \"Player\": ")]
    [InlineData("garbage \u0000\u0001")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"version\": 99, \"parts\": {} }")]
    [InlineData("{ \"parts\": {} }")]
    [InlineData("{ \"version\": 1, \"parts\": \"none\" }")]
    [InlineData("")]
    public void A_corrupt_file_is_listed_as_unreadable_and_loading_it_restores_nothing(string content)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(AutoPath(2), content);
        var bus = new FakeBus();
        var save = NewModule(bus);
        var player = new FakeSaver(bus, SaveModules.Player, "{}");
        Tick(save);

        var slot = bus.All<SaveSlots>().Last().Get(2);
        Assert.True(slot.Exists);
        Assert.Equal(SaveFileState.Unreadable, slot.Auto.State);
        Assert.Equal(SaveFileState.None, slot.Manual.State);
        Assert.False(bus.All<SaveSlots>().Last().TryGetNewest(out _, out _));

        bus.Publish(new LoadGame(2, true));
        Tick(save);
        Assert.Empty(player.Restored);
        Assert.Empty(bus.All<RestorePart>());
        Assert.Empty(bus.All<GameLoaded>());
        Assert.False(save.LastLoadOk);
        Assert.Equal(0, save.Current);

        // Saving over it works.
        bus.Publish(new SaveGame(2, true));
        Tick(save);
        Assert.Equal(SaveFileState.Ok, save.Slots.Get(2).Auto.State);
    }

    [Fact]
    public void Loading_a_missing_save_does_nothing()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        bus.Publish(new LoadGame(1, false));
        bus.Publish(new LoadGame(7, false));
        Tick(save);
        Assert.Empty(bus.All<GameLoaded>());
        Assert.Equal(0, save.Current);
    }

    // ---- Autosave ----

    [Fact]
    public void Autosaves_every_60_seconds_of_play_to_the_current_slots_auto_file()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        new FakeSaver(bus, SaveModules.Player, "{}");
        bus.Publish(new NewGameInSlot(3));
        StartPlaying(bus);
        Tick(save, 60 * 60 - 1);
        Assert.False(File.Exists(AutoPath(3)));
        Tick(save);
        Assert.True(File.Exists(AutoPath(3)));
        Assert.False(File.Exists(ManualPath(3)));
        Assert.Equal(new GameSaved(3, true, true), bus.All<GameSaved>().Single());

        Tick(save, 60 * 60 - 1);
        Assert.Single(bus.All<GameSaved>());
        Tick(save);
        Assert.Equal(2, bus.All<GameSaved>().Count);
    }

    [Fact]
    public void Play_time_does_not_count_on_the_title_screen_or_in_menus()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        // Before the game starts.
        Tick(save, 70 * 60);
        bus.Publish(new StartScreenState(true));
        Tick(save, 70 * 60);
        Assert.Empty(bus.All<GameSaved>());

        StartPlaying(bus);
        Tick(save, 30 * 60);
        // Another module's menu.
        bus.Publish(new UiFocus(77, true));
        Tick(save, 40 * 60);
        bus.Publish(new UiFocus(77, false));
        // The pause menu.
        Assert.True(save.OpenMenu());
        Tick(save, 40 * 60);
        save.CloseMenu();
        Assert.Empty(bus.All<GameSaved>());
        Assert.Equal(30.0, save.PlaySeconds, 3);
        Tick(save, 30 * 60);
        Assert.Equal(new GameSaved(1, true, true), bus.All<GameSaved>().Single());
    }

    [Fact]
    public void A_new_game_without_a_chosen_slot_plays_in_slot_1()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        Tick(save);
        Assert.Equal(0, save.Current);
        StartPlaying(bus);
        Tick(save);
        Assert.Equal(1, save.Current);
        Assert.Equal(1, bus.All<CurrentSlot>().Last().Slot);
    }

    [Fact]
    public void Autosaves_when_the_player_enters_Home()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        new FakeSaver(bus, SaveModules.Player, "{}");
        bus.Publish(new NewGameInSlot(2));
        bus.Publish(Player(Area.Town));
        Tick(save, 2 * 60);
        StartPlaying(bus);
        bus.Publish(Player(Area.Town));
        Tick(save);
        Assert.Empty(bus.All<GameSaved>());

        bus.Publish(Player(Area.Home));
        Tick(save);
        Assert.Equal(new GameSaved(2, true, true), bus.All<GameSaved>().Single());
        Assert.Equal(Area.Home, save.Slots.Get(2).Auto.Area);

        // Staying home does not save again; going out and back in does.
        bus.Publish(Player(Area.Home));
        Tick(save);
        bus.Publish(Player(Area.Home));
        Tick(save);
        Assert.Single(bus.All<GameSaved>());
        bus.Publish(Player(Area.Town));
        Tick(save);
        bus.Publish(Player(Area.Home));
        Tick(save);
        Assert.Equal(2, bus.All<GameSaved>().Count);
    }

    [Fact]
    public void Entering_Home_on_the_title_screen_or_just_after_a_load_does_not_autosave()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        new FakeSaver(bus, SaveModules.Player, "{}");
        bus.Publish(new StartScreenState(true));
        bus.Publish(Player(Area.Town));
        bus.Publish(Player(Area.Home));
        Tick(save);
        Assert.Empty(bus.All<GameSaved>());

        bus.Publish(new SaveGame(1, false));
        bus.Publish(new StartScreenState(false));
        Tick(save);
        bus.Clear();
        bus.Publish(Player(Area.Town));
        bus.Publish(new LoadGame(1, false));
        Tick(save);
        // The load puts the player back in the house.
        bus.Publish(Player(Area.Home));
        Tick(save);
        Assert.Empty(bus.All<GameSaved>());
    }

    // ---- Slot list ----

    [Fact]
    public void The_slot_list_tells_each_slots_saves_times_character_day_coins_and_area()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        bus.Publish(Player(Area.Forest, CharacterKind.Boy));
        bus.Publish(new WalletState(123));
        bus.Publish(new TimeOfDay(4, 10f, false));
        bus.Publish(new SaveGame(1, false));
        Tick(save);
        _now = _now.AddMinutes(5);
        bus.Publish(Player(Area.PetCity, CharacterKind.Boy));
        bus.Publish(new WalletState(150));
        bus.Publish(new SaveGame(1, true));
        Tick(save);

        var slots = bus.All<SaveSlots>().Last();
        var slot1 = slots.Get(1);
        Assert.True(slot1.Exists);
        Assert.Equal(SaveFileState.Ok, slot1.Manual.State);
        Assert.Equal(new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc), slot1.ManualSavedAt);
        Assert.Equal(new DateTime(2026, 10, 24, 12, 5, 0, DateTimeKind.Utc), slot1.AutoSavedAt);
        Assert.Equal("Boy", slot1.Manual.CharacterName);
        Assert.Equal(CharacterKind.Boy, slot1.Manual.Character);
        Assert.Equal(4, slot1.Manual.Day);
        Assert.Equal(123, slot1.Manual.Coins);
        Assert.Equal(Area.Forest, slot1.Manual.Area);
        Assert.Equal(150, slot1.Auto.Coins);
        Assert.Equal(Area.PetCity, slot1.Auto.Area);
        Assert.True(slot1.NewestIsAuto);
        Assert.Equal(150, slot1.Newest.Coins);
        Assert.False(slots.Get(2).Exists);
        Assert.False(slots.Get(3).Exists);
        Assert.Equal(1, slots.Slot1.Slot);

        // The header is in the file itself.
        string text = File.ReadAllText(ManualPath(1));
        foreach (var field in new[] { "\"version\": 1", "\"slot\": 1", "\"auto\": false", "\"savedAt\"", "\"characterName\": \"Boy\"", "\"character\": \"Boy\"", "\"day\": 4", "\"coins\": 123", "\"area\": \"Forest\"", "\"parts\"" })
            Assert.Contains(field, text);
    }

    [Fact]
    public void Continue_is_the_newest_save_of_any_slot_manual_or_auto()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        Tick(save);
        Assert.False(bus.All<SaveSlots>().Last().TryGetNewest(out _, out _));

        bus.Publish(new SaveGame(3, false));
        Tick(save);
        _now = _now.AddSeconds(1);
        bus.Publish(new SaveGame(1, true));
        Tick(save);
        _now = _now.AddSeconds(1);
        bus.Publish(new SaveGame(2, false));
        Tick(save);
        Assert.True(save.Slots.TryGetNewest(out int slot, out bool auto));
        Assert.Equal((2, false), (slot, auto));

        _now = _now.AddSeconds(1);
        bus.Publish(new SaveGame(1, true));
        Tick(save);
        Assert.True(save.Slots.TryGetNewest(out slot, out auto));
        Assert.Equal((1, true), (slot, auto));
    }

    [Fact]
    public void The_slot_list_is_published_every_second_and_at_once_after_a_change()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        Tick(save, 180);
        Assert.Equal(3, bus.All<SaveSlots>().Count);
        Assert.Equal(180, bus.All<CurrentSlot>().Count);

        bus.Clear();
        bus.Publish(new SaveGame(1, false));
        Tick(save);
        Assert.True(bus.All<SaveSlots>().Single().Get(1).Exists);
    }

    [Fact]
    public void DeleteSlot_removes_both_saves_of_that_slot_only()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        bus.Publish(new SaveGame(1, false));
        bus.Publish(new SaveGame(1, true));
        bus.Publish(new SaveGame(2, false));
        Tick(save);
        bus.Publish(new DeleteSlot(1));
        Tick(save);
        Assert.False(File.Exists(ManualPath(1)));
        Assert.False(File.Exists(AutoPath(1)));
        Assert.True(File.Exists(ManualPath(2)));
        Assert.False(save.Slots.Get(1).Exists);
        Assert.True(save.Slots.Get(2).Exists);
    }

    [Fact]
    public void NewGameInSlot_sets_the_current_slot_and_deletes_nothing_until_the_first_save()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        var player = new FakeSaver(bus, SaveModules.Player, "{\"old\":true}");
        bus.Publish(new SaveGame(2, false));
        bus.Publish(new SaveGame(2, true));
        Tick(save);

        player.Json = "{\"new\":true}";
        bus.Publish(new NewGameInSlot(2));
        Tick(save);
        Assert.Equal(2, save.Current);
        Assert.Equal(2, bus.All<CurrentSlot>().Last().Slot);
        Assert.True(File.Exists(ManualPath(2)));
        Assert.True(File.Exists(AutoPath(2)));

        // Save to the current slot (slot 0) overwrites its manual save.
        bus.Publish(new SaveGame(0, false));
        Tick(save);
        Assert.Equal("{\"new\":true}", save.Files!.Read(2, false)!.Parts.Single().Value);
        Assert.Equal("{\"old\":true}", save.Files!.Read(2, true)!.Parts.Single().Value);
    }

    [Fact]
    public void Saving_to_the_current_slot_with_none_does_nothing()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        bus.Publish(new SaveGame(0, false));
        bus.Publish(new SaveGame(4, false));
        Tick(save);
        Assert.Empty(bus.All<GameSaved>());
        Assert.False(Directory.Exists(_folder) && Directory.GetFiles(_folder).Length > 0);
    }

    // ---- Pause menu ----

    [Fact]
    public void The_pause_menu_opens_only_while_playing_and_publishes_UiFocus_while_open()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        Assert.False(save.OpenMenu());
        bus.Publish(new StartScreenState(true));
        bus.Publish(new GameStart(GameStart.Boy, GameStart.Dog));
        Assert.False(save.OpenMenu());
        bus.Publish(new StartScreenState(false));
        bus.Publish(new UiFocus(5, true));
        Assert.False(save.OpenMenu());
        bus.Publish(new UiFocus(5, false));
        Tick(save);
        Assert.DoesNotContain(bus.All<UiFocus>(), f => f.Owner == SaveModule.MenuOwner);

        Assert.True(save.OpenMenu());
        Assert.True(save.MenuOpen);
        Assert.False(save.CanOpenMenu);
        Tick(save, 3);
        Assert.Equal(new[] { new UiFocus(SaveModule.MenuOwner, true) }, bus.All<UiFocus>().Where(f => f.Owner == SaveModule.MenuOwner));
        // Its own UiFocus does not count as another menu.
        Assert.False(save.OtherMenuOpen);

        save.RequestSave(0, false);
        Tick(save);
        Assert.Equal(1, save.SaveCount);
        Assert.True(save.LastSaveOk);
        Assert.True(File.Exists(ManualPath(1)));

        save.CloseMenu();
        Tick(save);
        Assert.Equal(new UiFocus(SaveModule.MenuOwner, false), bus.All<UiFocus>().Last());
        Assert.True(save.CanOpenMenu);
    }

    [Fact]
    public void Loading_from_the_pause_menu_closes_it()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        StartPlaying(bus);
        bus.Publish(new SaveGame(3, false));
        Tick(save);
        Assert.True(save.OpenMenu());
        Tick(save);
        save.RequestLoad(3, false);
        Tick(save);
        Assert.False(save.MenuOpen);
        Assert.Equal(3, save.Current);
        Assert.Equal(new UiFocus(SaveModule.MenuOwner, false), bus.All<UiFocus>().Last());
    }

    [Fact]
    public void Ticks_do_not_allocate()
    {
        var bus = new CountingBus();
        var save = new SaveModule { SaveFolder = _folder };
        save.Start(bus);
        save.Handle(new GameStart(0, 0));
        save.Handle(new StartScreenState(false));
        for (int i = 0; i < 200; i++)
            save.Tick(new TickContext(i, Step));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 200; i < 2000; i++)
        {
            save.Handle(Player(Area.Town));
            save.Handle(new ActiveDevice(i % 2 == 0 ? DeviceKind.Xbox : DeviceKind.KeyboardMouse));
            save.Tick(new TickContext(i, Step));
        }
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    // ---- Button hints ----

    private static readonly string[] IconPrefixes = { "key_", "mouse_", "xbox_", "ps_", "ns_" };

    private static string PrefixOf(DeviceKind device) => device switch
    {
        DeviceKind.KeyboardMouse => "key_",
        DeviceKind.PlayStation => "ps_",
        DeviceKind.Nintendo => "ns_",
        _ => "xbox_", // Xbox and Generic pads
    };

    /// <summary>Every icon path in a hint line.</summary>
    private static List<string> Icons(string line) =>
        System.Text.RegularExpressions.Regex.Matches(line, @"\[img=(\d+)\](.*?)\[/img\]").Select(m => m.Groups[2].Value).ToList();

    [Theory]
    [InlineData(DeviceKind.KeyboardMouse, true, "key_up", "key_down", "key_enter", "key_esc")]
    [InlineData(DeviceKind.KeyboardMouse, false, "key_up", "key_down", "key_enter", "key_esc")]
    [InlineData(DeviceKind.Xbox, true, "xbox_dpad_up", "xbox_dpad_down", "xbox_a", "xbox_menu")]
    [InlineData(DeviceKind.Xbox, false, "xbox_dpad_up", "xbox_dpad_down", "xbox_a", "xbox_b")]
    [InlineData(DeviceKind.Generic, false, "xbox_dpad_up", "xbox_dpad_down", "xbox_a", "xbox_b")]
    [InlineData(DeviceKind.PlayStation, true, "ps_dpad_up", "ps_dpad_down", "ps_cross", "ps_options")]
    [InlineData(DeviceKind.PlayStation, false, "ps_dpad_up", "ps_dpad_down", "ps_cross", "ps_circle")]
    [InlineData(DeviceKind.Nintendo, false, "ns_dpad_up", "ns_dpad_down", "ns_b", "ns_a")]
    public void Hints_show_only_the_active_devices_icons(DeviceKind device, bool mainPage, string up, string down, string choose, string leave)
    {
        string line = PauseHints.Line(device, mainPage);
        var icons = Icons(line);
        Assert.Equal(
            new[] { up, down, choose, leave }.Select(g => Prompts.GlyphDirectory + g + ".svg"),
            icons);
        Assert.All(icons, icon => Assert.StartsWith(Prompts.GlyphDirectory + PrefixOf(device), icon));
        foreach (var prefix in IconPrefixes.Where(p => p != PrefixOf(device)))
            Assert.DoesNotContain(Prompts.GlyphDirectory + prefix, line);

        // Only icons and the words: no key names, no "E / X" or "(B)" text.
        string words = System.Text.RegularExpressions.Regex.Replace(line, @"\[img=\d+\].*?\[/img\]", "").Trim();
        Assert.Equal(mainPage ? new[] { "Move", "Choose", "Resume" } : new[] { "Move", "Choose", "Back" },
            words.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        Assert.DoesNotContain("/", words);
        Assert.DoesNotContain("(", words);
    }

    [Fact]
    public void Hint_icons_take_the_size_asked_for()
    {
        string line = PauseHints.Line(DeviceKind.PlayStation, true, 52);
        var sizes = System.Text.RegularExpressions.Regex.Matches(line, @"\[img=(\d+)\]").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(4, sizes.Count);
        Assert.All(sizes, size => Assert.Equal("52", size));
        Assert.Contains(Prompts.Rich(PromptAction.Confirm, DeviceKind.PlayStation, 52), line);
    }

    [Fact]
    public void Device_follows_ActiveDevice_so_the_hints_change_live()
    {
        var bus = new FakeBus();
        var save = NewModule(bus);
        Assert.Equal(DeviceKind.KeyboardMouse, save.Device);
        bus.Publish(new ActiveDevice(DeviceKind.Xbox));
        Assert.Equal(DeviceKind.Xbox, save.Device);
        Assert.Contains("xbox_a", PauseHints.Line(save.Device, true));
        bus.Publish(new ActiveDevice(DeviceKind.PlayStation));
        Assert.Equal(DeviceKind.PlayStation, save.Device);
        Assert.Contains("ps_cross", PauseHints.Line(save.Device, true));
        bus.Publish(new ActiveDevice(DeviceKind.KeyboardMouse));
        Assert.Equal(DeviceKind.KeyboardMouse, save.Device);
        Assert.Contains("key_enter", PauseHints.Line(save.Device, true));
    }
}
