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
            // The stem's radius is the obstacle's, its foot planted under its whole flared footprint.
            Assert.Equal(c.Radius, stems[i].Scale.X);
            Assert.Equal(c.Radius, stems[i].Scale.Z);
            AssertPlanted(stems[i].Position, c.Radius * DeepForestScenery.FootRadius, DeepForestScenery.BaseSink);
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
        foreach (var kind in new[] { PropKind.GiantStem, PropKind.Skirt, PropKind.GiantCap, PropKind.BranchStem, PropKind.Shelf, PropKind.Root })
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

    // ---- Planted on the uneven ground: no base floats where the ground drops away

    /// <summary>The lowest and highest ground on a dense check of a round footprint (the center, 48 points on each of 6 rings).</summary>
    private static (float Low, float High) DenseGround(float x, float z, float radius)
    {
        float low = Ground.HeightAt(x, z), high = low;
        for (int ring = 1; ring <= 6; ring++)
        for (int i = 0; i < 48; i++)
        {
            float a = i * MathF.Tau / 48f, d = radius * ring / 6f;
            float h = Ground.HeightAt(x + d * MathF.Cos(a), z + d * MathF.Sin(a));
            low = MathF.Min(low, h);
            high = MathF.Max(high, h);
        }
        return (low, high);
    }

    /// <summary>A base at <paramref name="at"/> is at or below the ground everywhere under a foot of <paramref name="radius"/>, and at most about 0.3 m below its lowest point.</summary>
    private static void AssertPlanted(Vector3 at, float radius, float sink)
    {
        var (low, _) = DenseGround(at.X, at.Z, radius);
        Assert.True(at.Y <= low, $"base at {at} (radius {radius}) floats {at.Y - low:F3} m over the ground under its foot");
        Assert.True(at.Y >= low - 0.3f, $"base at {at} (radius {radius}) is {low - at.Y:F3} m under the lowest ground of its foot");
        Assert.True(at.Y <= low - sink * 0.5f, $"base at {at} (radius {radius}) is barely in the ground");
    }

    [Fact]
    public void BaseHeightIsUnderTheWholeFootprintAndOnlyALittleUnderItsLowestPoint()
    {
        // Many spots over the Deep Forest's rough ground, its path and the slopes toward its cliffs, small feet and giant ones.
        int sloped = 0;
        var b = DeepForestLayout.Bounds;
        for (int k = 0; k < 600; k++)
        {
            float x = b.MinX - 4f + (b.MaxX - b.MinX + 20f) * DeepForestScenery.Rand(k, 900);
            float z = b.MinZ - 4f + (b.MaxZ - b.MinZ + 8f) * DeepForestScenery.Rand(k, 901);
            float radius = k % 3 == 0 ? 0.1f + 0.3f * DeepForestScenery.Rand(k, 902) : 1.3f * (1f + DeepForestScenery.Rand(k, 903) * 2f) * DeepForestScenery.FootRadius;
            var (low, high) = DeepForestScenery.GroundUnder(x, z, radius);
            Assert.True(low <= high);
            Assert.True(low <= Ground.HeightAt(x, z) && high >= Ground.HeightAt(x, z));
            float baseY = DeepForestScenery.BaseHeight(x, z, radius);
            Assert.Equal(low - DeepForestScenery.BaseSink, baseY);
            AssertPlanted(new Vector3(x, baseY, z), radius, DeepForestScenery.BaseSink);
            Assert.True(high - baseY < high - DenseGround(x, z, radius).Low + 0.3f);
            if (high - low > 0.3f)
                sloped++;
        }
        Assert.True(sloped > 50, $"only {sloped} sloped footprints sampled");
        // The sink is about 0.1-0.2 m.
        Assert.InRange(DeepForestScenery.BaseSink, 0.1f, 0.2f);
        Assert.InRange(DeepForestScenery.SmallSink, 0.1f, 0.2f);
    }

    [Fact]
    public void GiantStemsAreExtendedDownBySlopeAndFlaredIntoTheGround()
    {
        var stems = Scenery.Of(PropKind.GiantStem);
        var caps = Scenery.Of(PropKind.GiantCap);
        var skirts = Scenery.Of(PropKind.Skirt);
        Assert.Equal(stems.Length, skirts.Length);
        int sloped = 0;
        for (int i = 0; i < stems.Length; i++)
        {
            var s = stems[i];
            var c = Ground.Obstacles[s.Owner];
            var (low, high) = DenseGround(c.X, c.Z, c.Radius * DeepForestScenery.FootRadius);
            if (high - low > 0.3f)
                sloped++;
            // Still well over the high side: the stem's top (and its cap) never sink because the foot went down.
            float top = s.Position.Y + s.Scale.Y;
            Assert.True(top - high >= 10f + 3f * c.Radius - 0.05f, $"stem {i} stands only {top - high:F2} m over its high side");
            Assert.True(caps[i].Position.Y + caps[i].Scale.Y * 0.5f > high + 8f, $"cap {i} sinks");
            // Its skirt: from the same base, reaching over the high side, wider at the foot than the stem's radius, its top inside the stem.
            var k = skirts[i];
            Assert.Equal(s.Owner, k.Owner);
            Assert.Equal(s.Position, k.Position);
            Assert.True(k.Position.Y + k.Scale.Y > high + 0.5f, $"skirt {i} is buried");
            Assert.True(k.Scale.Y < 3f + (high - low) + 0.5f, $"skirt {i} is {k.Scale.Y} m tall");
            float flareTop = k.Scale.X, stemThere = c.Radius * DeepForestScenery.StemProfile(k.Scale.Y / s.Scale.Y);
            Assert.InRange(flareTop, stemThere * 0.9f, stemThere);
            Assert.True(flareTop * 1.12f > c.Radius * 1.3f, $"skirt {i} does not flare");
        }
        Assert.True(sloped > stems.Length / 4, $"only {sloped} giant stems on sloped ground");
    }

    [Fact]
    public void RootsLieInTheGroundAlongTheirWholeLength()
    {
        Assert.True(Scenery.Of(PropKind.Root).Length > 0);
        foreach (var p in Scenery.Of(PropKind.Root))
        {
            // The root's line in the world: from its position along its turned +X, length Scale.X.
            var along = Vector3.Transform(Vector3.UnitX, p.Rotation);
            var flat = Vector3.Normalize(new Vector3(along.X, 0f, along.Z));
            float length = p.Scale.X;
            for (int i = 0; i <= 20; i++)
            {
                var q = p.Position + along * (length * i / 20f);
                Assert.True(q.Y <= Ground.HeightAt(q.X, q.Z) + 0.01f, $"root at {p.Position} floats at {q}");
                var side = new Vector3(-flat.Z, 0f, flat.X) * (0.26f * p.Scale.Y);
                Assert.True(q.Y <= Ground.HeightAt(q.X + side.X, q.Z + side.Z) + 0.03f, $"root at {p.Position} floats beside {q}");
                Assert.True(q.Y <= Ground.HeightAt(q.X - side.X, q.Z - side.Z) + 0.03f, $"root at {p.Position} floats beside {q}");
            }
            Assert.True(p.Position.Y >= Ground.HeightAt(p.Position.X, p.Position.Z) - 0.8f, $"root at {p.Position} is buried");
        }
    }

    [Fact]
    public void SmallPropsKeepTheirHeightAboveTheHighSide()
    {
        foreach (var p in Scenery.Of(PropKind.Toadstool))
        {
            var (_, high) = DeepForestScenery.GroundUnder(p.Position.X, p.Position.Z, p.Scale.X * DeepForestScenery.ToadstoolFoot);
            float stemTop = p.Position.Y + DeepForestScenery.ToadstoolStem * p.Scale.Y;
            Assert.True(MathF.Abs(stemTop - high - DeepForestScenery.ToadstoolStem * p.Scale.X) < 0.001f, $"toadstool at {p.Position}");
        }
        foreach (var p in Scenery.Of(PropKind.Glow))
        {
            var (_, high) = DeepForestScenery.GroundUnder(p.Position.X, p.Position.Z, p.Scale.X * DeepForestScenery.GlowFoot);
            float top = p.Position.Y + DeepForestScenery.GlowHeight * p.Scale.Y;
            Assert.True(MathF.Abs(top - high - DeepForestScenery.GlowHeight * p.Scale.X) < 0.001f, $"glowing mushroom at {p.Position}");
        }
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
                float foot = kind switch
                {
                    PropKind.Glow => DeepForestScenery.GlowFoot,
                    PropKind.Toadstool => DeepForestScenery.ToadstoolFoot,
                    _ => DeepForestScenery.FernFoot,
                } * p.Scale.X;
                AssertPlanted(p.Position, foot, DeepForestScenery.SmallSink);
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

    private static float Saturation(Vector3 c) => (Max(c) - MathF.Min(c.X, MathF.Min(c.Y, c.Z))) / Max(c);

    private static float Max(Vector3 c) => MathF.Max(c.X, MathF.Max(c.Y, c.Z));

    [Fact]
    public void CapsAreVividPurplesRedsAndOrangeBrowns()
    {
        int reds = 0, purples = 0, oranges = 0;
        foreach (var kind in new[] { PropKind.GiantCap, PropKind.BranchCap, PropKind.Toadstool })
        {
            foreach (var p in Scenery.Of(kind))
            {
                var c = p.Color;
                // Clearly saturated and bright enough to pop, but not washed out to white.
                Assert.True(Saturation(c) >= 0.75f, $"{kind} color {c} is dull");
                Assert.InRange(Max(c), 0.4f, 1f);
                Assert.True(c.Y < c.X, $"{kind} color {c} is green"); // red, purple or orange-brown: never green-leaning
                if (c.Z > c.X * 0.6f)
                    purples++;
                else if (c.Y < c.X * 0.2f)
                {
                    reds++;
                    // A pure cherry/tomato red: neither brownish nor pink.
                    Assert.True(c.Z < c.X * 0.15f, $"red cap {c} is pinkish");
                }
                else
                    oranges++;
            }
        }
        Assert.True(reds > 20 && purples > 20 && oranges > 20, $"{reds} red, {purples} purple, {oranges} orange-brown caps");
    }

    [Fact]
    public void ShelvesAreARichOrangeBrown()
    {
        foreach (var p in Scenery.Of(PropKind.Shelf))
        {
            Assert.True(Saturation(p.Color) >= 0.8f, $"shelf color {p.Color} is dull");
            Assert.True(p.Color.X > p.Color.Y && p.Color.Y > p.Color.Z, $"shelf color {p.Color} is not orange-brown");
        }
    }

    [Fact]
    public void StemsAreACleanLightCream()
    {
        var cream = DeepForestScenery.StemCream;
        Assert.True(cream.X > 0.85f && cream.X >= cream.Y && cream.Y > cream.Z && cream.Z > 0.65f, $"{cream} is not a light cream");
        foreach (var kind in new[] { PropKind.GiantStem, PropKind.BranchStem, PropKind.Skirt })
        {
            foreach (var p in Scenery.Of(kind))
            {
                // The cream itself, at most a little shaded on the old giants (skirts a touch more).
                float shade = p.Color.X / cream.X;
                Assert.InRange(shade, DeepForestScenery.GiantStemShade * 0.92f - 0.001f, 1.001f);
                Assert.True(Vector3.Distance(p.Color, cream * shade) < 0.001f, $"{kind} color {p.Color} is not the cream");
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
    public void GlowingMushroomsGlowClearlyAndSporesGiveFaintLight()
    {
        Assert.True(Scenery.Of(PropKind.Glow).Length > 300);
        Assert.True(Scenery.Of(PropKind.Spore).Length > 500);
        // About twice the old glow (0.6..1.2, 0.9 on average), so they clearly glow at night.
        float sum = 0f;
        foreach (var p in Scenery.Of(PropKind.Glow))
        {
            Assert.InRange(p.Glow, DeepForestScenery.GlowStrength, 2f * DeepForestScenery.GlowStrength);
            sum += p.Glow;
        }
        Assert.InRange(sum / Scenery.Of(PropKind.Glow).Length, 1.6f, 2f);
        foreach (var p in Scenery.Of(PropKind.Spore))
            Assert.Equal(1f, p.Glow);
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
