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

/// <summary>
/// Asks the Wallet module to take coins (a purchase). The wallet takes them only if there are
/// enough, and answers with exactly one <see cref="SpendResult"/> carrying the same
/// <see cref="RequestId"/>.
/// </summary>
public readonly struct SpendCoins
{
    /// <summary>Chosen by the asker, echoed in the <see cref="SpendResult"/>.</summary>
    public readonly int RequestId;

    /// <summary>Mushroom Coins (M¢) to take (0 or more; a negative amount is refused).</summary>
    public readonly int Amount;

    /// <summary>What they pay for (for logs and the view), e.g. "Mushroom Crow".</summary>
    public readonly string What;

    public SpendCoins(int requestId, int amount, string what)
    {
        RequestId = requestId;
        Amount = amount;
        What = what;
    }
}

/// <summary>The Wallet module's answer to one <see cref="SpendCoins"/>.</summary>
public readonly struct SpendResult
{
    /// <summary>The <see cref="SpendCoins.RequestId"/> answered.</summary>
    public readonly int RequestId;

    /// <summary>True if the coins were taken; false if there were not enough (nothing changed).</summary>
    public readonly bool Ok;

    /// <summary>Mushroom Coins (M¢) in the wallet after the request.</summary>
    public readonly int CoinsLeft;

    public SpendResult(int requestId, bool ok, int coinsLeft)
    {
        RequestId = requestId;
        Ok = ok;
        CoinsLeft = coinsLeft;
    }
}

/// <summary>
/// Asks the Wallet module to add coins (a quest reward). The wallet adds them and publishes a
/// <see cref="CoinsEarned"/> at the player's position.
/// </summary>
public readonly struct GrantCoins
{
    /// <summary>Mushroom Coins (M¢) to add (ignored when 0 or less).</summary>
    public readonly int Amount;

    /// <summary>Why they are given (for logs and the view), e.g. "Millie's quest".</summary>
    public readonly string Why;

    public GrantCoins(int amount, string why)
    {
        Amount = amount;
        Why = why;
    }
}
