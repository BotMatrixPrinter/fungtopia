using System.Numerics;

namespace Fungtopia.Map;

/// <summary>
/// The colors of the ground the Map draws, as sRGB 0..1 triples. "Vibrant world" (Tony,
/// 2026-10-26): lush bright greens with yellow-green highlights, warm golden dirt, flowers in many
/// hues, clean bright stone; saturated like a cheerful cartoon but never neon or washed out, and
/// the dirt paths still clearly apart from the grass.
/// </summary>
public static class WorldPalette
{
    /// <summary>Grass, the deeper lush green it mottles with, and the towns' and roads' meadow.</summary>
    public static readonly Vector3 Grass = new(0.38f, 0.8f, 0.2f);
    public static readonly Vector3 Moss = new(0.16f, 0.64f, 0.3f);
    public static readonly Vector3 Meadow = new(0.52f, 0.88f, 0.2f);

    /// <summary>The sunny yellow-green the grass catches in patches.</summary>
    public static readonly Vector3 GrassHighlight = new(0.74f, 0.92f, 0.22f);

    /// <summary>Warm golden dirt of the roads and town paths.</summary>
    public static readonly Vector3 Dirt = new(0.94f, 0.7f, 0.34f);

    /// <summary>Warm sandstone cliff rock, the town square's clean bright stone, Pet City's lilac paving and pink tiles.</summary>
    public static readonly Vector3 Rock = new(0.86f, 0.6f, 0.46f);
    public static readonly Vector3 Stone = new(0.92f, 0.86f, 0.74f);
    public static readonly Vector3 Paving = new(0.68f, 0.58f, 0.9f);
    public static readonly Vector3 Tiles = new(1.0f, 0.5f, 0.64f);

    /// <summary>The two ends of the hedges' greens.</summary>
    public static readonly Vector3 HedgeDark = new(0.16f, 0.62f, 0.26f);
    public static readonly Vector3 HedgeLight = new(0.44f, 0.8f, 0.18f);

    /// <summary>The little flowers dotted over the grass: red, yellow, violet, blue, orange and pink.</summary>
    public static readonly Vector3[] Flowers =
    {
        new(1.0f, 0.22f, 0.3f), new(1.0f, 0.86f, 0.16f), new(0.62f, 0.36f, 1.0f),
        new(0.24f, 0.62f, 1.0f), new(1.0f, 0.45f, 0.08f), new(1.0f, 0.32f, 0.78f),
    };
}
