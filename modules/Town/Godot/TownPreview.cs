using System;
using Fungtopia.DayNight;
using Fungtopia.Map;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Town.View;

/// <summary>
/// A stand-alone scene for screenshots: <c>./screenshot.sh modules/Town/Godot/Preview.tscn</c>.
/// It runs a real <see cref="TownModule"/> with a <see cref="TownView"/> on the Map's ground (the
/// Map module's scene, loaded read-only) under a simple sky. <c>TOWN_PREVIEW=night</c> sets the
/// clock to 22:00 (lamps lit, a dark blue night), anything else is noon. <c>TOWN_VIEW</c> picks the
/// camera: <c>square</c> (default, over the town from its entrance), <c>home</c> (your house and
/// Millie's), <c>clinic</c> (the clinic and the furniture stall), <c>fountain</c>, <c>houses</c>.
/// </summary>
public partial class TownPreview : Node3D
{
    private sealed class SilentBus : IMessageBus
    {
        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct { }
        public void Publish<T>(in T message) where T : struct { }
    }

    private readonly TownModule _town = new();
    private long _tick;

    public override void _Ready()
    {
        bool night = (OS.GetEnvironment("TOWN_PREVIEW") ?? "").ToLowerInvariant() == "night";
        _town.Start(new SilentBus());
        float hour = night ? 22f : 12f;
        _town.Handle(new TimeOfDay(1, hour, TimeOfDay.IsNightAt(hour)));

        BuildSky(night);
        var map = GD.Load<PackedScene>("res://modules/Map/Godot/Main.tscn");
        if (map != null)
            AddChild(map.Instantiate());
        var view = (TownView)GD.Load<PackedScene>("res://modules/Town/Godot/Main.tscn").Instantiate();
        view.Bind(_town);
        AddChild(view);

        var camera = new Camera3D { Fov = 55f, Far = 800f };
        AddChild(camera);
        var (from, to) = (OS.GetEnvironment("TOWN_VIEW") ?? "").ToLowerInvariant() switch
        {
            "home" => (new Vector3(-4f, 7f, 205f), new Vector3(-22f, 3.5f, 226f)),
            "clinic" => (new Vector3(6f, 6f, 200f), new Vector3(22f, 3f, 222f)),
            "fountain" => (new Vector3(7f, 4.5f, 203f), new Vector3(0f, 1.2f, 215f)),
            "houses" => (new Vector3(-30f, 7f, 192f), new Vector3(-50f, 3f, 176f)),
            _ => (new Vector3(20f, 24f, 168f), new Vector3(-4f, 0f, 222f)),
        };
        camera.LookAtFromPosition(from + new Vector3(0f, Ground.HeightAt(from.X, from.Z), 0f), to);
        camera.Current = true;
    }

    public override void _PhysicsProcess(double delta) => _town.Tick(new TickContext(_tick++, 1.0 / 60));

    private void BuildSky(bool night)
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky
            {
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = night ? new Color(0.04f, 0.06f, 0.18f) : new Color(0.45f, 0.65f, 0.95f),
                    SkyHorizonColor = night ? new Color(0.16f, 0.2f, 0.4f) : new Color(0.85f, 0.9f, 1.0f),
                    GroundBottomColor = new Color(0.1f, 0.12f, 0.1f),
                    GroundHorizonColor = night ? new Color(0.16f, 0.2f, 0.4f) : new Color(0.8f, 0.85f, 0.75f),
                },
            },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = night ? new Color(0.3f, 0.36f, 0.68f) : new Color(0.62f, 0.58f, 0.72f),
            AmbientLightEnergy = night ? 0.45f : 0.9f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapWhite = 6f,
            GlowEnabled = true,
            GlowBloom = 0.05f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        AddChild(new DirectionalLight3D
        {
            LightColor = night ? new Color(0.62f, 0.72f, 1.0f) : new Color(1.0f, 0.92f, 0.78f),
            LightEnergy = night ? 0.35f : 1.3f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 150f,
            RotationDegrees = night ? new Vector3(-30f, 160f, 0f) : new Vector3(-50f, -30f, 0f),
        });
    }
}
