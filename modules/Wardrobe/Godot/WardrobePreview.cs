using Fungtopia.Shell;
using Fungtopia.Wallet;
using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// A stand-alone scene for screenshots: <c>./screenshot.sh modules/Wardrobe/Godot/Preview.tscn</c>.
/// A real <see cref="WardrobeModule"/> on a bus with a stand-in wallet (420 M¢), a dressed
/// <see cref="Mannequin"/> on the left, turned by the Core's <see cref="PreviewYaw"/> (here a
/// simulated mouse drag), and the <see cref="WardrobeView"/> on the right. The environment
/// variable WARDROBE_PREVIEW picks: "wardrobe" (default, the Top tab), "look" (the Look tab),
/// "shop" (Abby's shop after one purchase, trying on the Triangle Party Dress it cannot afford)
/// or "items" (four outfits side by side, no panel).
/// </summary>
public partial class WardrobePreview : Node3D, IMessageHandler<SpendCoins>, IMessageHandler<AppearanceState>,
    IMessageHandler<PreviewYaw>
{
    private readonly MessageBus _bus = new();
    private readonly WardrobeModule _wardrobe = new();
    private WardrobeView _view = null!;
    private Mannequin _mannequin = null!;
    private int _coins = 420;
    private string _mode = "wardrobe";
    private int _frame;
    private AppearanceState _look;

    public override void _Ready()
    {
        string mode = OS.GetEnvironment("WARDROBE_PREVIEW");
        if (!string.IsNullOrEmpty(mode))
            _mode = mode.ToLowerInvariant();
        _wardrobe.Start(_bus);
        _bus.Subscribe<SpendCoins>(this);
        _bus.Subscribe<AppearanceState>(this);
        _bus.Subscribe<PreviewYaw>(this);
        Stage();

        if (_mode == "items")
        {
            ShowOutfits();
            return;
        }
        _mannequin = new Mannequin { Position = new Vector3(0, 0, 0), RotationDegrees = new Vector3(0, 180, 0) };
        AddChild(_mannequin);
        var camera = new Camera3D { Fov = 40f, Current = true };
        AddChild(camera);
        camera.LookAtFromPosition(new Vector3(1.0f, 1.1f, 3.2f), new Vector3(1.0f, 0.75f, 0), Vector3.Up);

        _view = (WardrobeView)GD.Load<PackedScene>("res://modules/Wardrobe/Godot/Main.tscn").Instantiate();
        _view.Bind(_wardrobe);
        AddChild(_view);

        _bus.Publish(new WalletState(_coins));
        _bus.Publish(new GrantItem("round_glasses", "Millie's quest"));
        _bus.Publish(new SetLook(2, 4, 5, 2));
        _bus.Publish(new Equip("striped_tee"));
        _bus.Publish(new Equip("overalls"));
        _bus.Publish(new Equip("round_glasses"));
        if (_mode == "shop")
        {
            _bus.Publish(new OpenShop());
            _wardrobe.Buy("flower_clip");
            _wardrobe.Tick(new TickContext(0, 1.0 / 60));
            _bus.Publish(new Equip("flower_clip"));
        }
        else
        {
            _bus.Publish(new OpenWardrobe());
        }
        _wardrobe.Tick(new TickContext(0, 1.0 / 60));
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        if (_mode == "items")
            return;
        if (_frame == 3)
        {
            if (_mode == "shop" && _view.Shop != null)
            {
                _view.Shop.Select(Catalog.IndexOf("party_dress"));
                _view.Shop.Buy();
                // Nothing highlighted but the pick: the dress is tried on.
                GetViewport().GuiReleaseFocus();
            }
            else if (_mode == "look")
                _view.Wardrobe?.SelectTab(0);
            else
                _view.Wardrobe?.SelectTab(1);
        }
        // A drag to the left on the left half of the screen, a little each frame.
        if (_frame is > 3 and <= 33)
            _wardrobe.Drag(_mode == "shop" ? 1.2f : -1.4f);
        _wardrobe.Tick(new TickContext(_frame, delta));
        if (_frame % 20 == 1)
            _mannequin.Apply(_look);
    }

    public void Handle(in SpendCoins message)
    {
        bool ok = message.Amount >= 0 && message.Amount <= _coins;
        if (ok)
            _coins -= message.Amount;
        _bus.Publish(new SpendResult(message.RequestId, ok, _coins));
        _bus.Publish(new WalletState(_coins));
    }

    public void Handle(in AppearanceState message) => _look = message;

    /// <summary>The rig faces -Z, so π faces the camera; the yaw turns it from there.</summary>
    public void Handle(in PreviewYaw message)
    {
        if (_mannequin != null)
            _mannequin.Rotation = new Vector3(0f, Mathf.Pi + message.Yaw, 0f);
    }

    private void ShowOutfits()
    {
        var looks = new[]
        {
            new AppearanceState(0, 2, 6, 2, "spotted_dress", "", "mary_janes", "big_bow", "", Catalog.LadybugBackpack),
            new AppearanceState(3, 0, 1, 1, "mushroom_hoodie", "jeans", "sneakers", "mushroom_hat", "", ""),
            new AppearanceState(1, 4, 5, 3, "party_dress", "", "rain_boots", "flower_clip", "star_sunglasses", ""),
            new AppearanceState(5, 5, 2, 0, "sweater_vest", "shorts", "sneakers", "beanie", Catalog.SpiderSilkScarf, "mushroom_backpack"),
        };
        for (int i = 0; i < looks.Length; i++)
        {
            var mannequin = new Mannequin { Position = new Vector3(-1.95f + i * 1.3f, 0, 0), RotationDegrees = new Vector3(0, 195 - i * 10, 0) };
            AddChild(mannequin);
            mannequin.Apply(looks[i]);
        }
        var camera = new Camera3D { Fov = 40f, Current = true };
        AddChild(camera);
        camera.LookAtFromPosition(new Vector3(0, 1.3f, 4.6f), new Vector3(0, 0.75f, 0), Vector3.Up);
    }

    private void Stage()
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.98f, 0.86f, 0.8f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.62f, 0.55f, 0.72f),
            AmbientLightEnergy = 0.9f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapWhite = 6f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        AddChild(new DirectionalLight3D
        {
            LightColor = new Color(1.0f, 0.9f, 0.75f),
            LightEnergy = 1.3f,
            ShadowEnabled = true,
            RotationDegrees = new Vector3(-40f, -30f, 0f),
        });
        // A cozy wooden floor and the wardrobe's back wall.
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 6f, BottomRadius = 6f, Height = 0.1f },
            Position = new Vector3(0f, -0.05f, 0f),
            MaterialOverride = ToonGlb.Material(new Color(0.78f, 0.56f, 0.38f)),
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(14f, 6f, 0.2f) },
            Position = new Vector3(0f, 3f, -2.5f),
            MaterialOverride = ToonGlb.Material(new Color(1f, 0.88f, 0.82f)),
        });
    }
}
