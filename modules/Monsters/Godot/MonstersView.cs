using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Monsters.View;

/// <summary>
/// Draws every monster of the Monsters Core: its model placed between the last two fixed ticks,
/// turned to its facing, and animated from its motion, hits, poison, bites and defeat.
/// </summary>
public partial class MonstersView : Node3D, IModuleView
{
    private MonstersModule? _monsters;
    private MonsterModel[] _models = Array.Empty<MonsterModel>();
    private Vector3[] _previous = Array.Empty<Vector3>();
    private Vector3[] _current = Array.Empty<Vector3>();
    private float[] _previousFacing = Array.Empty<float>();
    private float[] _currentFacing = Array.Empty<float>();
    private int[] _ids = Array.Empty<int>();
    private long _seenTicks = -1;

    public void Bind(IModule module) => _monsters = (MonstersModule)module;

    public override void _Ready()
    {
        if (_monsters == null)
            return;
        var monsters = _monsters.Monsters;
        int n = monsters.Length;
        _models = new MonsterModel[n];
        _previous = new Vector3[n];
        _current = new Vector3[n];
        _previousFacing = new float[n];
        _currentFacing = new float[n];
        _ids = new int[n];
        for (int i = 0; i < n; i++)
        {
            _models[i] = MonsterModel.Create(monsters[i].Kind);
            AddChild(_models[i]);
            _current[i] = _previous[i] = ToGodot(monsters[i].Position);
            _currentFacing[i] = _previousFacing[i] = monsters[i].Facing;
            _ids[i] = monsters[i].Id;
        }
        _seenTicks = _monsters.Ticks;
    }

    public override void _Process(double delta)
    {
        if (_monsters == null || _models.Length == 0)
            return;
        var monsters = _monsters.Monsters;
        if (_monsters.Ticks != _seenTicks)
        {
            // More than one tick since the last frame: start from the newest only.
            bool one = _monsters.Ticks == _seenTicks + 1;
            for (int i = 0; i < monsters.Length; i++)
            {
                var now = ToGodot(monsters[i].Position);
                _previous[i] = one ? _current[i] : now;
                _previousFacing[i] = one ? _currentFacing[i] : monsters[i].Facing;
                _current[i] = now;
                _currentFacing[i] = monsters[i].Facing;
            }
            _seenTicks = _monsters.Ticks;
        }

        float alpha = (float)Engine.GetPhysicsInterpolationFraction();
        for (int i = 0; i < monsters.Length; i++)
        {
            var m = monsters[i];
            var model = _models[i];
            if (m.Id != _ids[i])
            {
                // A new monster took the slot: it appears where it spawned, it does not glide there.
                _ids[i] = m.Id;
                _previous[i] = _current[i];
                _previousFacing[i] = _currentFacing[i];
                model.Reset();
            }
            var from = _previous[i];
            var to = _current[i];
            var position = from.DistanceTo(to) > 2f ? to : from.Lerp(to, alpha);
            float facing = Mathf.LerpAngle(_previousFacing[i], _currentFacing[i], alpha);
            model.Transform = new Transform3D(new Basis(Vector3.Up, facing), position);
            // Long gone after the poof: skip the work until the slot respawns.
            model.Visible = m.Alive || m.DeadSeconds < 2f;
            if (!model.Visible)
                continue;
            var v = m.Velocity;
            float speed = MathF.Sqrt(v.X * v.X + v.Z * v.Z);
            model.Animate((float)delta, speed, m.HurtSeconds, m.Poisoned, m.SinceBite, m.Alive, m.DeadSeconds,
                m.MaxHealth > 0 ? (float)m.Health / m.MaxHealth : 1f);
        }
    }

    private static Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
}
