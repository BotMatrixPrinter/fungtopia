using System;
using Fungtopia.Map;

namespace Fungtopia.DeepForest;

/// <summary>
/// How deep into the Deep Forest's gloom a point is, for anything that wants to darken or fog
/// with it (the DeepForest view uses it for its canopy, undergrowth and fog; DayNight or Camera may
/// use it to dim their light). Pure, deterministic, allocation-free.
/// </summary>
public static class DeepForestGloom
{
    /// <summary>Where the gloom starts: the forest-side end of the overgrown path (x).</summary>
    public const float StartX = -96f;

    /// <summary>Where the gloom is full, some way past the entrance clearing (x).</summary>
    public const float FullX = -135f;

    /// <summary>
    /// Whether (x, z) is in the Deep Forest's ground: its rectangle (<see cref="DeepForestLayout.Bounds"/>)
    /// or the overgrown path to it (<see cref="Areas.DeepForestPath"/>), <paramref name="margin"/>
    /// meters from their edge.
    /// </summary>
    public static bool InArea(float x, float z, float margin = 0f) =>
        DeepForestLayout.Bounds.Contains(x, z, margin) || Areas.DeepForestPath.Contains(x, z, margin);

    /// <summary>
    /// 0 outside the Deep Forest and at the forest end of the path, rising smoothly as one walks
    /// west along the path and through the entrance, 1 from <see cref="FullX"/> on.
    /// </summary>
    public static float At(float x, float z)
    {
        if (!InArea(x, z))
            return 0f;
        float t = Math.Clamp((StartX - x) / (StartX - FullX), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
