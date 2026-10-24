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
/// <see cref="FocusOwner"/>) when the map opens and when it closes, so Input stops walking meanwhile.
/// </summary>
public sealed class TravelModule : IModule, IMessageHandler<PlayerInput>, IMessageHandler<PlayerState>
{
    /// <summary>How near a signpost (horizontal meters) its prompt shows and Interact opens the map.</summary>
    public const float PromptRadius = 2.5f;

    public const string PromptText = "E / X: Map";

    /// <summary>The fade to black after a pick, while the map still shows.</summary>
    public const float FadeOutSeconds = 0.3f;

    /// <summary>The fade back in after the teleport.</summary>
    public const float FadeInSeconds = 0.4f;

    /// <summary>How far in front of the signpost (along its facing) the player arrives.</summary>
    public const float ArrivalGap = 1.5f;

    /// <summary>Stick deflection that steps the map's selection; it must come back under half of it first.</summary>
    public const float StickStep = 0.5f;

    /// <summary>The <see cref="UiFocus.Owner"/> of the travel map.</summary>
    public const int FocusOwner = 0x7472_6176; // "trav"

    private static readonly Area[] s_destinations = { Area.Town, Area.Forest, Area.PetCity };

    private static readonly Place[] s_signposts =
    {
        Places.SignpostTown, Places.SignpostForestSouth, Places.SignpostForestNorth, Places.SignpostPetCity,
    };

    private static readonly Area[] s_signpostAreas = { Area.Town, Area.Forest, Area.Forest, Area.PetCity };

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
    private bool _stickCentered = true;
    private Phase _phase;
    private float _phaseSeconds;
    private int _pendingPick = -1;
    private int _pendingSteps;
    private bool _pendingClose;
    private int _hover = -1;
    private bool _pendingConfirm;
    private Vector2 _navigate;
    private bool _focused;

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

    /// <summary>Whether the "E / X: Map" prompt shows (near a signpost, map closed, outdoors).</summary>
    public bool ShowPrompt { get; private set; }

    /// <summary>Black over the screen, 0..1.</summary>
    public float Fade { get; private set; }

    /// <summary>The <see cref="Destinations"/> index being traveled to during a fade, or -1.</summary>
    public int Traveling { get; private set; } = -1;

    public long Ticks { get; private set; }

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

    /// <summary>View: the mouse moved onto destination <paramref name="index"/>. Selects it at the next tick while open.</summary>
    public void Hover(int index) => _hover = index;

    /// <summary>View: destination <paramref name="index"/> was clicked. Applied at the next tick while open.</summary>
    public void Pick(int index) => _pendingPick = index;

    /// <summary>View: arrow keys moved the selection by <paramref name="steps"/> (+ toward Pet City).</summary>
    public void Step(int steps) => _pendingSteps += steps;

    /// <summary>View: Enter / Space / gamepad A was pressed: picks the selected place at the next tick while open.</summary>
    public void Confirm() => _pendingConfirm = true;

    /// <summary>
    /// View: the held direction of the left stick (x right, y up, length 0..1). Kept until the next
    /// call; one push past <see cref="StickStep"/> steps the selection once.
    /// </summary>
    public void Navigate(Vector2 direction) => _navigate = direction;

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
        int stick = StickSteps();
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
                    TickOpen(confirm, openMap, stick, outdoors);
                else if (outdoors && alive && (openMap || (interact && NearSignpost >= 0)))
                {
                    Open = true;
                    Selected = Here;
                }
                break;
        }

        ShowPrompt = NearSignpost >= 0 && !Open && _phase == Phase.Idle && alive;
        _pendingPick = -1;
        _pendingSteps = 0;
        _pendingClose = false;
        _pendingConfirm = false;
        _hover = -1;
        _haveInput = false;
        if (Open != _focused)
        {
            _focused = Open;
            _bus.Publish(new UiFocus(FocusOwner, Open));
        }
        _bus.Publish(new MapState(Open));
    }

    private void TickOpen(bool confirm, bool openMap, int stick, bool outdoors)
    {
        if (_pendingClose || openMap || !outdoors)
        {
            Open = false;
            return;
        }
        if (_hover >= 0 && _hover < s_destinations.Length)
            Selected = _hover;
        Selected = Math.Clamp(Selected + _pendingSteps + stick, 0, s_destinations.Length - 1);
        int pick = _pendingPick >= 0 && _pendingPick < s_destinations.Length ? _pendingPick : confirm ? Selected : -1;
        if (pick < 0)
            return;
        Selected = pick;
        Traveling = pick;
        _phase = Phase.FadingOut;
        _phaseSeconds = 0f;
    }

    /// <summary>One step per stick push (right or up: +1, left or down: -1), after it came back near the center.</summary>
    private int StickSteps()
    {
        // Input zeroes PlayerInput.Move while the map holds UiFocus, so the stick comes from the view.
        var move = _navigate;
        float main = MathF.Abs(move.X) >= MathF.Abs(move.Y) ? move.X : move.Y;
        if (MathF.Abs(main) < StickStep * 0.5f)
        {
            _stickCentered = true;
            return 0;
        }
        if (!_stickCentered || MathF.Abs(main) < StickStep)
            return 0;
        _stickCentered = false;
        return main > 0f ? 1 : -1;
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
}
