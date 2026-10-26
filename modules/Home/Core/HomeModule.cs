using System;
using System.Numerics;
using Fungtopia.DayNight;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Pet;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.StartScreen;
using Fungtopia.Wallet;
using Fungtopia.Wardrobe;

namespace Fungtopia.Home;

/// <summary>What pressing Interact would do where the player stands.</summary>
public enum HomePrompt
{
    None,
    Enter,
    Leave,
    Shop,
    Decorate,
    Wardrobe,

    /// <summary>Near the Mushroom Cat at home: Interact publishes <see cref="SetActivePet"/> for it.</summary>
    TakeCat,

    /// <summary>Near the Mushroom Dog at home.</summary>
    TakeDog,

    /// <summary>Near the Mushroom Crow at home.</summary>
    TakeCrow,

    /// <summary>Near a placed Basic Bed or Mushroom Bed: Interact publishes <see cref="DayNight.Sleep"/> and a full <see cref="HealPlayer"/>.</summary>
    Sleep,
}

/// <summary>A line of decorating's picker: a kind of furniture owned and not placed, and how many of it.</summary>
public struct PickerEntry
{
    public FurnitureKind Kind;
    public int Count;
}

/// <summary>How the latest purchase went.</summary>
public enum ShopResult
{
    None,
    Bought,
    TooFewCoins,
    Full,
}

/// <summary>A piece of furniture the player owns: in the inventory, or placed on the floor grid.</summary>
public struct FurnitureItem
{
    public FurnitureKind Kind;
    public bool Placed;

    /// <summary>The north-west cell of its footprint (see <see cref="HomeRoom"/>), while placed.</summary>
    public int Column;
    public int Row;

    /// <summary>Quarter turns (0..3) about +Y; odd turns swap its footprint's width and depth.</summary>
    public int Rotation;

    public readonly int Width => Furniture.Width(Kind, Rotation);
    public readonly int Depth => Furniture.Depth(Kind, Rotation);

    /// <summary>The middle of its footprint on the floor.</summary>
    public readonly Vector3 Center => HomeRoom.CellsCenter(Column, Row, Width, Depth);

    /// <summary>Its yaw (radians about +Y; 0: its front faces +Z, into the room from the north wall).</summary>
    public readonly float Yaw => Rotation * MathF.PI * 0.5f;

    public readonly bool Covers(int column, int row) =>
        column >= Column && column < Column + Width && row >= Row && row < Row + Depth;
}

/// <summary>What a pet at home is doing.</summary>
public enum PetActivity
{
    Wander,
    Nap,
    Play,
}

/// <summary>An owned pet that is not out with the player: it wanders, naps and plays in the house.</summary>
public struct HomePet
{
    public PetKind Kind;

    /// <summary>It is at home (owned and not active); otherwise the rest is stale.</summary>
    public bool Present;

    /// <summary>Feet on the home floor.</summary>
    public Vector3 Position;

    /// <summary>Yaw (Godot's convention: 0 faces -Z).</summary>
    public float Facing;

    /// <summary>Meters per second this tick.</summary>
    public float Speed;

    public PetActivity Activity;

    /// <summary>Napping and arrived: asleep (on a pet bed when <see cref="BedItem"/> is not -1).</summary>
    public bool Asleep;

    /// <summary>The placed pet bed (index into <see cref="HomeModule.Items"/>) it naps on, or -1.</summary>
    public int BedItem;

    /// <summary>Asleep on a pet bed.</summary>
    public readonly bool OnBed => Asleep && BedItem >= 0;

    internal Vector3 Target;
    internal float Timer;
}

/// <summary>
/// The player's home: the house door (enter and leave by teleporting), the furniture shop in
/// town (bought with <see cref="SpendCoins"/>), placing furniture on the home's floor grid, and
/// the pets that stay at home, and the wardrobe that comes with the house (Interact publishes
/// <see cref="OpenWardrobe"/>; nothing else is usable while a wardrobe screen is open, and
/// nothing at all while the start screen is open), and sleeping in a placed bed (<see cref="DayNight.Sleep"/>). Publishes <see cref="HomeState"/> every tick. Its furniture
/// (owned and placed) is saved through the Save module: <see cref="CollectSave"/> is answered with a
/// <see cref="SavePart"/> for <see cref="SaveModules.Home"/> and <see cref="RestorePart"/> restores it (<see cref="HomeSave"/>).
/// </summary>
public sealed class HomeModule : IModule, IMessageHandler<PlayerState>, IMessageHandler<PlayerInput>,
    IMessageHandler<SpendResult>, IMessageHandler<WalletState>, IMessageHandler<OwnedPets>, IMessageHandler<WardrobeState>,
    IMessageHandler<StartScreenState>, IMessageHandler<CollectSave>, IMessageHandler<RestorePart>,
    IMessageHandler<ActiveDevice>, IMessageHandler<MenuNav>
{
    /// <summary>How many pieces of furniture the player can own.</summary>
    public const int MaxItems = 64;

    /// <summary>How near (meters, horizontally) the player must be to use the door outside or the shop.</summary>
    public const float OutdoorReach = 1.5f;

    /// <summary>How near the player must be to the door inside or the catalog book.</summary>
    public const float IndoorReach = 1.2f;

    /// <summary>The shop's panel closes once the player is this far from the shop.</summary>
    public const float ShopLeaveDistance = 3f;

    public const float PetWalkSpeed = 0.9f;
    public const float PetPlaySpeed = 1.8f;

    /// <summary>The first <see cref="SpendCoins.RequestId"/> used (this module's own range).</summary>
    public const int FirstRequestId = 7460000;

    /// <summary>The <see cref="UiFocus.Owner"/> of the furniture shop's panel.</summary>
    public const int ShopFocusOwner = 7461001;

    /// <summary>The <see cref="UiFocus.Owner"/> of placement mode (the decorating catalog and grid).</summary>
    public const int DecorateFocusOwner = 7461002;

    /// <summary>The <see cref="InteractAvailable.Owner"/> published while a prompt shows (door, shop, decorate, wardrobe, a pet at home, a bed), so the gamepad's A uses it instead of jumping.</summary>
    public const int InteractOwner = 7461003;

    private static readonly Vector3[] PetSpawns =
    {
        new(598f, Areas.HomeFloorY, 599f), new(602f, Areas.HomeFloorY, 599f), new(600f, Areas.HomeFloorY, 597.5f),
    };

    private readonly FurnitureItem[] _items = new FurnitureItem[MaxItems];
    private readonly HomePet[] _pets = new HomePet[3];
    private readonly bool[] _petBlocked;
    private IMessageBus? _bus;
    private PlayerState _player;
    private bool _hasPlayer;
    private bool _interact;
    private int _nextRequest = FirstRequestId;
    private int _pendingRequest = -1;
    private FurnitureKind _pendingKind;
    private uint _random = 0x9E3779B9u;
    private bool _shopFocus, _decorateFocus, _offered;
    private readonly FurnitureItem[] _restored = new FurnitureItem[MaxItems];
    private readonly PickerEntry[] _picker = new PickerEntry[Furniture.KindCount];
    private bool _heldFromPicker;

    public HomeModule()
    {
        _petBlocked = new bool[HomeRoom.Columns * HomeRoom.Rows];
        for (int i = 0; i < _pets.Length; i++)
            _pets[i] = new HomePet { Kind = (PetKind)i, Position = PetSpawns[i], BedItem = -1 };
        // The house starts cozy but sparse: a basic bed against the north wall, by the window.
        _items[0] = new FurnitureItem { Kind = FurnitureKind.BasicBed, Placed = true, Column = 2, Row = 0 };
        ItemCount = 1;
        RebuildPetGrid();
    }

    /// <summary>Furniture owned (placed or not); the first <see cref="ItemCount"/> of <see cref="Items"/>.</summary>
    public int ItemCount { get; private set; }

    public ReadOnlySpan<FurnitureItem> Items => _items.AsSpan(0, ItemCount);

    public int ItemsPlaced
    {
        get
        {
            int n = 0;
            for (int i = 0; i < ItemCount; i++)
                if (_items[i].Placed)
                    n++;
            return n;
        }
    }

    /// <summary>Cat, Dog and Crow at home (indexed by <see cref="PetKind"/>); only <see cref="HomePet.Present"/> ones are there.</summary>
    public ReadOnlySpan<HomePet> Pets => _pets;

    /// <summary>What Interact would do now (nothing while the shop is open, while decorating, or while a wardrobe screen or the start screen is open).</summary>
    public HomePrompt Prompt { get; private set; }

    /// <summary>The player is in the house (the latest <see cref="PlayerState.Area"/> is Home).</summary>
    public bool Inside => _hasPlayer && _player.Area == Area.Home;

    /// <summary>Mushroom Coins in the wallet (the latest <see cref="WalletState"/>), for the shop panel.</summary>
    public int Coins { get; private set; }

    public long Ticks { get; private set; }

    /// <summary>The device the player used last (the latest <see cref="ActiveDevice"/>; keyboard and mouse at start): the panels show its buttons only.</summary>
    public DeviceKind Device { get; private set; }

    /// <summary>A Wardrobe screen is open (the latest <see cref="WardrobeState.Open"/>): no decorating, leaving or other Interact.</summary>
    public bool WardrobeOpen { get; private set; }

    /// <summary>The start screen is showing (the latest <see cref="StartScreenState.Open"/>): no prompt, no shop, no decorating, Interact ignored.</summary>
    public bool StartScreenOpen { get; private set; }

    // Shop

    public bool ShopOpen { get; private set; }

    /// <summary>The highlighted line of the shop's catalog (index into <see cref="Furniture.ForSale"/>).</summary>
    public int ShopSelection { get; private set; }

    public ShopResult LastShopResult { get; private set; }

    public FurnitureKind LastShopKind { get; private set; }

    /// <summary>Counts purchase answers, so the view can tell a new one from the last.</summary>
    public int ShopResults { get; private set; }

    // Decorating

    /// <summary>Decorating is open: the picker (<see cref="Picking"/>) or placement mode.</summary>
    public bool Decorating { get; private set; }

    /// <summary>Decorating's picker is open (choosing what to place); otherwise, while <see cref="Decorating"/>, placement mode is.</summary>
    public bool Picking { get; private set; }

    /// <summary>The picker's lines: each kind owned and not placed, with how many, in <see cref="FurnitureKind"/> order.</summary>
    public ReadOnlySpan<PickerEntry> PickerEntries => _picker.AsSpan(0, PickerCount);

    /// <summary>How many kinds the picker lists (its last line, "Move placed items", comes after them).</summary>
    public int PickerCount { get; private set; }

    /// <summary>The picker's highlighted line: an index into <see cref="PickerEntries"/>, or <see cref="PickerCount"/> for "Move placed items".</summary>
    public int PickerSelection { get; private set; }

    /// <summary>The item being moved (index into <see cref="Items"/>, not placed), or -1 when only the cursor moves.</summary>
    public int Held { get; private set; } = -1;

    /// <summary>In placement mode with nothing held: the placed item under the cursor (Interact picks it up, <see cref="PutAway"/> puts it away), else -1.</summary>
    public int Highlighted => Decorating && !Picking && Held < 0 ? ItemAt(CursorColumn, CursorRow) : -1;

    /// <summary>The cursor's north-west cell (the held item's footprint starts there).</summary>
    public int CursorColumn { get; private set; }
    public int CursorRow { get; private set; }

    /// <summary>The held item's quarter turns.</summary>
    public int CursorRotation { get; private set; }

    /// <summary>Whether the held item fits where the cursor is.</summary>
    /// <summary>Where a refused restore is reported (one line per refused <see cref="RestorePart"/>); the console by default.</summary>
    public Action<string> Log { get; set; } = Console.WriteLine;

    public bool CanPlaceHeld => Held >= 0 && CanPlace(_items[Held].Kind, CursorColumn, CursorRow, CursorRotation);

    public void Start(IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(bus);
        _bus = bus;
        bus.Subscribe<PlayerState>(this);
        bus.Subscribe<PlayerInput>(this);
        bus.Subscribe<SpendResult>(this);
        bus.Subscribe<WalletState>(this);
        bus.Subscribe<OwnedPets>(this);
        bus.Subscribe<WardrobeState>(this);
        bus.Subscribe<StartScreenState>(this);
        bus.Subscribe<CollectSave>(this);
        bus.Subscribe<RestorePart>(this);
        bus.Subscribe<ActiveDevice>(this);
        bus.Subscribe<MenuNav>(this);
    }

    public void Tick(in TickContext context)
    {
        if (_bus == null)
            throw new InvalidOperationException("HomeModule.Tick before Start.");
        Ticks++;
        bool alive = _hasPlayer && _player.Alive;
        bool screenOpen = WardrobeOpen || StartScreenOpen;
        if (ShopOpen && (!alive || StartScreenOpen || Distance(_player.Position, Places.FurnitureShopSpot.Position) > ShopLeaveDistance))
            ShopOpen = false;
        if (Decorating && (!alive || _player.Area != Area.Home || screenOpen))
            StopDecorating();
        Prompt = ShopOpen || Decorating || screenOpen || !alive ? HomePrompt.None : PromptAt(_player.Position);

        // A press made while a wardrobe screen or the start screen is open belongs to that screen.
        if (screenOpen)
            _interact = false;
        if (_interact)
        {
            _interact = false;
            if (ShopOpen)
                Buy();
            else if (Decorating && Picking)
                Choose();
            else if (Decorating)
                PlaceOrPickUp();
            else
                Use(Prompt);
            if (ShopOpen || Decorating)
                Prompt = HomePrompt.None;
        }

        PublishFocus();
        if ((Prompt != HomePrompt.None) != _offered)
        {
            _offered = Prompt != HomePrompt.None;
            _bus.Publish(new InteractAvailable(InteractOwner, _offered));
        }
        TickPets((float)context.StepSeconds);
        _bus.Publish(new HomeState(ItemCount, ItemsPlaced, Decorating));
    }

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
    }

    public void Handle(in WalletState message) => Coins = message.Coins;

    public void Handle(in ActiveDevice message) => Device = message.Kind;

    public void Handle(in WardrobeState message) => WardrobeOpen = message.Open;

    public void Handle(in StartScreenState message) => StartScreenOpen = message.Open;

    public void Handle(in SpendResult message)
    {
        if (message.RequestId != _pendingRequest)
            return;
        _pendingRequest = -1;
        Coins = message.CoinsLeft;
        if (message.Ok)
        {
            // Room was checked when asking; the item lands in the inventory, not yet placed.
            _items[ItemCount++] = new FurnitureItem { Kind = _pendingKind };
            Answer(ShopResult.Bought, _pendingKind);
        }
        else
        {
            Answer(ShopResult.TooFewCoins, _pendingKind);
        }
    }

    public void Handle(in CollectSave message) =>
        _bus!.Publish(new SavePart(message.RequestId, SaveModules.Home, HomeSave.Write(Items)));

    /// <summary>
    /// Restores the furniture saved by <see cref="CollectSave"/>, exactly, and nothing else happens:
    /// no purchase, no shop result, no coins. The shop and placement mode close and a purchase still
    /// waiting for its answer is dropped. Missing, unreadable or unknown-version JSON, or placed
    /// furniture that does not fit the room, keeps the furniture as it is and logs one line.
    /// </summary>
    public void Handle(in RestorePart message)
    {
        if (message.Module != SaveModules.Home)
            return;
        if (!HomeSave.TryRead(message.Json, _restored, out int count, out string error) || !Fits(_restored, count, out error))
        {
            Log("Fungtopia: Home: saved furniture not restored, keeping it as it is: " + error);
            return;
        }
        Array.Clear(_items);
        Array.Copy(_restored, _items, count);
        ItemCount = count;
        ShopOpen = false;
        StopDecorating();
        _pendingRequest = -1;
        _interact = false;
        for (int i = 0; i < _pets.Length; i++)
        {
            // Bed indexes refer to the old furniture: each pet picks again.
            _pets[i].BedItem = -1;
            _pets[i].Asleep = false;
            _pets[i].Timer = 0f;
        }
        RebuildPetGrid();
    }

    /// <summary>Whether every placed item of <paramref name="items"/> fits the room without overlapping another.</summary>
    private static bool Fits(FurnitureItem[] items, int count, out string error)
    {
        for (int i = 0; i < count; i++)
        {
            if (!items[i].Placed)
                continue;
            for (int c = items[i].Column; c < items[i].Column + items[i].Width; c++)
                for (int r = items[i].Row; r < items[i].Row + items[i].Depth; r++)
                {
                    bool overlap = false;
                    for (int j = 0; j < i; j++)
                        overlap |= items[j].Placed && items[j].Covers(c, r);
                    if (!HomeRoom.Inside(c, r) || HomeRoom.KeptClear(c, r) || overlap)
                    {
                        error = "item " + i + " does not fit at " + items[i].Column + ", " + items[i].Row;
                        return false;
                    }
                }
        }
        error = "";
        return true;
    }

    public void Handle(in OwnedPets message)
    {
        for (int i = 0; i < _pets.Length; i++)
        {
            ref var pet = ref _pets[i];
            bool home = message.AtHome(pet.Kind);
            if (home && !pet.Present)
            {
                // Sent home while the player is in the house (swapped for a pet at home): it
                // appears near the player and starts wandering; otherwise at its own spot.
                bool nearPlayer = Inside && _player.Alive;
                pet.Present = true;
                pet.Position = FreePetSpot(nearPlayer ? NearPlayer() : PetSpawns[i]);
                pet.Target = pet.Position;
                pet.Speed = 0f;
                pet.Asleep = false;
                pet.BedItem = -1;
                if (nearPlayer)
                    Wander(ref pet);
                else
                    Choose(ref pet);
            }
            pet.Present = home;
        }
    }

    /// <summary>
    /// Publishes <see cref="UiFocus"/> when the shop's panel or placement mode opened or closed since
    /// the last tick (however it closed: Back, walking away, leaving the house, fainting or a screen
    /// opening), so Input stops walking, looking, jumping and firing while one is open.
    /// </summary>
    private void PublishFocus()
    {
        if (ShopOpen != _shopFocus)
        {
            _shopFocus = ShopOpen;
            _bus!.Publish(new UiFocus(ShopFocusOwner, _shopFocus));
        }
        if (Decorating != _decorateFocus)
        {
            _decorateFocus = Decorating;
            _bus!.Publish(new UiFocus(DecorateFocusOwner, _decorateFocus));
        }
    }

    /// <summary>
    /// The view's confirm button (Enter; the gamepad's A arrives as Interact), acted on at the next tick like an Interact
    /// press: buys in the shop, chooses in the picker, places or picks up in placement mode. Nothing when no panel is open.
    /// </summary>
    public void Confirm()
    {
        if (ShopOpen || Decorating)
            _interact = true;
    }

    /// <summary>
    /// The Input module's paced menu step (one when a direction is first pushed, then at most one per
    /// 0.2 s while held): moves the shop's or the picker's highlight (Vertical) or the decorating
    /// cursor (both) by one. Nothing when no panel is open.
    /// </summary>
    public void Handle(in MenuNav message)
    {
        if (ShopOpen)
        {
            if (message.Vertical != 0)
                MoveShopSelection(message.Vertical);
        }
        else if (Decorating && Picking)
        {
            if (message.Vertical != 0)
                MovePickerSelection(message.Vertical);
        }
        else if (Decorating && message.Any)
        {
            MoveCursor(message.Horizontal, message.Vertical);
        }
    }

    private HomePrompt PromptAt(Vector3 p)
    {
        if (_player.Area == Area.Home)
        {
            if (Distance(p, Places.HomeExit.Position) <= IndoorReach)
                return HomePrompt.Leave;
            if (Distance(p, HomeRoom.BookPosition) <= IndoorReach)
                return HomePrompt.Decorate;
            if (Distance(p, HomeRoom.WardrobeFront) <= IndoorReach)
                return HomePrompt.Wardrobe;
            if (NearBed(p))
                return HomePrompt.Sleep;
            // The nearest pet at home within reach can be taken along.
            int nearest = -1;
            float nearestDistance = IndoorReach;
            for (int i = 0; i < _pets.Length; i++)
            {
                float d = Distance(p, _pets[i].Position);
                if (_pets[i].Present && d <= nearestDistance)
                {
                    nearest = i;
                    nearestDistance = d;
                }
            }
            return nearest < 0 ? HomePrompt.None : TakePrompt(_pets[nearest].Kind);
        }
        if (Distance(p, Places.HomeDoor.Position) <= OutdoorReach)
            return HomePrompt.Enter;
        if (Distance(p, Places.FurnitureShopSpot.Position) <= OutdoorReach)
            return HomePrompt.Shop;
        return HomePrompt.None;
    }

    /// <summary>Whether <paramref name="p"/> is within <see cref="IndoorReach"/> of a placed Basic Bed or Mushroom Bed's footprint.</summary>
    private bool NearBed(Vector3 p)
    {
        for (int i = 0; i < ItemCount; i++)
        {
            ref readonly var item = ref _items[i];
            if (!item.Placed || (item.Kind != FurnitureKind.BasicBed && item.Kind != FurnitureKind.MushroomBed))
                continue;
            float minX = Areas.Home.MinX + item.Column * HomeRoom.Cell, minZ = Areas.Home.MinZ + item.Row * HomeRoom.Cell;
            float dx = MathF.Max(0f, MathF.Max(minX - p.X, p.X - (minX + item.Width * HomeRoom.Cell)));
            float dz = MathF.Max(0f, MathF.Max(minZ - p.Z, p.Z - (minZ + item.Depth * HomeRoom.Cell)));
            if (dx * dx + dz * dz <= IndoorReach * IndoorReach)
                return true;
        }
        return false;
    }

    /// <summary>The prompt that takes <paramref name="kind"/> along.</summary>
    public static HomePrompt TakePrompt(PetKind kind) => kind switch
    {
        PetKind.Dog => HomePrompt.TakeDog,
        PetKind.Crow => HomePrompt.TakeCrow,
        _ => HomePrompt.TakeCat,
    };

    private void Use(HomePrompt prompt)
    {
        switch (prompt)
        {
            case HomePrompt.Enter:
                Teleport(Places.HomeInterior);
                break;
            case HomePrompt.Leave:
                Teleport(Places.HomeDoor);
                break;
            case HomePrompt.Shop:
                OpenShop();
                break;
            case HomePrompt.Decorate:
                StartDecorating();
                break;
            case HomePrompt.Wardrobe:
                _bus!.Publish(new OpenWardrobe());
                break;
            case HomePrompt.TakeCat:
                _bus!.Publish(new SetActivePet(PetKind.Cat));
                break;
            case HomePrompt.TakeDog:
                _bus!.Publish(new SetActivePet(PetKind.Dog));
                break;
            case HomePrompt.TakeCrow:
                _bus!.Publish(new SetActivePet(PetKind.Crow));
                break;
            case HomePrompt.Sleep:
                // DayNight fades and skips to the morning; a night's sleep heals fully.
                _bus!.Publish(new Sleep());
                _bus.Publish(new HealPlayer(HealPlayer.Full));
                break;
        }
    }

    private void Teleport(in Place place)
    {
        _bus!.Publish(new TeleportPlayer(place.Position, place.Yaw));
        // Until the player's next state, act as if it is already there (no second press on the old spot).
        _player = new PlayerState(place.Position, place.Yaw, Vector3.Zero, true, _player.Health, _player.MaxHealth,
            _player.Alive, _player.Character);
        Prompt = HomePrompt.None;
    }

    private static float Distance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    // Shop

    public void OpenShop()
    {
        ShopOpen = true;
        LastShopResult = ShopResult.None;
    }

    /// <summary>
    /// The view's Back button: closes the shop's panel; in placement mode goes back to the picker (a
    /// held item back to the inventory); in the picker ends decorating.
    /// </summary>
    public void Back()
    {
        ShopOpen = false;
        if (Decorating && Picking)
            StopDecorating();
        else if (Decorating)
            OpenPicker();
    }

    /// <summary>Moves the shop's highlight up (-1) or down (+1), wrapping around.</summary>
    public void MoveShopSelection(int step)
    {
        int n = Furniture.ForSale.Length;
        ShopSelection = ((ShopSelection + step) % n + n) % n;
    }

    /// <summary>Buys the highlighted furniture.</summary>
    public void Buy() => Buy(Furniture.ForSale[ShopSelection]);

    /// <summary>
    /// Asks the wallet for <paramref name="kind"/>'s price; when it takes the coins the item goes
    /// to the inventory (see <see cref="LastShopResult"/>). Refused without asking when the
    /// inventory is full or another purchase is still waiting for its answer.
    /// </summary>
    public void Buy(FurnitureKind kind)
    {
        if (_bus == null || _pendingRequest != -1 || Furniture.Price(kind) <= 0)
            return;
        if (ItemCount >= MaxItems)
        {
            Answer(ShopResult.Full, kind);
            return;
        }
        _pendingRequest = _nextRequest++;
        _pendingKind = kind;
        _bus.Publish(new SpendCoins(_pendingRequest, Furniture.Price(kind), Furniture.Name(kind)));
    }

    private void Answer(ShopResult result, FurnitureKind kind)
    {
        LastShopResult = result;
        LastShopKind = kind;
        ShopResults++;
    }

    // Decorating

    /// <summary>Opens decorating at its picker (not while a wardrobe screen or the start screen is open).</summary>
    public void StartDecorating()
    {
        if (WardrobeOpen || StartScreenOpen)
            return;
        Decorating = true;
        CursorRotation = 0;
        CursorColumn = HomeRoom.Columns / 2;
        CursorRow = HomeRoom.Rows / 2 - 2;
        PickerSelection = 0;
        OpenPicker();
    }

    /// <summary>Ends decorating (picker and placement mode); a held item goes back to the inventory.</summary>
    public void StopDecorating()
    {
        Decorating = false;
        Picking = false;
        Held = -1;
        _heldFromPicker = false;
    }

    /// <summary>Shows the picker (a held item goes back to the inventory), its lines counted afresh.</summary>
    private void OpenPicker()
    {
        Picking = true;
        Held = -1;
        _heldFromPicker = false;
        PickerCount = 0;
        for (int k = 0; k < Furniture.KindCount; k++)
        {
            int count = 0;
            for (int i = 0; i < ItemCount; i++)
                if (!_items[i].Placed && (int)_items[i].Kind == k)
                    count++;
            if (count > 0)
                _picker[PickerCount++] = new PickerEntry { Kind = (FurnitureKind)k, Count = count };
        }
        PickerSelection = Math.Min(PickerSelection, PickerCount);
    }

    /// <summary>Moves the picker's highlight up (-1) or down (+1) over its kinds and "Move placed items", wrapping around.</summary>
    public void MovePickerSelection(int step)
    {
        if (!Decorating || !Picking)
            return;
        int n = PickerCount + 1;
        PickerSelection = ((PickerSelection + step) % n + n) % n;
    }

    /// <summary>The picker's confirm: holds an item of the highlighted kind, or on "Move placed items" opens placement mode with nothing held.</summary>
    public void Choose()
    {
        if (!Decorating || !Picking)
            return;
        if (PickerSelection < PickerCount)
            Choose(_picker[PickerSelection].Kind);
        else
            MovePlacedItems();
    }

    /// <summary>Holds the first unplaced item of <paramref name="kind"/> and switches to placement mode (false when there is none).</summary>
    public bool Choose(FurnitureKind kind)
    {
        if (!Decorating || !Picking)
            return false;
        for (int i = 0; i < ItemCount; i++)
        {
            if (_items[i].Placed || _items[i].Kind != kind)
                continue;
            Picking = false;
            Hold(i, _items[i].Rotation, CursorColumn, CursorRow);
            _heldFromPicker = true;
            return true;
        }
        return false;
    }

    /// <summary>The picker's "Move placed items": placement mode with nothing held, to pick up or put away what stands.</summary>
    public void MovePlacedItems()
    {
        if (!Decorating || !Picking)
            return;
        Picking = false;
        Held = -1;
        _heldFromPicker = false;
        ClampCursor();
    }

    /// <summary>
    /// In placement mode with nothing held, puts the placed item under the cursor (<see cref="Highlighted"/>)
    /// back in the inventory: no longer placed, still owned. False when there is none.
    /// </summary>
    public bool PutAway()
    {
        int under = Highlighted;
        if (under < 0)
            return false;
        ref var item = ref _items[under];
        item.Placed = false;
        // As saved: an unplaced item has no spot.
        item.Column = 0;
        item.Row = 0;
        RebuildPetGrid();
        return true;
    }

    /// <summary>Moves the cursor (and the held item) by whole cells, staying inside the room.</summary>
    public void MoveCursor(int columns, int rows)
    {
        if (!Decorating || Picking)
            return;
        CursorColumn += columns;
        CursorRow += rows;
        ClampCursor();
    }

    /// <summary>Turns the held item a quarter turn.</summary>
    public void Rotate()
    {
        if (!Decorating || Picking || Held < 0)
            return;
        CursorRotation = (CursorRotation + 1) & 3;
        ClampCursor();
    }

    /// <summary>
    /// Places the held item at the cursor if it fits (true), or with nothing held picks up the
    /// placed item under the cursor (true). False when nothing happened. Placing an item chosen in
    /// the picker opens the picker again; placing one picked up here keeps placement mode open.
    /// </summary>
    public bool PlaceOrPickUp()
    {
        if (!Decorating || Picking)
            return false;
        if (Held >= 0)
        {
            if (!CanPlaceHeld)
                return false;
            ref var item = ref _items[Held];
            item.Placed = true;
            item.Column = CursorColumn;
            item.Row = CursorRow;
            item.Rotation = CursorRotation;
            Held = -1;
            RebuildPetGrid();
            if (_heldFromPicker)
                OpenPicker();
            return true;
        }
        int under = ItemAt(CursorColumn, CursorRow);
        if (under < 0)
            return false;
        _items[under].Placed = false;
        Hold(under, _items[under].Rotation, _items[under].Column, _items[under].Row);
        _heldFromPicker = false;
        RebuildPetGrid();
        return true;
    }

    /// <summary>The placed item covering a cell, or -1.</summary>
    public int ItemAt(int column, int row)
    {
        for (int i = 0; i < ItemCount; i++)
            if (_items[i].Placed && _items[i].Covers(column, row))
                return i;
        return -1;
    }

    /// <summary>
    /// Whether <paramref name="kind"/> turned <paramref name="rotation"/> quarter turns fits with
    /// its north-west cell at (column, row): inside the room, off the door's way in and the
    /// catalog book's stand, and overlapping no placed furniture.
    /// </summary>
    public bool CanPlace(FurnitureKind kind, int column, int row, int rotation)
    {
        int w = Furniture.Width(kind, rotation), d = Furniture.Depth(kind, rotation);
        for (int c = column; c < column + w; c++)
            for (int r = row; r < row + d; r++)
                if (!HomeRoom.Inside(c, r) || HomeRoom.KeptClear(c, r) || ItemAt(c, r) >= 0)
                    return false;
        return true;
    }

    private void Hold(int index, int rotation, int column, int row)
    {
        Held = index;
        CursorRotation = rotation;
        CursorColumn = column;
        CursorRow = row;
        ClampCursor();
    }

    private void ClampCursor()
    {
        int w = 1, d = 1;
        if (Held >= 0)
        {
            w = Furniture.Width(_items[Held].Kind, CursorRotation);
            d = Furniture.Depth(_items[Held].Kind, CursorRotation);
        }
        CursorColumn = Math.Clamp(CursorColumn, 0, HomeRoom.Columns - w);
        CursorRow = Math.Clamp(CursorRow, 0, HomeRoom.Rows - d);
    }

    // Pets at home

    private void RebuildPetGrid()
    {
        for (int c = 0; c < HomeRoom.Columns; c++)
            for (int r = 0; r < HomeRoom.Rows; r++)
            {
                int i = ItemAt(c, r);
                bool book = c >= HomeRoom.BookColumn && c < HomeRoom.BookColumn + HomeRoom.BookSize && r >= HomeRoom.BookRow;
                _petBlocked[c * HomeRoom.Rows + r] = book || HomeRoom.UnderWardrobe(c, r) || (i >= 0 && _items[i].Kind != FurnitureKind.PetBed);
            }
    }

    private bool PetFree(Vector3 p)
    {
        int c = HomeRoom.ColumnAt(p.X), r = HomeRoom.RowAt(p.Z);
        return HomeRoom.Inside(c, r) && !_petBlocked[c * HomeRoom.Rows + r];
    }

    private bool PetPathFree(Vector3 from, Vector3 to)
    {
        float length = Distance(from, to);
        int steps = (int)(length / 0.25f) + 1;
        for (int s = 1; s <= steps; s++)
            if (!PetFree(Vector3.Lerp(from, to, s / (float)steps)))
                return false;
        return true;
    }

    private Vector3 FreePetSpot(Vector3 preferred)
    {
        if (PetFree(preferred))
            return preferred;
        for (int ring = 1; ring < HomeRoom.Columns; ring++)
            for (int dc = -ring; dc <= ring; dc++)
                for (int dr = -ring; dr <= ring; dr++)
                {
                    var p = preferred + new Vector3(dc * HomeRoom.Cell, 0f, dr * HomeRoom.Cell);
                    if (PetFree(p))
                        return p;
                }
        return preferred;
    }

    /// <summary>A floor spot a little beside the player (on the room's grid of cell centers).</summary>
    private Vector3 NearPlayer()
    {
        var p = _player.Position + new Vector3(0.75f, 0f, 0f);
        int c = Math.Clamp(HomeRoom.ColumnAt(p.X), 0, HomeRoom.Columns - 1), r = Math.Clamp(HomeRoom.RowAt(p.Z), 0, HomeRoom.Rows - 1);
        return HomeRoom.CellsCenter(c, r, 1, 1);
    }

    private uint NextRandom()
    {
        // xorshift32: deterministic, allocation-free.
        _random ^= _random << 13;
        _random ^= _random >> 17;
        _random ^= _random << 5;
        return _random;
    }

    private float RandomRange(float min, float max) => min + (max - min) * (NextRandom() % 10000u) / 10000f;

    /// <summary>A random free floor spot reachable in a straight line from <paramref name="from"/>.</summary>
    private Vector3 WanderTarget(Vector3 from)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int c = (int)(NextRandom() % (uint)HomeRoom.Columns), r = (int)(NextRandom() % (uint)HomeRoom.Rows);
            var p = HomeRoom.CellsCenter(c, r, 1, 1);
            if (PetFree(p) && PetPathFree(from, p))
                return p;
        }
        return from;
    }

    private void Choose(ref HomePet pet)
    {
        pet.Asleep = false;
        pet.BedItem = -1;
        uint roll = NextRandom() % 10u;
        if (roll < 4)
        {
            Wander(ref pet);
        }
        else if (roll < 7)
        {
            pet.Activity = PetActivity.Nap;
            pet.Timer = RandomRange(8f, 14f);
            pet.BedItem = FreePetBed(pet.Kind, pet.Position);
            pet.Target = pet.BedItem >= 0 ? _items[pet.BedItem].Center : WanderTarget(pet.Position);
        }
        else
        {
            pet.Activity = PetActivity.Play;
            pet.Timer = RandomRange(4f, 7f);
            pet.Target = WanderTarget(pet.Position);
        }
    }

    private void Wander(ref HomePet pet)
    {
        pet.Activity = PetActivity.Wander;
        pet.Timer = RandomRange(3f, 6f);
        pet.Target = WanderTarget(pet.Position);
    }

    /// <summary>The nearest placed pet bed no other napping pet has taken, or -1.</summary>
    private int FreePetBed(PetKind kind, Vector3 from)
    {
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < ItemCount; i++)
        {
            if (!_items[i].Placed || _items[i].Kind != FurnitureKind.PetBed)
                continue;
            bool taken = false;
            foreach (var other in _pets)
                if (other.Present && other.Kind != kind && other.Activity == PetActivity.Nap && other.BedItem == i)
                    taken = true;
            float d = Distance(from, _items[i].Center);
            if (!taken && d < bestDistance)
            {
                best = i;
                bestDistance = d;
            }
        }
        return best;
    }

    private int Playmate(PetKind kind)
    {
        for (int i = 0; i < _pets.Length; i++)
            if (_pets[i].Present && _pets[i].Kind != kind)
                return i;
        return -1;
    }

    private void TickPets(float dt)
    {
        for (int i = 0; i < _pets.Length; i++)
        {
            ref var pet = ref _pets[i];
            if (!pet.Present)
                continue;
            pet.Timer -= dt;
            if (pet.Timer <= 0f)
                Choose(ref pet);

            float speed = PetWalkSpeed;
            bool goingToBed = false;
            switch (pet.Activity)
            {
                case PetActivity.Nap:
                    if (pet.BedItem >= 0)
                    {
                        ref readonly var bed = ref _items[pet.BedItem];
                        if (!bed.Placed || bed.Kind != FurnitureKind.PetBed)
                        {
                            // Its bed was picked up: nap somewhere else.
                            pet.BedItem = -1;
                            pet.Asleep = false;
                            pet.Target = WanderTarget(pet.Position);
                        }
                        else
                        {
                            pet.Target = bed.Center;
                            goingToBed = true;
                        }
                    }
                    break;
                case PetActivity.Play:
                    speed = PetPlaySpeed;
                    int mate = Playmate(pet.Kind);
                    // Chase a playmate (the lower kind chases the other); alone, zoom from spot to spot.
                    if (mate > i && Distance(pet.Position, _pets[mate].Position) > 0.6f && PetPathFree(pet.Position, _pets[mate].Position))
                        pet.Target = _pets[mate].Position;
                    else if (Distance(pet.Position, pet.Target) < 0.05f)
                        pet.Target = WanderTarget(pet.Position);
                    break;
            }

            var to = pet.Target - pet.Position;
            to.Y = 0f;
            float distance = to.Length();
            float step = speed * dt;
            if (distance <= step)
            {
                pet.Position = new Vector3(pet.Target.X, HomeRoom.FloorY, pet.Target.Z);
                pet.Speed = distance / dt;
                if (pet.Activity == PetActivity.Nap)
                    pet.Asleep = true;
            }
            else
            {
                var next = pet.Position + to / distance * step;
                if (!goingToBed && !PetFree(next))
                {
                    // Furniture moved into its way: pick somewhere else.
                    pet.Target = WanderTarget(pet.Position);
                    pet.Speed = 0f;
                    continue;
                }
                pet.Position = new Vector3(next.X, HomeRoom.FloorY, next.Z);
                pet.Speed = speed;
                pet.Asleep = false;
                pet.Facing = MathF.Atan2(-to.X, -to.Z);
            }
        }
    }
}
