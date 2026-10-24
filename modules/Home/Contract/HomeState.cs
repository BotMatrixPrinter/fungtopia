namespace Fungtopia.Home;

/// <summary>The player's home at the end of a fixed tick, published by the Home module exactly once per tick.</summary>
public readonly struct HomeState
{
    /// <summary>Pieces of furniture the player owns, placed in the house or not.</summary>
    public readonly int ItemsOwned;

    /// <summary>Pieces of furniture standing in the house.</summary>
    public readonly int ItemsPlaced;

    /// <summary>The player is arranging furniture (placement mode).</summary>
    public readonly bool Decorating;

    public HomeState(int itemsOwned, int itemsPlaced, bool decorating)
    {
        ItemsOwned = itemsOwned;
        ItemsPlaced = itemsPlaced;
        Decorating = decorating;
    }
}
