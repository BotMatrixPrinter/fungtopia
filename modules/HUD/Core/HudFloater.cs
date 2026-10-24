using System;

namespace Fungtopia.HUD;

/// <summary>A floating "+50" rising from the coin counter after a reward.</summary>
public readonly struct HudFloater
{
    /// <summary>Mushroom Coins earned (0 for a slot never used).</summary>
    public readonly int Amount;

    /// <summary>Seconds since the reward, at the last tick.</summary>
    public readonly float Elapsed;

    private readonly float _step;

    public HudFloater(int amount, float elapsed, float step)
    {
        Amount = amount;
        Elapsed = elapsed;
        _step = step;
    }

    public bool Active => Amount > 0 && Elapsed < HudModule.FloaterSeconds;

    /// <summary>Pixels risen at a fraction (0 to 1) of the way from the last tick to the next.</summary>
    public float Rise(float fraction)
    {
        float x = Progress(fraction);
        return HudModule.FloaterRisePixels * (1f - (1f - x) * (1f - x));
    }

    /// <summary>Opacity: full for the first 40 %, then fading out.</summary>
    public float Alpha(float fraction)
    {
        if (!Active)
            return 0f;
        float x = Progress(fraction);
        return x < 0.4f ? 1f : Math.Clamp(1f - (x - 0.4f) / 0.6f, 0f, 1f);
    }

    private float Progress(float fraction) =>
        Math.Clamp(HudModule.TimeAt(Elapsed, _step, fraction) / HudModule.FloaterSeconds, 0f, 1f);
}
