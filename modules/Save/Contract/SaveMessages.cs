using System;
using Fungtopia.Map;
using Fungtopia.Player;

namespace Fungtopia.Save;

/// <summary>
/// Published by the Save module when it saves. Every module with saved state answers at once
/// (the bus is synchronous) with one <see cref="SavePart"/> carrying the same
/// <see cref="RequestId"/>.
/// </summary>
public readonly struct CollectSave
{
    public readonly int RequestId;

    public CollectSave(int requestId) => RequestId = requestId;
}

/// <summary>A module's answer to <see cref="CollectSave"/>: its whole saved state as JSON.</summary>
public readonly struct SavePart
{
    /// <summary>The <see cref="CollectSave.RequestId"/> answered.</summary>
    public readonly int RequestId;

    /// <summary>The module's name, one of <see cref="SaveModules"/>.</summary>
    public readonly string Module;

    /// <summary>The module's state, any JSON the module can read back.</summary>
    public readonly string Json;

    public SavePart(int requestId, string module, string json)
    {
        RequestId = requestId;
        Module = module;
        Json = json;
    }
}

/// <summary>
/// Published by the Save module once per saved part when it loads, before
/// <see cref="GameLoaded"/>. The module named <see cref="Module"/> restores its state from
/// <see cref="Json"/>; when that is missing or unreadable it keeps its defaults.
/// </summary>
public readonly struct RestorePart
{
    public readonly string Module;

    public readonly string Json;

    public RestorePart(string module, string json)
    {
        Module = module;
        Json = json;
    }
}

/// <summary>Published by the Save module after every <see cref="RestorePart"/> of a load.</summary>
public readonly struct GameLoaded
{
    /// <summary>The slot loaded, 1..<see cref="SaveSlots.Count"/>.</summary>
    public readonly int Slot;

    /// <summary>Whether it was the slot's autosave (false: its manual save).</summary>
    public readonly bool Auto;

    public GameLoaded(int slot) : this(slot, false)
    {
    }

    public GameLoaded(int slot, bool auto)
    {
        Slot = slot;
        Auto = auto;
    }
}

/// <summary>
/// Asks the Save module to save the game to <see cref="Slot"/>'s manual save, or its autosave
/// when <see cref="Auto"/>. Slot 0 means the current slot (ignored when there is none).
/// </summary>
public readonly struct SaveGame
{
    public readonly int Slot;

    public readonly bool Auto;

    public SaveGame(int slot, bool auto)
    {
        Slot = slot;
        Auto = auto;
    }
}

/// <summary>Published by the Save module once per save it tried (manual or auto).</summary>
public readonly struct GameSaved
{
    public readonly int Slot;

    public readonly bool Auto;

    /// <summary>False when the file could not be written (the old one is kept).</summary>
    public readonly bool Ok;

    public GameSaved(int slot, bool auto, bool ok)
    {
        Slot = slot;
        Auto = auto;
        Ok = ok;
    }
}

/// <summary>Asks the Save module to load <see cref="Slot"/>'s manual save, or its autosave when <see cref="Auto"/>.</summary>
public readonly struct LoadGame
{
    public readonly int Slot;

    public readonly bool Auto;

    public LoadGame(int slot, bool auto)
    {
        Slot = slot;
        Auto = auto;
    }
}

/// <summary>Asks the Save module to delete both saves (manual and auto) of <see cref="Slot"/>.</summary>
public readonly struct DeleteSlot
{
    public readonly int Slot;

    public DeleteSlot(int slot) => Slot = slot;
}

/// <summary>
/// A new game is started in <see cref="Slot"/>: it becomes the current slot. Nothing is deleted
/// until the first save overwrites the slot's files.
/// </summary>
public readonly struct NewGameInSlot
{
    public readonly int Slot;

    public NewGameInSlot(int slot) => Slot = slot;
}

/// <summary>The slot being played, published by the Save module every tick (0: none).</summary>
public readonly struct CurrentSlot
{
    public readonly int Slot;

    public CurrentSlot(int slot) => Slot = slot;
}

/// <summary>What is in one save file.</summary>
public enum SaveFileState
{
    /// <summary>No file.</summary>
    None,

    /// <summary>A file that was read; the header fields are valid.</summary>
    Ok,

    /// <summary>A file that exists but cannot be read (corrupt, or from a newer version).</summary>
    Unreadable,
}

/// <summary>The header of one save file, for the slot list.</summary>
public readonly struct SaveInfo
{
    public readonly SaveFileState State;

    /// <summary>When it was saved (UTC); default when <see cref="State"/> is not Ok.</summary>
    public readonly DateTime SavedAt;

    /// <summary>The character's name ("Girl" or "Boy" until characters get names); "" when not Ok.</summary>
    public readonly string CharacterName;

    public readonly CharacterKind Character;

    /// <summary>The DayNight day (1 and up; 0 when unknown).</summary>
    public readonly int Day;

    public readonly int Coins;

    public readonly Area Area;

    public SaveInfo(SaveFileState state, DateTime savedAt, string characterName, CharacterKind character, int day, int coins, Area area)
    {
        State = state;
        SavedAt = savedAt;
        CharacterName = characterName ?? "";
        Character = character;
        Day = day;
        Coins = coins;
        Area = area;
    }

    public bool Exists => State != SaveFileState.None;

    public bool Ok => State == SaveFileState.Ok;

    public static SaveInfo Missing => new(SaveFileState.None, default, "", CharacterKind.Girl, 0, 0, Area.Town);

    public static SaveInfo Unreadable => new(SaveFileState.Unreadable, default, "", CharacterKind.Girl, 0, 0, Area.Town);
}

/// <summary>One save slot: its manual save and its separate autosave.</summary>
public readonly struct SlotInfo
{
    /// <summary>1..<see cref="SaveSlots.Count"/>.</summary>
    public readonly int Slot;

    public readonly SaveInfo Manual;

    public readonly SaveInfo Auto;

    public SlotInfo(int slot, SaveInfo manual, SaveInfo auto)
    {
        Slot = slot;
        Manual = manual;
        Auto = auto;
    }

    /// <summary>Either file exists (readable or not).</summary>
    public bool Exists => Manual.Exists || Auto.Exists;

    public DateTime ManualSavedAt => Manual.SavedAt;

    public DateTime AutoSavedAt => Auto.SavedAt;

    /// <summary>Whether the newest readable save is the autosave (false when only the manual one is, or none).</summary>
    public bool NewestIsAuto => Auto.Ok && (!Manual.Ok || Auto.SavedAt > Manual.SavedAt);

    /// <summary>The newest readable save's header (manual or auto), or <see cref="SaveInfo.Missing"/>.</summary>
    public SaveInfo Newest => NewestIsAuto ? Auto : Manual.Ok ? Manual : SaveInfo.Missing;
}

/// <summary>
/// The save slots on disk, published by the Save module every second (and at once after it
/// saves or deletes).
/// </summary>
public readonly struct SaveSlots
{
    public const int Count = 3;

    public readonly SlotInfo Slot1;

    public readonly SlotInfo Slot2;

    public readonly SlotInfo Slot3;

    public SaveSlots(SlotInfo slot1, SlotInfo slot2, SlotInfo slot3)
    {
        Slot1 = slot1;
        Slot2 = slot2;
        Slot3 = slot3;
    }

    /// <summary>Slot 1..<see cref="Count"/>; anything else throws.</summary>
    public SlotInfo Get(int slot) => slot switch
    {
        1 => Slot1,
        2 => Slot2,
        3 => Slot3,
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };

    /// <summary>
    /// The newest readable save of any slot, manual or auto (what "Continue" loads). False when
    /// there is none.
    /// </summary>
    public bool TryGetNewest(out int slot, out bool auto)
    {
        slot = 0;
        auto = false;
        DateTime best = default;
        for (int i = 1; i <= Count; i++)
        {
            var info = Get(i);
            var newest = info.Newest;
            if (newest.Ok && (slot == 0 || newest.SavedAt > best))
            {
                slot = i;
                auto = info.NewestIsAuto;
                best = newest.SavedAt;
            }
        }
        return slot != 0;
    }
}

/// <summary>The module names used in <see cref="SavePart"/> and <see cref="RestorePart"/>.</summary>
public static class SaveModules
{
    public const string Player = "Player";
    public const string Wardrobe = "Wardrobe";
    public const string Wallet = "Wallet";
    public const string Pet = "Pet";
    public const string Home = "Home";
    public const string NPCs = "NPCs";
    public const string DayNight = "DayNight";
}
