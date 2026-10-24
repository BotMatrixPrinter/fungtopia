using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Fungtopia.Map;
using Fungtopia.Player;

namespace Fungtopia.Save;

/// <summary>One save file: the header shown in the slot list and the modules' parts.</summary>
public sealed class SaveData
{
    public int Version = SaveFiles.Version;
    public int Slot;
    public bool Auto;
    public DateTime SavedAt;
    public string CharacterName = "";
    public CharacterKind Character;
    public int Day;
    public int Coins;
    public Area Area;

    /// <summary>(module, json) in the order they were saved; one per module.</summary>
    public readonly List<KeyValuePair<string, string>> Parts = new();

    public SaveInfo Info => new(SaveFileState.Ok, SavedAt, CharacterName, Character, Day, Coins, Area);
}

/// <summary>
/// The save files in one folder: slot&lt;N&gt;.json (manual) and slot&lt;N&gt;-auto.json (auto).
/// Writes are atomic (a temp file, then a rename over the old file); unreadable files are
/// reported, never thrown.
/// </summary>
public sealed class SaveFiles
{
    /// <summary>The file format written; files with a newer (or no) version are unreadable.</summary>
    public const int Version = 1;

    public SaveFiles(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        Folder = folder;
    }

    public string Folder { get; }

    public string PathOf(int slot, bool auto) =>
        Path.Combine(Folder, auto ? $"slot{slot}-auto.json" : $"slot{slot}.json");

    /// <summary>The temp file a write goes through before it is renamed to <see cref="PathOf"/>.</summary>
    public string TempPathOf(int slot, bool auto) => PathOf(slot, auto) + ".tmp";

    /// <summary>Writes the save atomically; false (the old file untouched) when that fails.</summary>
    public bool Write(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        string path = PathOf(data.Slot, data.Auto);
        string temp = TempPathOf(data.Slot, data.Auto);
        try
        {
            Directory.CreateDirectory(Folder);
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(Serialize(data));
                stream.Flush(true);
            }
            File.Move(temp, path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch (Exception e2) when (e2 is IOException or UnauthorizedAccessException)
            {
                // Best effort: a stale temp file is overwritten by the next write.
            }
            return false;
        }
    }

    /// <summary>Reads a save; null when there is no file or it cannot be read.</summary>
    public SaveData? Read(int slot, bool auto) => Read(slot, auto, out _);

    public SaveData? Read(int slot, bool auto, out SaveFileState state)
    {
        string path = PathOf(slot, auto);
        byte[] bytes;
        try
        {
            if (!File.Exists(path))
            {
                state = SaveFileState.None;
                return null;
            }
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            state = SaveFileState.Unreadable;
            return null;
        }
        var data = Parse(bytes);
        state = data == null ? SaveFileState.Unreadable : SaveFileState.Ok;
        return data;
    }

    /// <summary>The header of one file for the slot list.</summary>
    public SaveInfo ReadInfo(int slot, bool auto)
    {
        var data = Read(slot, auto, out var state);
        return state switch
        {
            SaveFileState.Ok => data!.Info,
            SaveFileState.Unreadable => SaveInfo.Unreadable,
            _ => SaveInfo.Missing,
        };
    }

    public SlotInfo ReadSlot(int slot) => new(slot, ReadInfo(slot, false), ReadInfo(slot, true));

    public SaveSlots ReadSlots() => new(ReadSlot(1), ReadSlot(2), ReadSlot(3));

    /// <summary>Deletes both files of a slot (and stale temp files); false if one could not be deleted.</summary>
    public bool Delete(int slot)
    {
        bool ok = true;
        foreach (bool auto in new[] { false, true })
        {
            foreach (var path in new[] { PathOf(slot, auto), TempPathOf(slot, auto) })
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    ok = false;
                }
            }
        }
        return ok;
    }

    /// <summary>
    /// The file's UTF-8 JSON: the header fields, then "parts" {module: json}. A part whose JSON is
    /// valid is embedded as JSON; any other text is kept as a JSON string.
    /// </summary>
    public static byte[] Serialize(SaveData data)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", data.Version);
            writer.WriteNumber("slot", data.Slot);
            writer.WriteBoolean("auto", data.Auto);
            writer.WriteString("savedAt", data.SavedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("characterName", data.CharacterName);
            writer.WriteString("character", data.Character.ToString());
            writer.WriteNumber("day", data.Day);
            writer.WriteNumber("coins", data.Coins);
            writer.WriteString("area", data.Area.ToString());
            writer.WriteStartObject("parts");
            foreach (var part in data.Parts)
            {
                writer.WritePropertyName(part.Key);
                if (IsJson(part.Value))
                    writer.WriteRawValue(part.Value, skipInputValidation: true);
                else
                    writer.WriteStringValue(part.Value);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    /// <summary>Reads a file's bytes; null when they are not a save this version can read.</summary>
    public static SaveData? Parse(byte[] bytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(bytes);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out int v)
                || v < 1 || v > Version
                || !root.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Object)
                return null;
            var data = new SaveData
            {
                Version = v,
                Slot = Int(root, "slot"),
                Auto = root.TryGetProperty("auto", out var auto) && auto.ValueKind == JsonValueKind.True,
                SavedAt = Time(root, "savedAt"),
                CharacterName = Text(root, "characterName"),
                Character = Enum.TryParse<CharacterKind>(Text(root, "character"), out var kind) ? kind : CharacterKind.Girl,
                Day = Int(root, "day"),
                Coins = Int(root, "coins"),
                Area = Enum.TryParse<Area>(Text(root, "area"), out var area) ? area : Area.Town,
            };
            foreach (var part in parts.EnumerateObject())
            {
                string json = part.Value.ValueKind == JsonValueKind.String ? part.Value.GetString()! : part.Value.GetRawText();
                data.Parts.Add(new KeyValuePair<string, string>(part.Name, json));
            }
            return data;
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or FormatException or DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool IsJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int Int(JsonElement root, string name) =>
        root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int v) ? v : 0;

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";

    private static DateTime Time(JsonElement root, string name) =>
        DateTime.TryParse(Text(root, name), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
            ? DateTime.SpecifyKind(t, DateTimeKind.Utc)
            : default;
}
