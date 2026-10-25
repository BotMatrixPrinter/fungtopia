using System;
using System.Collections.Generic;
using Fungtopia.Map;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Town.View;

/// <summary>
/// Draws Mushroom Town from <see cref="TownLayout"/>: each Town building of the Map exactly where
/// the Map puts it, the Map's street lamps, benches, fences, planters and crates exactly where and as
/// large as the Map says, and small flower clumps, all from town.glb. The
/// street lamps' bulbs and every window light up while <see cref="TownModule.LampsLit"/> (night),
/// fading over a second or two, each lamp (and the food stall's lanterns) with a warm light of its own. Nothing else moves.
/// </summary>
public partial class TownView : Node3D, IModuleView
{
    private const float LampEnergy = 2.4f;
    private const float FadeSeconds = 1.5f;
    /// <summary>Where the bulb hangs, as a share of the lamp's height (2.62 m of the 3.2 m model).</summary>
    private const float LampLightHeight = 0.8f;
    /// <summary>The food stall's light: among its lanterns, under its cap (1.9 m of the 3 m model).</summary>
    private const float StallLightHeight = 0.63f;

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
            var fit = TownGround.Fit(p);
            node.Position = new Vector3(p.X, fit.Base, p.Z);
            node.Rotation = new Vector3(0f, p.Yaw, 0f);
            node.Scale = new Vector3(p.Width / model.X, fit.DrawnHeight / model.Y, p.Depth / model.Z);
            AddChild(node);
            if (TownGround.HasRootFlare(p.Piece))
                AddChild(RootFlare(p, fit));
            if (p.Piece is TownPiece.StreetLamp or TownPiece.FoodStall)
            {
                float share = p.Piece == TownPiece.StreetLamp ? LampLightHeight : StallLightHeight;
                var light = new OmniLight3D
                {
                    Name = $"LampLight_{_lights.Count}",
                    Position = node.Position + new Vector3(0f, fit.DrawnHeight * share, 0f),
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
        // How bright bulbs, glowing caps and windows glow, day to night: TownLook.
        TownModels.Glow?.SetShaderParameter("self_light", TownLook.GlowAt(lit));
        TownModels.Window?.SetShaderParameter("self_light", TownLook.WindowGlowAt(lit));
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
        TownPiece.ClothingShop => new(10f, 8f, 9f),
        TownPiece.FoodStall => new(4.2f, 3f, 2.4f),
        TownPiece.StreetLamp => new(0.8f, 3.2f, 0.8f),
        TownPiece.Bench => new(1.8f, 0.9f, 0.7f),
        TownPiece.Fence => new(3.5f, 0.8f, 0.2f),
        TownPiece.Planter => new(1.2f, 0.7f, 1.2f),
        TownPiece.Crate => new(0.71f, 0.8f, 0.71f),
        TownPiece.Flowers => Size(TownLayout.FlowersSize),
        _ => throw new ArgumentOutOfRangeException(nameof(piece)),
    };

    private static Vector3 Size(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    /// <summary>
    /// A stone skirt round the foot of a big stem, foundation or basin: as wide as its foot at the
    /// highest ground (just above it), widening down into the ground under the lowest, so it looks
    /// rooted however the ground falls away under it.
    /// </summary>
    private static MeshInstance3D RootFlare(in TownPlacement p, in GroundFit fit)
    {
        // The foot's radius (local X, Z) as town.py models it.
        (float rx, float rz, float flare, float lift) = p.Piece switch
        {
            TownPiece.StreetLamp => (p.Width * 0.4f, p.Depth * 0.4f, 1.45f, 0.05f),
            TownPiece.Planter => (p.Width * 0.47f, p.Depth * 0.47f, 1.18f, 0.04f),
            TownPiece.Fountain => (p.Width * 0.49f, p.Depth * 0.49f, 1.08f, 0.06f),
            _ => (p.Width * 0.4f + 0.25f, p.Depth * 0.4f + 0.25f, 1.1f, 0.1f),
        };
        float bottom = fit.Base - 0.05f, top = fit.High + lift;
        var mesh = new CylinderMesh
        {
            TopRadius = 1f,
            BottomRadius = flare,
            Height = 1f,
            RadialSegments = 32,
            Rings = 1,
            CapTop = false,
            CapBottom = false,
        };
        return new MeshInstance3D
        {
            Name = $"RootFlare_{p.Piece}",
            Mesh = mesh,
            MaterialOverride = TownModels.Surface("Stone"),
            Position = new Vector3(p.X, (bottom + top) * 0.5f, p.Z),
            Rotation = new Vector3(0f, p.Yaw, 0f),
            Scale = new Vector3(rx, top - bottom, rz),
        };
    }
}
