using System;
using System.Numerics;
using Fungtopia.Wardrobe;

namespace Fungtopia.Player;

/// <summary>
/// The built-in pieces of girl.glb and boy.glb (the old fixed outfits and hair). Each is a set of
/// nodes whose names start with <see cref="Rig.NodePrefix"/>, shown only while its slot is empty.
/// </summary>
[Flags]
public enum BuiltinPieces
{
    None = 0,

    /// <summary>The built-in hair (node "Hair" on the head), replaced by any chosen hair style.</summary>
    Hair = 1,

    /// <summary>The Girl's pink spotted dress (it colors her triangle body); the Boy's blue shirt and red bow tie.</summary>
    Top = 2,

    /// <summary>The Boy's trousers, a band round the bottom of his triangle body (the Girl has none: her dress covers it).</summary>
    Bottom = 4,

    /// <summary>The round brown shoes: the feet (the models have no legs).</summary>
    Shoes = 8,

    /// <summary>The Girl's ribbon bow on her head.</summary>
    Head = 16,
}

/// <summary>
/// The Player's modular rig (characters.py), the same for the Girl and the Boy: the parts the
/// game animates and the attach points worn models go on. A worn model's objects are named like
/// the part they go on (<see cref="Attach"/>) with their origin at its pivot: <see cref="Head"/>
/// (hair, hats, glasses), <see cref="Body"/> (tops, dresses, the hips of bottoms, backpacks,
/// scarves), <see cref="LegL"/> and <see cref="LegR"/> (the feet: shoes; the legs themselves are
/// invisible pivots at the hips the feet hang from).
/// </summary>
public static class Rig
{
    public const string Head = Attach.Head;
    public const string Body = Attach.Body;
    public const string LegL = Attach.LegL;
    public const string LegR = Attach.LegR;

    /// <summary>Whether a worn model's object named <paramref name="name"/> goes on a rig part of that name.</summary>
    public static bool IsAttachPoint(string name) =>
        name == Head || name == Body || name == LegL || name == LegR;

    /// <summary>The name prefix of the nodes of one built-in piece (e.g. "DefaultTop", "DefaultShoesLegL").</summary>
    public static string NodePrefix(BuiltinPieces piece) => piece switch
    {
        BuiltinPieces.Hair => "Hair",
        BuiltinPieces.Top => "DefaultTop",
        BuiltinPieces.Bottom => "DefaultBottom",
        BuiltinPieces.Shoes => "DefaultShoes",
        BuiltinPieces.Head => "DefaultHead",
        _ => throw new ArgumentOutOfRangeException(nameof(piece)),
    };

    /// <summary>The pieces girl.glb or boy.glb has.</summary>
    public static BuiltinPieces Pieces(CharacterKind kind) => kind == CharacterKind.Girl
        ? BuiltinPieces.Hair | BuiltinPieces.Top | BuiltinPieces.Shoes | BuiltinPieces.Head
        : BuiltinPieces.Hair | BuiltinPieces.Top | BuiltinPieces.Bottom | BuiltinPieces.Shoes;

    /// <summary>The colors characters.py gives the skin, eyes and hair (RGB 0..1, like <see cref="Looks"/>).</summary>
    public static readonly Vector3 BuiltinSkin = new(0.98f, 0.68f, 0.52f);
    public static readonly Vector3 BuiltinEye = new(0.07f, 0.04f, 0.06f);

    public static Vector3 BuiltinHair(CharacterKind kind) =>
        kind == CharacterKind.Girl ? new Vector3(0.45f, 0.22f, 0.14f) : new Vector3(0.3f, 0.18f, 0.1f);
}

/// <summary>
/// What the character looks like, resolved from a <see cref="AppearanceState"/> (plain values for
/// the view): the tints of the skin parts (material "Skin"), the eyes ("Eye") and the hair (a hair
/// style's material "Hair"), the hair style's model, the model of each worn item, and which
/// built-in pieces show. Does not allocate.
/// </summary>
public readonly struct CharacterLook
{
    /// <summary>From a <see cref="AppearanceState"/>; false: the built-in model as it is (no Wardrobe yet).</summary>
    public readonly bool Custom;

    public readonly CharacterKind Character;

    public readonly Vector3 SkinColor;
    public readonly Vector3 HairColor;
    public readonly Vector3 EyeColor;

    /// <summary>The hair style's .glb (res://), attached to the head; empty: the built-in hair.</summary>
    public readonly string HairModel;

    /// <summary>The worn items' .glb paths (res://), empty for none.</summary>
    public readonly string Top, Bottom, Shoes, Head, Face, Back;

    /// <summary>The built-in pieces shown (each only while its slot is empty).</summary>
    public readonly BuiltinPieces Builtin;

    private CharacterLook(bool custom, CharacterKind character, Vector3 skin, Vector3 hair, Vector3 eye,
        string hairModel, string top, string bottom, string shoes, string head, string face, string back,
        BuiltinPieces builtin)
    {
        Custom = custom;
        Character = character;
        SkinColor = skin;
        HairColor = hair;
        EyeColor = eye;
        HairModel = hairModel;
        Top = top;
        Bottom = bottom;
        Shoes = shoes;
        Head = head;
        Face = face;
        Back = back;
        Builtin = builtin;
    }

    /// <summary>The model worn in <paramref name="slot"/>, or empty.</summary>
    public string Model(Slot slot) => slot switch
    {
        Slot.Top => Top,
        Slot.Bottom => Bottom,
        Slot.Shoes => Shoes,
        Slot.Head => Head,
        Slot.Face => Face,
        _ => Back,
    };

    public bool Shows(BuiltinPieces piece) => (Builtin & piece) == piece;

    /// <summary>The character as characters.py drew it: built-in hair, colors and outfit.</summary>
    public static CharacterLook Default(CharacterKind kind) =>
        new(false, kind, Rig.BuiltinSkin, Rig.BuiltinHair(kind), Rig.BuiltinEye, "", "", "", "", "", "", "",
            Rig.Pieces(kind));

    /// <summary>
    /// The character dressed by <paramref name="look"/>. Out-of-range looks keep the built-in
    /// color (or hair). An item is worn only in its own slot and when it fits the character;
    /// unknown ids are nothing. A slot with nothing in it shows the built-in piece of that slot
    /// (the old fixed outfit), except that a catalog dress replaces the bottom (the built-in
    /// trousers or a worn bottom); the Girl's built-in dress, shown with no top, still lets a
    /// worn bottom show under it.
    /// </summary>
    public static CharacterLook From(in AppearanceState look, CharacterKind kind)
    {
        int character = kind == CharacterKind.Boy ? 1 : 0;
        var skin = (uint)look.Skin < (uint)Looks.SkinTones.Length ? Looks.SkinTones[look.Skin] : Rig.BuiltinSkin;
        var hair = (uint)look.HairColor < (uint)Looks.HairColors.Length ? Looks.HairColors[look.HairColor] : Rig.BuiltinHair(kind);
        var eye = (uint)look.EyeColor < (uint)Looks.EyeColors.Length ? Looks.EyeColors[look.EyeColor] : Rig.BuiltinEye;
        string hairModel = (uint)look.HairStyle < (uint)Looks.HairStyles.Length ? Looks.HairStyles[look.HairStyle].ModelPath : "";

        bool dress = Catalog.TryGet(look.Top, out var topItem) && topItem.Slot == Slot.Top && topItem.Dress &&
            Fits.Matches(topItem.Fit, character);
        string top = Worn(look.Top, Slot.Top, character);
        string bottom = dress ? "" : Worn(look.Bottom, Slot.Bottom, character);
        string shoes = Worn(look.Shoes, Slot.Shoes, character);
        string head = Worn(look.Head, Slot.Head, character);
        string face = Worn(look.Face, Slot.Face, character);
        string back = Worn(look.Back, Slot.Back, character);

        var builtin = Rig.Pieces(kind);
        if (hairModel.Length > 0)
            builtin &= ~BuiltinPieces.Hair;
        if (top.Length > 0)
            builtin &= ~BuiltinPieces.Top;
        if (bottom.Length > 0 || dress)
            builtin &= ~BuiltinPieces.Bottom;
        if (shoes.Length > 0)
            builtin &= ~BuiltinPieces.Shoes;
        if (head.Length > 0)
            builtin &= ~BuiltinPieces.Head;
        return new CharacterLook(true, kind, skin, hair, eye, hairModel, top, bottom, shoes, head, face, back, builtin);
    }

    private static string Worn(string? id, Slot slot, int character) =>
        Catalog.TryGet(id, out var item) && item.Slot == slot && Fits.Matches(item.Fit, character) ? item.ModelPath : "";
}
