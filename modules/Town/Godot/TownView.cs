using System;
using System.Collections.Generic;
using Fungtopia.Map;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Town.View;

/// <summary>
/// Draws Mushroom Town from <see cref="TownLayout"/>: each Town building of the Map exactly where
/// the Map puts it, and the street lamps, benches, fences and flower beds, all from town.glb. The
/// street lamps' bulbs and every window light up while <see cref="TownModule.LampsLit"/> (night),
/// fading over a second or two, each lamp with a warm light of its own. Nothing else moves.
/// </summary>
public partial class TownView : Node3D, IModuleView
{
    /// <summary>How bright bulbs and windows glow by day (self light; the night value is <see cref="NightGlow"/>).</summary>
    public const float DayGlow = 0.12f;
    public const float NightGlow = 2.2f;
    private const float LampEnergy = 2.4f;
    private const float FadeSeconds = 1.5f;

    private TownModule? _town;
    private readonly List<OmniLight3D> _lights = new();
    private float _lit = -1f;

    public void Bind(IModule module) => _town = (TownModule)module;

    public override void _Ready()
    {
        foreach (var p in TownLayout.All)
        {
            var node = TownModels.Instance(ModelName(p));
            if (node == null)
                continue;
            var model = ModelSize(p.Piece);
            node.Name = $"{ModelName(p)}_{GetChildCount()}";
            node.Position = new Vector3(p.X, BaseHeight(p), p.Z);
            node.Rotation = new Vector3(0f, p.Yaw, 0f);
            node.Scale = new Vector3(p.Width / model.X, p.Height / model.Y, p.Depth / model.Z);
            AddChild(node);
            if (p.Piece == TownPiece.StreetLamp)
            {
                var light = new OmniLight3D
                {
                    Name = $"LampLight_{_lights.Count}",
                    Position = node.Position + new Vector3(0f, 2.55f, 0f),
                    LightColor = new Color(1f, 0.78f, 0.45f),
                    OmniRange = 10f,
                    OmniAttenuation = 1.2f,
                    ShadowEnabled = false,
                    Visible = false,
                };
                _lights.Add(light);
                AddChild(light);
            }
        }
        Apply(_town?.LampsLit == true ? 1f : 0f);
    }

    public override void _ExitTree() => TownModels.Release();

    public override void _Process(double delta)
    {
        float target = _town?.LampsLit == true ? 1f : 0f;
        if (_lit != target)
            Apply(Mathf.MoveToward(_lit, target, (float)delta / FadeSeconds));
    }

    private void Apply(float lit)
    {
        _lit = lit;
        float glow = Mathf.Lerp(DayGlow, NightGlow, lit);
        TownModels.Glow?.SetShaderParameter("self_light", glow);
        TownModels.Window?.SetShaderParameter("self_light", glow * 0.7f);
        foreach (var light in _lights)
        {
            light.Visible = lit > 0f;
            light.LightEnergy = LampEnergy * lit;
        }
    }

    /// <summary>The object of town.glb a placement draws.</summary>
    public static string ModelName(in TownPlacement p) =>
        p.Piece == TownPiece.House ? $"House{p.Variant}" : p.Piece.ToString();

    /// <summary>The size (width, height, depth) each object is modelled at in town.py.</summary>
    private static Vector3 ModelSize(TownPiece piece) => piece switch
    {
        TownPiece.PlayerHouse => new(12f, 10f, 12f),
        TownPiece.MillieHouse => new(10f, 8f, 10f),
        TownPiece.Clinic => new(12f, 8.5f, 10f),
        TownPiece.FurnitureShop => new(7f, 3.5f, 4f),
        TownPiece.Fountain => new(7f, 2.2f, 7f),
        TownPiece.House => new(9f, 7f, 9f),
        TownPiece.StreetLamp => Size(TownLayout.LampSize),
        TownPiece.Bench => Size(TownLayout.BenchSize),
        TownPiece.Fence => Size(TownLayout.FenceSize),
        TownPiece.FlowerBed => Size(TownLayout.FlowerBedSize),
        _ => throw new ArgumentOutOfRangeException(nameof(piece)),
    };

    private static Vector3 Size(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    /// <summary>The lowest ground under a building's footprint (its foundation reaches down into higher ground), or under a prop's center.</summary>
    private static float BaseHeight(in TownPlacement p)
    {
        if (p.BuildingIndex < 0)
            return Ground.HeightAt(p.X, p.Z);
        var b = Buildings.All[p.BuildingIndex];
        float low = Ground.HeightAt(b.X, b.Z);
        for (int i = -1; i <= 1; i++)
        for (int j = -1; j <= 1; j++)
        {
            var w = b.ToWorld(i * b.Width * 0.4f, j * b.Depth * 0.4f);
            low = MathF.Min(low, Ground.HeightAt(w.X, w.Y));
        }
        return low;
    }
}
