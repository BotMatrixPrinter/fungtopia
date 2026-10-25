namespace Fungtopia.Town;

/// <summary>
/// How the town's mushrooms are shaded ("vivid", the same rule for every mushroom in the game):
/// which town.glb materials are mushroom caps (drawn with a soft glossy band and a light rim),
/// and how brightly the glowing bits (lamp bulbs, glowing caps and spots: the Glow material) and
/// the windows light up by day and at night. Plain data, read by the view.
/// </summary>
public static class TownLook
{
    /// <summary>The town.glb materials that are mushroom caps (houses, shop and stall roofs, the fountain, lamps, mailbox, bench feet).</summary>
    public static readonly string[] Caps = { "Cap", "CapPink", "CapWhite", "CapOrange", "CapLilac", "CapBlue", "CapYellow", "CapMint" };

    /// <summary>Strength of a cap's small specular band (the toon shader's <c>gloss</c>; 0 on everything else).</summary>
    public const float CapGloss = 0.5f;

    /// <summary>Strength of the light rim along a cap's edge (the toon shader's <c>rim_light</c>; 0 on everything else).</summary>
    public const float CapRim = 0.4f;

    /// <summary>Glow (lamp bulbs, glowing caps and spots) self light by day and at night: twice what it was before the vivid look (0.12, 2.2).</summary>
    public const float DayGlow = 0.24f;
    public const float NightGlow = 4.4f;

    /// <summary>Window self light by day and at night (unchanged: windows are not mushrooms).</summary>
    public const float WindowDayGlow = 0.084f;
    public const float WindowNightGlow = 1.54f;

    /// <summary>Whether material <paramref name="name"/> of town.glb is a mushroom cap.</summary>
    public static bool IsCap(string name) => System.Array.IndexOf(Caps, name) >= 0;

    /// <summary>Glow's self light at <paramref name="lit"/> (0 day .. 1 night, fading between).</summary>
    public static float GlowAt(float lit) => DayGlow + (NightGlow - DayGlow) * lit;

    /// <summary>Windows' self light at <paramref name="lit"/> (0 day .. 1 night).</summary>
    public static float WindowGlowAt(float lit) => WindowDayGlow + (WindowNightGlow - WindowDayGlow) * lit;
}
