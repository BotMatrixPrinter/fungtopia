using System;
using System.Text.Json;

namespace Fungtopia.Food;

/// <summary>
/// The Food module's saved state as compact JSON:
/// <c>{"v":1,"muffins":3,"soups":0,"pies":1,"selected":0}</c> (the bag's counts and the selection).
/// </summary>
public static class FoodSave
{
    public const int Version = 1;

    public static string Write(int muffins, int soups, int pies, int selected) =>
        $"{{\"v\":{Version},\"muffins\":{muffins},\"soups\":{soups},\"pies\":{pies},\"selected\":{selected}}}";

    /// <summary>
    /// Reads <paramref name="json"/> into <paramref name="counts"/> (3, by <see cref="FoodKinds"/>)
    /// and <paramref name="selected"/>. False with an <paramref name="error"/> when it is missing,
    /// not JSON, another version, or a field is missing or out of range (counts 0..9, selection 0..2).
    /// </summary>
    public static bool TryRead(string? json, int[] counts, out int selected, out string error)
    {
        selected = 0;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "no saved food";
            return false;
        }
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "not a JSON object";
                return false;
            }
            if (!Int(root, "v", out int version) || version != Version)
            {
                error = "unknown version";
                return false;
            }
            string[] names = { "muffins", "soups", "pies" };
            for (int i = 0; i < FoodKinds.Count; i++)
            {
                if (!Int(root, names[i], out counts[i]) || counts[i] < 0 || counts[i] > FoodCatalog.MaxEach)
                {
                    error = $"bad \"{names[i]}\"";
                    return false;
                }
            }
            if (!Int(root, "selected", out selected) || !FoodCatalog.Valid(selected))
            {
                error = "bad \"selected\"";
                return false;
            }
        }
        catch (JsonException e)
        {
            error = "not JSON: " + e.Message;
            return false;
        }
        error = "";
        return true;
    }

    private static bool Int(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out value);
    }
}
