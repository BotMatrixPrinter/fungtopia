using System;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;

namespace Fungtopia.Town;

/// <summary>
/// Mushroom Town's simulation. The town itself is fixed data (<see cref="TownLayout"/>); the state
/// is whether the street lamps are lit, which follows <see cref="TimeOfDay.IsNight"/>, and how loud
/// the town's music plays (<see cref="MusicLevel"/>), which follows <see cref="PlayerState.Area"/>.
/// <see cref="TownLights"/> is published only when the lamps switch; nothing allocates per tick.
/// </summary>
public sealed class TownModule : IModule, IMessageHandler<TimeOfDay>, IMessageHandler<PlayerState>
{
    /// <summary>Ticks the music takes to fade fully in or out: 2 s at 60 ticks a second.</summary>
    public const int MusicFadeTicks = 120;

    private IMessageBus? _bus;
    private bool _inTown;
    private int _musicTicks;

    /// <summary>Whether the street lamps are lit (night). Off until the first night.</summary>
    public bool LampsLit { get; private set; }

    /// <summary>
    /// How loud the town's music plays, 0 (silent) to 1: rises by 1/<see cref="MusicFadeTicks"/> each
    /// tick while the player is in <see cref="Area.Town"/>, falls as fast anywhere else. 0 before the
    /// first <see cref="PlayerState"/> (the title screen).
    /// </summary>
    public float MusicLevel => _musicTicks / (float)MusicFadeTicks;

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<TimeOfDay>(this);
        bus.Subscribe<PlayerState>(this);
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("TownModule.Tick before Start.");
        if (_inTown)
        {
            if (_musicTicks < MusicFadeTicks)
                _musicTicks++;
        }
        else if (_musicTicks > 0)
        {
            _musicTicks--;
        }
    }

    public void Handle(in TimeOfDay message)
    {
        if (message.IsNight == LampsLit)
            return;
        LampsLit = message.IsNight;
        _bus?.Publish(new TownLights(LampsLit));
    }

    public void Handle(in PlayerState message) => _inTown = message.Area == Area.Town;

    /// <summary>
    /// Whether <paramref name="bytes"/> start like an MP3 file (an ID3v2 tag or an MPEG audio frame
    /// header), so the view can warn and stay silent instead of handing Godot a broken file.
    /// </summary>
    public static bool LooksLikeMp3(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4)
            return false;
        if (bytes[0] == (byte)'I' && bytes[1] == (byte)'D' && bytes[2] == (byte)'3')
            return bytes.Length > 10;
        // Frame sync (11 bits set), an MPEG version and layer that exist, a bitrate that is not "bad".
        return bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0 && (bytes[1] & 0x18) != 0x08
            && (bytes[1] & 0x06) != 0 && (bytes[2] & 0xF0) != 0xF0;
    }
}
