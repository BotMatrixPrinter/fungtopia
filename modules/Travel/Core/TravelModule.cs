using System;
using System.Numerics;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;

namespace Fungtopia.Travel;

/// <summary>
/// Fast travel between Mushroom Town, Mushroom Forest and Pet City. Shows a prompt near the
/// signposts, opens the travel map on Interact there or on OpenMap anywhere outdoors, and on a
/// pick fades out, publishes <see cref="TeleportPlayer"/> to that place's signpost and fades in.
/// Publishes <see cref="MapState"/> every tick, and <see cref="UiFocus"/> (owner
/// <see cref="FocusOwner"/>) when the map opens and when it closes, so Input stops walking meanwhile, and
/// <see cref="InteractAvailable"/> (owner <see cref="InteractOwner"/>) when the signpost prompt shows and hides.
/// </summary>
public sealed class TravelModule : IModule, IMessageHandler<PlayerInput>, IMessageHandler<PlayerState>,
    IMessageHandler<ActiveDevice>, IMessageHandler<MenuNav>
{
    /// <summary>How near a signpost (horizontal meters) its prompt shows and Interact opens the map.</summary>
    public const float PromptRadius = 2.5f;

    /// <summary>Icon size (pixels) of the signpost prompt's button.</summary>
    public const int PromptIconSize = 40;

    /// <summary>Icon size (pixels, on the 1920 x 1080 map canvas) of the map's hint line.</summary>
    public const int HintIconSize = 44;

    /// <summary>The fade to black after a pick, while the map still shows.</summary>
    public const float FadeOutSeconds = 0.3f;

    /// <summary>The fade back in after the teleport.</summary>
    public const float FadeInSeconds = 0.4f;

    /// <summary>How far in front of the signpost (along its facing) the player arrives.</summary>
    public const float ArrivalGap = 1.5f;

    /// <summary>The <see cref="UiFocus.Owner"/> of the travel map.</summary>
    public const int FocusOwner = 0x7472_6176; // "trav"

    /// <summary>The <see cref="InteractAvailable.Owner"/> published while a signpost's prompt shows (the gamepad's A opens the map then).</summary>
    public const int InteractOwner = 0x7369_676E; // "sign"

    private static readonly Area[] s_destinations = { Area.Town, Area.Forest, Area.PetCity };

    private static readonly Place[] s_signposts =
    {
        Places.SignpostTown, Places.SignpostForestSouth, Places.SignpostForestNorth, Places.SignpostPetCity,
    };

    private static readonly Area[] s_signpostAreas = { Area.Town, Area.Forest, Area.Forest, Area.PetCity };

    private static readonly DeviceKind[] s_devices = Enum.GetValues<DeviceKind>();
    private static readonly string[] s_prompts = PerDevice(BuildPrompt);
    private static readonly string[] s_hints = PerDevice(BuildHint);

    private enum Phase
    {
        Idle,
        FadingOut,
        FadingIn,
    }

    private IMessageBus? _bus;
    private PlayerInput _input;
    private bool _haveInput;
    private PlayerState _player;
    private bool _havePlayer;
    private Phase _phase;
    private float _phaseSeconds;
    private int _pendingPick = -1;
    private bool _pendingClose;
    private int _hover = -1;
    private bool _pendingConfirm;
    private MenuNav _nav;
    private bool _focused;
    private bool _offered;

    /// <summary>The three places, in map order: Town, Forest, PetCity.</summary>
    public static ReadOnlySpan<Area> Destinations => s_destinations;

    /// <summary>Every signpost: Town, Forest south, Forest north, Pet City.</summary>
    public static ReadOnlySpan<Place> Signposts => s_signposts;

    /// <summary>The area each of <see cref="Signposts"/> stands in.</summary>
    public static ReadOnlySpan<Area> SignpostAreas => s_signpostAreas;

    /// <summary>The map is shown (from opening until the fade after a pick is black).</summary>
    public bool Open { get; private set; }

    /// <summary>The highlighted place on the map (a <see cref="Destinations"/> index).</summary>
    public int Selected { get; private set; }

    /// <summary>"You are here": the <see cref="Destinations"/> index nearest the player.</summary>
    public int Here { get; private set; }

    /// <summary>The <see cref="Signposts"/> index within <see cref="PromptRadius"/> of the player, or -1.</summary>
    public int NearSignpost { get; private set; } = -1;

    /// <summary>Whether the signpost's "[Interact] Map" prompt shows (near a signpost, map closed, outdoors).</summary>
    public bool ShowPrompt { get; private set; }

    /// <summary>Black over the screen, 0..1.</summary>
    public float Fade { get; private set; }

    /// <summary>The <see cref="Destinations"/> index being traveled to during a fade, or -1.</summary>
    public int Traveling { get; private set; } = -1;

    public long Ticks { get; private set; }

    /// <summary>The device used last, from Input's <see cref="ActiveDevice"/> (keyboard and mouse until then).</summary>
    public DeviceKind Device { get; private set; } = DeviceKind.KeyboardMouse;

    /// <summary>The signpost prompt for <see cref="Device"/>: RichTextLabel BBCode.</summary>
    public string Prompt => PromptRich(Device);

    /// <summary>The map's hint line for <see cref="Device"/>: RichTextLabel BBCode.</summary>
    public string MapHint => MapHintRich(Device);

    /// <summary>The signpost prompt on <paramref name="device"/>: its Interact icon, then "Map" (built once per device).</summary>
    public static string PromptRich(DeviceKind device) => s_prompts[Array.IndexOf(s_devices, device)];

    /// <summary>
    /// The map's hint line on <paramref name="device"/>, only that device's icons (built once per device):
    /// pick (left click, keyboard and mouse only), choose, travel (Confirm) and close (Back).
    /// </summary>
    public static string MapHintRich(DeviceKind device) => s_hints[Array.IndexOf(s_devices, device)];

    /// <summary>The <see cref="Destinations"/> index of Town, Forest or PetCity.</summary>
    public static int IndexOf(Area area) => area switch
    {
        Area.Town => 0,
        Area.Forest => 1,
        Area.PetCity => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, "Only Town, Forest and PetCity are destinations."),
    };

    /// <summary>The signpost a trip to <paramref name="area"/> arrives at (the Forest's south one).</summary>
    public static Place Destination(Area area) => Places.Signpost(area);

    /// <summary>Where the player is put: <see cref="ArrivalGap"/> in front of the destination's signpost, on the ground.</summary>
    public static Vector3 ArrivalPoint(Area area)
    {
        var place = Destination(area);
        var p = place.Position + place.Facing * ArrivalGap;
        return new Vector3(p.X, Ground.HeightAt(p.X, p.Z), p.Z);
    }

    /// <summary>The destination nearest (x, z): the area containing it, else the one it is least outside of.</summary>
    public static int NearestDestination(float x, float z)
    {
        int best = 0;
        float bestDepth = float.NegativeInfinity;
        for (int i = 0; i < s_destinations.Length; i++)
        {
            float depth = Areas.Bounds(s_destinations[i]).Depth(x, z);
            if (depth > bestDepth)
            {
                bestDepth = depth;
                best = i;
            }
        }
        return best;
    }

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerInput>(this);
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<ActiveDevice>(this);
        bus.Subscribe<MenuNav>(this);
    }

    public void Handle(in PlayerInput message)
    {
        _input = message;
        _haveInput = true;
    }

    public void Handle(in PlayerState message)
    {
        _player = message;
        _havePlayer = true;
    }

    public void Handle(in ActiveDevice message)
    {
        if (Array.IndexOf(s_devices, message.Kind) >= 0)
            Device = message.Kind;
    }

    /// <summary>Input's paced menu step for this tick; the only way the keys, D-pad and stick move the selection.</summary>
    public void Handle(in MenuNav message) => _nav = message;

    /// <summary>View: the mouse moved onto destination <paramref name="index"/>. Selects it at the next tick while open.</summary>
    public void Hover(int index) => _hover = index;

    /// <summary>View: destination <paramref name="index"/> was clicked. Applied at the next tick while open.</summary>
    public void Pick(int index) => _pendingPick = index;

    /// <summary>View: Enter / Space / gamepad A was pressed: picks the selected place at the next tick while open.</summary>
    public void Confirm() => _pendingConfirm = true;

    /// <summary>View: Esc / B was pressed. Applied at the next tick.</summary>
    public void Close() => _pendingClose = true;

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("TravelModule.Tick before Start.");
        Ticks++;
        float dt = (float)context.StepSeconds;
        bool outdoors = _havePlayer && _player.Area != Area.Home;
        bool alive = _havePlayer && _player.Alive;

        NearSignpost = outdoors ? FindSignpost(_player.Position) : -1;
        if (_havePlayer)
            Here = NearestDestination(_player.Position.X, _player.Position.Z);

        bool interact = _haveInput && _input.Interact;
        bool openMap = _haveInput && _input.OpenMap;
        // Right or up: toward Pet City; left or down: toward Town (at most one field is non-zero).
        int step = _nav.Horizontal - _nav.Vertical;
        bool confirm = interact || _pendingConfirm;

        switch (_phase)
        {
            case Phase.FadingOut:
                _phaseSeconds += dt;
                Fade = MathF.Min(1f, _phaseSeconds / FadeOutSeconds);
                if (_phaseSeconds >= FadeOutSeconds)
                {
                    var area = s_destinations[Traveling];
                    Open = false;
                    _bus.Publish(new TeleportPlayer(ArrivalPoint(area), Destination(area).Yaw));
                    _phase = Phase.FadingIn;
                    _phaseSeconds = 0f;
                }
                break;
            case Phase.FadingIn:
                _phaseSeconds += dt;
                Fade = MathF.Max(0f, 1f - _phaseSeconds / FadeInSeconds);
                if (_phaseSeconds >= FadeInSeconds)
                {
                    _phase = Phase.Idle;
                    Fade = 0f;
                    Traveling = -1;
                }
                break;
            default:
                if (Open)
                    TickOpen(confirm, openMap, step, outdoors);
                else if (outdoors && alive && (openMap || (interact && NearSignpost >= 0)))
                {
                    Open = true;
                    Selected = Here;
                }
                break;
        }

        ShowPrompt = NearSignpost >= 0 && !Open && _phase == Phase.Idle && alive;
        _pendingPick = -1;
        _nav = default;
        _pendingClose = false;
        _pendingConfirm = false;
        _hover = -1;
        _haveInput = false;
        if (Open != _focused)
        {
            _focused = Open;
            _bus.Publish(new UiFocus(FocusOwner, Open));
        }
        if (ShowPrompt != _offered)
        {
            _offered = ShowPrompt;
            _bus.Publish(new InteractAvailable(InteractOwner, ShowPrompt));
        }
        _bus.Publish(new MapState(Open));
    }

    private void TickOpen(bool confirm, bool openMap, int step, bool outdoors)
    {
        if (_pendingClose || openMap || !outdoors)
        {
            Open = false;
            return;
        }
        if (_hover >= 0 && _hover < s_destinations.Length)
            Selected = _hover;
        Selected = Math.Clamp(Selected + step, 0, s_destinations.Length - 1);
        int pick = _pendingPick >= 0 && _pendingPick < s_destinations.Length ? _pendingPick : confirm ? Selected : -1;
        if (pick < 0)
            return;
        Selected = pick;
        Traveling = pick;
        _phase = Phase.FadingOut;
        _phaseSeconds = 0f;
    }

    private static int FindSignpost(Vector3 position)
    {
        for (int i = 0; i < s_signposts.Length; i++)
        {
            float dx = position.X - s_signposts[i].Position.X;
            float dz = position.Z - s_signposts[i].Position.Z;
            if (dx * dx + dz * dz <= PromptRadius * PromptRadius)
                return i;
        }
        return -1;
    }

    private static string[] PerDevice(Func<DeviceKind, string> build)
    {
        var texts = new string[s_devices.Length];
        for (int i = 0; i < texts.Length; i++)
            texts[i] = build(s_devices[i]);
        return texts;
    }

    private static string BuildPrompt(DeviceKind device) =>
        Prompts.Rich(PromptAction.Interact, device, PromptIconSize) + " Map";

    private static string BuildHint(DeviceKind device)
    {
        const string gap = "      ";
        int size = HintIconSize;
        string choose = Prompts.Rich(PromptAction.MenuLeft, device, size) + Prompts.Rich(PromptAction.MenuRight, device, size);
        string pick = "";
        if (device == DeviceKind.KeyboardMouse)
            // Fire is the left click on keyboard and mouse: clicking a place picks it.
            pick = Prompts.Rich(PromptAction.Fire, device, size) + " Pick a place" + gap;
        else
            choose += " " + Prompts.Rich(PromptAction.Move, device, size);
        return pick + choose + " Choose" + gap
            + Prompts.Rich(PromptAction.Confirm, device, size) + " Travel" + gap
            + Prompts.Rich(PromptAction.Back, device, size) + " Close";
    }
}
