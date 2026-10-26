using System;

namespace Fungtopia.DayNight;

/// <summary>A linear color, 0..1 per channel; the view turns it into a Godot <c>Color</c>.</summary>
public readonly struct Rgb
{
    public readonly float R, G, B;

    public Rgb(float r, float g, float b)
    {
        R = r;
        G = g;
        B = b;
    }

    public Rgb Lerp(in Rgb to, float t) => new(R + (to.R - R) * t, G + (to.G - G) * t, B + (to.B - B) * t);

    /// <summary>
    /// Like <see cref="Lerp"/>, but keeps the blend as saturated as the two ends (their
    /// saturations interpolated), so a peach to blue sky passes through no grey on the way.
    /// </summary>
    public Rgb VividLerp(in Rgb to, float t)
    {
        var c = Lerp(to, t);
        float s = c.Saturation, target = Saturation + (to.Saturation - Saturation) * t;
        if (s <= 0.0001f || target <= s)
            return c;
        float v = c.Value, k = target / s;
        return new Rgb(MathF.Max(0f, v - (v - c.R) * k), MathF.Max(0f, v - (v - c.G) * k), MathF.Max(0f, v - (v - c.B) * k));
    }

    /// <summary>The largest channel (HSV value).</summary>
    public float Value => MathF.Max(R, MathF.Max(G, B));

    /// <summary>HSV saturation: 0 for grey or white, 1 for a pure color.</summary>
    public float Saturation
    {
        get
        {
            float max = Value;
            return max <= 0f ? 0f : (max - MathF.Min(R, MathF.Min(G, B))) / max;
        }
    }

    /// <summary>HSV hue in degrees, 0..360 (0 red, 60 yellow, 120 green, 240 blue, 300 magenta); 0 for grey.</summary>
    public float Hue
    {
        get
        {
            float max = Value, min = MathF.Min(R, MathF.Min(G, B)), d = max - min;
            if (d <= 0f)
                return 0f;
            float h = max == R ? (G - B) / d : max == G ? 2f + (B - R) / d : 4f + (R - G) / d;
            h *= 60f;
            return h < 0f ? h + 360f : h;
        }
    }

    /// <summary>Relative luminance (Rec. 709 weights).</summary>
    public float Luminance => 0.2126f * R + 0.7152f * G + 0.0722f * B;
}

/// <summary>The look of one moment of the day: sky gradient, sun or moon light, ambient, fog, stars and glow.</summary>
public readonly struct Mood
{
    public readonly float Hour;
    public readonly Rgb Top, Horizon, Ground, Light, Ambient, Fog;
    public readonly float AmbientEnergy, FogDensity, Stars, Glow;

    public Mood(float hour, Rgb top, Rgb horizon, Rgb ground, Rgb light, Rgb ambient, float ambientEnergy,
        Rgb fog, float fogDensity, float stars, float glow)
    {
        Hour = hour;
        Top = top;
        Horizon = horizon;
        Ground = ground;
        Light = light;
        Ambient = ambient;
        AmbientEnergy = ambientEnergy;
        Fog = fog;
        FogDensity = fogDensity;
        Stars = stars;
        Glow = glow;
    }

    /// <summary>The blend <paramref name="t"/> (0..1) of the way to <paramref name="to"/>, colors kept vivid.</summary>
    public Mood Lerp(in Mood to, float t, float hour) => new(hour, Top.VividLerp(to.Top, t), Horizon.VividLerp(to.Horizon, t),
        Ground.VividLerp(to.Ground, t), Light.VividLerp(to.Light, t), Ambient.VividLerp(to.Ambient, t),
        AmbientEnergy + (to.AmbientEnergy - AmbientEnergy) * t, Fog.VividLerp(to.Fog, t),
        FogDensity + (to.FogDensity - FogDensity) * t, Stars + (to.Stars - Stars) * t, Glow + (to.Glow - Glow) * t);
}

/// <summary>
/// The palette of the game's one sun, sky and environment, a cheerful cartoon look: a clear
/// saturated blue day sky, warm bright sunlight, colored ambient and thin haze by day, golden
/// evenings, pink-purple dusk and deep blue-purple nights in which glowing mushrooms stand out.
/// </summary>
public static class Lighting
{
    private static Rgb C(float r, float g, float b) => new(r, g, b);

    private static readonly Rgb NightTop = C(0.05f, 0.03f, 0.22f), NightHorizon = C(0.2f, 0.12f, 0.44f),
        NightGround = C(0.07f, 0.05f, 0.16f), Moonlight = C(0.58f, 0.6f, 1.0f), NightAmbient = C(0.34f, 0.26f, 0.74f),
        NightFog = C(0.18f, 0.12f, 0.42f);

    private static readonly Rgb DayTop = C(0.1f, 0.42f, 1.0f), DayHorizon = C(0.56f, 0.82f, 1.0f),
        DayGround = C(0.32f, 0.52f, 0.2f), DayAmbient = C(0.64f, 0.7f, 0.94f), DayFog = C(0.6f, 0.8f, 1.0f);

    private static readonly Mood[] All =
    {
        new(0f, NightTop, NightHorizon, NightGround, Moonlight, NightAmbient, 0.45f, NightFog, 0.009f, 1f, 1.2f),
        new(4.6f, NightTop, NightHorizon, NightGround, Moonlight, NightAmbient, 0.45f, NightFog, 0.009f, 1f, 1.2f),
        // Pink dawn.
        new(6f, C(0.36f, 0.32f, 0.76f), C(1.0f, 0.56f, 0.62f), C(0.42f, 0.32f, 0.28f), C(1.0f, 0.62f, 0.5f),
            C(0.68f, 0.48f, 0.76f), 0.7f, C(0.96f, 0.62f, 0.72f), 0.008f, 0.25f, 0.85f),
        // Warm morning.
        new(8f, C(0.26f, 0.54f, 0.98f), C(1.0f, 0.84f, 0.6f), C(0.36f, 0.5f, 0.22f), C(1.0f, 0.86f, 0.6f),
            C(0.6f, 0.62f, 0.9f), 0.72f, C(0.96f, 0.86f, 0.68f), 0.003f, 0f, 0.6f),
        // Late morning: the peach horizon turns blue by way of lilac (not a grey or mint one).
        new(9.5f, C(0.18f, 0.48f, 1.0f), C(0.84f, 0.8f, 1.0f), C(0.34f, 0.51f, 0.21f), C(1.0f, 0.9f, 0.68f),
            C(0.66f, 0.68f, 0.94f), 0.74f, C(0.86f, 0.82f, 1.0f), 0.002f, 0f, 0.55f),
        // Bright day.
        new(11f, DayTop, DayHorizon, DayGround, C(1.0f, 0.94f, 0.76f), DayAmbient, 0.75f, DayFog, 0.0012f, 0f, 0.5f),
        new(16f, DayTop, DayHorizon, DayGround, C(1.0f, 0.92f, 0.72f), DayAmbient, 0.75f, DayFog, 0.0012f, 0f, 0.5f),
        // Late afternoon: lilac again on the way from blue to gold.
        new(17.3f, C(0.2f, 0.44f, 0.98f), C(0.94f, 0.78f, 0.96f), C(0.4f, 0.46f, 0.18f), C(1.0f, 0.84f, 0.54f),
            C(0.66f, 0.6f, 0.98f), 0.74f, C(0.94f, 0.8f, 0.94f), 0.0025f, 0f, 0.6f),
        // Golden evening.
        new(18.6f, C(0.34f, 0.46f, 0.92f), C(1.0f, 0.68f, 0.26f), C(0.48f, 0.34f, 0.16f), C(1.0f, 0.72f, 0.32f),
            C(0.92f, 0.56f, 0.64f), 0.72f, C(1.0f, 0.74f, 0.42f), 0.005f, 0f, 0.75f),
        // Pink-purple dusk.
        new(20f, C(0.32f, 0.16f, 0.58f), C(1.0f, 0.42f, 0.66f), C(0.26f, 0.13f, 0.3f), C(1.0f, 0.46f, 0.62f),
            C(0.6f, 0.36f, 0.76f), 0.64f, C(0.74f, 0.38f, 0.7f), 0.008f, 0.35f, 1.0f),
        new(21.4f, NightTop, NightHorizon, NightGround, Moonlight, NightAmbient, 0.45f, NightFog, 0.009f, 1f, 1.2f),
        new(24f, NightTop, NightHorizon, NightGround, Moonlight, NightAmbient, 0.45f, NightFog, 0.009f, 1f, 1.2f),
    };

    /// <summary>The key moods, hours ascending from 0 to 24; the first and last are the same moment.</summary>
    public static ReadOnlySpan<Mood> Moods => All;

    /// <summary>The night, dawn, morning, day (11:00), evening and dusk keys: indexes into <see cref="Moods"/>.</summary>
    public const int Night = 0, Dawn = 2, Morning = 3, Day = 5, Evening = 8, Dusk = 9;

    /// <summary>The mood at <paramref name="hour"/> (0..24), smoothly blended between its two key moods.</summary>
    public static Mood At(float hour)
    {
        int i = 0;
        while (i < All.Length - 2 && hour >= All[i + 1].Hour)
            i++;
        ref readonly var a = ref All[i];
        ref readonly var b = ref All[i + 1];
        float t = Math.Clamp((hour - a.Hour) / (b.Hour - a.Hour), 0f, 1f);
        return a.Lerp(b, t * t * (3f - 2f * t), hour);
    }

    /// <summary>The sun's energy at full height, and the moon's.</summary>
    public const float SunEnergy = 1.2f, MoonEnergy = 0.45f;

    /// <summary>The environment's color adjustment: a gentle saturation boost, a touch of contrast and brightness.</summary>
    public const float AdjustmentSaturation = 1.25f, AdjustmentContrast = 1.05f, AdjustmentBrightness = 1.02f;

    /// <summary>The forest's haze tint by day (lush green-gold) and by night (teal-violet).</summary>
    public static readonly Rgb ForestFogDay = C(0.6f, 0.84f, 0.4f), ForestFogNight = C(0.12f, 0.26f, 0.44f);

    /// <summary>The forest's ambient tint, mixed in a little (a fresh green).</summary>
    public static readonly Rgb ForestAmbient = C(0.42f, 0.7f, 0.56f);
}
