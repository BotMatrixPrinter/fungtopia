using System;
using System.Collections.Generic;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.PetCity.View;

/// <summary>
/// A stand-alone scene showing Pet City, for screenshots:
/// <c>./screenshot.sh modules/PetCity/Godot/Preview.tscn</c>. It runs its own PetCity Core on a
/// little bus, draws the Map's ground under it (the Map module's scene, read-only) and its own
/// simple sky and sun. PETCITY_PREVIEW picks the view: "plaza" (default, from the south
/// entrance), "store" (Lucy's Pet Store up close), "block", "props" (the statue, benches and lamps on the plaza's west side); add "night" (for example
/// "store,night") for the city at night.
/// </summary>
public partial class PetCityPreview : Node3D
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

    private readonly PreviewBus _bus = new();
    private readonly PetCityModule _city = new();

    public override void _Ready()
    {
        string mode = OS.GetEnvironment("PETCITY_PREVIEW").ToLowerInvariant();
        bool night = mode.Contains("night");
        _city.Start(_bus);
        _bus.Publish(new TimeOfDay(1, night ? 22f : 11f, night));

        AddEnvironment(night);
        if (GD.Load<PackedScene>("res://modules/Map/Godot/Main.tscn") is { } map)
            AddChild(map.Instantiate());
        var view = new PetCityView();
        view.Bind(_city);
        AddChild(view);

        var camera = new Camera3D { Fov = 55f, Current = true, Far = 900f };
        AddChild(camera);
        var store = Places.LucySpot.Position;
        var c = Buildings.CityPlaza;
        var plaza = new System.Numerics.Vector3(c.X, Ground.HeightAt(c.X, c.Y), c.Y);
        if (mode.Contains("store"))
            camera.LookAtFromPosition(G(store) + new Vector3(4f, 3.2f, 13f), G(store) + new Vector3(0f, 2.6f, -4f), Vector3.Up);
        else if (mode.Contains("props"))
            camera.LookAtFromPosition(G(plaza) + new Vector3(-6f, 3.2f, 13f), G(plaza) + new Vector3(-20f, 1.2f, -2f), Vector3.Up);
        else if (mode.Contains("block"))
            camera.LookAtFromPosition(G(plaza) + new Vector3(-14f, 2f, 12f), G(plaza) + new Vector3(30f, 12f, -10f), Vector3.Up);
        else
            camera.LookAtFromPosition(new Vector3(0f, 16f, -150f), new Vector3(0f, 6f, -232f), Vector3.Up);
    }

    private static Vector3 G(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

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
                AmbientLightEnergy = night ? 0.35f : 0.55f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                TonemapWhite = 6f,
                GlowEnabled = night,
            },
        });
        AddChild(new DirectionalLight3D
        {
            LightColor = night ? new Color(0.55f, 0.6f, 0.95f) : new Color(1f, 0.94f, 0.82f),
            LightEnergy = night ? 0.3f : 1.1f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 220f,
            RotationDegrees = new Vector3(-50f, -30f, 0f),
        });
    }
}
