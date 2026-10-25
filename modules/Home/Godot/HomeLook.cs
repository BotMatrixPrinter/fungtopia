namespace Fungtopia.Home.View;

/// <summary>
/// How each .glb material of the home is toon-shaded, by its name: how much it glows, and the
/// mushroom caps' glossy highlight and rim light. Plain C# (no Godot), so the tests check it too.
/// </summary>
public static class HomeLook
{
    /// <summary>The rim light every surface has (the toon shader's default).</summary>
    public const float Rim = 0.3f;

    /// <summary>The mushroom caps (the furniture's and the pets' mushroom hats).</summary>
    public static bool IsCap(string material) => material is "Cap" or "CapPink" or "CapGlow";

    /// <summary>The emission strength (0: none). The mushroom lamp's glow is twice what it was (1.2).</summary>
    public static float Glow(string material) => material switch
    {
        "Glow" => 2.4f,
        "CapGlow" => 0.5f,
        "Sun" => 1.2f,
        "EyeShine" => 0.6f,
        _ => 0f,
    };

    /// <summary>The strength of the soft specular band (0: none; only mushroom caps have one).</summary>
    public static float Gloss(string material) => IsCap(material) ? 1f : 0f;

    /// <summary>The rim light's strength: brighter along the mushroom caps' edges.</summary>
    public static float RimLight(string material) => IsCap(material) ? 0.6f : Rim;
}
