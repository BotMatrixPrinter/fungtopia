using System.Numerics;

namespace Fungtopia.Wallet;

/// <summary>The player's Mushroom Coins, published by the Wallet module every tick.</summary>
public readonly struct WalletState
{
    /// <summary>Mushroom Coins (M¢) earned this session.</summary>
    public readonly int Coins;

    public WalletState(int coins)
    {
        Coins = coins;
    }
}

/// <summary>Published by the Wallet module once per reward added to the wallet.</summary>
public readonly struct CoinsEarned
{
    /// <summary>Mushroom Coins (M¢) added (more than 0).</summary>
    public readonly int Amount;

    /// <summary>Where they were earned (the defeated monster's feet), meters.</summary>
    public readonly Vector3 Position;

    public CoinsEarned(int amount, Vector3 position)
    {
        Amount = amount;
        Position = position;
    }
}
