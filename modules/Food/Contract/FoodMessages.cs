namespace Fungtopia.Food;

/// <summary>The kinds of food, as <see cref="FoodEaten.Kind"/> and <see cref="FoodBag.Selected"/> number them.</summary>
public static class FoodKinds
{
    /// <summary>Berry Muffin: heals 1 heart.</summary>
    public const int Muffin = 0;

    /// <summary>Mushroom Soup: heals 3 hearts.</summary>
    public const int Soup = 1;

    /// <summary>Spore Pie: heals fully.</summary>
    public const int Pie = 2;

    public const int Count = 3;
}

/// <summary>The player's food bag at the end of a fixed tick, published by the Food module exactly once per tick.</summary>
public readonly struct FoodBag
{
    /// <summary>Berry Muffins carried (0..9).</summary>
    public readonly int Muffins;

    /// <summary>Mushroom Soups carried (0..9).</summary>
    public readonly int Soups;

    /// <summary>Spore Pies carried (0..9).</summary>
    public readonly int Pies;

    /// <summary>The food Eat eats, one of <see cref="FoodKinds"/> (0..2).</summary>
    public readonly int Selected;

    /// <summary>The player is eating (standing still for about a second).</summary>
    public readonly bool Eating;

    /// <summary>How far the meal is, 0..1 (0 when not <see cref="Eating"/>).</summary>
    public readonly float EatProgress;

    public FoodBag(int muffins, int soups, int pies, int selected, bool eating, float eatProgress)
    {
        Muffins = muffins;
        Soups = soups;
        Pies = pies;
        Selected = selected;
        Eating = eating;
        EatProgress = eatProgress;
    }

    /// <summary>How many of <paramref name="kind"/> (one of <see cref="FoodKinds"/>) are carried; 0 for any other number.</summary>
    public int Count(int kind) => kind switch
    {
        FoodKinds.Muffin => Muffins,
        FoodKinds.Soup => Soups,
        FoodKinds.Pie => Pies,
        _ => 0,
    };

    /// <summary>Nothing in the bag.</summary>
    public bool Empty => Muffins + Soups + Pies == 0;
}

/// <summary>Published by the Food module once per meal, when it is eaten (with the <c>HealPlayer</c> it sent).</summary>
public readonly struct FoodEaten
{
    /// <summary>What was eaten, one of <see cref="FoodKinds"/>.</summary>
    public readonly int Kind;

    /// <summary>Health given back (2 health is 1 heart), at most what was missing.</summary>
    public readonly int Healed;

    public FoodEaten(int kind, int healed)
    {
        Kind = kind;
        Healed = healed;
    }
}

/// <summary>
/// Asks the Food module to open the food stall's menu (published by the stall's cook, Pip, when
/// the player talks to them). Once one has arrived, the Food module stops opening the menu on
/// its own at the stall.
/// </summary>
public readonly struct OpenFoodMenu
{
}
