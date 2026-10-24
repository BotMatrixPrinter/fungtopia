using System;
using Fungtopia.Bow;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.Wallet;

namespace Fungtopia.HUD;

/// <summary>
/// The HUD's state: hearts from <see cref="PlayerState"/> (shaking on a hit), Mushroom Coins from
/// <see cref="WalletState"/> (popping, with a floating "+50" per <see cref="CoinsEarned"/>), the bow
/// draw ring from <see cref="BowState"/> and the soft faint fade after <see cref="PlayerDefeated"/>.
/// Animations run on the fixed ticks; the view reads them at a fraction between two ticks.
/// </summary>
public sealed class HudModule : IModule,
    IMessageHandler<PlayerState>, IMessageHandler<PlayerDefeated>,
    IMessageHandler<WalletState>, IMessageHandler<CoinsEarned>, IMessageHandler<BowState>
{
    public const int HealthPerHeart = 2;
    public const float ShakeSeconds = 0.4f;
    public const float ShakePixels = 9f;
    public const float ShakeHertz = 14f;
    public const float PopSeconds = 0.35f;
    public const float PopGrowth = 0.35f;
    public const int FloaterCapacity = 4;
    public const float FloaterSeconds = 1.2f;
    public const float FloaterRisePixels = 70f;
    public const float FadeSeconds = 1.2f;
    public const float FadeMaxAlpha = 0.75f;

    private IMessageBus? _bus;
    private float _step = 1f / 60f;

    private float _shakeElapsed = float.PositiveInfinity;
    private float _popElapsed = float.PositiveInfinity;
    private readonly int[] _floaterAmounts = new int[FloaterCapacity];
    private readonly float[] _floaterElapsed = new float[FloaterCapacity];

    private float _drawLatest, _drawPrevious, _drawCurrent;
    private float _fadePrevious, _fadeCurrent;

    public bool HasPlayer { get; private set; }
    public int Health { get; private set; }
    public int MaxHealth { get; private set; }
    public int Coins { get; private set; }

    /// <summary>Rewards seen so far (each one floats a "+N" in slot <c>(EarnedCount - 1) % FloaterCapacity</c>).</summary>
    public long EarnedCount { get; private set; }

    /// <summary>True from <see cref="PlayerDefeated"/> until a <see cref="PlayerState"/> is alive again.</summary>
    public bool Fainted { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<PlayerDefeated>(this);
        bus.Subscribe<WalletState>(this);
        bus.Subscribe<CoinsEarned>(this);
        bus.Subscribe<BowState>(this);
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("HudModule.Tick before Start.");
        float dt = (float)context.StepSeconds;
        _step = dt;
        _shakeElapsed += dt;
        _popElapsed += dt;
        for (int i = 0; i < FloaterCapacity; i++)
            _floaterElapsed[i] += dt;

        _drawPrevious = _drawCurrent;
        _drawCurrent = _drawLatest;

        _fadePrevious = _fadeCurrent;
        float target = Fainted ? 1f : 0f;
        float change = dt / FadeSeconds;
        _fadeCurrent = _fadeCurrent < target
            ? MathF.Min(target, _fadeCurrent + change)
            : MathF.Max(target, _fadeCurrent - change);
    }

    public void Handle(in PlayerState message)
    {
        int max = Math.Max(0, message.MaxHealth);
        int health = Math.Clamp(message.Health, 0, max);
        if (HasPlayer && health < Health)
            _shakeElapsed = 0f;
        HasPlayer = true;
        Health = health;
        MaxHealth = max;
        if (message.Alive)
            Fainted = false;
    }

    public void Handle(in PlayerDefeated message) => Fainted = true;

    public void Handle(in WalletState message) => Coins = message.Coins;

    public void Handle(in CoinsEarned message)
    {
        if (message.Amount <= 0)
            return;
        int slot = (int)(EarnedCount % FloaterCapacity);
        _floaterAmounts[slot] = message.Amount;
        _floaterElapsed[slot] = 0f;
        EarnedCount++;
        _popElapsed = 0f;
    }

    public void Handle(in BowState message) =>
        _drawLatest = float.IsNaN(message.Draw) ? 0f : Math.Clamp(message.Draw, 0f, 1f);

    // Hearts

    public int HeartCount => HasPlayer ? (MaxHealth + HealthPerHeart - 1) / HealthPerHeart : 0;

    /// <summary>How full heart <paramref name="index"/> (0 at the left) is: 0, 0.5 or 1.</summary>
    public float HeartFill(int index)
    {
        if (index < 0 || index >= HeartCount)
            return 0f;
        int left = Health - index * HealthPerHeart;
        return left >= HealthPerHeart ? 1f : left <= 0 ? 0f : (float)left / HealthPerHeart;
    }

    public bool HeartsShaking => _shakeElapsed < ShakeSeconds;

    /// <summary>Sideways offset of the hearts in pixels, a dying wobble after a hit.</summary>
    public float HeartsShake(float fraction)
    {
        float t = TimeAt(_shakeElapsed, _step, fraction);
        if (t >= ShakeSeconds)
            return 0f;
        return ShakePixels * (1f - t / ShakeSeconds) * MathF.Sin(2f * MathF.PI * ShakeHertz * t);
    }

    // Mushroom Coins

    /// <summary>Scale of the coin counter: a quick swell to 1 + <see cref="PopGrowth"/> and back after a reward.</summary>
    public float CoinsPopScale(float fraction)
    {
        float t = TimeAt(_popElapsed, _step, fraction);
        return t >= PopSeconds ? 1f : 1f + PopGrowth * MathF.Sin(MathF.PI * t / PopSeconds);
    }

    public HudFloater Floater(int slot) => new(_floaterAmounts[slot], _floaterElapsed[slot], _step);

    // Bow draw ring

    /// <summary>How much of the ring around the crosshair is filled (the bow's draw, 0 to 1).</summary>
    public float DrawRing(float fraction) => _drawPrevious + (_drawCurrent - _drawPrevious) * Math.Clamp(fraction, 0f, 1f);

    public bool DrawRingVisible => _drawCurrent > 0f || _drawPrevious > 0f;

    public bool DrawFull => _drawCurrent >= 1f;

    // Fainting

    /// <summary>Opacity of the soft fade over the screen, up to <see cref="FadeMaxAlpha"/>.</summary>
    public float FaintFade(float fraction) => FadeMaxAlpha * FaintTextAlpha(fraction);

    /// <summary>Opacity of "You fainted..." (0 to 1).</summary>
    public float FaintTextAlpha(float fraction)
    {
        float x = _fadePrevious + (_fadeCurrent - _fadePrevious) * Math.Clamp(fraction, 0f, 1f);
        return 1f - (1f - x) * (1f - x);
    }

    /// <summary>Seconds since an event at a fraction of the way from the last tick to the next.</summary>
    internal static float TimeAt(float elapsed, float step, float fraction) =>
        MathF.Max(0f, elapsed - (1f - Math.Clamp(fraction, 0f, 1f)) * step);
}
