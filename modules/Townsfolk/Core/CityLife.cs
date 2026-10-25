using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.Townsfolk;

/// <summary>A place in Pet City someone goes to: where to stand (x, z) and which way to face there.</summary>
public readonly struct Spot
{
    /// <summary>Where to stand (x, z).</summary>
    public readonly Vector2 At;

    /// <summary>The yaw to face there (0 faces -Z).</summary>
    public readonly float Yaw;

    /// <summary>The <see cref="Buildings.All"/> index of the building it belongs to, or the <see cref="Props.All"/> index of a bench (seats), else -1.</summary>
    public readonly int Index;

    public Spot(Vector2 at, float yaw, int index)
    {
        At = at;
        Yaw = yaw;
        Index = index;
    }
}

/// <summary>
/// Where life happens in Pet City, worked out once from the Map's fixed data (the same every run):
/// building doors, shop windows, bench seats, open ground on the plaza and the avenue, rooftops
/// for crows and spots for butterflies. Every standing spot is in <see cref="Area.PetCity"/>,
/// walkable and clear of obstacles for a <see cref="TownsfolkModule.FolkRadius"/> walker.
/// </summary>
public static class CityLife
{
    /// <summary>How far in front of a building's front its door spot is (meters).</summary>
    public const float DoorGap = 1.3f;

    /// <summary>How far round the plaza's center counts as the plaza (meters).</summary>
    public const float PlazaRadius = 17f;

    /// <summary>Spacing of the open-ground grid (meters).</summary>
    public const float GridStep = 2.5f;

    private static readonly Spot[] s_doors;
    private static readonly Spot[] s_windows;
    private static readonly Spot[] s_seats;
    private static readonly Vector2[] s_seatApproach;
    private static readonly Vector2[] s_open;
    private static readonly int s_busyCount;
    private static readonly Vector3[] s_roofs;
    private static readonly Vector3[] s_flowers;

    static CityLife()
    {
        var doors = new List<Spot>();
        var windows = new List<Spot>();
        var roofs = new List<Vector3>();
        var buildings = Buildings.All;
        for (int b = 0; b < buildings.Length; b++)
        {
            var building = buildings[b];
            if (building.Area != Area.PetCity)
                continue;
            float inward = building.Rotation + MathF.PI;
            float front = -building.Depth * 0.5f;
            if (building.Kind == BuildingKind.PetStore)
            {
                // Lucy stands in the middle of the store's open front: customers go in to her sides.
                TryAdd(doors, building.ToWorld(-4.5f, front - DoorGap), inward, b);
                TryAdd(doors, building.ToWorld(4.5f, front - DoorGap), inward, b);
            }
            else
            {
                TryAdd(doors, building.ToWorld(0f, front - DoorGap), inward, b);
                TryAdd(windows, building.ToWorld(-3.4f, front - 1.5f), inward, b);
                TryAdd(windows, building.ToWorld(3.4f, front - 1.5f), inward, b);
            }
            float top = float.MinValue;
            for (int i = -1; i <= 1; i += 2)
            for (int j = -1; j <= 1; j += 2)
            {
                var corner = building.ToWorld(i * building.Width * 0.5f, j * building.Depth * 0.5f);
                top = MathF.Max(top, Ground.HeightAt(corner.X, corner.Y));
            }
            roofs.Add(new Vector3(building.X, top + 0.3f + building.Height, building.Z));
        }

        var seats = new List<Spot>();
        var approach = new List<Vector2>();
        var flowers = new List<Vector3>();
        var props = Props.All;
        for (int p = 0; p < props.Length; p++)
        {
            var prop = props[p];
            if (prop.Area != Area.PetCity)
                continue;
            if (prop.Kind == PropKind.Planter)
                flowers.Add(new Vector3(prop.X, Ground.HeightAt(prop.X, prop.Z) + prop.Height, prop.Z));
            if (prop.Kind != PropKind.Bench)
                continue;
            for (int k = -1; k <= 1; k += 2)
            {
                var seat = prop.Center + prop.Along * (k * prop.Radius * 0.45f);
                var stand = seat + prop.Front * (prop.Radius + 0.45f);
                if (!Clear(stand, TownsfolkModule.FolkRadius))
                    continue;
                seats.Add(new Spot(seat + prop.Front * 0.12f, prop.Rotation, p));
                approach.Add(stand);
            }
        }
        // Flowers on the plaza, too: butterflies dance round its middle and its benches.
        var plaza = Buildings.CityPlaza;
        for (int a = 0; a < 6; a++)
        {
            float angle = a * MathF.Tau / 6f + 0.3f;
            float x = plaza.X + MathF.Sin(angle) * 7f, z = plaza.Y + MathF.Cos(angle) * 7f;
            flowers.Add(new Vector3(x, Ground.HeightAt(x, z), z));
        }

        // Open ground: the plaza and the avenue first (where most people go), then the rest.
        var busy = new List<Vector2>();
        var quiet = new List<Vector2>();
        var bounds = Areas.PetCity;
        for (float z = bounds.MinZ + GridStep * 0.5f; z < bounds.MaxZ; z += GridStep)
        for (float x = bounds.MinX + GridStep * 0.5f; x < bounds.MaxX; x += GridStep)
        {
            var at = new Vector2(x, z);
            if (!Clear(at, 1.0f) || !InCity(at, 3f))
                continue;
            if (Vector2.Distance(at, plaza) < PlazaRadius || Props.CityAvenueDistance(x, z) < Props.CityAvenueHalfWidth + 1f)
                busy.Add(at);
            else
                quiet.Add(at);
        }
        s_busyCount = busy.Count;
        busy.AddRange(quiet);
        s_open = busy.ToArray();

        s_doors = doors.ToArray();
        s_windows = windows.ToArray();
        s_seats = seats.ToArray();
        s_seatApproach = approach.ToArray();
        s_roofs = roofs.ToArray();
        s_flowers = flowers.ToArray();
    }

    private static void TryAdd(List<Spot> spots, Vector2 at, float yaw, int index)
    {
        if (Clear(at, TownsfolkModule.FolkRadius) && InCity(at))
            spots.Add(new Spot(at, yaw, index));
    }

    /// <summary>Pet City's doors folk go in by: one in front of each city block, two at the pet store's open front (beside Lucy).</summary>
    public static ReadOnlySpan<Spot> Doors => s_doors;

    /// <summary>Shop windows to look into: two beside each city block's door, facing the building.</summary>
    public static ReadOnlySpan<Spot> Windows => s_windows;

    /// <summary>Two seats on each Pet City bench (on the bench, facing its front).</summary>
    public static ReadOnlySpan<Spot> Seats => s_seats;

    /// <summary>Where to stand before sitting down on each seat: in front of it, clear of the bench.</summary>
    public static ReadOnlySpan<Vector2> SeatApproach => s_seatApproach;

    /// <summary>Open ground (a <see cref="GridStep"/> grid, clear with 1 m to spare): the plaza and avenue first (<see cref="BusyCount"/>), then the rest.</summary>
    public static ReadOnlySpan<Vector2> Open => s_open;

    /// <summary>How many of <see cref="Open"/> are on the plaza or the avenue.</summary>
    public static int BusyCount => s_busyCount;

    /// <summary>The top middle of each Pet City building (where crows perch).</summary>
    public static ReadOnlySpan<Vector3> Roofs => s_roofs;

    /// <summary>What butterflies flutter over: each Pet City planter's top and points round the plaza's middle.</summary>
    public static ReadOnlySpan<Vector3> Flowers => s_flowers;

    /// <summary>Whether (x, z) is in Pet City, <paramref name="margin"/> meters inside its rectangle.</summary>
    public static bool InCity(Vector2 at, float margin = 0.5f) =>
        Areas.PetCity.Contains(at.X, at.Y, margin) && Areas.At(at.X, at.Y) == Area.PetCity;

    /// <summary>Whether a walker of <paramref name="radius"/> may stand at (x, z): walkable, off every obstacle and in Pet City.</summary>
    public static bool Clear(Vector2 at, float radius) =>
        Areas.Walkable(at.X, at.Y, radius) && !Ground.Blocks(at.X, at.Y, radius) && InCity(at);

    /// <summary>The yaw facing along (dx, dz) (0 faces -Z).</summary>
    public static float YawOf(Vector2 d) => MathF.Atan2(-d.X, -d.Y);

    /// <summary>The unit (x, z) direction a yaw faces.</summary>
    public static Vector2 Facing(float yaw) => new(-MathF.Sin(yaw), -MathF.Cos(yaw));

    /// <summary>Turns <paramref name="from"/> toward <paramref name="to"/> by at most <paramref name="step"/> radians, the short way round.</summary>
    public static float TurnToward(float from, float to, float step)
    {
        float d = to - from;
        d -= MathF.Round(d / MathF.Tau) * MathF.Tau;
        return MathF.Abs(d) <= step ? to : from + MathF.Sign(d) * step;
    }
}
