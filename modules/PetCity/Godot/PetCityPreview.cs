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
/// entrance), "store" (Lucy's Pet Store up close), "block", "props" (the statue, benches and lamps on the plaza's west side), "fungi" (close up along a city block's side, where its little fungi grow), "planter" (close up on a planter's and a lamp's fungi); add "night" (for example
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
        if (mode.Contains("slope"))
        {
            // The building with the most drop under its footprint, from its low corner.
            var steep = PetCityLayout.Pieces[0];
            foreach (var piece in PetCityLayout.Pieces)
                if (piece.Footing.High - piece.Footing.Low > steep.Footing.High - steep.Footing.Low)
                    steep = piece;
            var b = Buildings.All[steep.BuildingIndex];
            var low = b.Center;
            float lowest = float.MaxValue;
            for (int i = 0; i < Planting.SidePoints; i++)
            for (int j = 0; j < Planting.SidePoints; j++)
            {
                var q = Planting.GridPoint(b, i, j);
                float h = Ground.HeightAt(q.X, q.Y);
                if (h < lowest)
                {
                    lowest = h;
                    low = q;
                }
            }
            var outward = System.Numerics.Vector2.Normalize(low - b.Center);
            var eye = low + outward * 9f + new System.Numerics.Vector2(outward.Y, -outward.X) * 4f;
            camera.LookAtFromPosition(new Vector3(eye.X, Ground.HeightAt(eye.X, eye.Y) + 1.6f, eye.Y),
                new Vector3(low.X, lowest + 1.2f, low.Y), Vector3.Up);
        }
        else if (mode.Contains("fungi"))
        {
            // Along a city block's side, low down, where a row of little fungi grows by its plinth.
            var b = Buildings.All[PetCityLayout.Pieces[1].BuildingIndex];
            float hw = b.Width * 0.5f, hd = b.Depth * 0.5f;
            var eye = b.ToWorld(hw + 2.4f, -hd - 1.4f);
            var look = b.ToWorld(hw + 0.5f, hd * 0.1f);
            camera.LookAtFromPosition(new Vector3(eye.X, Ground.HeightAt(eye.X, eye.Y) + 1f, eye.Y),
                new Vector3(look.X, Ground.HeightAt(look.X, look.Y) + 0.1f, look.Y), Vector3.Up);
        }
        else if (mode.Contains("planter"))
        {
            // Close up on the little fungi: a planter on the avenue, a lamp's foot and post, a building's foot behind.
            // The planter nearest the plaza.
            var planter = PetCityLayout.CityProps[0];
            float best = float.MaxValue;
            foreach (var p in PetCityLayout.CityProps)
            {
                float d = MathF.Abs(p.Position.Z - plaza.Z) + MathF.Abs(p.Position.X - plaza.X);
                if (p.Kind == PropKind.Planter && d < best)
                {
                    best = d;
                    planter = p;
                }
            }
            var at = G(planter.Position) + new Vector3(0f, planter.Height, 0f);
            float side = MathF.Sign(planter.Position.X);
            camera.LookAtFromPosition(at + new Vector3(-side * 1.2f, 0.5f, -1.9f), at + new Vector3(side * 0.6f, -0.35f, 2.5f), Vector3.Up);
        }
        else if (mode.Contains("store"))
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
