using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Camera.View;

/// <summary>
/// The game's 3D camera: made current at once (the Input module aims through the screen center
/// of the current camera) and placed from <see cref="CameraPose"/>, interpolated between the
/// last two fixed ticks.
/// </summary>
public partial class CameraView : Camera3D, IModuleView
{
    /// <summary>A pose jump farther than this between two ticks is shown as a cut, meters.</summary>
    private const float CutDistance = 3f;

    private CameraModule? _camera;
    private CameraPose _previous;
    private CameraPose _current;
    private long _seenTicks = -1;

    public void Bind(IModule module) => _camera = (CameraModule)module;

    public override void _Ready()
    {
        MakeCurrent();
        if (_camera == null)
            return;
        _current = _previous = _camera.Pose;
        _seenTicks = _camera.Ticks;
        Place(_current.Position, _current.Orientation);
        Fov = Mathf.RadToDeg(_current.FieldOfView);
    }

    public override void _Process(double delta)
    {
        if (_camera == null)
            return;
        if (_camera.Ticks != _seenTicks)
        {
            // More than one tick since the last frame: start from the newest only.
            _previous = _camera.Ticks == _seenTicks + 1 ? _current : _camera.Pose;
            _current = _camera.Pose;
            _seenTicks = _camera.Ticks;
        }
        float alpha = (float)Engine.GetPhysicsInterpolationFraction();
        var from = _previous.Position;
        var to = _current.Position;
        var position = System.Numerics.Vector3.Distance(from, to) > CutDistance ? to : System.Numerics.Vector3.Lerp(from, to, alpha);
        var orientation = System.Numerics.Quaternion.Slerp(_previous.Orientation, _current.Orientation, alpha);
        Place(position, orientation);
        Fov = Mathf.RadToDeg(_previous.FieldOfView + (_current.FieldOfView - _previous.FieldOfView) * alpha);
    }

    private void Place(System.Numerics.Vector3 p, System.Numerics.Quaternion q)
    {
        var rotation = new Quaternion(q.X, q.Y, q.Z, q.W).Normalized();
        GlobalTransform = new Transform3D(new Basis(rotation), new Vector3(p.X, p.Y, p.Z));
    }
}
