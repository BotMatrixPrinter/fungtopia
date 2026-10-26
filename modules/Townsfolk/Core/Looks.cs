using System;
using System.Numerics;

namespace Fungtopia.Townsfolk;

/// <summary>The body shapes of folk.glb (Body's children of the same names): a dress (a tall cone, a triangle from the front) or a rounded capsule.</summary>
public enum BodyShape : byte { Dress, Capsule }

/// <summary>What a townsperson wears besides the body: a big bow tie at the capsule's top (BowTie), a bow on the head (Bow), or nothing.</summary>
public enum Accessory : byte { None, BowTie, Bow }

/// <summary>The mushroom-cap hats of folk.glb (its Head's children Hat&lt;name&gt;), or none.</summary>
public enum HatStyle : byte { None, Dome, Flat, Tall }

/// <summary>How one townsperson looks: the view tints and dresses folk.glb with it. Colors are sRGB (0..1).</summary>
public readonly struct Look
{
    /// <summary>The height folk.glb is built at (meters, the top of its head).</summary>
    public const float ModelHeight = 1.47f;

    public readonly Vector3 Skin;
    public readonly BodyShape Shape;
    /// <summary>The body's color (the dress or the capsule).</summary>
    public readonly Vector3 Outfit;
    /// <summary>The tube legs and their rounded feet.</summary>
    public readonly Vector3 Legs;
    public readonly Accessory Accessory;
    /// <summary>The bow tie's or the bow's color.</summary>
    public readonly Vector3 AccessoryColor;
    public readonly HatStyle Hat;
    public readonly Vector3 HatColor;
    /// <summary>Height in meters (the top of the head, or of the head under a hat).</summary>
    public readonly float Height;

    public Look(Vector3 skin, BodyShape shape, Vector3 outfit, Vector3 legs, Accessory accessory, Vector3 accessoryColor,
        HatStyle hat, Vector3 hatColor, float height)
    {
        Skin = skin;
        Shape = shape;
        Outfit = outfit;
        Legs = legs;
        Accessory = accessory;
        AccessoryColor = accessoryColor;
        Hat = hat;
        HatColor = hatColor;
        Height = height;
    }

    /// <summary>The scale folk.glb is drawn at for this height.</summary>
    public float Scale => Height / ModelHeight;
}

/// <summary>The palettes looks are mixed from (with a seeded <see cref="Random"/>).</summary>
public static class Looks
{
    public static readonly Vector3[] SkinTones =
    {
        new(0.98f, 0.8f, 0.68f), new(0.98f, 0.68f, 0.52f), new(0.86f, 0.6f, 0.42f),
        new(0.68f, 0.44f, 0.3f), new(0.5f, 0.32f, 0.22f), new(1f, 0.86f, 0.76f),
    };

    public static readonly Vector3[] Outfits =
    {
        new(0.55f, 0.75f, 0.95f), new(0.98f, 0.5f, 0.62f), new(0.55f, 0.82f, 0.62f), new(1f, 0.82f, 0.36f),
        new(0.72f, 0.58f, 0.92f), new(0.95f, 0.45f, 0.32f), new(0.4f, 0.8f, 0.8f), new(0.98f, 0.7f, 0.45f),
        new(0.35f, 0.45f, 0.75f), new(0.85f, 0.85f, 0.9f), new(0.62f, 0.85f, 0.35f),
    };

    /// <summary>Bow ties and bows: red, cream, yellow, pink, sky, lilac.</summary>
    public static readonly Vector3[] AccessoryColors =
    {
        new(0.9f, 0.2f, 0.25f), new(1f, 0.97f, 0.9f), new(1f, 0.82f, 0.3f), new(0.98f, 0.5f, 0.68f),
        new(0.45f, 0.7f, 0.98f), new(0.68f, 0.5f, 0.9f),
    };

    /// <summary>Legs: navy, brown, charcoal, cream, plum (or, for about 1 in 3, the skin).</summary>
    public static readonly Vector3[] LegColors =
    {
        new(0.28f, 0.32f, 0.52f), new(0.42f, 0.3f, 0.24f), new(0.26f, 0.26f, 0.3f), new(0.96f, 0.92f, 0.84f),
        new(0.42f, 0.26f, 0.42f),
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

    /// <summary>
    /// A look mixed with <paramref name="rng"/>: about half wear a dress, the others a capsule body
    /// (most of those with a bow tie); about 4 in 10 wear a mushroom-cap hat, and some without one a
    /// bow on the head; heights 1.35..1.55 m.
    /// </summary>
    public static Look Mix(Random rng)
    {
        var skin = Pick(rng, SkinTones);
        var shape = rng.Next(2) == 0 ? BodyShape.Dress : BodyShape.Capsule;
        var outfit = Pick(rng, Outfits);
        var legs = rng.Next(3) == 0 ? skin : Pick(rng, LegColors);
        var hat = rng.Next(10) < 4 ? (HatStyle)(1 + rng.Next(3)) : HatStyle.None;
        var hatColor = Pick(rng, HatColors);
        int roll = rng.Next(10);
        var accessory = shape == BodyShape.Capsule
            ? roll < 7 ? Accessory.BowTie : Accessory.None
            : hat == HatStyle.None && roll < 6 ? Accessory.Bow : Accessory.None;
        var accessoryColor = Pick(rng, AccessoryColors);
        float height = 1.35f + (float)rng.NextDouble() * 0.2f;
        return new Look(skin, shape, outfit, legs, accessory, accessoryColor, hat, hatColor, height);
    }
}
