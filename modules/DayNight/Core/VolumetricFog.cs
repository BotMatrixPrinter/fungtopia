namespace Fungtopia.DayNight;

/// <summary>
/// The global volumetric fog of the game's one WorldEnvironment. It is on, so other modules can
/// add local FogVolume nodes (the Deep Forest's fog), but has no density of its own: open areas
/// look exactly as without it. Short and coarse, which is cheap and enough for a cartoon look.
/// </summary>
public static class VolumetricFog
{
    /// <summary>Volumetric fog is enabled in the global environment.</summary>
    public const bool Enabled = true;

    /// <summary>Global density: none, only FogVolume nodes add fog.</summary>
    public const float Density = 0f;

    /// <summary>Metres the fog froxels reach from the camera (Godot's default is 64).</summary>
    public const float Length = 40f;

    /// <summary>How froxel detail spreads with distance (Godot's default is 2; higher puts more near the camera).</summary>
    public const float DetailSpread = 2f;

    /// <summary>Froxel grid width/height and depth (Godot's defaults are 64 and 64).</summary>
    public const int VolumeSize = 48, VolumeDepth = 32;

    /// <summary>No GI injected (the game has none); some ambient light, so shaded fog is not black.</summary>
    public const float GiInject = 0f, AmbientInject = 0.5f;
}
