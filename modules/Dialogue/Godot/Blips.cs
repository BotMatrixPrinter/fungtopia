using System;
using Godot;

namespace Fungtopia.Dialogue.View;

/// <summary>
/// The short letter blips of the four voices, synthesized once: deadpan (a low, flat square-ish
/// buzz), bubbly (a high chirp sliding up), kind (a soft, warm triangle), neutral (a plain mid
/// sine). Each letter plays its voice with a small pitch wobble picked from the letter, so a line
/// always sounds the same.
/// </summary>
public static class Blips
{
    private const int MixRate = 22050;

    private static readonly float[] Wobble = { 0f, 0.09f, 0.05f, 0.04f };

    public static AudioStreamWav[] Make()
    {
        return new[]
        {
            Make(150f, 150f, 0.055f, 0.35f, Wave.Square),
            Make(620f, 820f, 0.045f, 0.4f, Wave.Sine),
            Make(360f, 330f, 0.06f, 0.45f, Wave.Triangle),
            Make(440f, 440f, 0.05f, 0.35f, Wave.Sine),
        };
    }

    /// <summary>The pitch scale for <paramref name="letter"/> in <paramref name="voice"/>.</summary>
    public static float Pitch(int voice, char letter)
    {
        int step = letter * 37 % 7 - 3;
        return 1f + step * Wobble[Math.Clamp(voice, 0, Wobble.Length - 1)] / 3f;
    }

    /// <summary>True for letters that blip (not spaces or punctuation).</summary>
    public static bool Sounds(char letter) => char.IsLetterOrDigit(letter);

    private enum Wave { Sine, Square, Triangle }

    private static AudioStreamWav Make(float startHz, float endHz, float seconds, float volume, Wave wave)
    {
        int count = (int)(MixRate * seconds);
        var data = new byte[count * 2];
        double phase = 0;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            phase += (startHz + (endHz - startHz) * t) / MixRate;
            double p = phase - Math.Floor(phase);
            double s = wave switch
            {
                Wave.Square => Math.Tanh(Math.Sin(2 * Math.PI * p) * 3) * 0.6,
                Wave.Triangle => 1 - 4 * Math.Abs(p - 0.5),
                _ => Math.Sin(2 * Math.PI * p),
            };
            // A fast attack and a soft fade.
            double envelope = Math.Min(1, t * 20) * (1 - t) * (1 - t);
            short sample = (short)(s * envelope * volume * short.MaxValue);
            data[2 * i] = (byte)(sample & 0xff);
            data[2 * i + 1] = (byte)((sample >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = MixRate, Stereo = false, Data = data };
    }
}
