using System;
using System.Collections.Generic;
using System.Numerics;
using Fungtopia.Map;
using Fungtopia.Shell;
using Xunit;

namespace Fungtopia.DeepForest.Tests;

public class DeepForestTests
{
    /// <summary>Fake of the shell's bus: counts subscriptions and publications.</summary>
    private sealed class FakeBus : IMessageBus
    {
        public int Subscriptions, Published;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct => Subscriptions++;

        public void Publish<T>(in T message) where T : struct => Published++;
    }

    private static readonly DeepForestScenery Scenery = DeepForestScenery.Create();

    private static int FirstDeepTrunk => Ground.Obstacles.Length - Ground.DeepForestTrunkCount;

    private static bool IsDeepTrunk(int index) => index >= FirstDeepTrunk && index < Ground.Obstacles.Length;

    private static float Horizontal(Vector3 a, float x, float z) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(x, z));

    // ---- The module

    [Fact]
    public void ModuleBuildsTheSceneryAtStartAndDoesNothingPerTick()
    {
        var bus = new FakeBus();
        var module = new DeepForestModule();
        module.Start(bus);
        Assert.True(module.Started);
        Assert.NotNull(module.Scenery);
        var scenery = module.Scenery;
        for (int i = 0; i < 600; i++)
            module.Tick(new TickContext(i, 1.0 / 60));
        Assert.Equal(0, bus.Subscriptions);
        Assert.Equal(0, bus.Published);
        Assert.Same(scenery, module.Scenery);
    }

    [Fact]
    public void TickBeforeStartThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new DeepForestModule().Tick(new TickContext(0, 1.0 / 60)));
        Assert.Throws<ArgumentNullException>(() => new DeepForestModule().Start(null!));
    }

    [Fact]
    public void TheSceneryIsTheSameEveryRun()
    {
        var again = DeepForestScenery.Create();
        foreach (var kind in Enum.GetValues<PropKind>())
            Assert.True(Scenery.Of(kind).SequenceEqual(again.Of(kind)), kind.ToString());
        Assert.Equal(Scenery.Fog, again.Fog);
        Assert.Equal(Scenery.Lights, again.Lights);
    }

    // ---- Giant mushrooms on the Map's obstacle spots

    [Fact]
    public void AGiantMushroomStandsOnEveryDeepForestObstacleExactly()
    {
        var stems = Scenery.Of(PropKind.GiantStem);
        var caps = Scenery.Of(PropKind.GiantCap);
        Assert.Equal(Ground.DeepForestTrunkCount, stems.Length);
        Assert.Equal(Ground.DeepForestTrunkCount, caps.Length);
        for (int i = 0; i < stems.Length; i++)
        {
            int o = FirstDeepTrunk + i;
            var c = Ground.Obstacles[o];
            Assert.Equal(o, stems[i].Owner);
            Assert.Equal(c.X, stems[i].Position.X);
            Assert.Equal(c.Z, stems[i].Position.Z);
            // The stem's radius is the obstacle's, its foot sunk a little into the ground.
            Assert.Equal(c.Radius, stems[i].Scale.X);
            Assert.Equal(c.Radius, stems[i].Scale.Z);
            float ground = Ground.HeightAt(c.X, c.Z);
            Assert.InRange(stems[i].Position.Y, ground - 1f, ground);
            // Huge and old: well over the player's head, caps far wider than the stem.
            Assert.InRange(stems[i].Scale.Y, 12f, 30f);
            Assert.Equal(o, caps[i].Owner);
            Assert.True(Horizontal(caps[i].Position, c.X, c.Z) < 0.01f);
            Assert.True(caps[i].Scale.X >= c.Radius * 2.5f);
            Assert.True(caps[i].Position.Y > stems[i].Position.Y + stems[i].Scale.Y * 0.8f);
        }
    }

    [Fact]
    public void NoGiantMushroomOrStemPropStandsOutsideTheMapsObstacles()
    {
        // Everything that is part of a giant mushroom grows from one of the Deep Forest's obstacle circles.
        foreach (var kind in new[] { PropKind.GiantStem, PropKind.GiantCap, PropKind.BranchStem, PropKind.Shelf, PropKind.Root })
        {
            foreach (var p in Scenery.Of(kind))
            {
                Assert.True(IsDeepTrunk(p.Owner), $"{kind} at {p.Position} grows on obstacle {p.Owner}, not a Deep Forest trunk");
                var c = Ground.Obstacles[p.Owner];
                Assert.True(Horizontal(p.Position, c.X, c.Z) <= c.Radius + 0.01f, $"{kind} at {p.Position} is not on its trunk {c.Center}");
            }
        }
        foreach (var kind in new[] { PropKind.BranchCap, PropKind.Web })
            foreach (var p in Scenery.Of(kind))
                Assert.True(IsDeepTrunk(p.Owner), $"{kind} at {p.Position} has no trunk");
        // Branch caps sit at the tip of their own trunk's side stems, high in the canopy.
        foreach (var p in Scenery.Of(PropKind.BranchCap))
            Assert.True(p.Position.Y - Ground.HeightAt(p.Position.X, p.Position.Z) > 6f, $"branch cap at {p.Position} is too low");
    }

    // ---- Everything inside the area

    [Fact]
    public void EveryPropIsInsideTheDeepForestOrItsPath()
    {
        int total = 0;
        foreach (var kind in Enum.GetValues<PropKind>())
        {
            foreach (var p in Scenery.Of(kind))
            {
                Assert.True(DeepForestGloom.InArea(p.Position.X, p.Position.Z), $"{kind} at {p.Position} is outside the Deep Forest");
                Assert.NotEqual(Area.Town, Areas.At(p.Position.X, p.Position.Z));
                total++;
            }
        }
        Assert.True(total > 5000, $"only {total} props");
        foreach (var l in Scenery.Lights)
            Assert.True(DeepForestGloom.InArea(l.Position.X, l.Position.Z), $"light at {l.Position}");
    }

    [Fact]
    public void GroundPropsStandOnTheGroundOffTheStemsAndOffThePathsMiddle()
    {
        foreach (var kind in new[] { PropKind.Glow, PropKind.Toadstool, PropKind.Fern })
        {
            Assert.True(Scenery.Of(kind).Length > 100, $"too few {kind}");
            foreach (var p in Scenery.Of(kind))
            {
                float x = p.Position.X, z = p.Position.Z;
                Assert.Equal(-1, p.Owner);
                Assert.InRange(p.Position.Y, Ground.HeightAt(x, z) - 0.1f, Ground.HeightAt(x, z));
                Assert.False(Ground.Blocks(x, z, DeepForestScenery.StemClearance), $"{kind} at {p.Position} is inside a stem");
                if (Areas.DeepForestPath.Contains(x, z))
                    Assert.True(MathF.Abs(z) >= DeepForestScenery.PathClearHalfWidth, $"{kind} at {p.Position} blocks the path's middle");
            }
        }
        foreach (var kind in new[] { PropKind.Spore, PropKind.Mist })
            foreach (var p in Scenery.Of(kind))
                Assert.InRange(p.Position.Y - Ground.HeightAt(p.Position.X, p.Position.Z), 0.3f, 6.5f);
    }

    [Fact]
    public void TheMonsterSpawnSpotsStayClear()
    {
        // Undergrowth may cover them, but no root, shelf or web hides a spawning monster.
        foreach (var kind in new[] { PropKind.Root, PropKind.Web })
            foreach (var p in Scenery.Of(kind))
                foreach (var s in DeepForestLayout.SpawnPoints)
                    Assert.True(Horizontal(p.Position, s.X, s.Y) > 1.5f, $"{kind} at {p.Position} sits on spawn {s}");
    }

    // ---- The look

    [Fact]
    public void CapsAreDarkPurplesRedsAndBrowns()
    {
        foreach (var kind in new[] { PropKind.GiantCap, PropKind.BranchCap })
        {
            foreach (var p in Scenery.Of(kind))
            {
                var c = p.Color;
                Assert.True(MathF.Max(c.X, MathF.Max(c.Y, c.Z)) < 0.7f, $"{kind} color {c} is not dark");
                Assert.True(c.Y < c.X, $"{kind} color {c} is green"); // red, purple or brown: never green-leaning
            }
        }
    }

    [Fact]
    public void TheCanopyBlocksMostOfTheSkyDeepInsideAndLessAtTheEntrance()
    {
        // Samples on the ground, how many have a cap (main or side) right above them.
        float Covered(Func<float, float, bool> where)
        {
            int inside = 0, covered = 0;
            var b = DeepForestLayout.Bounds;
            for (float x = b.MinX; x <= Areas.DeepForestPath.MaxX; x += 1.5f)
            for (float z = b.MinZ; z <= b.MaxZ; z += 1.5f)
            {
                if (!DeepForestGloom.InArea(x, z) || !where(x, z))
                    continue;
                inside++;
                bool under = false;
                foreach (var kind in new[] { PropKind.GiantCap, PropKind.BranchCap })
                    foreach (var p in Scenery.Of(kind))
                        under |= Horizontal(p.Position, x, z) < p.Scale.X * 0.95f;
                if (under)
                    covered++;
            }
            return covered / (float)Math.Max(1, inside);
        }

        float deep = Covered((x, z) => DeepForestLayout.Bounds.Contains(x, z, 15f) && x < -140f && MathF.Abs(z) < 40f);
        float entrance = Covered((x, z) => x > DeepForestLayout.Entrance.X - 4f);
        Assert.True(deep >= 0.85f, $"only {deep:P0} of the deep ground is under the canopy");
        Assert.True(entrance < deep - 0.2f, $"the entrance ({entrance:P0}) is as closed as the depths ({deep:P0})");
    }

    [Fact]
    public void WebsHangOnSomeCapsNotAll()
    {
        var owners = new HashSet<int>();
        foreach (var p in Scenery.Of(PropKind.Web))
        {
            owners.Add(p.Owner);
            Assert.InRange(p.Scale.X, 0.5f, 3.6f);
        }
        float share = owners.Count / (float)Ground.DeepForestTrunkCount;
        Assert.InRange(share, 0.15f, 0.5f);
    }

    [Fact]
    public void GlowingMushroomsAndSporesGiveFaintLight()
    {
        Assert.True(Scenery.Of(PropKind.Glow).Length > 300);
        Assert.True(Scenery.Of(PropKind.Spore).Length > 500);
        foreach (var p in Scenery.Of(PropKind.Glow))
            Assert.InRange(p.Glow, 0.3f, 1.5f);
        Assert.InRange(Scenery.Lights.Length, 6, DeepForestScenery.MaxLights);
        // Spread out, not all in one corner.
        foreach (var a in Scenery.Lights)
            foreach (var b in Scenery.Lights)
                Assert.True(a.Equals(b) || Vector3.Distance(a.Position, b.Position) > 20f);
    }

    [Fact]
    public void TheOvergrownPathHasUndergrowthAlongItsEdges()
    {
        int ferns = 0, glow = 0;
        foreach (var p in Scenery.Of(PropKind.Fern))
            if (Areas.DeepForestPath.Contains(p.Position.X, p.Position.Z))
                ferns++;
        foreach (var p in Scenery.Of(PropKind.Glow))
            if (Areas.DeepForestPath.Contains(p.Position.X, p.Position.Z))
                glow++;
        Assert.True(ferns >= 40, $"only {ferns} ferns on the path");
        Assert.True(glow >= 3, $"only {glow} glowing mushrooms on the path");
    }

    // ---- Fog

    [Fact]
    public void FogIsSoftLowInsideTheAreaAndThickerInHollows()
    {
        Assert.NotEmpty(Scenery.Fog);
        int hollows = 0;
        foreach (var f in Scenery.Fog)
        {
            // Within the Deep Forest's rectangle or its path, never out over the Mushroom Forest.
            float minX = f.Center.X - f.Size.X * 0.5f, maxX = f.Center.X + f.Size.X * 0.5f;
            float minZ = f.Center.Z - f.Size.Z * 0.5f, maxZ = f.Center.Z + f.Size.Z * 0.5f;
            bool inRect = minX >= DeepForestLayout.Bounds.MinX - 0.01f && maxX <= DeepForestLayout.Bounds.MaxX + 0.01f
                && minZ >= DeepForestLayout.Bounds.MinZ - 0.01f && maxZ <= DeepForestLayout.Bounds.MaxZ + 0.01f;
            bool inPath = minX >= Areas.DeepForestPath.MinX - 0.01f && maxX <= Areas.DeepForestPath.MaxX + 0.01f
                && minZ >= Areas.DeepForestPath.MinZ - 0.01f && maxZ <= Areas.DeepForestPath.MaxZ + 0.01f;
            Assert.True(inRect || inPath, $"fog at {f.Center} size {f.Size} leaves the Deep Forest");
            // Slightly foggy, low.
            Assert.InRange(f.Density, 0.005f, 0.06f);
            Assert.True(f.Size.Y <= 12f, $"fog at {f.Center} is {f.Size.Y} m tall");
            if (f.Ellipsoid)
            {
                hollows++;
                Assert.True(DeepForestScenery.Hollowness(f.Center.X, f.Center.Z) >= 0.5f);
            }
        }
        Assert.True(hollows >= 3, $"only {hollows} hollow fogs");
        // The main layer covers the depths; the path's fog is thinner than the entrance's, which is thinner than the depths'.
        float Density(float x, float z)
        {
            float d = 0f;
            foreach (var f in Scenery.Fog)
                if (!f.Ellipsoid && MathF.Abs(x - f.Center.X) <= f.Size.X * 0.5f && MathF.Abs(z - f.Center.Z) <= f.Size.Z * 0.5f)
                    d += f.Density;
            return d;
        }
        Assert.True(Density(-98f, 0f) > 0f);
        Assert.True(Density(-98f, 0f) < Density(-115f, 0f));
        Assert.True(Density(-115f, 0f) < Density(-200f, 0f));
        Assert.Equal(0f, Density(-50f, 0f));
    }

    // ---- The gradual transition

    [Fact]
    public void GloomRisesGraduallyAlongThePathAndIsZeroOutside()
    {
        Assert.Equal(0f, DeepForestGloom.At(0f, 0f));
        Assert.Equal(0f, DeepForestGloom.At(-90f, 0f));
        Assert.Equal(0f, DeepForestGloom.At(0f, 215f));
        Assert.Equal(1f, DeepForestGloom.At(-200f, 30f));
        float previous = 0f;
        for (float x = -96f; x >= -140f; x -= 0.5f)
        {
            float g = DeepForestGloom.At(x, 0f);
            Assert.True(g >= previous, $"gloom falls at x = {x}");
            Assert.True(g - previous < 0.05f, $"gloom jumps at x = {x}");
            previous = g;
        }
        Assert.InRange(DeepForestGloom.At(DeepForestLayout.Entrance.X, 0f), 0.2f, 0.6f);
    }

    [Fact]
    public void InAreaIsTheRectangleAndThePath()
    {
        Assert.True(DeepForestGloom.InArea(-200f, 0f));
        Assert.True(DeepForestGloom.InArea(-104f, 0f));
        Assert.True(DeepForestGloom.InArea(-98f, 3f));
        Assert.False(DeepForestGloom.InArea(-104f, 8f)); // the cliff strip beside the path
        Assert.False(DeepForestGloom.InArea(-90f, 0f));  // the Mushroom Forest
        Assert.False(DeepForestGloom.InArea(-200f, 95f));
        foreach (var s in DeepForestLayout.SpawnPoints)
            Assert.True(DeepForestGloom.InArea(s.X, s.Y));
    }
}

internal static class SpanExtensions
{
    public static bool SequenceEqual(this ReadOnlySpan<Prop> a, ReadOnlySpan<Prop> b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
            if (!a[i].Equals(b[i]))
                return false;
        return true;
    }
}
