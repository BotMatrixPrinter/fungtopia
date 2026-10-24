using System;
using Fungtopia.Monsters;
using Fungtopia.Shell;

namespace Fungtopia.Wallet;

/// <summary>
/// The player's Mushroom Coins (M¢) for the session: adds the reward of every
/// <see cref="MonsterKilled"/>, whoever made the kill (player or pet), publishes
/// <see cref="CoinsEarned"/> once per reward and <see cref="WalletState"/> every tick. Nothing
/// takes coins away: the player's defeat costs none. Not saved to disk yet.
/// </summary>
public sealed class WalletModule : IModule, IMessageHandler<MonsterKilled>
{
    /// <summary>How many of the latest rewards <see cref="Recent"/> keeps for the view.</summary>
    public const int RecentCapacity = 16;

    private readonly CoinsEarned[] _recent = new CoinsEarned[RecentCapacity];
    private IMessageBus? _bus;

    /// <summary>Mushroom Coins (M¢) earned this session.</summary>
    public int Coins { get; private set; }

    /// <summary>Rewards earned this session, each one a <see cref="CoinsEarned"/>.</summary>
    public long EarnedCount { get; private set; }

    /// <summary>Fixed ticks run so far.</summary>
    public long Ticks { get; private set; }

    /// <summary>
    /// The latest rewards, a ring: reward number <c>n</c> (0-based, below <see cref="EarnedCount"/>)
    /// is at <c>n % RecentCapacity</c> while it is among the last <see cref="RecentCapacity"/>.
    /// </summary>
    public ReadOnlySpan<CoinsEarned> Recent => _recent;

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<MonsterKilled>(this);
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("WalletModule.Tick before Start.");
        Ticks++;
        _bus.Publish(new WalletState(Coins));
    }

    public void Handle(in MonsterKilled message)
    {
        if (message.Reward <= 0)
            return;
        // Saturate rather than wrap around: a full wallet stays full.
        Coins = (int)Math.Min((long)Coins + message.Reward, int.MaxValue);
        var earned = new CoinsEarned(message.Reward, message.Position);
        _recent[EarnedCount % RecentCapacity] = earned;
        EarnedCount++;
        _bus?.Publish(earned);
    }
}
