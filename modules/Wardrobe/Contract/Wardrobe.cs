using System;
using System.Numerics;

namespace Fungtopia.Wardrobe;

/// <summary>Where a piece of clothing is worn. One item per slot.</summary>
public enum Slot
{
    Top,
    Bottom,
    Shoes,
    Head,
    Face,
    Back,
}

/// <summary>Who an item fits (<see cref="ClothingItem.Fit"/>).</summary>
public static class Fits
{
    public const int Both = 0;
    public const int Girl = 1;
    public const int Boy = 2;

    /// <summary>Whether an item of <paramref name="fit"/> fits the character (0 Girl, 1 Boy, like <c>Fungtopia.Player.CharacterKind</c>).</summary>
    public static bool Matches(int fit, int character) =>
        fit == Both || (fit == Girl && character == 0) || (fit == Boy && character == 1);
}

/// <summary>How an item is obtained (<see cref="ClothingItem.Source"/>).</summary>
public static class Sources
{
    /// <summary>Owned from the start (the starter outfits).</summary>
    public const string Starter = "starter";

    /// <summary>Sold by Abby in Mushroom Town for <see cref="ClothingItem.Price"/> M¢.</summary>
    public const string Shop = "shop";

    /// <summary>Given for something (a first defeat, a quest) by <see cref="GrantItem"/>.</summary>
    public const string Reward = "reward";
}

/// <summary>One piece of clothing of the <see cref="Catalog"/>.</summary>
public readonly struct ClothingItem
{
    /// <summary>Unique, snake_case, e.g. "striped_tee"; also the name of its model files.</summary>
    public readonly string Id;

    /// <summary>Shown in the wardrobe and the shop, e.g. "Striped Tee".</summary>
    public readonly string Name;

    public readonly Slot Slot;

    /// <summary>Mushroom Coins (M¢) in Abby's shop; 0 for starters and rewards.</summary>
    public readonly int Price;

    /// <summary><see cref="Fits.Both"/> (0), <see cref="Fits.Girl"/> (1) or <see cref="Fits.Boy"/> (2).</summary>
    public readonly int Fit;

    /// <summary><see cref="Sources.Starter"/>, <see cref="Sources.Shop"/> or <see cref="Sources.Reward"/>.</summary>
    public readonly string Source;

    /// <summary>The res:// path of its .glb (see <see cref="Attach"/> for how it sits on the character).</summary>
    public readonly string ModelPath;

    /// <summary>A dress: a <see cref="Slot.Top"/> that covers the legs, so no bottom is shown with it.</summary>
    public readonly bool Dress;

    public ClothingItem(string id, string name, Slot slot, int price, int fit, string source, string modelPath,
        bool dress = false)
    {
        Id = id;
        Name = name;
        Slot = slot;
        Price = price;
        Fit = fit;
        Source = source;
        ModelPath = modelPath;
        Dress = dress;
    }
}

/// <summary>A free starting outfit (all its items are starters). Empty ids leave a slot empty.</summary>
public readonly struct StarterOutfit
{
    public readonly string Name;
    public readonly int Fit;
    public readonly string Top;
    public readonly string Bottom;
    public readonly string Shoes;
    public readonly string Head;

    public StarterOutfit(string name, int fit, string top, string bottom, string shoes, string head)
    {
        Name = name;
        Fit = fit;
        Top = top;
        Bottom = bottom;
        Shoes = shoes;
        Head = head;
    }
}

/// <summary>
/// Every piece of clothing in the game. Adding one: one entry in <see cref="Items"/> plus one
/// Blender script modules/Wardrobe/Models/Clothes/&lt;id&gt;.py that exports &lt;id&gt;.glb.
/// </summary>
public static class Catalog
{
    private const string Clothes = "res://modules/Wardrobe/Models/Clothes/";

    /// <summary>The reward for the first Mushroom Tarantula defeated.</summary>
    public const string SpiderSilkScarf = "spider_silk_scarf";

    /// <summary>The reward for the first Mushroom Ladybug defeated.</summary>
    public const string LadybugBackpack = "ladybug_backpack";

    private static ClothingItem Item(string id, string name, Slot slot, int price, int fit, string source,
        bool dress = false) => new(id, name, slot, price, fit, source, Clothes + id + ".glb", dress);

    private static readonly ClothingItem[] s_items =
    {
        // Tops.
        Item("striped_tee", "Striped Tee", Slot.Top, 0, Fits.Both, Sources.Starter),
        Item("mushroom_hoodie", "Mushroom Pocket Hoodie", Slot.Top, 350, Fits.Both, Sources.Shop),
        Item("overalls_top", "Overalls Top", Slot.Top, 0, Fits.Both, Sources.Starter),
        Item("sweater_vest", "Sweater Vest", Slot.Top, 250, Fits.Both, Sources.Shop),
        // Dresses (tops that hide the bottom).
        Item("spotted_dress", "Spotted Mushroom Dress", Slot.Top, 0, Fits.Girl, Sources.Starter, dress: true),
        Item("flower_dress", "Flower Dress", Slot.Top, 400, Fits.Girl, Sources.Shop, dress: true),
        Item("party_dress", "Triangle Party Dress", Slot.Top, 600, Fits.Both, Sources.Shop, dress: true),
        // Bottoms.
        Item("shorts", "Shorts", Slot.Bottom, 0, Fits.Both, Sources.Starter),
        Item("skirt", "Skirt", Slot.Bottom, 150, Fits.Girl, Sources.Shop),
        Item("overalls", "Overalls", Slot.Bottom, 0, Fits.Both, Sources.Starter),
        Item("jeans", "Jeans", Slot.Bottom, 200, Fits.Both, Sources.Shop),
        // Shoes.
        Item("sneakers", "Sneakers", Slot.Shoes, 0, Fits.Both, Sources.Starter),
        Item("rain_boots", "Rain Boots", Slot.Shoes, 0, Fits.Both, Sources.Starter),
        Item("mary_janes", "Mary-Janes", Slot.Shoes, 0, Fits.Both, Sources.Starter),
        // Head.
        Item("mushroom_hat", "Red Mushroom-Cap Hat", Slot.Head, 450, Fits.Both, Sources.Shop),
        Item("beanie", "Beanie", Slot.Head, 0, Fits.Both, Sources.Starter),
        Item("flower_clip", "Flower Clip", Slot.Head, 100, Fits.Both, Sources.Shop),
        Item("big_bow", "Big Bow", Slot.Head, 0, Fits.Both, Sources.Starter),
        // Face.
        Item("round_glasses", "Round Glasses", Slot.Face, 200, Fits.Both, Sources.Shop),
        Item("star_sunglasses", "Star Sunglasses", Slot.Face, 300, Fits.Both, Sources.Shop),
        // Back.
        Item("little_backpack", "Little Backpack", Slot.Back, 300, Fits.Both, Sources.Shop),
        Item("mushroom_backpack", "Mushroom Backpack", Slot.Back, 500, Fits.Both, Sources.Shop),
        // Rewards.
        Item(SpiderSilkScarf, "Spider Silk Scarf", Slot.Face, 0, Fits.Both, Sources.Reward),
        Item(LadybugBackpack, "Ladybug Backpack", Slot.Back, 0, Fits.Both, Sources.Reward),
    };

    private static readonly StarterOutfit[] s_starterOutfits =
    {
        new("Sunny Stripes", Fits.Both, "striped_tee", "shorts", "sneakers", ""),
        new("Mushroom Dress", Fits.Girl, "spotted_dress", "", "mary_janes", "big_bow"),
        new("Cozy Overalls", Fits.Both, "overalls_top", "overalls", "rain_boots", "beanie"),
    };

    public static ReadOnlySpan<ClothingItem> Items => s_items;

    /// <summary>The three free outfits to choose from when creating a character.</summary>
    public static ReadOnlySpan<StarterOutfit> StarterOutfits => s_starterOutfits;

    public static int Count => s_items.Length;

    /// <summary>The index of the item with <paramref name="id"/> in <see cref="Items"/>, or -1. Does not allocate.</summary>
    public static int IndexOf(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return -1;
        for (int i = 0; i < s_items.Length; i++)
            if (string.Equals(s_items[i].Id, id, StringComparison.Ordinal))
                return i;
        return -1;
    }

    /// <summary>Finds the item with <paramref name="id"/>.</summary>
    public static bool TryGet(string? id, out ClothingItem item)
    {
        int i = IndexOf(id);
        item = i >= 0 ? s_items[i] : default;
        return i >= 0;
    }

    /// <summary>The starter outfit a new <paramref name="character"/> (0 Girl, 1 Boy) wears: the dress for the Girl.</summary>
    public static int DefaultOutfit(int character) => character == 0 ? 1 : 0;
}

/// <summary>One of the <see cref="Looks.HairStyles"/>.</summary>
public readonly struct HairStyle
{
    public readonly string Id;
    public readonly string Name;

    /// <summary>The res:// path of its .glb (attached to the head; its "Hair" material takes the hair color).</summary>
    public readonly string ModelPath;

    public HairStyle(string id, string name, string modelPath)
    {
        Id = id;
        Name = name;
        ModelPath = modelPath;
    }
}

/// <summary>The character's looks to choose from (all usable by the Girl and the Boy). Colors are linear-ish sRGB 0..1.</summary>
public static class Looks
{
    private const string Hair = "res://modules/Wardrobe/Models/Hair/";

    private static readonly Vector3[] s_skinTones =
    {
        new(1.0f, 0.86f, 0.76f),   // light
        new(0.98f, 0.76f, 0.62f),
        new(0.93f, 0.66f, 0.5f),
        new(0.78f, 0.52f, 0.36f),
        new(0.6f, 0.38f, 0.25f),
        new(0.42f, 0.26f, 0.18f),  // deep
    };

    private static readonly HairStyle[] s_hairStyles =
    {
        new("short", "Short", Hair + "short.glb"),
        new("bob", "Bob", Hair + "bob.glb"),
        new("pigtails", "Pigtails", Hair + "pigtails.glb"),
        new("ponytail", "Ponytail", Hair + "ponytail.glb"),
        new("top_bun", "Top Bun", Hair + "top_bun.glb"),
        new("curly", "Curly", Hair + "curly.glb"),
    };

    private static readonly Vector3[] s_hairColors =
    {
        new(0.3f, 0.18f, 0.1f),    // brown
        new(0.12f, 0.09f, 0.08f),  // black
        new(0.95f, 0.78f, 0.42f),  // blond
        new(0.85f, 0.4f, 0.2f),    // ginger
        new(0.55f, 0.35f, 0.2f),   // light brown
        new(1.0f, 0.55f, 0.72f),   // pink
        new(0.55f, 0.92f, 0.78f),  // mint
        new(0.62f, 0.5f, 0.9f),    // lilac
    };

    private static readonly Vector3[] s_eyeColors =
    {
        new(0.07f, 0.04f, 0.06f),  // dark
        new(0.32f, 0.18f, 0.1f),   // brown
        new(0.15f, 0.35f, 0.75f),  // blue
        new(0.2f, 0.55f, 0.3f),    // green
        new(0.75f, 0.55f, 0.15f),  // amber
        new(0.5f, 0.3f, 0.7f),     // violet
    };

    private static readonly string[] s_hairColorNames = { "Brown", "Black", "Blond", "Ginger", "Light Brown", "Pink", "Mint", "Lilac" };
    private static readonly string[] s_eyeColorNames = { "Dark", "Brown", "Blue", "Green", "Amber", "Violet" };

    /// <summary>6 skin tones, light to deep.</summary>
    public static ReadOnlySpan<Vector3> SkinTones => s_skinTones;

    /// <summary>6 hair styles: short, bob, pigtails, ponytail, top bun (like Abby's), curly.</summary>
    public static ReadOnlySpan<HairStyle> HairStyles => s_hairStyles;

    /// <summary>8 hair colors, pink and mint among them.</summary>
    public static ReadOnlySpan<Vector3> HairColors => s_hairColors;

    public static ReadOnlySpan<string> HairColorNames => s_hairColorNames;

    /// <summary>6 eye colors.</summary>
    public static ReadOnlySpan<Vector3> EyeColors => s_eyeColors;

    public static ReadOnlySpan<string> EyeColorNames => s_eyeColorNames;

    /// <summary>Whether all four indices are in range.</summary>
    public static bool Valid(int skin, int hairStyle, int hairColor, int eyeColor) =>
        (uint)skin < (uint)s_skinTones.Length && (uint)hairStyle < (uint)s_hairStyles.Length &&
        (uint)hairColor < (uint)s_hairColors.Length && (uint)eyeColor < (uint)s_eyeColors.Length;
}

/// <summary>
/// How clothing and hair models sit on the Player rig (characters.py). Each .glb holds one object
/// per rig part it covers, named like the part (<see cref="Head"/>, <see cref="Body"/>,
/// <see cref="LegL"/>, <see cref="LegR"/>), with its origin at that part's pivot: parent it to the
/// part with no offset and it moves with it. Shoes are on the legs (the feet), backpacks and
/// tops on the body, hats, glasses, scarves and hair on the head (scarves on the body).
/// Pivots in meters from the feet, Godot axes (Y up, -Z forward).
/// </summary>
public static class Attach
{
    public const string Head = "Head";
    public const string Body = "Body";
    public const string LegL = "LegL";
    public const string LegR = "LegR";

    public static readonly Vector3 HeadPivot = new(0f, 0.72f, 0f);
    public static readonly Vector3 BodyPivot = new(0f, 0.3f, 0f);
    public static readonly Vector3 LegLPivot = new(-0.11f, 0.32f, 0f);
    public static readonly Vector3 LegRPivot = new(0.11f, 0.32f, 0f);
}

/// <summary>
/// The player's look and what they wear, published by the Wardrobe module every tick. Item ids
/// are from the <see cref="Catalog"/>, empty for nothing; <see cref="Bottom"/> is empty while a
/// dress is worn.
/// </summary>
public readonly struct AppearanceState
{
    /// <summary>Index in <see cref="Looks.SkinTones"/>.</summary>
    public readonly int Skin;

    /// <summary>Index in <see cref="Looks.HairStyles"/>.</summary>
    public readonly int HairStyle;

    /// <summary>Index in <see cref="Looks.HairColors"/>.</summary>
    public readonly int HairColor;

    /// <summary>Index in <see cref="Looks.EyeColors"/>.</summary>
    public readonly int EyeColor;

    public readonly string Top;
    public readonly string Bottom;
    public readonly string Shoes;
    public readonly string Head;
    public readonly string Face;
    public readonly string Back;

    public AppearanceState(int skin, int hairStyle, int hairColor, int eyeColor, string top, string bottom,
        string shoes, string head, string face, string back)
    {
        Skin = skin;
        HairStyle = hairStyle;
        HairColor = hairColor;
        EyeColor = eyeColor;
        Top = top;
        Bottom = bottom;
        Shoes = shoes;
        Head = head;
        Face = face;
        Back = back;
    }

    /// <summary>The item shown in <paramref name="slot"/>, or empty.</summary>
    public string Worn(Slot slot) => slot switch
    {
        Slot.Top => Top,
        Slot.Bottom => Bottom,
        Slot.Shoes => Shoes,
        Slot.Head => Head,
        Slot.Face => Face,
        _ => Back,
    };
}

/// <summary>Changes the look (indices into <see cref="Looks"/>); ignored when any is out of range. Free.</summary>
public readonly struct SetLook
{
    public readonly int Skin;
    public readonly int HairStyle;
    public readonly int HairColor;
    public readonly int EyeColor;

    public SetLook(int skin, int hairStyle, int hairColor, int eyeColor)
    {
        Skin = skin;
        HairStyle = hairStyle;
        HairColor = hairColor;
        EyeColor = eyeColor;
    }
}

/// <summary>
/// Puts on an owned item (in its own slot), or with an empty <see cref="ItemId"/> takes off
/// whatever is in <see cref="EquipSlot"/>. Free; ignored for items not owned or not fitting.
/// </summary>
public readonly struct Equip
{
    /// <summary>The item to put on, or empty to unequip <see cref="EquipSlot"/>.</summary>
    public readonly string ItemId;

    /// <summary>The slot to empty when <see cref="ItemId"/> is empty (unused otherwise).</summary>
    public readonly Slot EquipSlot;

    public Equip(string itemId)
    {
        ItemId = itemId;
        EquipSlot = Slot.Top;
    }

    public Equip(string itemId, Slot equipSlot)
    {
        ItemId = itemId;
        EquipSlot = equipSlot;
    }

    /// <summary>Takes off whatever is in <paramref name="slot"/>.</summary>
    public static Equip Unequip(Slot slot) => new("", slot);
}

/// <summary>Gives the player an item for free (a quest reward); nothing happens if it is already owned or unknown.</summary>
public readonly struct GrantItem
{
    public readonly string ItemId;

    /// <summary>Why it is given, e.g. "Millie's quest".</summary>
    public readonly string Why;

    public GrantItem(string itemId, string why)
    {
        ItemId = itemId;
        Why = why;
    }
}

/// <summary>Published by the Wardrobe module once when the player gets a new item (bought, granted or a reward).</summary>
public readonly struct ItemReceived
{
    public readonly string ItemId;

    /// <summary>"Abby's shop" for a purchase, else the <see cref="GrantItem.Why"/> or the reward's reason.</summary>
    public readonly string Why;

    public ItemReceived(string itemId, string why)
    {
        ItemId = itemId;
        Why = why;
    }
}

/// <summary>Opens the wardrobe screen (the wardrobe in the house).</summary>
public readonly struct OpenWardrobe
{
}

/// <summary>Opens Abby's shop screen (talking to Abby in Mushroom Town).</summary>
public readonly struct OpenShop
{
}

/// <summary>Published by the Wardrobe module every tick.</summary>
public readonly struct WardrobeState
{
    /// <summary>A screen (wardrobe or shop) is open: the player stands still and the camera frames them.</summary>
    public readonly bool Open;

    /// <summary>The open screen is Abby's shop (false: the wardrobe).</summary>
    public readonly bool Shop;

    public WardrobeState(bool open, bool shop)
    {
        Open = open;
        Shop = shop;
    }
}

/// <summary>
/// Published by the Wardrobe module every tick while the wardrobe or the shop screen is open:
/// how the player has turned the character to look at it (drag on the left half, Q / E, LB / RB,
/// the right stick). The Player module turns the character by it while <see cref="WardrobeState.Open"/>.
/// </summary>
public readonly struct PreviewYaw
{
    /// <summary>
    /// Radians in [-π, π): 0 faces the camera, positive turns the character's front towards the
    /// screen's right (counterclockwise seen from above). 0 each time a screen opens.
    /// </summary>
    public readonly float Yaw;

    public PreviewYaw(float yaw) => Yaw = yaw;
}
