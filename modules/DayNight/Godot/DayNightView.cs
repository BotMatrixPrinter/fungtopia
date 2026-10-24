using System;
using System.Globalization;
using Fungtopia.Map;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.DayNight.View;

/// <summary>
/// The one sun (or moon) light, sky and WorldEnvironment of the whole game, following the Core's
/// clock: warm morning, bright day, golden evening, blue night with a big moon, stars and soft
/// fog. In the forest the fog thickens to a green-gold haze and the ambient drops, so cap
/// shadows dapple the ground and glowing mushrooms read better.
/// </summary>
public partial class DayNightView : Node3D, IModuleView
{
    private const string Shaders = "res://modules/DayNight/Godot/Shaders/";

    /// <summary>The look of one moment of the day; the view blends between neighbours.</summary>
    private readonly struct Mood
    {
        public readonly float Hour;
        public readonly Color Top, Horizon, Ground, Light, Ambient, Fog;
        public readonly float AmbientEnergy, FogDensity, Stars, Glow;

        public Mood(float hour, Color top, Color horizon, Color ground, Color light, Color ambient, float ambientEnergy,
            Color fog, float fogDensity, float stars, float glow)
        {
            Hour = hour;
            Top = top;
            Horizon = horizon;
            Ground = ground;
            Light = light;
            Ambient = ambient;
            AmbientEnergy = ambientEnergy;
            Fog = fog;
            FogDensity = fogDensity;
            Stars = stars;
            Glow = glow;
        }
    }

    private static Color C(float r, float g, float b) => new(r, g, b);

    private static readonly Color NightTop = C(0.05f, 0.07f, 0.22f), NightHorizon = C(0.16f, 0.2f, 0.42f),
        NightGround = C(0.06f, 0.08f, 0.14f), Moonlight = C(0.62f, 0.72f, 1.0f), NightAmbient = C(0.3f, 0.36f, 0.68f),
        NightFog = C(0.18f, 0.24f, 0.46f);

    // Hours ascending from 0 to 24; the first and last are the same moment.
    private static readonly Mood[] Moods =
    {
        new(0f, NightTop, NightHorizon, NightGround, Moonlight, NightAmbient, 0.45f, NightFog, 0.011f, 1f, 1.1f),
        new(4.6f, NightTop, NightHorizon, NightGround, Moonlight, NightAmbient, 0.45f, NightFog, 0.011f, 1f, 1.1f),
        new(6f, C(0.32f, 0.36f, 0.66f), C(1.0f, 0.62f, 0.55f), C(0.35f, 0.3f, 0.35f), C(1.0f, 0.6f, 0.45f),
            C(0.6f, 0.5f, 0.66f), 0.7f, C(0.92f, 0.66f, 0.66f), 0.011f, 0.25f, 0.8f),
        new(8f, C(0.45f, 0.62f, 0.92f), C(1.0f, 0.84f, 0.68f), C(0.4f, 0.42f, 0.32f), C(1.0f, 0.86f, 0.66f),
            C(0.64f, 0.58f, 0.7f), 0.7f, C(0.95f, 0.86f, 0.78f), 0.0045f, 0f, 0.6f),
        new(11f, C(0.32f, 0.6f, 0.98f), C(0.8f, 0.92f, 1.0f), C(0.42f, 0.48f, 0.35f), C(1.0f, 0.97f, 0.9f),
            C(0.6f, 0.64f, 0.76f), 0.7f, C(0.78f, 0.88f, 1.0f), 0.002f, 0f, 0.5f),
        new(16f, C(0.32f, 0.6f, 0.98f), C(0.8f, 0.92f, 1.0f), C(0.42f, 0.48f, 0.35f), C(1.0f, 0.95f, 0.86f),
            C(0.6f, 0.64f, 0.76f), 0.7f, C(0.78f, 0.88f, 1.0f), 0.002f, 0f, 0.5f),
        new(18.6f, C(0.48f, 0.55f, 0.88f), C(1.0f, 0.74f, 0.45f), C(0.42f, 0.36f, 0.28f), C(1.0f, 0.74f, 0.42f),
            C(0.7f, 0.56f, 0.6f), 0.72f, C(0.98f, 0.78f, 0.6f), 0.007f, 0f, 0.7f),
        new(20f, C(0.24f, 0.22f, 0.5f), C(0.95f, 0.5f, 0.5f), C(0.22f, 0.18f, 0.25f), C(1.0f, 0.5f, 0.45f),
            C(0.48f, 0.4f, 0.64f), 0.65f, C(0.62f, 0.44f, 0.6f), 0.01f, 0.35f, 0.95f),
        new(21.4f, NightTop, NightHorizon, NightGround, Moonlight, NightAmbient, 0.45f, NightFog, 0.011f, 1f, 1.1f),
        new(24f, NightTop, NightHorizon, NightGround, Moonlight, NightAmbient, 0.45f, NightFog, 0.011f, 1f, 1.1f),
    };

    // The sun is up from 5.5 to 20.5 (highest at 13:00), the moon from 20.5 to 5.5.
    private const float SunRise = 5.5f, SunSet = 20.5f;
    private const float SunMaxElevation = 60f, MoonMaxElevation = 30f;
    private const float SunEnergy = 1.1f, MoonEnergy = 0.45f;

    private static readonly StringName TopColor = "top_color", HorizonColor = "horizon_color", GroundColor = "ground_color",
        SunDir = "sun_dir", SunColor = "sun_color", SunAmount = "sun_amount", MoonDir = "moon_dir",
        MoonAmount = "moon_amount", StarAmount = "star_amount";

    private DayNightModule? _core;
    private ShaderMaterial _sky = null!;
    private Godot.Environment _environment = null!;
    private DirectionalLight3D _light = null!;
    private float _forest;

    public void Bind(IModule module)
    {
        _core = (DayNightModule)module;
        // For screenshots: DAYNIGHT_HOUR=22 starts the clock at 22:00.
        var hour = OS.GetEnvironment("DAYNIGHT_HOUR");
        if (!string.IsNullOrEmpty(hour) && double.TryParse(hour, NumberStyles.Float, CultureInfo.InvariantCulture, out double h))
            _core.SetTime(_core.Day, h);
    }

    public override void _Ready()
    {
        _sky = new ShaderMaterial { Shader = GD.Load<Shader>(Shaders + "sky.gdshader") };
        _environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = _sky, RadianceSize = Sky.RadianceSizeEnum.Size64, ProcessMode = Sky.ProcessModeEnum.Incremental },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapWhite = 6f,
            GlowEnabled = true,
            GlowBloom = 0.05f,
            FogEnabled = true,
            FogSkyAffect = 0.25f,
            // On but empty, so other modules' FogVolume nodes (the Deep Forest) show; see VolumetricFog.
            VolumetricFogEnabled = VolumetricFog.Enabled,
            VolumetricFogDensity = VolumetricFog.Density,
            VolumetricFogLength = VolumetricFog.Length,
            VolumetricFogDetailSpread = VolumetricFog.DetailSpread,
            VolumetricFogGIInject = VolumetricFog.GiInject,
            VolumetricFogAmbientInject = VolumetricFog.AmbientInject,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.2f,
        };
        AddChild(new WorldEnvironment { Name = "WorldEnvironment", Environment = _environment });
        RenderingServer.EnvironmentSetVolumetricFogVolumeSize(VolumetricFog.VolumeSize, VolumetricFog.VolumeDepth);
        _light = new DirectionalLight3D
        {
            Name = "Sun",
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 90f,
            ShadowBlur = 1.5f,
        };
        AddChild(_light);
        Apply(0f);
    }

    public override void _Process(double delta) => Apply((float)delta);

    private void Apply(float delta)
    {
        float hour = _core?.HourF ?? 9f;
        bool inForest = _core?.PlayerArea == Area.Forest;
        _forest = Mathf.MoveToward(_forest, inForest ? 1f : 0f, delta * 0.5f);
        float forest = Mathf.SmoothStep(0f, 1f, _forest);

        int i = 0;
        while (i < Moods.Length - 2 && hour >= Moods[i + 1].Hour)
            i++;
        ref readonly var a = ref Moods[i];
        ref readonly var b = ref Moods[i + 1];
        float t = Mathf.SmoothStep(0f, 1f, (hour - a.Hour) / (b.Hour - a.Hour));

        // Sun and moon rise in the east (+X) and set in the west; the sun crosses the south (+Z, so it
        // lights what you see looking north up the roads), the big moon the north, in front of you.
        var sun = Arc(Phase(hour, SunRise, SunSet), SunMaxElevation, 1f, out float sunUp);
        var moon = Arc(Phase(hour, SunSet, SunRise + 24f), MoonMaxElevation, -1f, out float moonUp);
        float sunAmount = Mathf.SmoothStep(-0.03f, 0.18f, sunUp);
        float moonAmount = Mathf.SmoothStep(-0.03f, 0.18f, moonUp) * (1f - sunAmount);

        var lightColor = a.Light.Lerp(b.Light, t);
        bool byMoon = moonAmount > sunAmount;
        var toLight = byMoon ? moon : sun;
        _light.Basis = Basis.LookingAt(-toLight, Mathf.Abs(toLight.Y) > 0.99f ? Vector3.Forward : Vector3.Up);
        _light.LightColor = lightColor;
        _light.LightEnergy = byMoon ? MoonEnergy * moonAmount : SunEnergy * sunAmount * (1f - 0.12f * forest);
        _light.Visible = _light.LightEnergy > 0.01f;

        // The forest: a denser green-gold haze by day, a deeper teal one by night, less ambient.
        var forestFog = (byMoon ? C(0.12f, 0.28f, 0.34f) : C(0.68f, 0.78f, 0.52f));
        _environment.AmbientLightColor = a.Ambient.Lerp(b.Ambient, t).Lerp(C(0.5f, 0.6f, 0.62f), 0.3f * forest);
        _environment.AmbientLightEnergy = Mathf.Lerp(a.AmbientEnergy, b.AmbientEnergy, t) * (1f - 0.2f * forest);
        _environment.FogLightColor = a.Fog.Lerp(b.Fog, t).Lerp(forestFog, 0.45f * forest);
        _environment.FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, t) + 0.006f * forest;
        _environment.GlowIntensity = Mathf.Lerp(a.Glow, b.Glow, t) + 0.15f * forest;

        _sky.SetShaderParameter(TopColor, a.Top.Lerp(b.Top, t));
        _sky.SetShaderParameter(HorizonColor, a.Horizon.Lerp(b.Horizon, t));
        _sky.SetShaderParameter(GroundColor, a.Ground.Lerp(b.Ground, t));
        _sky.SetShaderParameter(SunDir, sun);
        _sky.SetShaderParameter(SunColor, a.Light.Lerp(b.Light, t));
        _sky.SetShaderParameter(SunAmount, Mathf.SmoothStep(-0.08f, 0.02f, sunUp));
        _sky.SetShaderParameter(MoonDir, moon);
        _sky.SetShaderParameter(MoonAmount, Mathf.SmoothStep(-0.08f, 0.04f, moonUp));
        _sky.SetShaderParameter(StarAmount, Mathf.Lerp(a.Stars, b.Stars, t));
    }

    /// <summary>How far (0..1) <paramref name="hour"/> is from <paramref name="rise"/> to <paramref name="set"/>; outside 0..1 when down.</summary>
    private static float Phase(float hour, float rise, float set)
    {
        if (hour < rise - 12f)
            hour += 24f;
        return (hour - rise) / (set - rise);
    }

    /// <summary>The unit direction toward a body at <paramref name="phase"/> of its arc; <paramref name="up"/> is its height (sin of elevation).</summary>
    private static Vector3 Arc(float phase, float maxElevation, float south, out float up)
    {
        float angle = phase * MathF.PI; // 0 east, pi/2 south (or north), pi west
        float elevation = MathF.Sin(angle) * Mathf.DegToRad(maxElevation);
        up = MathF.Sin(elevation);
        float flat = MathF.Cos(elevation);
        return new Vector3(MathF.Cos(angle) * flat, up, south * MathF.Sin(angle) * flat);
    }
}
