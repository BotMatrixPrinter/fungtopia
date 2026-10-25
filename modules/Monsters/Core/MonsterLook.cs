using System;

namespace Fungtopia.Monsters;

/// <summary>
/// How the view shades each material of the monsters' models (by its glTF material name, from
/// Models/monsters.py), kept here so tests can check it. The mushrooms on the monsters (caps,
/// spots, stems) follow the game's "vivid mushroom" rule: their caps get a small glossy highlight
/// and a light rim along the edge, and glowing caps glow twice as bright as they used to; every
/// other material keeps the plain toon look.
/// </summary>
public static class MonsterLook
{
    /// <summary>Strength of the small specular band on a mushroom cap (0 elsewhere).</summary>
    public const float CapGloss = 0.45f;

    /// <summary>Strength of the light rim along a mushroom cap's edge (0 elsewhere).</summary>
    public const float CapRim = 0.35f;

    /// <summary>Self-light of the white spots on mushroom caps, so they stay crisp pure white in shade.</summary>
    public const float SpotLight = 0.25f;

    /// <summary>Self-light of the ladybug's glowing caps (it was 0.2 before the caps went vivid).</summary>
    public const float GlowingCapLight = 0.4f;

    /// <summary>A mushroom cap material: <c>Cap</c> (red) and the ladybug's <c>CapTan</c>, <c>CapPink</c>, <c>CapGold</c>.</summary>
    public static bool IsCap(string material) => material.StartsWith("Cap", StringComparison.Ordinal);

    /// <summary>Any part of a mushroom: a cap, its white spots or its stem.</summary>
    public static bool IsMushroom(string material) => IsCap(material) || material is "Spots" or "Stem";

    /// <summary>How much the material glows by itself (emission, times its own color).</summary>
    public static float SelfLight(MonsterKind kind, string material)
    {
        if (material == "EyeShine")
            return 0.6f;
        if (kind == MonsterKind.Ladybug && material == "LadyShell")
            return 0.32f;
        if (kind == MonsterKind.Ladybug && IsCap(material))
            return GlowingCapLight;
        if (material == "Spots")
            return SpotLight;
        return 0f;
    }

    /// <summary>The glossy highlight band's strength.</summary>
    public static float Gloss(string material) => IsCap(material) ? CapGloss : 0f;

    /// <summary>The extra light rim's strength.</summary>
    public static float Rim(string material) => IsCap(material) ? CapRim : 0f;
}
