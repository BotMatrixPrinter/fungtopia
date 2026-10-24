namespace Fungtopia.Forest;

/// <summary>Fixed pseudo-random values for laying out the forest's dressing: the same scene every run.</summary>
public static class ForestRandom
{
    /// <summary>A fixed pseudo-random value in [0, 1) for (i, k).</summary>
    public static float Value(int i, int k)
    {
        uint h = unchecked((uint)i * 2654435761u ^ (uint)k * 2246822519u ^ 0x5bd1e995u);
        h = (h ^ (h >> 15)) * 2246822519u;
        h = (h ^ (h >> 13)) * 3266489917u;
        h ^= h >> 16;
        return (h >> 8) * (1f / 16777216f);
    }
}
