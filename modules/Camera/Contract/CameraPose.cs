using System.Numerics;

namespace Fungtopia.Camera;

/// <summary>
/// Where the third-person camera is at the end of a fixed tick, published by the Camera module
/// exactly once per tick.
/// </summary>
public readonly struct CameraPose
{
    /// <summary>World position of the camera, meters (Y up).</summary>
    public readonly Vector3 Position;

    /// <summary>
    /// World orientation of the camera; like a Godot Camera3D it looks along its local -Z, with
    /// local +Y up and local +X to the right of the screen.
    /// </summary>
    public readonly Quaternion Orientation;

    /// <summary>
    /// Yaw in radians, Godot's convention (like <c>PlayerState.Facing</c>): 0 looks toward -Z,
    /// positive turns left. The horizontal view direction is (-sin Yaw, 0, -cos Yaw).
    /// </summary>
    public readonly float Yaw;

    public CameraPose(Vector3 position, Quaternion orientation, float yaw)
    {
        Position = position;
        Orientation = orientation;
        Yaw = yaw;
    }
}
