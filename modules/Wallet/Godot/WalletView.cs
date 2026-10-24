using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Wallet.View;

/// <summary>
/// Plays a little burst of gold Mushroom Coins flying up from where each reward was earned
/// (the defeated monster's feet), from the Wallet Core's latest rewards.
/// </summary>
public partial class WalletView : Node3D, IModuleView
{
    private const int Bursts = 8;

    private WalletModule? _wallet;
    private readonly CoinBurst[] _bursts = new CoinBurst[Bursts];
    private long _seen;
    private int _next;

    public void Bind(IModule module) => _wallet = (WalletModule)module;

    public override void _Ready()
    {
        for (int i = 0; i < Bursts; i++)
        {
            _bursts[i] = CoinBurst.Create();
            AddChild(_bursts[i]);
        }
        _seen = _wallet?.EarnedCount ?? 0;
    }

    public override void _Process(double delta)
    {
        if (_wallet == null)
            return;
        long earned = _wallet.EarnedCount;
        // Only the latest rewards are kept; older ones missed between frames are skipped.
        _seen = Math.Max(_seen, earned - WalletModule.RecentCapacity);
        var recent = _wallet.Recent;
        for (; _seen < earned; _seen++)
        {
            var reward = recent[(int)(_seen % WalletModule.RecentCapacity)];
            Play(new Vector3(reward.Position.X, reward.Position.Y, reward.Position.Z), reward.Amount, (int)_seen);
        }
    }

    /// <summary>Plays a burst, reusing an idle one (or the oldest when all are playing).</summary>
    public void Play(Vector3 position, int amount, int seed)
    {
        var burst = _bursts[_next];
        for (int i = 0; i < Bursts; i++)
        {
            int k = (_next + i) % Bursts;
            if (!_bursts[k].Playing)
            {
                burst = _bursts[k];
                _next = k;
                break;
            }
        }
        _next = (_next + 1) % Bursts;
        burst.Play(position, amount, seed);
    }
}
