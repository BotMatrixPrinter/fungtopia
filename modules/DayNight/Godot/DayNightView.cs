using System;
using System.Globalization;
using Fungtopia.Map;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.DayNight.View;

/// <summary>
/// The one sun (or moon) light, sky and WorldEnvironment of the whole game, following the Core's
/// clock and the palette in Core's Lighting: pink dawn, bright day, golden evening, pink-purple
/// dusk, deep blue-purple night with a big moon, stars and soft
/// fog. In the forest the fog thickens to a green-gold haze and the ambient drops, so cap
/// shadows dapple the ground and glowing mushrooms read better. While the player sleeps
/// (<see cref="DayNightModule.Sleeping"/>) the screen fades to black and back after waking.
/// </summary>
public partial class DayNightView : Node3D, IModuleView
{
    private const string Shaders = "res://modules/DayNight/Godot/Shaders/";

    private static Color C(in Rgb c) => new(c.R, c.G, c.B);

    // The sun is up from 5.5 to 20.5 (highest at 13:00), the moon from 20.5 to 5.5.
    private const float SunRise = 5.5f, SunSet = 20.5f;
    private const float SunMaxElevation = 60f, MoonMaxElevation = 30f;

    private static readonly StringName TopColor = "top_color", HorizonColor = "horizon_color", GroundColor = "ground_color",
        SunDir = "sun_dir", SunColor = "sun_color", SunAmount = "sun_amount", MoonDir = "moon_dir",
        MoonAmount = "moon_amount", StarAmount = "star_amount";

    private DayNightModule? _core;
    private ShaderMaterial _sky = null!;
    private Godot.Environment _environment = null!;
    private DirectionalLight3D _light = null!;
    private float _forest;
    private ColorRect _sleepFade = null!;
    private float _sleep;

    /// <summary>Seconds the sleep fade takes to darken fully, and to clear after waking (as the faint's fade).</summary>
    private const float SleepFadeIn = 1.2f, SleepFadeOut = 2f;

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
            AdjustmentSaturation = Lighting.AdjustmentSaturation,
            AdjustmentContrast = Lighting.AdjustmentContrast,
            AdjustmentBrightness = Lighting.AdjustmentBrightness,
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

        // The fade to black while sleeping, over the panels (layer 11) so the bed's prompt hides too.
        var layer = new CanvasLayer { Name = "SleepFade", Layer = 12 };
        _sleepFade = new ColorRect { Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _sleepFade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_sleepFade);
        AddChild(layer);
        Apply(0f);
    }

    public override void _Process(double delta)
    {
        Apply((float)delta);
        bool sleeping = _core?.Sleeping ?? false;
        _sleep = Mathf.MoveToward(_sleep, sleeping ? 1f : 0f, (float)delta / (sleeping ? SleepFadeIn : SleepFadeOut));
        _sleepFade.Visible = _sleep > 0f;
        _sleepFade.Color = new Color(0f, 0f, 0f, 1f - (1f - _sleep) * (1f - _sleep));
    }

    private void Apply(float delta)
    {
        float hour = _core?.HourF ?? 9f;
        bool inForest = _core?.PlayerArea == Area.Forest;
        _forest = Mathf.MoveToward(_forest, inForest ? 1f : 0f, delta * 0.5f);
        float forest = Mathf.SmoothStep(0f, 1f, _forest);

        var mood = Lighting.At(hour);

        // Sun and moon rise in the east (+X) and set in the west; the sun crosses the south (+Z, so it
        // lights what you see looking north up the roads), the big moon the north, in front of you.
        var sun = Arc(Phase(hour, SunRise, SunSet), SunMaxElevation, 1f, out float sunUp);
        var moon = Arc(Phase(hour, SunSet, SunRise + 24f), MoonMaxElevation, -1f, out float moonUp);
        float sunAmount = Mathf.SmoothStep(-0.03f, 0.18f, sunUp);
        float moonAmount = Mathf.SmoothStep(-0.03f, 0.18f, moonUp) * (1f - sunAmount);

        var lightColor = C(mood.Light);
        bool byMoon = moonAmount > sunAmount;
        var toLight = byMoon ? moon : sun;
        _light.Basis = Basis.LookingAt(-toLight, Mathf.Abs(toLight.Y) > 0.99f ? Vector3.Forward : Vector3.Up);
        _light.LightColor = lightColor;
        _light.LightEnergy = byMoon ? Lighting.MoonEnergy * moonAmount : Lighting.SunEnergy * sunAmount * (1f - 0.12f * forest);
        _light.Visible = _light.LightEnergy > 0.01f;

        // The forest: a denser green-gold haze by day, a deeper teal one by night, less ambient.
        var forestFog = byMoon ? Lighting.ForestFogNight : Lighting.ForestFogDay;
        _environment.AmbientLightColor = C(mood.Ambient.VividLerp(Lighting.ForestAmbient, 0.3f * forest));
        _environment.AmbientLightEnergy = mood.AmbientEnergy * (1f - 0.2f * forest);
        _environment.FogLightColor = C(mood.Fog.VividLerp(forestFog, 0.45f * forest));
        _environment.FogDensity = mood.FogDensity + 0.006f * forest;
        _environment.GlowIntensity = mood.Glow + 0.15f * forest;

        _sky.SetShaderParameter(TopColor, C(mood.Top));
        _sky.SetShaderParameter(HorizonColor, C(mood.Horizon));
        _sky.SetShaderParameter(GroundColor, C(mood.Ground));
        _sky.SetShaderParameter(SunDir, sun);
        _sky.SetShaderParameter(SunColor, lightColor);
        _sky.SetShaderParameter(SunAmount, Mathf.SmoothStep(-0.08f, 0.02f, sunUp));
        _sky.SetShaderParameter(MoonDir, moon);
        _sky.SetShaderParameter(MoonAmount, Mathf.SmoothStep(-0.08f, 0.04f, moonUp));
        _sky.SetShaderParameter(StarAmount, mood.Stars);
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
