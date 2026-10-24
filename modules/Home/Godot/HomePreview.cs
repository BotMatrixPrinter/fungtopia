using System;
using System.Collections.Generic;
using Fungtopia.Map;
using Fungtopia.Pet;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.Wallet;
using Godot;

namespace Fungtopia.Home.View;

/// <summary>
/// A stand-alone scene showing a decorated home, for screenshots:
/// <c>./screenshot.sh modules/Home/Godot/Preview.tscn</c>. It runs its own Home Core on a little
/// bus that also answers purchases like the wallet, buys a room full of furniture, places it,
/// and keeps the Cat and the Dog at home (the Crow is out). The room's walls and floor are a simple
/// backdrop (the Map module draws the real ones), its south wall left out so the camera can look
/// in. The environment variable HOME_PREVIEW = "decorate" shows placement mode with an item held,
/// "shop" the furniture shop's catalog.
/// </summary>
public partial class HomePreview : Node3D
{
    private sealed class PreviewBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public int Coins = 5000;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(message);
            if (message is SpendCoins spend)
            {
                bool ok = spend.Amount <= Coins;
                if (ok)
                    Coins -= spend.Amount;
                Publish(new SpendResult(spend.RequestId, ok, Coins));
            }
        }
    }

    private readonly PreviewBus _bus = new();
    private readonly HomeModule _home = new();
    private string _mode = "";
    private long _tick;

    public override void _Ready()
    {
        _mode = OS.GetEnvironment("HOME_PREVIEW").ToLowerInvariant();
        _home.Start(_bus);
        Furnish();
        AddBackdrop();

        var view = new HomeView();
        view.Bind(_home);
        AddChild(view);

        var camera = new Camera3D { Fov = 50f, Current = true };
        AddChild(camera);
        float y = Areas.HomeFloorY;
        if (_mode == "shop")
        {
            _bus.Publish(new WalletState(_bus.Coins));
            _bus.Publish(new PlayerState(Places.FurnitureShopSpot.Position, 0f, System.Numerics.Vector3.Zero, true, 10, 10, true, CharacterKind.Girl));
            _home.OpenShop();
            _home.MoveShopSelection(7);
            _home.Buy();
        }
        if (_mode == "decorate")
        {
            _home.Buy(FurnitureKind.Bookshelf);
            _home.StartDecorating();
            _home.MoveCursor(4, 2);
        }
        camera.LookAtFromPosition(new Vector3(601.8f, y + 3.7f, 605.8f), new Vector3(598.4f, y + 0.4f, 598.4f), Vector3.Up);
    }

    private void Place(FurnitureKind kind, int column, int row, int rotation)
    {
        _home.Buy(kind);
        _home.StartDecorating();
        while (_home.Held >= 0 && _home.Items[_home.Held].Kind != kind)
            _home.NextItem(1);
        _home.MoveCursor(column - _home.CursorColumn, row - _home.CursorRow);
        for (int r = 0; r < rotation; r++)
            _home.Rotate();
        _home.MoveCursor(column - _home.CursorColumn, row - _home.CursorRow);
        if (!_home.PlaceOrPickUp())
            GD.PushWarning($"Fungtopia: Home preview: {kind} does not fit at {column}, {row}.");
        _home.StopDecorating();
    }

    private void Furnish()
    {
        // Swap the basic bed for a mushroom bed.
        _home.StartDecorating();
        _home.MoveCursor(2 - _home.CursorColumn, -_home.CursorRow);
        _home.PlaceOrPickUp();
        _home.StopDecorating();
        Place(FurnitureKind.MushroomBed, 2, 0, 0);
        Place(FurnitureKind.Lamp, 5, 0, 0);
        Place(FurnitureKind.PottedMushroom, 0, 0, 0);
        Place(FurnitureKind.Painting, 8, 0, 0);
        Place(FurnitureKind.Bookshelf, 0, 5, 1);
        Place(FurnitureKind.Rug, 8, 5, 0);
        Place(FurnitureKind.ToadstoolStool, 7, 6, 0);
        Place(FurnitureKind.Table, 16, 4, 0);
        Place(FurnitureKind.Chair, 16, 6, 2);
        Place(FurnitureKind.Chair, 18, 4, 3);
        Place(FurnitureKind.PetBed, 3, 11, 0);
        Place(FurnitureKind.PottedMushroom, 23, 0, 0);
        Place(FurnitureKind.ToadstoolStool, 13, 6, 0);

        // Let the pets settle in until one naps on the pet bed.
        _bus.Publish(new PlayerState(Places.HomeInterior.Position, 0f, System.Numerics.Vector3.Zero, true, 10, 10, true, CharacterKind.Girl));
        for (int t = 0; t < 60 * 120; t++)
        {
            Step();
            if (_home.Pets[(int)PetKind.Cat].OnBed || _home.Pets[(int)PetKind.Dog].OnBed)
                break;
        }
    }

    private void Step()
    {
        _bus.Publish(new OwnedPets(true, true, true, PetKind.Crow));
        _home.Tick(new TickContext(_tick++, 1.0 / 60));
    }

    public override void _PhysicsProcess(double delta) => Step();

    private void AddBackdrop()
    {
        var room = Areas.Home;
        float y = Areas.HomeFloorY, cx = room.Center.X, cz = room.Center.Y;
        float w = room.MaxX - room.MinX, d = room.MaxZ - room.MinZ;
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.98f, 0.86f, 0.8f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.7f, 0.62f, 0.68f),
                AmbientLightEnergy = 0.5f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                TonemapWhite = 6f,
            },
        });
        AddChild(new DirectionalLight3D
        {
            LightColor = new Color(1f, 0.9f, 0.75f),
            LightEnergy = 0.6f,
            ShadowEnabled = true,
            RotationDegrees = new Vector3(-55f, -35f, 0f),
        });
        AddChild(new OmniLight3D
        {
            LightColor = new Color(1f, 0.86f, 0.66f),
            LightEnergy = 0.9f,
            OmniRange = 11f,
            Position = new Vector3(cx, y + 3.2f, cz),
        });
        Box(new Vector3(cx, y - 0.1f, cz), new Vector3(w + 0.6f, 0.2f, d + 0.6f), new Color(0.72f, 0.52f, 0.4f));
        var wall = new Color(0.94f, 0.8f, 0.7f);
        Box(new Vector3(cx, y + 1.5f, room.MinZ - 0.15f), new Vector3(w + 0.6f, 3f, 0.3f), wall);
        Box(new Vector3(room.MinX - 0.15f, y + 1.5f, cz), new Vector3(0.3f, 3f, d), wall);
        Box(new Vector3(room.MaxX + 0.15f, y + 1.5f, cz), new Vector3(0.3f, 3f, d), wall);
        // A round window in the north wall, over the bed.
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.6f, Height = 0.04f },
            MaterialOverride = HomeModels.Toon(new Color(0.66f, 0.86f, 1f), false, 0.5f),
            Transform = new Transform3D(new Basis(Vector3.Right, MathF.PI / 2f), new Vector3(cx - 3f, y + 2f, room.MinZ + 0.01f)),
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.6f, OuterRadius = 0.76f },
            MaterialOverride = HomeModels.Toon(new Color(0.62f, 0.4f, 0.26f)),
            Transform = new Transform3D(new Basis(Vector3.Right, MathF.PI / 2f), new Vector3(cx - 3f, y + 2f, room.MinZ + 0.03f)),
        });
    }

    private void Box(Vector3 center, Vector3 size, Color color) => AddChild(new MeshInstance3D
    {
        Mesh = new BoxMesh { Size = size },
        MaterialOverride = HomeModels.Toon(color, false),
        Position = center,
    });
}
