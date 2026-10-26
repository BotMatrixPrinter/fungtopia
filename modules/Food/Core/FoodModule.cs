using System;
using System.Numerics;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Fungtopia.Wallet;

namespace Fungtopia.Food;

/// <summary>How the latest purchase at the stall went.</summary>
public enum BuyResult
{
    None,
    Bought,
    TooFewCoins,
    Full,
}

/// <summary>
/// Food that heals: the food bag (up to <see cref="FoodCatalog.MaxEach"/> of each kind), the
/// Mushroom Munchies stall's menu (opened by <see cref="OpenFoodMenu"/>, or until one has ever
/// arrived by Interact at <see cref="Places.FoodSpot"/>; purchases through <see cref="SpendCoins"/>),
/// the selection (<see cref="PlayerInput.FoodSelect"/> / <see cref="PlayerInput.FoodCycle"/>) and eating
/// (<see cref="PlayerInput.Eat"/>: <see cref="EatSeconds"/> standing still, then <see cref="HealPlayer"/>
/// and <see cref="FoodEaten"/>). Publishes <see cref="FoodBag"/> every tick, and <see cref="UiFocus"/>
/// while the menu is open or the player is eating. The bag and selection are saved through the Save
/// module (<see cref="FoodSave"/>).
/// </summary>
public sealed class FoodModule : IModule, IMessageHandler<PlayerState>, IMessageHandler<PlayerInput>,
    IMessageHandler<SpendResult>, IMessageHandler<WalletState>, IMessageHandler<OpenFoodMenu>,
    IMessageHandler<StartScreenState>, IMessageHandler<CollectSave>, IMessageHandler<RestorePart>,
    IMessageHandler<ActiveDevice>, IMessageHandler<MenuNav>
{
    /// <summary>The <see cref="SavePart.Module"/> name of the Food module's part.</summary>
    public const string SaveName = "Food";

    /// <summary>How long a meal takes, seconds.</summary>
    public const float EatSeconds = 1f;

    /// <summary>How near (meters, horizontally) the player must be to <see cref="Places.FoodSpot"/> to open the menu with Interact.</summary>
    public const float StallReach = 1.5f;

    /// <summary>The first <see cref="SpendCoins.RequestId"/> used (this module's own range).</summary>
    public const int FirstRequestId = 8240000;

    /// <summary>The <see cref="UiFocus.Owner"/> of the stall's menu.</summary>
    public const int MenuFocusOwner = 8241001;

    /// <summary>The <see cref="UiFocus.Owner"/> held while eating (the player stops to eat).</summary>
    public const int EatFocusOwner = 8241002;

    /// <summary>The <see cref="InteractAvailable.Owner"/> published while <see cref="StallPrompt"/> shows (the gamepad's A opens the menu then).</summary>
    public const int InteractOwner = 8241003;

    private readonly int[] _counts = new int[FoodKinds.Count];
    private readonly int[] _restored = new int[FoodKinds.Count];
    private IMessageBus? _bus;
    private PlayerState _player;
    private bool _hasPlayer;
    private bool _interact, _eat;
    private int _select = -1, _cycle;
    private int _nextRequest = FirstRequestId;
    private int _pendingRequest = -1;
    private int _pendingKind;
    private float _eatTime;
    private bool _menuFocus, _eatFocus, _offered;

    /// <summary>How many of <paramref name="kind"/> are in the bag (0..9).</summary>
    public int Count(int kind) => FoodCatalog.Valid(kind) ? _counts[kind] : 0;

    /// <summary>The food Eat eats (<see cref="FoodKinds"/>).</summary>
    public int Selected { get; private set; }

    /// <summary>Nothing in the bag (the food bar hides).</summary>
    public bool BagEmpty => _counts[0] + _counts[1] + _counts[2] == 0;

    /// <summary>A meal is under way.</summary>
    public bool Eating { get; private set; }

    /// <summary>What is being eaten (valid while <see cref="Eating"/>).</summary>
    public int EatingKind { get; private set; }

    /// <summary>How far the meal is, 0..1.</summary>
    public float EatProgress => Eating ? Math.Clamp(_eatTime / EatSeconds, 0f, 1f) : 0f;

    /// <summary>The latest meal (for the view's "+3 ♥" and crumbs): its kind, health healed, and a count of meals so far.</summary>
    public int LastEatenKind { get; private set; }
    public int LastHealed { get; private set; }
    public int Meals { get; private set; }

    /// <summary>The stall's menu is open.</summary>
    public bool MenuOpen { get; private set; }

    /// <summary>The highlighted line of the menu (<see cref="FoodKinds"/>).</summary>
    public int MenuSelection { get; private set; }

    public BuyResult LastBuyResult { get; private set; }

    public int LastBuyKind { get; private set; }

    /// <summary>Counts purchase answers, so the view can tell a new one from the last.</summary>
    public int BuyResults { get; private set; }

    /// <summary>An <see cref="OpenFoodMenu"/> has arrived (the stall's cook is there): Interact at the stall no longer opens the menu here.</summary>
    public bool CookPresent { get; private set; }

    /// <summary>The "E / X: Food" prompt shows: near the stall, alive, nothing open, and no cook yet.</summary>
    public bool StallPrompt { get; private set; }

    /// <summary>Mushroom Coins in the wallet (the latest <see cref="WalletState"/>), for the menu.</summary>
    public int Coins { get; private set; }

    /// <summary>The device used last (the latest <see cref="ActiveDevice"/>): the view shows its buttons only.</summary>
    public DeviceKind Device { get; private set; }

    public bool StartScreenOpen { get; private set; }

    /// <summary>The latest player position (feet) and facing, for the view's eating effect.</summary>
    public Vector3 PlayerPosition => _player.Position;
    public float PlayerFacing => _player.Facing;

    public long Ticks { get; private set; }

    /// <summary>Where a refused restore is reported (one line per refused <see cref="RestorePart"/>); the console by default.</summary>
    public Action<string> Log { get; set; } = Console.WriteLine;

    private bool Alive => _hasPlayer && _player.Alive;

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<PlayerInput>(this);
        bus.Subscribe<SpendResult>(this);
        bus.Subscribe<WalletState>(this);
        bus.Subscribe<OpenFoodMenu>(this);
        bus.Subscribe<StartScreenState>(this);
        bus.Subscribe<CollectSave>(this);
        bus.Subscribe<RestorePart>(this);
        bus.Subscribe<ActiveDevice>(this);
        bus.Subscribe<MenuNav>(this);
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("FoodModule.Tick before Start.");
        Ticks++;
        if (MenuOpen && (!Alive || StartScreenOpen))
            MenuOpen = false;
        if (StartScreenOpen)
            _interact = _eat = false;

        if (_interact)
        {
            _interact = false;
            if (MenuOpen)
                Buy();
            else if (StallPromptNow())
                OpenMenu();
        }
        StallPrompt = StallPromptNow();

        if (!Eating && !MenuOpen)
        {
            if (_select >= 0)
                Select(_select);
            if (_cycle != 0)
                Cycle(_cycle);
        }
        _select = -1;
        _cycle = 0;
        if (_eat)
        {
            _eat = false;
            TryEat();
        }
        if (Eating)
            TickEating((float)context.StepSeconds);

        PublishFocus();
        if (StallPrompt != _offered)
        {
            _offered = StallPrompt;
            _bus.Publish(new InteractAvailable(InteractOwner, StallPrompt));
        }
        _bus.Publish(new FoodBag(_counts[0], _counts[1], _counts[2], Selected, Eating, EatProgress));
    }

    private bool StallPromptNow() =>
        !CookPresent && !MenuOpen && !Eating && !StartScreenOpen && Alive
        && Distance(_player.Position, Places.FoodSpot.Position) <= StallReach;

    public void Handle(in PlayerState message)
    {
        _player = message;
        _hasPlayer = true;
    }

    public void Handle(in PlayerInput message)
    {
        // Acted on at the next tick (presses between ticks are not lost).
        if (message.Interact)
            _interact = true;
        if (message.Eat)
            _eat = true;
        if (message.FoodSelect >= 0)
            _select = message.FoodSelect;
        if (message.FoodCycle != 0)
            _cycle = message.FoodCycle;
    }

    public void Handle(in WalletState message) => Coins = message.Coins;

    public void Handle(in ActiveDevice message) => Device = message.Kind;

    /// <summary>The Input module's paced menu step: Up / Down move the open menu's highlight one row (the Input module does the repeating).</summary>
    public void Handle(in MenuNav message)
    {
        if (MenuOpen && message.Vertical != 0)
            MoveMenuSelection(message.Vertical);
    }

    public void Handle(in StartScreenState message) => StartScreenOpen = message.Open;

    /// <summary>Pip asks for the menu: it opens (not while defeated or on the start screen), and the stall stops opening it on Interact.</summary>
    public void Handle(in OpenFoodMenu message)
    {
        CookPresent = true;
        // The press that talked to Pip is not a purchase.
        _interact = false;
        if (Alive && !StartScreenOpen)
            OpenMenu();
    }

    public void Handle(in SpendResult message)
    {
        if (message.RequestId != _pendingRequest)
            return;
        _pendingRequest = -1;
        Coins = message.CoinsLeft;
        if (message.Ok)
        {
            // Room was checked when asking.
            _counts[_pendingKind] = Math.Min(_counts[_pendingKind] + 1, FoodCatalog.MaxEach);
            Answer(BuyResult.Bought, _pendingKind);
        }
        else
        {
            Answer(BuyResult.TooFewCoins, _pendingKind);
        }
    }

    public void Handle(in CollectSave message) =>
        _bus!.Publish(new SavePart(message.RequestId, SaveName, FoodSave.Write(_counts[0], _counts[1], _counts[2], Selected)));

    /// <summary>
    /// Puts back the saved bag and selection, and nothing else happens (no purchase, no meal, no
    /// healing): the menu closes, a meal under way stops and a purchase still waiting for its answer
    /// is dropped. Missing or unreadable JSON keeps the bag as it is and logs one line.
    /// </summary>
    public void Handle(in RestorePart message)
    {
        if (message.Module != SaveName)
            return;
        if (!FoodSave.TryRead(message.Json, _restored, out int selected, out string error))
        {
            Log("Fungtopia: Food: saved food not restored, keeping it as it is: " + error);
            return;
        }
        Array.Copy(_restored, _counts, FoodKinds.Count);
        Selected = selected;
        MenuOpen = false;
        Eating = false;
        _eatTime = 0f;
        _pendingRequest = -1;
        _interact = _eat = false;
        _select = -1;
        _cycle = 0;
    }

    // Menu

    /// <summary>Opens the stall's menu; <see cref="UiFocus"/> follows at the tick.</summary>
    public void OpenMenu()
    {
        MenuOpen = true;
        LastBuyResult = BuyResult.None;
    }

    /// <summary>Closes the menu (the view's Back button, Q / B).</summary>
    public void Back()
    {
        MenuOpen = false;
    }

    /// <summary>The view's confirm button (Enter; the gamepad's A arrives as Interact): buys at the next tick like Interact, only while the menu is open.</summary>
    public void Confirm()
    {
        if (MenuOpen)
            _interact = true;
    }

    /// <summary>Moves the menu's highlight up (-1) or down (+1), wrapping around.</summary>
    public void MoveMenuSelection(int step)
    {
        int n = FoodKinds.Count;
        MenuSelection = ((MenuSelection + Math.Sign(step)) % n + n) % n;
    }

    /// <summary>Buys the highlighted food.</summary>
    public void Buy() => Buy(MenuSelection);

    /// <summary>
    /// Asks the wallet for <paramref name="kind"/>'s price; when it takes the coins one goes into the
    /// bag (see <see cref="LastBuyResult"/>). Refused without asking when 9 are carried already or
    /// another purchase is still waiting for its answer.
    /// </summary>
    public void Buy(int kind)
    {
        if (_bus == null || _pendingRequest != -1 || !FoodCatalog.Valid(kind))
            return;
        if (_counts[kind] >= FoodCatalog.MaxEach)
        {
            Answer(BuyResult.Full, kind);
            return;
        }
        _pendingRequest = _nextRequest++;
        _pendingKind = kind;
        _bus.Publish(new SpendCoins(_pendingRequest, FoodCatalog.Price(kind), FoodCatalog.Name(kind)));
    }

    private void Answer(BuyResult result, int kind)
    {
        LastBuyResult = result;
        LastBuyKind = kind;
        BuyResults++;
    }

    // Selection and eating

    /// <summary>Selects <paramref name="kind"/> (0..2), carried or not.</summary>
    public void Select(int kind)
    {
        if (FoodCatalog.Valid(kind))
            Selected = kind;
    }

    /// <summary>Selects the next (+1) or previous (-1) kind that is carried, wrapping around; unchanged when no other is.</summary>
    public void Cycle(int step)
    {
        step = Math.Sign(step);
        if (step == 0)
            return;
        int n = FoodKinds.Count;
        for (int k = 1; k < n; k++)
        {
            int kind = ((Selected + step * k) % n + n) % n;
            if (_counts[kind] > 0)
            {
                Selected = kind;
                return;
            }
        }
    }

    /// <summary>Whether Eat would start a meal now: the selected food is carried, the player alive and below full health, nothing open.</summary>
    public bool CanEat =>
        !Eating && !MenuOpen && !StartScreenOpen && _counts[Selected] > 0 && Alive && !_player.FullHealth;

    /// <summary>Starts eating the selected food if <see cref="CanEat"/>; true when it did.</summary>
    public bool TryEat()
    {
        if (!CanEat)
            return false;
        Eating = true;
        EatingKind = Selected;
        _eatTime = 0f;
        return true;
    }

    private void TickEating(float dt)
    {
        // Fainting (or healing up some other way) stops the meal; the food stays in the bag.
        if (!Alive || _player.FullHealth || StartScreenOpen || _counts[EatingKind] <= 0)
        {
            Eating = false;
            _eatTime = 0f;
            return;
        }
        _eatTime += dt;
        // A small tolerance so 60 steps of 1/60 s make exactly one second.
        if (_eatTime < EatSeconds - 1e-4f)
            return;
        int kind = EatingKind;
        int healed = Math.Min(FoodCatalog.HealAmount(kind), Math.Max(0, _player.MaxHealth - _player.Health));
        _counts[kind]--;
        Eating = false;
        _eatTime = 0f;
        LastEatenKind = kind;
        LastHealed = healed;
        Meals++;
        _bus!.Publish(new HealPlayer(FoodCatalog.HealAmount(kind)));
        _bus.Publish(new FoodEaten(kind, healed));
        // Until the player's next state, count the healing as done (no second meal on old health).
        _player = new PlayerState(_player.Position, _player.Facing, _player.Velocity, _player.Grounded,
            Math.Min(_player.MaxHealth, _player.Health + healed), _player.MaxHealth, _player.Alive, _player.Character, _player.Area);
    }

    /// <summary>
    /// Publishes <see cref="UiFocus"/> when the menu or a meal started or ended since the last tick,
    /// so Input stops walking, looking, jumping and firing while the menu is open or the player eats.
    /// </summary>
    private void PublishFocus()
    {
        if (MenuOpen != _menuFocus)
        {
            _menuFocus = MenuOpen;
            _bus!.Publish(new UiFocus(MenuFocusOwner, _menuFocus));
        }
        if (Eating != _eatFocus)
        {
            _eatFocus = Eating;
            _bus!.Publish(new UiFocus(EatFocusOwner, _eatFocus));
        }
    }

    private static float Distance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
