using System;
using System.Collections.Generic;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Fungtopia.Wallet;
using Godot;

namespace Fungtopia.Food.View;

/// <summary>
/// A stand-alone scene for screenshots: <c>./screenshot.sh modules/Food/Godot/Preview.tscn</c>. It
/// runs its own Food Core on a little bus that answers purchases like the wallet, with a simple
/// backdrop (a grassy square and a stand-in for the player; the Town and Player modules draw the
/// real ones). The environment variable FOOD_PREVIEW = "menu" (the default) shows the Mushroom
/// Munchies menu over a stocked food bar, "bar" the food bar alone, "eat" the player halfway
/// through a soup with crumbs falling, "eaten" the "+3 ♥" just after; FOOD_PREVIEW_DEVICE =
/// "xbox", "playstation", "nintendo" or "generic" shows that pad's buttons (keyboard otherwise).
/// </summary>
public partial class FoodPreview : Node3D
{
    private sealed class PreviewBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public int Coins = 120;

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
    private readonly FoodModule _food = new();
    private string _mode = "";
    private long _tick;
    private int _health = 10;
    private System.Numerics.Vector3 _feet;

    public override void _Ready()
    {
        _mode = OS.GetEnvironment("FOOD_PREVIEW").ToLowerInvariant();
        if (_mode == "")
            _mode = "menu";
        _food.Start(_bus);
        _bus.Publish(new ActiveDevice(OS.GetEnvironment("FOOD_PREVIEW_DEVICE").ToLowerInvariant() switch
        {
            "xbox" => DeviceKind.Xbox,
            "playstation" or "ps" => DeviceKind.PlayStation,
            "nintendo" => DeviceKind.Nintendo,
            "generic" => DeviceKind.Generic,
            _ => DeviceKind.KeyboardMouse,
        }));
        _bus.Publish(new WalletState(_bus.Coins));
        _feet = Places.FoodSpot.Position;
        _health = 4;
        Player();

        // A bag with some of everything: muffins, two soups, a pie.
        _food.OpenMenu();
        for (int i = 0; i < 4; i++)
            _food.Buy(FoodKinds.Muffin);
        _food.Buy(FoodKinds.Soup);
        _food.Buy(FoodKinds.Soup);
        _food.Buy(FoodKinds.Pie);
        _food.Back();
        _food.Select(FoodKinds.Soup);
        if (_mode == "menu")
        {
            _food.OpenMenu();
            _food.MoveMenuSelection(1);
            _food.Buy(FoodKinds.Pie);
            _food.Buy(FoodKinds.Pie);
            _food.Buy(FoodKinds.Pie); // too few coins by now
        }

        AddBackdrop();
        var view = new FoodView();
        view.Bind(_food);
        AddChild(view);

        var camera = new Camera3D { Fov = 45f, Current = true };
        AddChild(camera);
        var p = new Vector3(_feet.X, _feet.Y, _feet.Z);
        float yaw = Places.FoodSpot.Yaw;
        var forward = new Vector3(-MathF.Sin(yaw), 0f, -MathF.Cos(yaw));
        var side = forward.Cross(Vector3.Up);
        camera.LookAtFromPosition(p + forward * 3.8f + side * 1.6f + Vector3.Up * 2.1f, p + Vector3.Up * 0.9f, Vector3.Up);
    }

    private void Player() =>
        _bus.Publish(new PlayerState(_feet, Places.FoodSpot.Yaw, System.Numerics.Vector3.Zero, true, _health, 10, true, CharacterKind.Girl));

    public override void _PhysicsProcess(double delta)
    {
        // "eat" starts the meal so the screenshot (frame 90) is about halfway; "eaten" so it has just finished.
        if ((_mode == "eat" && _tick == 55) || (_mode == "eaten" && _tick == 15))
            _food.TryEat();
        Player();
        _food.Tick(new TickContext(_tick++, 1.0 / 60));
    }

    private void AddBackdrop()
    {
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.66f, 0.84f, 0.98f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.8f, 0.78f, 0.82f),
                AmbientLightEnergy = 0.4f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                TonemapWhite = 6f,
            },
        });
        AddChild(new DirectionalLight3D
        {
            LightColor = new Color(1f, 0.94f, 0.82f),
            LightEnergy = 0.7f,
            ShadowEnabled = true,
            RotationDegrees = new Vector3(-50f, -40f, 0f),
        });
        var p = new Vector3(_feet.X, _feet.Y, _feet.Z);
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 12f, BottomRadius = 12f, Height = 0.2f },
            MaterialOverride = FoodModels.Toon(new Color(0.56f, 0.8f, 0.46f), false),
            Position = p - Vector3.Up * 0.1f,
        });
        // A stand-in for the player: a soft capsule body and a round head.
        AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.26f, Height = 1.0f },
            MaterialOverride = FoodModels.Toon(new Color(0.98f, 0.7f, 0.78f)),
            Position = p + Vector3.Up * 0.5f,
        });
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.26f, Height = 0.52f },
            MaterialOverride = FoodModels.Toon(new Color(1f, 0.88f, 0.78f)),
            Position = p + Vector3.Up * 1.22f,
        });
    }
}
