using System;
using System.Numerics;

namespace Fungtopia.Townsfolk;

/// <summary>The hair styles of folk.glb (its Head's children of the same names).</summary>
public enum HairStyle : byte { Short, Bob, Buns, Spiky, Long }

/// <summary>The mushroom-cap hats of folk.glb (its Head's children Hat&lt;name&gt;), or none.</summary>
public enum HatStyle : byte { None, Dome, Flat, Tall }

/// <summary>How one townsperson looks: the view tints and dresses folk.glb with it. Colors are sRGB (0..1).</summary>
public readonly struct Look
{
    public readonly Vector3 Skin;
    public readonly HairStyle Hair;
    public readonly Vector3 HairColor;
    /// <summary>The cone body's color (the outfit).</summary>
    public readonly Vector3 Outfit;
    /// <summary>The hem and collar.</summary>
    public readonly Vector3 Trim;
    /// <summary>Trousers (a band round the bottom of the cone) rather than a dress.</summary>
    public readonly bool Trousers;
    public readonly Vector3 TrousersColor;
    public readonly HatStyle Hat;
    public readonly Vector3 HatColor;
    /// <summary>Drawn size (1 = the NPCs' size).</summary>
    public readonly float Scale;

    public Look(Vector3 skin, HairStyle hair, Vector3 hairColor, Vector3 outfit, Vector3 trim, bool trousers,
        Vector3 trousersColor, HatStyle hat, Vector3 hatColor, float scale)
    {
        Skin = skin;
        Hair = hair;
        HairColor = hairColor;
        Outfit = outfit;
        Trim = trim;
        Trousers = trousers;
        TrousersColor = trousersColor;
        Hat = hat;
        HatColor = hatColor;
        Scale = scale;
    }
}

/// <summary>The palettes looks are mixed from (with a seeded <see cref="Random"/>).</summary>
public static class Looks
{
    public static readonly Vector3[] SkinTones =
    {
        new(0.98f, 0.8f, 0.68f), new(0.98f, 0.68f, 0.52f), new(0.86f, 0.6f, 0.42f),
        new(0.68f, 0.44f, 0.3f), new(0.5f, 0.32f, 0.22f), new(1f, 0.86f, 0.76f),
    };

    public static readonly Vector3[] HairColors =
    {
        new(0.35f, 0.22f, 0.14f), new(0.98f, 0.78f, 0.42f), new(0.12f, 0.1f, 0.12f), new(0.85f, 0.42f, 0.18f),
        new(0.55f, 0.3f, 0.18f), new(0.98f, 0.62f, 0.78f), new(0.45f, 0.55f, 0.95f), new(0.9f, 0.9f, 0.92f),
        new(0.3f, 0.2f, 0.36f),
    };

    public static readonly Vector3[] Outfits =
    {
        new(0.55f, 0.75f, 0.95f), new(0.98f, 0.5f, 0.62f), new(0.55f, 0.82f, 0.62f), new(1f, 0.82f, 0.36f),
        new(0.72f, 0.58f, 0.92f), new(0.95f, 0.45f, 0.32f), new(0.4f, 0.8f, 0.8f), new(0.98f, 0.7f, 0.45f),
        new(0.35f, 0.45f, 0.75f), new(0.85f, 0.85f, 0.9f), new(0.62f, 0.85f, 0.35f),
    };

    public static readonly Vector3[] Trims =
    {
        new(1f, 0.97f, 0.9f), new(1f, 0.85f, 0.4f), new(0.98f, 0.55f, 0.7f), new(0.5f, 0.8f, 0.98f),
    };

    public static readonly Vector3[] TrousersColors =
    {
        new(0.35f, 0.4f, 0.62f), new(0.45f, 0.35f, 0.3f), new(0.3f, 0.3f, 0.35f), new(0.55f, 0.5f, 0.42f),
    };

    /// <summary>Mushroom-cap colors: red, orange, brown, purple, blue, pink, yellow.</summary>
    public static readonly Vector3[] HatColors =
    {
        new(0.9f, 0.2f, 0.22f), new(0.98f, 0.55f, 0.2f), new(0.62f, 0.4f, 0.25f), new(0.65f, 0.4f, 0.85f),
        new(0.35f, 0.6f, 0.95f), new(0.98f, 0.5f, 0.7f), new(1f, 0.85f, 0.3f),
    };

    /// <summary>Fur of the pets: orange, cream, grey, brown, black, white, ginger.</summary>
    public static readonly Vector3[] Furs =
    {
        new(1f, 0.55f, 0.22f), new(0.98f, 0.88f, 0.7f), new(0.6f, 0.6f, 0.66f), new(0.62f, 0.4f, 0.24f),
        new(0.22f, 0.2f, 0.24f), new(0.97f, 0.96f, 0.94f), new(0.86f, 0.45f, 0.22f),
    };

    /// <summary>Crow feathers: dark blue-purple, slate, plum.</summary>
    public static readonly Vector3[] Feathers =
    {
        new(0.16f, 0.15f, 0.27f), new(0.26f, 0.3f, 0.4f), new(0.3f, 0.16f, 0.32f),
    };

    /// <summary>Butterfly wings: pink, yellow, sky, lilac, orange, mint.</summary>
    public static readonly Vector3[] Wings =
    {
        new(1f, 0.55f, 0.75f), new(1f, 0.88f, 0.3f), new(0.5f, 0.78f, 1f), new(0.78f, 0.6f, 1f),
        new(1f, 0.62f, 0.3f), new(0.55f, 0.95f, 0.75f),
    };

    private static Vector3 Pick(Random rng, Vector3[] palette) => palette[rng.Next(palette.Length)];

    /// <summary>A look mixed with <paramref name="rng"/>: most wear a mushroom-cap hat, about half wear trousers.</summary>
    public static Look Mix(Random rng)
    {
        var skin = Pick(rng, SkinTones);
        var hair = (HairStyle)rng.Next(5);
        var hairColor = Pick(rng, HairColors);
        var outfit = Pick(rng, Outfits);
        var trim = Pick(rng, Trims);
        bool trousers = rng.Next(2) == 0;
        var trousersColor = Pick(rng, TrousersColors);
        var hat = rng.Next(10) < 7 ? (HatStyle)(1 + rng.Next(3)) : HatStyle.None;
        var hatColor = Pick(rng, HatColors);
        float scale = 0.94f + (float)rng.NextDouble() * 0.2f;
        return new Look(skin, hair, hairColor, outfit, trim, trousers, trousersColor, hat, hatColor, scale);
    }
}
