using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.PetCity;

/// <summary>What the view puts down for one piece of Pet City.</summary>
public enum PieceKind
{
    /// <summary>Lucy's Pet Store (the <see cref="BuildingKind.PetStore"/> building).</summary>
    PetStore,
    /// <summary>A tall city block (a <see cref="BuildingKind.CityBlock"/> building).</summary>
    CityBlock,
}

/// <summary>
/// One piece the view draws: a model standing at <see cref="Position"/> (its foot, on the lowest
/// ground under its footprint), turned by <see cref="Yaw"/> (radians about +Y; at 0 its front
/// faces -Z), and sized <see cref="Size"/> (width along its local X, height, depth along its local Z).
/// </summary>
public readonly struct Piece
{
    public readonly PieceKind Kind;
    /// <summary>The index in <see cref="Buildings.All"/> it draws.</summary>
    public readonly int BuildingIndex;
    public readonly Vector3 Position;
    public readonly float Yaw;
    public readonly Vector3 Size;
    /// <summary>Upper stories above the shop floor (city blocks; 0 otherwise).</summary>
    public readonly int Stories;
    /// <summary>Which color scheme and roof it gets (0..<see cref="PetCityLayout.StyleCount"/>-1).</summary>
    public readonly int Style;

    public Piece(PieceKind kind, int buildingIndex, Vector3 position, float yaw, Vector3 size, int stories, int style)
    {
        Kind = kind;
        BuildingIndex = buildingIndex;
        Position = position;
        Yaw = yaw;
        Size = size;
        Stories = stories;
        Style = style;
    }
}

/// <summary>
/// One solid prop of the Map the view draws (a lamp, bench, planter, statue, ...): the
/// <see cref="Props.All"/> prop <see cref="PropIndex"/>, standing at <see cref="Position"/> (its x,
/// z; y the lowest ground under it), turned by <see cref="Yaw"/> (its <see cref="Prop.Rotation"/>),
/// <see cref="Radius"/> (its obstacle radius; half the length of a bench) and <see cref="Height"/> as the Map says.
/// </summary>
public readonly struct CityProp
{
    public readonly PropKind Kind;
    /// <summary>The index in <see cref="Props.All"/> it draws.</summary>
    public readonly int PropIndex;
    public readonly Vector3 Position;
    public readonly float Yaw;
    public readonly float Radius;
    public readonly float Height;

    public CityProp(PropKind kind, int propIndex, Vector3 position, float yaw, float radius, float height)
    {
        Kind = kind;
        PropIndex = propIndex;
        Position = position;
        Yaw = yaw;
        Radius = radius;
        Height = height;
    }
}

/// <summary>
/// Everything Pet City draws, computed once from the Map Contract: one piece per
/// <see cref="Buildings.All"/> building of <see cref="Area.PetCity"/>, exactly on its footprint, in
/// the Map's order; and one <see cref="CityProp"/> per
/// <see cref="Props.All"/> prop of <see cref="Area.PetCity"/>, in the Map's order (Pet City places
/// nothing solid of its own). Fixed data, the same every run.
/// </summary>
public static class PetCityLayout
{
    /// <summary>A city block's shop floor (meters), as the models are built.</summary>
    public const float GroundFloorHeight = 4f;

    /// <summary>An upper story's height (meters), as the models are built.</summary>
    public const float StoryHeight = 3f;

    /// <summary>The roof on top of a city block's last story (meters), as the models are built.</summary>
    public const float RoofHeight = 3f;

    /// <summary>Fewest and most upper stories a city block model has (models exist for each).</summary>
    public const int MinStories = 3, MaxStories = 10;

    /// <summary>Number of color schemes and roofs.</summary>
    public const int StyleCount = 4;

    /// <summary>The pet store's counter: width along the store's front and depth (meters).</summary>
    public const float CounterWidth = 5f, CounterDepth = 1f;

    private static readonly Piece[] s_pieces = Build();
    private static readonly CityProp[] s_props = BuildProps();

    /// <summary>Every piece: exactly the Map's Pet City buildings, in <see cref="Buildings.All"/> order.</summary>
    public static ReadOnlySpan<Piece> Pieces => s_pieces;

    /// <summary>Every Pet City prop of the Map, in <see cref="Props.All"/> order: exactly what the view draws.</summary>
    public static ReadOnlySpan<CityProp> CityProps => s_props;

    /// <summary>Upper stories that fit a city block of <paramref name="height"/> meters, within the models'.</summary>
    public static int StoriesFor(float height) =>
        Math.Clamp((int)MathF.Round((height - GroundFloorHeight - RoofHeight) / StoryHeight), MinStories, MaxStories);

    /// <summary>The height of a city block model with <paramref name="stories"/> upper stories, before scaling.</summary>
    public static float ModelHeight(int stories) => GroundFloorHeight + stories * StoryHeight + RoofHeight;

    /// <summary>
    /// The pet store counter's center (x, z on the ground) and yaw: along the inside of the store's
    /// open front, its customer side toward <see cref="Places.LucySpot"/>, right behind it.
    /// </summary>
    public static Place Counter
    {
        get
        {
            var spot = Places.LucySpot;
            var p = spot.Position - spot.Facing * (Places.FrontGap + CounterDepth * 0.5f);
            return new Place(p.X, p.Z, spot.Yaw);
        }
    }

    /// <summary>The lowest ground height under a footprint (its corners, edge middles and center).</summary>
    public static float FootHeight(in Building b)
    {
        float low = float.MaxValue;
        for (int i = -1; i <= 1; i++)
        for (int j = -1; j <= 1; j++)
        {
            var p = b.ToWorld(i * b.Width * 0.5f, j * b.Depth * 0.5f);
            low = MathF.Min(low, Ground.HeightAt(p.X, p.Y));
        }
        return low;
    }

    /// <summary>
    /// The petcity.glb object that draws a prop of <paramref name="kind"/>, and the radius (half the
    /// length of a bench) and height it is built at, which the view scales to each prop's own; null
    /// for a kind Pet City has no model of.
    /// </summary>
    public static (string Name, float Radius, float Height)? PropModel(PropKind kind) => kind switch
    {
        PropKind.Lamp => ("CityLamp", 0.3f, 2.6f),
        PropKind.Bench => ("Bench", 0.8f, 0.9f),
        PropKind.Planter => ("Planter", 0.6f, 0.7f),
        PropKind.Statue => ("Statue", 1.1f, 3.4f),
        _ => null,
    };

    /// <summary>The lowest ground height under a prop (its center and four points on its radius).</summary>
    public static float FootHeight(in Prop p)
    {
        float low = Ground.HeightAt(p.X, p.Z);
        for (int k = 0; k < 4; k++)
        {
            float a = k * MathF.PI * 0.5f;
            low = MathF.Min(low, Ground.HeightAt(p.X + MathF.Sin(a) * p.Radius, p.Z + MathF.Cos(a) * p.Radius));
        }
        return low;
    }

    private static CityProp[] BuildProps()
    {
        var all = Props.All;
        var props = new List<CityProp>();
        for (int i = 0; i < all.Length; i++)
        {
            var p = all[i];
            if (p.Area == Area.PetCity)
                props.Add(new CityProp(p.Kind, i, new Vector3(p.X, FootHeight(p), p.Z), p.Rotation, p.Radius, p.Height));
        }
        return props.ToArray();
    }

    private static Piece[] Build()
    {
        var all = Buildings.All;
        int count = 0;
        foreach (var b in all)
            if (b.Area == Area.PetCity)
                count++;
        var pieces = new Piece[count];
        int n = 0, blocks = 0;
        for (int i = 0; i < all.Length; i++)
        {
            var b = all[i];
            if (b.Area != Area.PetCity)
                continue;
            var at = new Vector3(b.X, FootHeight(b), b.Z);
            var size = new Vector3(b.Width, b.Height, b.Depth);
            pieces[n++] = b.Kind switch
            {
                BuildingKind.PetStore => new Piece(PieceKind.PetStore, i, at, b.Rotation, size, 0, 0),
                BuildingKind.CityBlock => new Piece(PieceKind.CityBlock, i, at, b.Rotation, size, StoriesFor(b.Height), blocks++ % StyleCount),
                _ => throw new InvalidOperationException($"Pet City has no drawing for a {b.Kind}."),
            };
        }
        return pieces;
    }
}
