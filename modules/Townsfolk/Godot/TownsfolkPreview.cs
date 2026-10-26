using System;
using System.Collections.Generic;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.Player;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Townsfolk.View;

/// <summary>
/// A stand-alone scene showing Pet City's townsfolk, for screenshots:
/// <c>./screenshot.sh modules/Townsfolk/Godot/Preview.tscn</c>. It runs its own Townsfolk Core on a
/// little bus (two minutes simulated first, so everyone is about their business), draws the Map's
/// ground and Pet City (the Map and PetCity modules' scenes, read-only) and its own simple sky and
/// sun. TOWNSFOLK_PREVIEW picks the view: "plaza" (default, the plaza by day from above the avenue),
/// "close" (down among the folk on the plaza), "night" (the plaza at 22:00), or "close,night";
/// "near" looks at the one out nearest the plaza's middle from 3 m, "sit" at one sitting on a bench
/// from 2.4 m, "crowd" at the busiest group of folk from 5.5 m.
/// </summary>
public partial class TownsfolkPreview : Node3D
{
    private sealed class PreviewBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();

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
        }
    }

    private const double Step = 1.0 / 60;

    private readonly PreviewBus _bus = new();
    private readonly TownsfolkModule _town = new();
    private long _ticks;
    private float _hour;
    private System.Numerics.Vector3 _player;

    public override void _Ready()
    {
        string mode = OS.GetEnvironment("TOWNSFOLK_PREVIEW").ToLowerInvariant();
        bool night = mode.Contains("night");
        bool close = mode.Contains("close");
        _hour = night ? 22f : 11f;
        _town.Start(_bus);
        var plaza = Buildings.CityPlaza;
        // The player stands where the camera looks from (the folk step round it).
        var eye = close ? new Vector3(-3f, 0f, plaza.Y + 15f) : new Vector3(0f, 0f, -170f);
        _player = new System.Numerics.Vector3(eye.X, Ground.HeightAt(eye.X, eye.Z), eye.Z);
        for (int i = 0; i < 120 * 60; i++)
            Tick();

        AddEnvironment(night);
        foreach (var path in new[] { "res://modules/Map/Godot/Main.tscn", "res://modules/PetCity/Godot/Main.tscn" })
            if (GD.Load<PackedScene>(path) is { } scene)
                AddChild(scene.Instantiate());
        var view = (TownsfolkView)GD.Load<PackedScene>("res://modules/Townsfolk/Godot/Main.tscn").Instantiate();
        view.Bind(_town);
        AddChild(view);

        var camera = new Camera3D { Fov = 55f, Current = true, Far = 900f };
        AddChild(camera);
        float ground = Ground.HeightAt(plaza.X, plaza.Y);
        if (mode.Contains("crowd") && Busiest() is { } middle)
        {
            var at = new Vector3(middle.Position.X, middle.Position.Y, middle.Position.Z);
            var from = at + new Vector3(0f, 0f, 5.5f);
            _player = new System.Numerics.Vector3(from.X, Ground.HeightAt(from.X, from.Z), from.Z);
            camera.LookAtFromPosition(from + new Vector3(0f, 1.6f, 0f), at + new Vector3(0f, 0.7f, 0f), Vector3.Up);
        }
        else if ((mode.Contains("near") || mode.Contains("sit")) && Subject(mode.Contains("sit")) is { } folk)
        {
            // From in front of it (facing -Z turned by its yaw), a little to its right.
            var at = new Vector3(folk.Position.X, folk.Position.Y, folk.Position.Z);
            var front = new Vector3(-MathF.Sin(folk.Yaw + 0.5f), 0f, -MathF.Cos(folk.Yaw + 0.5f));
            float distance = mode.Contains("sit") ? 2.4f : 3.2f;
            var from = at + front * distance;
            _player = new System.Numerics.Vector3(from.X, Ground.HeightAt(from.X, from.Z), from.Z);
            camera.LookAtFromPosition(from + new Vector3(0f, 1.3f, 0f), at + new Vector3(0f, 0.75f, 0f), Vector3.Up);
        }
        else if (close)
            camera.LookAtFromPosition(new Vector3(eye.X, _player.Y + 2.6f, eye.Z), new Vector3(2f, ground + 0.6f, plaza.Y + 2f), Vector3.Up);
        else
            camera.LookAtFromPosition(new Vector3(0f, _player.Y + 11f, -172f), new Vector3(0f, ground + 1f, -222f), Vector3.Up);
    }

    /// <summary>The townsperson to look at closely: one sitting on a bench, or the one out nearest the plaza's middle.</summary>
    private Folk? Subject(bool sitting)
    {
        var plaza = Buildings.CityPlaza;
        Folk? best = null;
        float bestDistance = float.MaxValue;
        foreach (var f in _town.Folk)
        {
            if (!f.Visible || (sitting && f.State != FolkState.Sitting))
                continue;
            float d = new Vector2(f.Position.X - plaza.X, f.Position.Z - plaza.Y).Length();
            if (d < bestDistance)
                (best, bestDistance) = (f, d);
        }
        return best;
    }

    /// <summary>The townsperson out with the most others out within 4 m.</summary>
    private Folk? Busiest()
    {
        var folk = _town.Folk;
        Folk? best = null;
        int most = -1;
        foreach (var f in folk)
        {
            if (!f.Visible)
                continue;
            int near = 0;
            foreach (var g in folk)
                if (g.Visible && System.Numerics.Vector3.Distance(f.Position, g.Position) < 4f)
                    near++;
            if (near > most)
                (best, most) = (f, near);
        }
        return best;
    }

    public override void _PhysicsProcess(double delta) => Tick();

    private void Tick()
    {
        _bus.Publish(new PlayerState(_player, 0f, System.Numerics.Vector3.Zero, true, 10, 10, true, CharacterKind.Girl));
        _bus.Publish(new TimeOfDay(1, _hour, TimeOfDay.IsNightAt(_hour)));
        _town.Tick(new TickContext(_ticks++, Step));
    }

    private void AddEnvironment(bool night)
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = night ? new Color(0.06f, 0.07f, 0.2f) : new Color(0.42f, 0.66f, 0.98f),
            SkyHorizonColor = night ? new Color(0.2f, 0.18f, 0.36f) : new Color(0.86f, 0.9f, 1f),
            GroundBottomColor = night ? new Color(0.05f, 0.05f, 0.1f) : new Color(0.5f, 0.62f, 0.5f),
            GroundHorizonColor = night ? new Color(0.2f, 0.18f, 0.36f) : new Color(0.86f, 0.9f, 1f),
        };
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = sky },
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = night ? new Color(0.3f, 0.32f, 0.6f) : new Color(0.75f, 0.75f, 0.85f),
                AmbientLightEnergy = night ? 0.45f : 0.55f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                TonemapWhite = 6f,
                GlowEnabled = night,
            },
        });
        AddChild(new DirectionalLight3D
        {
            LightColor = night ? new Color(0.55f, 0.6f, 0.95f) : new Color(1f, 0.94f, 0.82f),
            LightEnergy = night ? 0.35f : 1.1f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 220f,
            RotationDegrees = new Vector3(-50f, -30f, 0f),
        });
    }
}
