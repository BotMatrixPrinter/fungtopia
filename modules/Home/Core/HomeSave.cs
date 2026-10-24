using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fungtopia.Home;

/// <summary>
/// The Home module's saved state (its <c>SavePart</c> for module "Home"): every piece of furniture
/// owned, in order, with where it stands when placed. Compact JSON, e.g.
/// <c>{"v":1,"items":[{"k":"BasicBed","p":true,"c":2,"r":0,"t":0}]}</c>.
/// </summary>
public static class HomeSave
{
    /// <summary>The format written; any other version is refused when restoring.</summary>
    public const int Version = 1;

    internal sealed record Data(
        [property: JsonPropertyName("v")] int Version,
        [property: JsonPropertyName("items")] Item[]? Items);

    internal sealed record Item(
        [property: JsonPropertyName("k")] FurnitureKind Kind,
        [property: JsonPropertyName("p")] bool Placed,
        [property: JsonPropertyName("c")] int Column,
        [property: JsonPropertyName("r")] int Row,
        [property: JsonPropertyName("t")] int Rotation);

    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter<FurnitureKind>(allowIntegerValues: false) },
    };

    /// <summary>The JSON of <paramref name="items"/> (an unplaced item is written at 0, 0).</summary>
    public static string Write(ReadOnlySpan<FurnitureItem> items)
    {
        var saved = new Item[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            saved[i] = item.Placed
                ? new Item(item.Kind, true, item.Column, item.Row, item.Rotation)
                : new Item(item.Kind, false, 0, 0, item.Rotation);
        }
        return JsonSerializer.Serialize(new Data(Version, saved), Options);
    }

    /// <summary>
    /// Reads furniture written by <see cref="Write"/> into <paramref name="items"/>: false (and
    /// <paramref name="error"/> says why) for missing or unreadable JSON, another version, an
    /// unknown kind, more than <paramref name="items"/> holds, or a turn outside 0..3. Whether
    /// placed items fit the room is left to the caller.
    /// </summary>
    public static bool TryRead(string? json, FurnitureItem[] items, out int count, out string error)
    {
        count = 0;
        Data? data;
        try
        {
            data = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<Data>(json, Options);
        }
        catch (JsonException e)
        {
            error = "unreadable JSON (" + e.Message + ")";
            return false;
        }
        if (data == null)
        {
            error = "no JSON";
            return false;
        }
        if (data.Version != Version)
        {
            error = "unknown version " + data.Version;
            return false;
        }
        var saved = data.Items ?? Array.Empty<Item>();
        if (saved.Length > items.Length)
        {
            error = saved.Length + " items, more than " + items.Length;
            return false;
        }
        for (int i = 0; i < saved.Length; i++)
        {
            var s = saved[i];
            if (s == null || (uint)s.Kind >= Furniture.KindCount || s.Rotation < 0 || s.Rotation > 3)
            {
                error = "bad item " + i;
                return false;
            }
            items[i] = new FurnitureItem
            {
                Kind = s.Kind, Placed = s.Placed, Column = s.Placed ? s.Column : 0, Row = s.Placed ? s.Row : 0,
                Rotation = s.Rotation,
            };
        }
        count = saved.Length;
        error = "";
        return true;
    }
}
