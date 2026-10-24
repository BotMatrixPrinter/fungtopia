using System;
using Fungtopia.Bow;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.NPCs;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Fungtopia.Wallet;

namespace Fungtopia.HUD;

/// <summary>
/// The HUD's state: hearts from <see cref="PlayerState"/> (shaking on a hit), Mushroom Coins from
/// <see cref="WalletState"/> (popping, with a floating "+50" per <see cref="CoinsEarned"/>), the bow
/// crosshair and draw ring from <see cref="BowState"/> (only while the bow is in use), the fade to black after <see cref="PlayerDefeated"/> and the
/// wake-up text after <see cref="WokeUp"/>, the place-name banner when <see cref="PlayerState.Area"/>
/// changes, the clock from <see cref="TimeOfDay"/> and the quest tracker from <see cref="QuestState"/>;
/// all of it hidden while <see cref="StartScreenState.Open"/>.
/// Animations run on the fixed ticks; the view reads them at a fraction between two ticks.
/// </summary>
public sealed class HudModule : IModule,
    IMessageHandler<PlayerState>, IMessageHandler<PlayerDefeated>,
    IMessageHandler<WalletState>, IMessageHandler<CoinsEarned>, IMessageHandler<BowState>,
    IMessageHandler<TimeOfDay>, IMessageHandler<WokeUp>, IMessageHandler<QuestState>,
    IMessageHandler<StartScreenState>
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
    public const float FadeInSeconds = 1.2f;
    public const float FadeOutSeconds = 2f;
    public const float FadeMaxAlpha = 1f;
    public const float WakeSeconds = 4.5f;
    public const float WakeTextInSeconds = 0.5f;
    public const float WakeTextOutSeconds = 1f;
    public const float BannerSeconds = 3f;
    public const float BannerSlideSeconds = 0.45f;
    public const float CrosshairLingerSeconds = 0.5f;
    public const float CrosshairFadeSeconds = 0.1f;

    private IMessageBus? _bus;
    private float _step = 1f / 60f;

    private float _shakeElapsed = float.PositiveInfinity;
    private float _popElapsed = float.PositiveInfinity;
    private readonly int[] _floaterAmounts = new int[FloaterCapacity];
    private readonly float[] _floaterElapsed = new float[FloaterCapacity];

    private float _drawLatest, _drawPrevious, _drawCurrent;
    private float _sinceDrawn = float.PositiveInfinity;
    private float _crosshairPrevious, _crosshairCurrent;
    private float _fadePrevious, _fadeCurrent;
    private float _wakeElapsed = float.PositiveInfinity;
    private float _bannerElapsed = float.PositiveInfinity;
    private bool _hasBannerArea;
    private float _questPopElapsed = float.PositiveInfinity;

    public bool HasPlayer { get; private set; }
    public int Health { get; private set; }
    public int MaxHealth { get; private set; }
    public int Coins { get; private set; }

    /// <summary>Rewards seen so far (each one floats a "+N" in slot <c>(EarnedCount - 1) % FloaterCapacity</c>).</summary>
    public long EarnedCount { get; private set; }

    /// <summary>True from <see cref="PlayerDefeated"/> until a <see cref="PlayerState"/> is alive again.</summary>
    public bool Fainted { get; private set; }

    /// <summary>The day of the last <see cref="WokeUp"/> (0 before any).</summary>
    public int WakeDay { get; private set; }

    /// <summary>The named place the banner shows (or last showed); see <see cref="HudText.AreaName"/>.</summary>
    public Area BannerArea { get; private set; }

    /// <summary>Banners started so far (one per new named place entered).</summary>
    public int BannerCount { get; private set; }

    /// <summary>The banner's place is a spooky one (the Deep Forest): the view draws it darker.</summary>
    public bool BannerSpooky => HudText.IsSpooky(BannerArea);

    /// <summary>The start screen is open: the whole HUD is hidden (shown again once it closes).</summary>
    public bool Hidden { get; private set; }

    public bool HasClock { get; private set; }
    public int Day { get; private set; }

    /// <summary>Whole minutes after midnight, 0..1439.</summary>
    public int ClockMinute { get; private set; }

    /// <summary>Night (20:00-6:00): the clock shows a moon instead of the sun.</summary>
    public bool IsNight { get; private set; }

    public string QuestTitle { get; private set; } = "";
    public int QuestProgress { get; private set; }
    public int QuestGoal { get; private set; }
    public bool QuestComplete { get; private set; }

    /// <summary>A quest is given: the tracker shows.</summary>
    public bool QuestActive { get; private set; }

    /// <summary>Counts up whenever the quest tracker's text or check changes.</summary>
    public int QuestVersion { get; private set; }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<PlayerDefeated>(this);
        bus.Subscribe<WalletState>(this);
        bus.Subscribe<CoinsEarned>(this);
        bus.Subscribe<BowState>(this);
        bus.Subscribe<TimeOfDay>(this);
        bus.Subscribe<WokeUp>(this);
        bus.Subscribe<QuestState>(this);
        bus.Subscribe<StartScreenState>(this);
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
        _wakeElapsed += dt;
        _bannerElapsed += dt;
        _questPopElapsed += dt;

        _drawPrevious = _drawCurrent;
        _drawCurrent = _drawLatest;

        // The crosshair shows while the bow is drawn and lingers a moment after the shot.
        _sinceDrawn = _drawCurrent > 0f ? 0f : _sinceDrawn + dt;
        _crosshairPrevious = _crosshairCurrent;
        float aim = _sinceDrawn <= CrosshairLingerSeconds ? 1f : 0f;
        float fade = dt / CrosshairFadeSeconds;
        _crosshairCurrent = _crosshairCurrent < aim
            ? MathF.Min(aim, _crosshairCurrent + fade)
            : MathF.Max(aim, _crosshairCurrent - fade);

        _fadePrevious = _fadeCurrent;
        float target = Fainted ? 1f : 0f;
        float change = dt / (Fainted ? FadeInSeconds : FadeOutSeconds);
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
        {
            Fainted = false;
            // A new place's banner; the roads between places have none, and nothing slides in
            // while the player lies fainted.
            if (HudText.AreaName(message.Area).Length > 0 && (!_hasBannerArea || message.Area != BannerArea))
            {
                _hasBannerArea = true;
                BannerArea = message.Area;
                BannerCount++;
                _bannerElapsed = 0f;
            }
        }
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

    public void Handle(in TimeOfDay message)
    {
        HasClock = true;
        Day = message.Day;
        float hour = float.IsNaN(message.Hour) ? 0f : message.Hour;
        ClockMinute = Math.Clamp((int)(hour * 60f), 0, 24 * 60 - 1);
        IsNight = message.IsNight;
    }

    public void Handle(in WokeUp message)
    {
        WakeDay = message.Day;
        _wakeElapsed = 0f;
    }

    public void Handle(in StartScreenState message) => Hidden = message.Open;

    public void Handle(in QuestState message)
    {
        bool active = message.Active && !string.IsNullOrEmpty(message.Title) && message.Goal > 0;
        string title = active ? message.Title : "";
        int goal = active ? message.Goal : 0;
        int progress = active ? Math.Clamp(message.Progress, 0, goal) : 0;
        bool complete = active && message.Complete;
        if (active == QuestActive && progress == QuestProgress && goal == QuestGoal && complete == QuestComplete
            && string.Equals(title, QuestTitle, StringComparison.Ordinal))
            return;
        bool sameQuest = active && QuestActive && string.Equals(title, QuestTitle, StringComparison.Ordinal);
        if (sameQuest && (progress > QuestProgress || (complete && !QuestComplete)))
            _questPopElapsed = 0f;
        QuestActive = active;
        QuestTitle = title;
        QuestProgress = progress;
        QuestGoal = goal;
        QuestComplete = complete;
        QuestVersion++;
    }

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

    /// <summary>
    /// Opacity of the whole crosshair (dot and ring), 0 to 1: it fades in over
    /// <see cref="CrosshairFadeSeconds"/> once the bow is drawn, stays while drawing and for
    /// <see cref="CrosshairLingerSeconds"/> after the draw returns to 0, then fades out.
    /// </summary>
    public float CrosshairAlpha(float fraction) =>
        _crosshairPrevious + (_crosshairCurrent - _crosshairPrevious) * Math.Clamp(fraction, 0f, 1f);

    public bool CrosshairVisible => _crosshairCurrent > 0f || _crosshairPrevious > 0f;

    public bool DrawFull => _drawCurrent >= 1f;

    // Fainting and waking up

    /// <summary>Opacity of the fade to black over the screen, up to <see cref="FadeMaxAlpha"/>.</summary>
    public float FaintFade(float fraction)
    {
        float x = _fadePrevious + (_fadeCurrent - _fadePrevious) * Math.Clamp(fraction, 0f, 1f);
        return FadeMaxAlpha * (1f - (1f - x) * (1f - x));
    }

    /// <summary>Opacity of "You fainted..." (0 to 1): with the fade, until the wake-up text takes over.</summary>
    public float FaintTextAlpha(float fraction) => Waking ? 0f : FaintFade(fraction) / FadeMaxAlpha;

    /// <summary>The wake-up text is showing (for <see cref="WakeSeconds"/> after <see cref="WokeUp"/>).</summary>
    public bool Waking => _wakeElapsed < WakeSeconds;

    /// <summary>Opacity of "Day N - you wake up at Dr. Jeremy's": in quickly, held, then out.</summary>
    public float WakeTextAlpha(float fraction)
    {
        float t = TimeAt(_wakeElapsed, _step, fraction);
        if (t >= WakeSeconds)
            return 0f;
        return Math.Clamp(MathF.Min(t / WakeTextInSeconds, (WakeSeconds - t) / WakeTextOutSeconds), 0f, 1f);
    }

    // Place-name banner

    public bool BannerVisible => _bannerElapsed < BannerSeconds;

    /// <summary>How far the banner has slid in: 0 hidden above the screen, 1 fully in.</summary>
    public float BannerSlide(float fraction)
    {
        float t = TimeAt(_bannerElapsed, _step, fraction);
        if (t >= BannerSeconds)
            return 0f;
        float x = Math.Clamp(MathF.Min(t, BannerSeconds - t) / BannerSlideSeconds, 0f, 1f);
        return 1f - (1f - x) * (1f - x) * (1f - x);
    }

    // Quest tracker

    /// <summary>Scale of the quest tracker: a little swell when progress is made or it completes.</summary>
    public float QuestPopScale(float fraction)
    {
        float t = TimeAt(_questPopElapsed, _step, fraction);
        return t >= PopSeconds ? 1f : 1f + 0.5f * PopGrowth * MathF.Sin(MathF.PI * t / PopSeconds);
    }

    /// <summary>Seconds since an event at a fraction of the way from the last tick to the next.</summary>
    internal static float TimeAt(float elapsed, float step, float fraction) =>
        MathF.Max(0f, elapsed - (1f - Math.Clamp(fraction, 0f, 1f)) * step);
}
