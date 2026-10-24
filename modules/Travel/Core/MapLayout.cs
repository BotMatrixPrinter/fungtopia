using System;
using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.Travel;

/// <summary>
/// Where the travel map draws its three circles, on a virtual 1920 x 1080 canvas (y down) that
/// the view scales to fit the screen. Index order is <see cref="TravelModule.Destinations"/>:
/// Mushroom Town on the left, Mushroom Forest raised in the middle, Pet City on the right.
/// </summary>
public static class MapLayout
{
    public const float Width = 1920f;
    public const float Height = 1080f;
    public const float Radius = 215f;

    private static readonly Vector2[] s_centers =
    {
        new(400f, 610f),
        new(960f, 500f),
        new(1520f, 610f),
    };

    private static readonly string[] s_names = { "Mushroom Town", "Mushroom Forest", "Pet City" };

    /// <summary>The circles' centers, in <see cref="TravelModule.Destinations"/> order.</summary>
    public static ReadOnlySpan<Vector2> Centers => s_centers;

    /// <summary>The label of destination <paramref name="index"/>.</summary>
    public static string Name(int index) => s_names[index];

    /// <summary>The label of a destination area (Town, Forest or PetCity).</summary>
    public static string Name(Area area) => s_names[TravelModule.IndexOf(area)];

    /// <summary>The destination whose circle contains <paramref name="point"/> (canvas units), or -1.</summary>
    public static int Hit(Vector2 point)
    {
        for (int i = 0; i < s_centers.Length; i++)
        {
            if (Vector2.DistanceSquared(point, s_centers[i]) <= Radius * Radius)
                return i;
        }
        return -1;
    }

    /// <summary>The scale that fits the canvas into a screen of <paramref name="screen"/> pixels.</summary>
    public static float Scale(Vector2 screen) => MathF.Min(screen.X / Width, screen.Y / Height);

    /// <summary>The canvas's top-left corner on a screen of <paramref name="screen"/> pixels (centered).</summary>
    public static Vector2 Origin(Vector2 screen) => (screen - new Vector2(Width, Height) * Scale(screen)) * 0.5f;

    /// <summary>A screen pixel to canvas units.</summary>
    public static Vector2 ToCanvas(Vector2 screenPoint, Vector2 screen) => (screenPoint - Origin(screen)) / Scale(screen);
}
