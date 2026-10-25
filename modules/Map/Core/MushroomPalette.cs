using System.Numerics;

namespace Fungtopia.Map;

/// <summary>
/// The colors and glow of the mushrooms the Map draws (giant mushrooms on the cliffs, the little
/// mushroom lamps beside the roads), as sRGB 0..1 triples. "Vivid" (Tony, 2026-10-25,
/// the same rule for every mushroom in the game): caps clearly saturated and bright, spots pure
/// white, stems a clean light cream, glowing mushrooms about twice as bright as before.
/// </summary>
public static class MushroomPalette
{
    /// <summary>Giant mushroom caps: cherry and tomato reds, orange, warm brown, violet and orchid pink.</summary>
    public static readonly Vector3[] CapColors =
    {
        new(0.96f, 0.08f, 0.12f), new(1.0f, 0.24f, 0.1f), new(1.0f, 0.5f, 0.06f),
        new(0.86f, 0.44f, 0.14f), new(0.66f, 0.16f, 0.98f), new(0.92f, 0.18f, 1.0f),
    };

    /// <summary>Lamp caps: warm golden, candy pink and sky blue.</summary>
    public static readonly Vector3[] LampColors =
    {
        new(1.0f, 0.78f, 0.12f), new(1.0f, 0.16f, 0.48f), new(0.14f, 0.7f, 1.0f),
    };

    /// <summary>The two ends of the stems' clean light cream (each stem picks a shade between).</summary>
    public static readonly Vector3 StemLight = new(1.0f, 0.97f, 0.88f);
    public static readonly Vector3 StemDark = new(0.98f, 0.92f, 0.8f);

    /// <summary>The caps' spots: crisp pure white.</summary>
    public static readonly Vector3 SpotColor = new(1f, 1f, 1f);

    /// <summary>Emission of a glowing giant cap (about 1 in 8) and of a lamp (was 1.1 and 1.4).</summary>
    public const float GiantGlow = 2.2f;
    public const float LampGlow = 2.8f;
}
