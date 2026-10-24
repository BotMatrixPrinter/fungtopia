using System;
using System.Numerics;
using System.Text.Json;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;

namespace Fungtopia.Wallet;

/// <summary>
/// The player's Mushroom Coins (M¢) for the session: adds the reward of every
/// <see cref="MonsterKilled"/>, whoever made the kill (player or pet), and every
/// <see cref="GrantCoins"/> (at the player's position, from <see cref="PlayerState"/>),
/// publishes <see cref="CoinsEarned"/> once per reward and <see cref="WalletState"/> every tick.
/// Only a <see cref="SpendCoins"/> takes coins away, and only when there are enough; it is
/// answered with one <see cref="SpendResult"/>. The player's defeat costs none. Saved through the
/// Save module: answers <see cref="CollectSave"/> with a "Wallet" <see cref="SavePart"/> and
/// restores the coins from the matching <see cref="RestorePart"/>, quietly (no rewards).
/// </summary>
public sealed class WalletModule : IModule, IMessageHandler<MonsterKilled>, IMessageHandler<SpendCoins>,
    IMessageHandler<GrantCoins>, IMessageHandler<PlayerState>, IMessageHandler<CollectSave>,
    IMessageHandler<RestorePart>
{
    /// <summary>How many of the latest rewards <see cref="Recent"/> keeps for the view.</summary>
    public const int RecentCapacity = 16;

    /// <summary>The version of the saved JSON this module writes and reads.</summary>
    public const int SaveVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>The saved state: <c>{"version":1,"coins":123}</c>.</summary>
    private sealed record SavedWallet(int Version, int Coins);

    private readonly CoinsEarned[] _recent = new CoinsEarned[RecentCapacity];
    private IMessageBus? _bus;
    private Vector3 _playerPosition;
    private bool _loggedBadSave;

    /// <summary>Where a save part that cannot be restored is reported (once); the console by default.</summary>
    public Action<string> Log { get; set; } = Console.WriteLine;

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
        bus.Subscribe<SpendCoins>(this);
        bus.Subscribe<GrantCoins>(this);
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<CollectSave>(this);
        bus.Subscribe<RestorePart>(this);
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("WalletModule.Tick before Start.");
        Ticks++;
        _bus.Publish(new WalletState(Coins));
    }

    public void Handle(in MonsterKilled message) => Earn(message.Reward, message.Position);

    public void Handle(in GrantCoins message) => Earn(message.Amount, _playerPosition);

    public void Handle(in PlayerState message) => _playerPosition = message.Position;

    public void Handle(in SpendCoins message)
    {
        bool ok = message.Amount >= 0 && message.Amount <= Coins;
        if (ok)
            Coins -= message.Amount;
        _bus?.Publish(new SpendResult(message.RequestId, ok, Coins));
    }

    public void Handle(in CollectSave message) =>
        _bus?.Publish(new SavePart(message.RequestId, SaveModules.Wallet, SaveJson()));

    public void Handle(in RestorePart message)
    {
        if (message.Module != SaveModules.Wallet)
            return;
        // Only the coins change: no CoinsEarned (no pop-up or burst), nothing in Recent.
        if (TryReadSave(message.Json, out int coins))
        {
            Coins = coins;
            return;
        }
        Coins = 0;
        if (!_loggedBadSave)
        {
            _loggedBadSave = true;
            Log("Fungtopia: Wallet: the saved wallet cannot be read; starting with an empty wallet.");
        }
    }

    /// <summary>The wallet's saved state as compact JSON, what <see cref="CollectSave"/> is answered with.</summary>
    public string SaveJson() => JsonSerializer.Serialize(new SavedWallet(SaveVersion, Coins), JsonOptions);

    private static bool TryReadSave(string? json, out int coins)
    {
        coins = 0;
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            var saved = JsonSerializer.Deserialize<SavedWallet>(json, JsonOptions);
            if (saved == null || saved.Version != SaveVersion || saved.Coins < 0)
                return false;
            coins = saved.Coins;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void Earn(int amount, Vector3 position)
    {
        if (amount <= 0)
            return;
        // Saturate rather than wrap around: a full wallet stays full.
        Coins = (int)Math.Min((long)Coins + amount, int.MaxValue);
        var earned = new CoinsEarned(amount, position);
        _recent[EarnedCount % RecentCapacity] = earned;
        EarnedCount++;
        _bus?.Publish(earned);
    }
}
