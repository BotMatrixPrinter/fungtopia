using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Linq;
using System.Text.RegularExpressions;
using Fungtopia.Input;
using Fungtopia.Map;
using Fungtopia.Monsters;
using Fungtopia.Player;
using Fungtopia.Save;
using Fungtopia.Shell;
using Fungtopia.Wallet;
using Xunit;

namespace Fungtopia.Wardrobe.Tests;

public class CatalogTests
{
    /// <summary>The repository root, found from the test assembly's folder.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string FileOf(string resPath)
    {
        Assert.StartsWith("res://", resPath);
        return Path.Combine(RepoRoot(), resPath.Substring("res://".Length));
    }

    [Fact]
    public void IdsAreUniqueAndItemsWellFormed()
    {
        var ids = new HashSet<string>();
        foreach (var item in Catalog.Items)
        {
            Assert.Matches("^[a-z][a-z0-9_]*$", item.Id);
            Assert.True(ids.Add(item.Id), $"duplicate id {item.Id}");
            Assert.False(string.IsNullOrWhiteSpace(item.Name));
            Assert.True(Enum.IsDefined(item.Slot), $"{item.Id}: slot {item.Slot}");
            Assert.InRange(item.Fit, Fits.Both, Fits.Boy);
            Assert.Contains(item.Source, new[] { Sources.Starter, Sources.Shop, Sources.Reward });
            if (item.Source == Sources.Shop)
                Assert.InRange(item.Price, 100, 600);
            else
                Assert.Equal(0, item.Price);
            if (item.Dress)
                Assert.Equal(Slot.Top, item.Slot);
            Assert.Equal(item.Id, Catalog.Items[Catalog.IndexOf(item.Id)].Id);
        }
        Assert.Equal(Catalog.Count, ids.Count);
        Assert.Equal(-1, Catalog.IndexOf("no_such_item"));
        Assert.Equal(-1, Catalog.IndexOf(""));
    }

    [Fact]
    public void EveryModelFileAndItsScriptExist()
    {
        foreach (var item in Catalog.Items)
        {
            string glb = FileOf(item.ModelPath);
            Assert.EndsWith(item.Id + ".glb", glb);
            Assert.True(File.Exists(glb), $"missing model {glb}");
            Assert.True(new FileInfo(glb).Length > 1000, $"empty model {glb}");
            Assert.True(File.Exists(Path.ChangeExtension(glb, ".py")), $"missing model script for {item.Id}");
        }
        foreach (var hair in Looks.HairStyles)
        {
            if (hair.Id == "none")
                continue; // "No hair" has no model
            string glb = FileOf(hair.ModelPath);
            Assert.True(File.Exists(glb), $"missing hair model {glb}");
            Assert.True(File.Exists(Path.ChangeExtension(glb, ".py")), $"missing hair script for {hair.Id}");
        }
    }

    [Fact]
    public void TheStarterCatalogHasEveryKindOfPiece()
    {
        int Count(Slot slot, bool dress = false)
        {
            int n = 0;
            foreach (var item in Catalog.Items)
                if (item.Slot == slot && item.Dress == dress && item.Source != Sources.Reward)
                    n++;
            return n;
        }
        Assert.Equal(4, Count(Slot.Top));
        Assert.Equal(3, Count(Slot.Top, dress: true));
        Assert.Equal(4, Count(Slot.Bottom));
        Assert.Equal(3, Count(Slot.Shoes));
        Assert.Equal(4, Count(Slot.Head));
        Assert.Equal(2, Count(Slot.Face));
        Assert.Equal(2, Count(Slot.Back));
        foreach (var item in Catalog.Items)
            if (item.Dress)
                Assert.NotEqual(Fits.Boy, item.Fit);
        Assert.True(Catalog.TryGet(Catalog.SpiderSilkScarf, out var scarf));
        Assert.Equal(Sources.Reward, scarf.Source);
        Assert.True(Catalog.TryGet(Catalog.LadybugBackpack, out var backpack));
        Assert.Equal(Sources.Reward, backpack.Source);
        Assert.Equal(Slot.Back, backpack.Slot);
    }

    [Fact]
    public void StarterOutfitsAreThreeOutfitsOfStarterItemsInTheirSlots()
    {
        Assert.Equal(3, Catalog.StarterOutfits.Length);
        foreach (var outfit in Catalog.StarterOutfits)
        {
            void Check(string id, Slot slot, bool required)
            {
                if (string.IsNullOrEmpty(id))
                {
                    Assert.False(required, $"{outfit.Name} has no {slot}");
                    return;
                }
                Assert.True(Catalog.TryGet(id, out var item), id);
                Assert.Equal(slot, item.Slot);
                Assert.Equal(Sources.Starter, item.Source);
            }
            Check(outfit.Top, Slot.Top, true);
            Check(outfit.Bottom, Slot.Bottom, !Catalog.Items[Catalog.IndexOf(outfit.Top)].Dress);
            Check(outfit.Shoes, Slot.Shoes, true);
            Check(outfit.Head, Slot.Head, false);
        }
        // Both characters have an outfit to start in.
        Assert.True(Fits.Matches(Catalog.StarterOutfits[Catalog.DefaultOutfit(0)].Fit, 0));
        Assert.True(Fits.Matches(Catalog.StarterOutfits[Catalog.DefaultOutfit(1)].Fit, 1));
    }

    [Fact]
    public void LooksHaveTheDesignedChoices()
    {
        Assert.Equal(6, Looks.SkinTones.Length);
        Assert.Equal(7, Looks.HairStyles.Length);
        Assert.Equal(8, Looks.HairColors.Length);
        Assert.Equal(6, Looks.EyeColors.Length);
        Assert.Equal(Looks.HairColors.Length, Looks.HairColorNames.Length);
        Assert.Equal(Looks.EyeColors.Length, Looks.EyeColorNames.Length);
        Assert.Contains("Pink", Looks.HairColorNames.ToArray());
        Assert.Contains("Mint", Looks.HairColorNames.ToArray());
        var styles = new List<string>();
        foreach (var style in Looks.HairStyles)
            styles.Add(style.Id);
        // "No hair" appended last, so saved indices keep their meaning; it has no model.
        Assert.Equal(new[] { "short", "bob", "pigtails", "ponytail", "top_bun", "curly", "none" }, styles);
        Assert.Equal(6, Looks.NoHair);
        Assert.Equal("No hair", Looks.HairStyles[Looks.NoHair].Name);
        Assert.Equal("", Looks.HairStyles[Looks.NoHair].ModelPath);
        Assert.True(Looks.Valid(0, Looks.NoHair, 0, 0));
        Assert.False(Looks.Valid(0, Looks.HairStyles.Length, 0, 0));
        for (int i = 0; i < Looks.NoHair; i++)
            Assert.NotEqual("", Looks.HairStyles[i].ModelPath);
        // Light to deep.
        for (int i = 1; i < Looks.SkinTones.Length; i++)
        {
            var a = Looks.SkinTones[i - 1];
            var b = Looks.SkinTones[i];
            Assert.True(a.X + a.Y + a.Z > b.X + b.Y + b.Z, $"skin tone {i} is not deeper");
        }
        foreach (var span in new[] { Looks.SkinTones.ToArray(), Looks.HairColors.ToArray(), Looks.EyeColors.ToArray() })
            foreach (var c in span)
                Assert.True(c.X is >= 0 and <= 1 && c.Y is >= 0 and <= 1 && c.Z is >= 0 and <= 1);
    }
}

/// <summary>
/// The clothes and hair styles fit the Player's simple-shapes rig (modules/Player/Models/girl.glb
/// and boy.glb, read back): a big round ball of a head floating over the body (the Girl's a tall
/// cone, the Boy's a tall rounded capsule), big oval hands floating beside it, no arms, two
/// straight tube legs coming out from under it down to rounded feet. Every body object is checked
/// against the body of each character it is worn on (<see cref="Attach.PartFor"/>). Godot's
/// frame: Y up, -Z the front.
/// </summary>
public class ModelFitTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static Glb Model(string resPath) => new(Path.Combine(RepoRoot(), resPath.Substring("res://".Length)));

    /// <summary>The two characters' rigs: 0 the Girl, 1 the Boy (like Fungtopia.Player.CharacterKind).</summary>
    private static readonly Rig[] s_rigs = { new("girl.glb"), new("boy.glb") };

    /// <summary>A character's rig read back, with the shapes the clothes go on.</summary>
    private sealed class Rig
    {
        public readonly string File;
        public readonly Glb Glb;
        /// <summary>The body's skin: its radius at each height (rounding included), bottom and top.</summary>
        public readonly (float Y, float R)[] Rings;
        public readonly float Bottom, Top, HeadBottom, MaxR;
        /// <summary>The round head: its center and radius; the eyes' centers and how far out their fronts reach.</summary>
        public readonly Vector3 HeadCenter;
        public readonly float HeadRadius;
        public readonly (Vector3 Center, float Front)[] Eyes;
        public readonly (Vector3 Center, float Radius)[] Hands;
        /// <summary>The leg tubes: each axis's x (they stand at z 0), their radius, and the span of their straight part.</summary>
        public readonly float LegRadius, LegLow, LegHigh;

        public Rig(string file)
        {
            File = file;
            Glb = new Glb(Path.Combine(RepoRoot(), "modules", "Player", "Models", file));
            Rings = Glb.Vertices(Attach.Body, "Skin").GroupBy(v => MathF.Round(v.Y, 4))
                .Select(g => (g.Key, g.Max(Radius))).OrderBy(r => r.Key).ToArray();
            Bottom = Rings[0].Y;
            Top = Rings[^1].Y;
            MaxR = Rings.Max(r => r.R);
            HeadBottom = Glb.VerticesUnder(Attach.Head).Min(v => v.Y);
            var head = Glb.Vertices(Attach.Head, "Skin");
            HeadCenter = (head.Aggregate(Vector3.Min) + head.Aggregate(Vector3.Max)) / 2;
            HeadRadius = head.Max(v => Vector3.Distance(v, HeadCenter));
            var eye = Glb.Vertices(Attach.Head, "Eye");
            var center = HeadCenter;
            Eyes = new[] { eye.Where(v => v.X < 0).ToList(), eye.Where(v => v.X > 0).ToList() }.Select(e =>
            {
                var c = (e.Aggregate(Vector3.Min) + e.Aggregate(Vector3.Max)) / 2;
                var n = Vector3.Normalize(c - center);
                return (c, e.Max(v => Vector3.Dot(v - center, n)));
            }).ToArray();
            Hands = new[] { "HandL", "HandR" }.Select(h =>
            {
                var c = Glb.Origin(h);
                return (c, Glb.Vertices(h).Max(v => Vector3.Distance(v, c)));
            }).ToArray();
            var leg = Glb.Vertices(Attach.LegL, "Skin");
            float x = Glb.Origin(Attach.LegL).X;
            LegRadius = leg.Max(v => MathF.Sqrt((v.X - x) * (v.X - x) + v.Z * v.Z));
            LegLow = leg.Min(v => v.Y) + LegRadius;
            LegHigh = leg.Max(v => v.Y) - LegRadius;
        }

        public float R(float y)
        {
            if (y <= Bottom)
                return Rings[0].R;
            for (int i = 1; i < Rings.Length; i++)
                if (y <= Rings[i].Y)
                {
                    var (y0, r0) = Rings[i - 1];
                    var (y1, r1) = Rings[i];
                    return r0 + (r1 - r0) * (y - y0) / (y1 - y0);
                }
            return 0f;
        }

        /// <summary>How far the point is from the body's outline (side, rim and top) in its half-plane through the axis.</summary>
        public float Distance(Vector3 v)
        {
            var p = new Vector2(Radius(v), v.Y);
            float best = float.MaxValue;
            for (int i = 0; i < Rings.Length; i++)
            {
                var a = new Vector2(Rings[i].R, Rings[i].Y);
                var b = i + 1 < Rings.Length ? new Vector2(Rings[i + 1].R, Rings[i + 1].Y) : new Vector2(0f, Top);
                var ab = b - a;
                float t = ab.LengthSquared() < 1e-12f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
                best = MathF.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        /// <summary>How far the point is out from the body's side (negative: inside the body).</summary>
        public float Gap(Vector3 v) => v.Y < Bottom || v.Y > Top ? float.PositiveInfinity : Radius(v) - R(v.Y);

        /// <summary>How far the point is out from the nearer leg tube's side (negative: inside it), along the tube's straight part.</summary>
        public float LegGap(Vector3 v) => v.Y < LegLow - LegRadius || v.Y > LegHigh
            ? float.PositiveInfinity
            : MathF.Min(LegDistance(v, Attach.LegLPivot.X), LegDistance(v, Attach.LegRPivot.X)) - LegRadius;

        /// <summary>The point's distance from the vertical axis of the leg at x.</summary>
        public static float LegDistance(Vector3 v, float x) => MathF.Sqrt((v.X - x) * (v.X - x) + v.Z * v.Z);
    }

    private static float Radius(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);

    private static IEnumerable<ClothingItem> On(params Slot[] slots) => Catalog.Items.ToArray().Where(i => slots.Contains(i.Slot));

    private static IEnumerable<string> HairModels() => Looks.HairStyles.ToArray().Where(h => h.ModelPath != "").Select(h => h.ModelPath);

    /// <summary>The model's body object worn by each character the item fits, with that character's rig.</summary>
    private static IEnumerable<(string Name, Rig Rig, List<Vector3> V)> Bodies(ClothingItem item, Glb glb)
    {
        for (int character = 0; character < s_rigs.Length; character++)
        {
            if (!Fits.Matches(item.Fit, character))
                continue;
            var names = glb.Nodes.Select(n => n.Name).Where(n => Attach.PartFor(n, character) == Attach.Body).ToList();
            Assert.True(names.Count <= 1, $"{item.Id}: {names.Count} body objects for character {character}");
            if (names.Count == 1)
                yield return (names[0], s_rigs[character], glb.Vertices(names[0]));
        }
    }

    /// <summary>The bodies of every item on the slots that has one.</summary>
    private static IEnumerable<(ClothingItem Item, string Name, Rig Rig, List<Vector3> V)> BodiesOn(params Slot[] slots) =>
        On(slots).SelectMany(item => Bodies(item, Model(item.ModelPath)).Select(b => (item, b.Name, b.Rig, b.V)));

    /// <summary>Whether vertices close round the body (within <paramref name="near"/> of its side) cover every heading between the heights.</summary>
    private static bool AllRound(Rig rig, List<Vector3> v, float y0, float y1, float near = 0.04f)
    {
        var headings = new HashSet<int>();
        foreach (var p in v)
            if (p.Y >= y0 && p.Y <= y1 && Radius(p) - rig.R(p.Y) < near)
                headings.Add((int)MathF.Floor((MathF.Atan2(p.X, -p.Z) + MathF.PI) / (2 * MathF.PI) * 24) % 24);
        return headings.Count == 24;
    }

    /// <summary>Whether vertices round the leg at x (within <paramref name="near"/> of its axis) cover every heading between the heights.</summary>
    private static bool AllRoundLeg(List<Vector3> v, float x, float y0, float y1, float near)
    {
        var headings = new HashSet<int>();
        foreach (var p in v)
            if (p.Y >= y0 && p.Y <= y1 && Rig.LegDistance(p, x) < near)
                headings.Add((int)MathF.Floor((MathF.Atan2(p.X - x, -p.Z) + MathF.PI) / (2 * MathF.PI) * 24) % 24);
        return headings.Count == 24;
    }

    [Fact]
    public void TheRigsHaveTheConeAndCapsuleBodiesThePartsAndThePivotsAttachNames()
    {
        foreach (var rig in s_rigs)
        {
            Assert.InRange(rig.Bottom, 0.34f, 0.38f);
            Assert.InRange(rig.Top, 1.03f, 1.07f);
            Assert.InRange(rig.HeadBottom - rig.Top, 0.02f, 0.06f);
            Assert.InRange(rig.HeadRadius, 0.18f, 0.2f);
            Assert.InRange(rig.HeadCenter.Y, 1.26f, 1.3f);
            Assert.InRange(rig.LegRadius, 0.055f, 0.065f);
            Assert.True(rig.LegLow < 0.12f && rig.LegHigh > rig.Bottom, $"{rig.File}: the legs come out from under the body down to the feet");
            foreach (var (eye, _) in rig.Eyes)
                Assert.True(eye.Z < -0.1f && eye.Y < rig.HeadCenter.Y && MathF.Abs(eye.X) > 0.04f, $"{rig.File}: an eye at {eye}");
            foreach (var (hand, radius) in rig.Hands)
                Assert.True(MathF.Abs(hand.X) - radius > rig.R(hand.Y) + 0.05f, $"{rig.File}: the hands float well out beside the body");
            Assert.True(Vector3.Distance(Attach.HeadPivot, rig.Glb.Origin(Attach.Head)) < 0.001f, rig.File);
            Assert.True(Vector3.Distance(Attach.BodyPivot, rig.Glb.Origin(Attach.Body)) < 0.001f, rig.File);
            Assert.True(Vector3.Distance(Attach.LegLPivot, rig.Glb.Origin(Attach.LegL)) < 0.001f, rig.File);
            Assert.True(Vector3.Distance(Attach.LegRPivot, rig.Glb.Origin(Attach.LegR)) < 0.001f, rig.File);
        }
        var (girl, boy) = (s_rigs[0], s_rigs[1]);
        Assert.True(girl.R(girl.Bottom + 0.06f) > 2.5f * girl.R(girl.Top - 0.12f), "the Girl's body is a cone, wide at the bottom");
        float h = boy.Top - boy.Bottom;
        Assert.True(MathF.Abs(boy.R(boy.Bottom + h * 0.3f) - boy.R(boy.Bottom + h * 0.7f)) < 0.003f, "the Boy's body is a capsule, straight up the side");
        Assert.InRange(boy.R(boy.Bottom + h / 2), 0.14f, 0.18f);
    }

    [Fact]
    public void EveryModelIsMadeOfRigPartsWithTheirOriginsAtThePartsPivots()
    {
        var pivots = new Dictionary<string, Vector3>
        {
            [Attach.Head] = Attach.HeadPivot, [Attach.Body] = Attach.BodyPivot,
            [Attach.BodyGirl] = Attach.BodyPivot, [Attach.BodyBoy] = Attach.BodyPivot,
            [Attach.LegL] = Attach.LegLPivot, [Attach.LegR] = Attach.LegRPivot,
        };
        string[] Expected(ClothingItem item)
        {
            // Body items hold a body object per body they are shaped over: both for an item that fits both.
            var body = item.Fit == Fits.Both ? new[] { Attach.BodyGirl, Attach.BodyBoy } : new[] { Attach.Body };
            return item.Slot switch
            {
                // Bottoms also go round the leg tubes; the skirt only flares round the body.
                Slot.Bottom when item.Id != "skirt" => body.Concat(new[] { Attach.LegL, Attach.LegR }).ToArray(),
                Slot.Top or Slot.Bottom or Slot.Back => body,
                Slot.Shoes => new[] { Attach.LegL, Attach.LegR },
                Slot.Head => new[] { Attach.Head },
                _ => item.Id == Catalog.SpiderSilkScarf ? body : new[] { Attach.Head },
            };
        }
        var models = Catalog.Items.ToArray().Select(i => (i.ModelPath, Expected(i)))
            .Concat(HairModels().Select(path => (path, new[] { Attach.Head })));
        foreach (var (path, parts) in models)
        {
            var glb = Model(path);
            Assert.Equal(parts.OrderBy(p => p, StringComparer.Ordinal),
                glb.Nodes.Select(n => n.Name).OrderBy(p => p, StringComparer.Ordinal));
            foreach (var part in parts)
            {
                Assert.True(Vector3.Distance(pivots[part], glb.Origin(part)) < 0.001f, $"{path}: {part}");
                Assert.True(glb.Vertices(part).Count > 50, $"{path}: {part} is empty");
            }
        }
        // Every hair style but "No hair" has a model.
        Assert.Equal(Looks.HairStyles.ToArray().Count(h => h.Id != "none"), HairModels().Count());
    }

    [Fact]
    public void NothingOnTheBodyPokesThroughItOrTheLegsReachesTheHeadOrTheHandsOrFloatsLoose()
    {
        foreach (var item in Catalog.Items)
        {
            var glb = Model(item.ModelPath);
            foreach (var (name, rig, v) in Bodies(item, glb))
            {
                string what = $"{item.Id} ({name} on {rig.File})";
                var inside = v.Where(p => rig.Gap(p) < -0.002f).ToList();
                Assert.True(inside.Count == 0, $"{what} pokes into the body at {(inside.Count > 0 ? inside[0] : default)}");
                var inLegs = v.Where(p => rig.LegGap(p) < -0.002f).ToList();
                Assert.True(inLegs.Count == 0, $"{what} pokes into the legs at {(inLegs.Count > 0 ? inLegs[0] : default)}");
                Assert.True(v.Max(p => p.Y) < rig.HeadBottom - 0.03f, $"{what} reaches up to the head");
                foreach (var (center, radius) in rig.Hands)
                    Assert.True(v.Min(p => Vector3.Distance(p, center)) > radius + 0.02f, $"{what} reaches the floating hands");
                // It lies on the body: its inside touches the side.
                Assert.True(v.Min(rig.Gap) < 0.02f, $"{what} floats off the body");
            }
            // Leg objects (trouser legs, shoes) stay out of the body and away from the hands.
            foreach (var leg in new[] { Attach.LegL, Attach.LegR }.Where(glb.Has))
                foreach (var rig in s_rigs)
                {
                    var v = glb.Vertices(leg);
                    Assert.True(v.Max(p => p.Y) < rig.Bottom, $"{item.Id}: {leg} reaches into {rig.File}'s body");
                    foreach (var (center, radius) in rig.Hands)
                        Assert.True(v.Min(p => Vector3.Distance(p, center)) > radius + 0.05f, $"{item.Id}: {leg} reaches the hands");
                }
        }
    }

    [Fact]
    public void TopsAndDressesCoverTheBodyWithoutSleeves()
    {
        foreach (var (item, name, rig, v) in BodiesOn(Slot.Top))
        {
            string what = $"{item.Id} ({name} on {rig.File})";
            float h = rig.Top - rig.Bottom;
            Assert.True(v.Max(p => p.Y) >= rig.Top, $"{what} leaves the top of the body bare");
            Assert.True(AllRound(rig, v, rig.Bottom + h * 0.45f, rig.Bottom + h * 0.7f, 0.02f), $"{what} does not go all round");
            // No sleeves (no arms): nothing sticks out sideways from the body.
            Assert.True(v.All(p => rig.Distance(p) < 0.07f), $"{what} sticks out from the body (sleeves?)");
            if (item.Dress)
            {
                Assert.True(v.Min(p => p.Y) <= rig.Bottom, $"{what}: a dress goes down to the bottom edge");
                Assert.True(AllRound(rig, v, rig.Bottom + 0.01f, rig.Bottom + h * 0.15f, 0.02f), $"{what} does not go all round at the bottom");
            }
            else
            {
                // Down into the lower band, under every bottom's top edge (2 cm over it), so no skin shows between them.
                float hem = v.Min(p => p.Y);
                Assert.True(hem > rig.Bottom + h * 0.1f, $"{what} is as long as a dress");
                int character = Array.IndexOf(s_rigs, rig);
                foreach (var bottom in On(Slot.Bottom).Where(b => Fits.Matches(b.Fit, character)))
                {
                    var band = Bodies(bottom, Model(bottom.ModelPath)).Single(b => b.Rig == rig).V;
                    Assert.True(band.Max(p => p.Y) > hem + 0.02f, $"{what} over {bottom.Id}");
                }
            }
        }
    }

    [Fact]
    public void BottomsAreABandRoundTheBodysLowerEdgeAndLegsRoundTheLegTubes()
    {
        foreach (var (item, name, rig, v) in BodiesOn(Slot.Bottom))
        {
            string what = $"{item.Id} ({name} on {rig.File})";
            float h = rig.Top - rig.Bottom;
            float top = v.Max(p => p.Y);
            Assert.InRange(top, rig.Bottom + h * 0.2f, rig.Bottom + h * 0.4f);
            Assert.True(AllRound(rig, v, top - 0.06f, top - 0.02f, 0.04f), $"{what} does not go all round");
            Assert.True(v.All(p => rig.Distance(p) < 0.08f), $"{what} sticks out from the body");
            Assert.True(v.Min(p => p.Y) <= rig.Bottom, $"{what} goes down round the bottom edge");
            Assert.True(AllRound(rig, v, rig.Bottom, rig.Bottom + 0.05f, 0.04f), $"{what} does not go all round the bottom edge");
        }
        foreach (var item in On(Slot.Bottom))
        {
            var glb = Model(item.ModelPath);
            if (item.Id == "skirt")
            {
                // The skirt flares out round the cone past its bottom edge; the legs come out under it.
                var (_, rig, v) = Bodies(item, glb).Single();
                Assert.True(v.Where(p => p.Y < rig.Bottom + 0.01f).Max(Radius) > rig.R(rig.Bottom) + 0.03f, "the skirt flares");
                Assert.False(glb.Has(Attach.LegL) || glb.Has(Attach.LegR), "the skirt has no legs");
                continue;
            }
            var rigs = s_rigs.Where((_, c) => Fits.Matches(item.Fit, c)).ToList();
            foreach (var (leg, pivot) in new[] { (Attach.LegL, Attach.LegLPivot), (Attach.LegR, Attach.LegRPivot) })
            {
                var v = glb.Vertices(leg);
                string what = $"{item.Id}: {leg}";
                foreach (var rig in rigs)
                {
                    // Round the leg tube, snug over it (not poking into it), from just under the body down.
                    float inside = v.Min(p => Rig.LegDistance(p, pivot.X)) - rig.LegRadius;
                    Assert.True(inside > -0.002f, $"{what} pokes into the leg ({inside})");
                    Assert.True(v.All(p => Rig.LegDistance(p, pivot.X) < rig.LegRadius + 0.045f), $"{what} is not snug round the leg");
                    float high = v.Max(p => p.Y), low = v.Min(p => p.Y);
                    Assert.InRange(high, rig.Bottom - 0.03f, rig.Bottom);
                    Assert.True(AllRoundLeg(v, pivot.X, low, high, rig.LegRadius + 0.025f), $"{what} does not go all round the leg");
                    var foot = rig.Glb.Vertices("DefaultShoes" + leg);
                    float footTop = foot.Max(p => p.Y);
                    if (item.Id == "shorts")
                        Assert.True(low > (rig.Bottom + footTop) / 2, $"{what}: shorts are short, ending above the knee");
                    else
                        Assert.InRange(low, footTop + 0.005f, footTop + 0.05f);
                }
            }
        }
    }

    [Fact]
    public void ShoesFitTheRoundedFeet()
    {
        foreach (var item in On(Slot.Shoes))
        {
            var glb = Model(item.ModelPath);
            foreach (var rig in s_rigs)
                foreach (var leg in new[] { Attach.LegL, Attach.LegR })
                {
                    string what = $"{item.Id}: {leg} on {rig.File}";
                    var v = glb.Vertices(leg);
                    var foot = rig.Glb.Vertices("DefaultShoes" + leg);
                    Vector3 low = v.Aggregate(Vector3.Min), high = v.Aggregate(Vector3.Max);
                    Vector3 footLow = foot.Aggregate(Vector3.Min), footHigh = foot.Aggregate(Vector3.Max);
                    Vector3 footCenter = (footLow + footHigh) / 2, footRadii = (footHigh - footLow) / 2;
                    // Flat on the ground: a sole with many vertices at y 0.
                    Assert.InRange(low.Y, -0.002f, 0.002f);
                    Assert.True(v.Count(p => MathF.Abs(p.Y) < 0.001f) >= 16, $"{what} is not flat on the ground");
                    Assert.True(high.Y >= footHigh.Y + 0.005f, $"{what} is lower than the foot");
                    // Over the foot: nothing pokes into it, as wide and long as it (a little more), and not much more.
                    var into = v.Where(p => Vector3.Divide(p - footCenter, footRadii).Length() < 0.98f).ToList();
                    Assert.True(into.Count == 0, $"{what} pokes into the foot at {(into.Count > 0 ? into[0] : default)}");
                    Assert.True(low.X <= footLow.X - 0.005f && high.X >= footHigh.X + 0.005f, $"{what} across");
                    Assert.True(low.Z <= footLow.Z - 0.005f && high.Z >= footHigh.Z + 0.005f, $"{what} along");
                    Assert.True(high.X - low.X < (footHigh.X - footLow.X) + 0.05f, $"{what} too wide");
                    Assert.True(high.Z - low.Z < (footHigh.Z - footLow.Z) + 0.06f, $"{what} too long");
                }
            // Whatever a shoe has up the leg (a boot's shaft) goes under every trouser leg, inside it.
            foreach (var bottom in On(Slot.Bottom).Select(b => Model(b.ModelPath)).Where(b => b.Has(Attach.LegL)))
                foreach (var (leg, pivot) in new[] { (Attach.LegL, Attach.LegLPivot), (Attach.LegR, Attach.LegRPivot) })
                {
                    var trousers = bottom.Vertices(leg);
                    float hem = trousers.Min(p => p.Y), inner = trousers.Min(p => Rig.LegDistance(p, pivot.X));
                    float shaft = glb.Vertices(leg).Where(p => p.Y > hem).Select(p => Rig.LegDistance(p, pivot.X)).DefaultIfEmpty(0f).Max();
                    Assert.True(shaft < inner - 0.001f, $"{item.Id}: {leg} does not go under the trouser legs ({shaft} out, inside {inner})");
                }
        }
    }

    [Fact]
    public void HatsGlassesClipsAndHairSitOnTheHeadAboveTheBody()
    {
        var face = On(Slot.Face).Select(i => i.ModelPath).ToHashSet();
        var models = Catalog.Items.ToArray().Select(i => i.ModelPath).Concat(HairModels());
        foreach (var path in models)
        {
            var glb = Model(path);
            if (!glb.Has(Attach.Head))
                continue;
            var v = glb.Vertices(Attach.Head);
            foreach (var rig in s_rigs)
            {
                var (c, r) = (rig.HeadCenter, rig.HeadRadius);
                Assert.True(v.Min(p => p.Y) > rig.Top + 0.03f, $"{path} hangs down onto the body");
                Assert.True(v.Max(p => Vector3.Distance(p, c)) < r * 2.1f, $"{path} floats away from the head");
                Assert.True(v.Min(p => Vector3.Distance(p, c)) < r * 1.1f, $"{path} does not touch the head");
                // Only glasses go over the eyes.
                if (!face.Contains(path))
                    foreach (var (eye, _) in rig.Eyes)
                        Assert.True(v.Min(p => Vector3.Distance(p, eye)) > 0.06f, $"{path} covers an eye");
            }
        }
        // Hats and hair cover the top of the round head.
        var girl = s_rigs[0];
        foreach (var path in On(Slot.Head).Where(i => i.Id is "beanie" or "mushroom_hat").Select(i => i.ModelPath).Concat(HairModels()))
        {
            var v = Model(path).Vertices(Attach.Head);
            var crown = v.Where(p => p.Y > girl.HeadCenter.Y + girl.HeadRadius * 0.7f && Vector3.Distance(p, girl.HeadCenter) > girl.HeadRadius).ToList();
            Assert.True(v.Max(p => p.Y) > girl.HeadCenter.Y + girl.HeadRadius, $"{path} does not cover the top of the head");
            var headings = crown.Select(p => (int)MathF.Floor((MathF.Atan2(p.X, -p.Z) + MathF.PI) / (2 * MathF.PI) * 12) % 12).ToHashSet();
            Assert.True(headings.Count == 12, $"{path} leaves the crown bare");
        }
    }

    [Fact]
    public void GlassesSitInFrontOfTheEyesWithFramesRoundThem()
    {
        foreach (var item in On(Slot.Face).Where(i => i.Id != Catalog.SpiderSilkScarf))
        {
            var v = Model(item.ModelPath).Vertices(Attach.Head);
            foreach (var rig in s_rigs)
                foreach (var (eye, front) in rig.Eyes)
                {
                    var n = Vector3.Normalize(eye - rig.HeadCenter);
                    var across = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, n));
                    var up = Vector3.Cross(n, across);
                    // The vertices in front of the eye, by where they are round its axis.
                    var near = v.Select(p => (Depth: Vector3.Dot(p - rig.HeadCenter, n), X: Vector3.Dot(p - eye, across), Y: Vector3.Dot(p - eye, up)))
                        .Where(q => MathF.Sqrt(q.X * q.X + q.Y * q.Y) < 0.08f).ToList();
                    var ring = near.Where(q => MathF.Sqrt(q.X * q.X + q.Y * q.Y) > 0.045f).ToList();
                    var sectors = ring.Select(q => (int)MathF.Floor((MathF.Atan2(q.Y, q.X) + MathF.PI) / (2 * MathF.PI) * 8) % 8).ToHashSet();
                    Assert.True(sectors.Count == 8, $"{item.Id}: the frame does not go round the eye on {rig.File}");
                    Assert.True(ring.Average(q => q.Depth) > front, $"{item.Id}: the frame is not in front of the eye on {rig.File}");
                    Assert.True(near.Where(q => MathF.Sqrt(q.X * q.X + q.Y * q.Y) < 0.07f).All(q => q.Depth > rig.HeadRadius * 0.95f), $"{item.Id}: the frame sinks into the face on {rig.File}");
                    if (item.Id == "round_glasses")
                        Assert.True(near.All(q => MathF.Sqrt(q.X * q.X + q.Y * q.Y) > 0.035f), "round_glasses: no lenses hide the eyes");
                }
        }
    }

    [Fact]
    public void BackpacksSitOnTheBodysBackAndTheScarfRoundItsTop()
    {
        foreach (var (item, name, rig, v) in BodiesOn(Slot.Back))
        {
            string what = $"{item.Id} ({name} on {rig.File})";
            // The bag behind the body (Godot's +Z), most of it below the top, straps over the top.
            var bag = v.Where(p => p.Z > rig.R(p.Y) + 0.03f).ToList();
            Assert.True(bag.Count > v.Count / 3, $"{what}: the bag is not on the back");
            float y = bag.Average(p => p.Y);
            Assert.True(y > rig.Bottom + 0.15f && y < rig.Top - 0.15f, $"{what}: the bag is not on the back");
            Assert.True(bag.Max(p => p.Z) < rig.MaxR + 0.16f, $"{what} sticks out far behind");
            Assert.Contains(v, p => p.Z < -0.05f && rig.Gap(p) < 0.05f);
            Assert.Contains(v, p => p.Y > rig.Top - 0.15f);
        }
        foreach (var (item, name, rig, scarf) in BodiesOn(Slot.Face))
        {
            string what = $"the scarf ({name} on {rig.File})";
            Assert.Equal(Catalog.SpiderSilkScarf, item.Id);
            Assert.True(AllRound(rig, scarf, rig.Top - 0.15f, rig.Top - 0.06f, 0.06f), $"{what} goes round the top");
            Assert.True(scarf.Min(p => p.Y) > rig.Bottom + (rig.Top - rig.Bottom) * 0.35f, $"{what}: its tails hang down the front a little");
            Assert.True(scarf.Where(p => p.Y < rig.Top - 0.2f).All(p => p.Z < 0f), $"{what}: its tails hang down the front");
        }
    }

    private static void Near(Vector3 expected, Vector3 actual, string what) =>
        Assert.True(Vector3.Distance(expected, actual) < 0.002f, $"{what}: {actual}, expected {expected}");

    /// <summary>Hue in degrees (0 red), saturation and value of an RGB color.</summary>
    private static (float Hue, float Saturation, float Value) Hsv(Vector3 c)
    {
        float max = Math.Max(c.X, Math.Max(c.Y, c.Z)), min = Math.Min(c.X, Math.Min(c.Y, c.Z)), d = max - min;
        float hue = d == 0 ? 0 : max == c.X ? 60f * ((c.Y - c.Z) / d) : max == c.Y ? 60f * ((c.Z - c.X) / d + 2) : 60f * ((c.X - c.Y) / d + 4);
        return ((hue + 360f) % 360f, max == 0 ? 0 : d / max, max);
    }

    [Fact]
    public void MushroomColorsAreVivid()
    {
        // Caps a saturated, bright cherry red (not brownish or dull pink): before they were (0.92, 0.22, 0.25), saturation 0.76.
        var (hue, saturation, value) = Hsv(MushroomLook.CapRed);
        Assert.True(hue >= 350f || hue <= 5f, $"cap hue {hue}");
        Assert.True(saturation >= 0.88f, $"cap saturation {saturation}");
        Assert.True(value >= 0.9f, $"cap brightness {value}");
        Assert.Equal(new Vector3(1f, 1f, 1f), MushroomLook.Spot);
        // Stems: a clean light cream, warm but not white and not muddy.
        var stem = MushroomLook.Stem;
        Assert.True(stem.X >= 0.98f && stem.X >= stem.Y && stem.Y > stem.Z && stem.Z >= 0.78f && stem.Z <= 0.9f, $"stem {stem}");
        var (_, stemSaturation, _) = Hsv(stem);
        Assert.True(stemSaturation is > 0.08f and < 0.25f, $"stem saturation {stemSaturation}");
    }

    [Fact]
    public void EveryMushroomOnTheClothesUsesTheVividColors()
    {
        foreach (var id in MushroomLook.Items)
        {
            Assert.True(Catalog.TryGet(id, out var item), id);
            var colors = Model(item.ModelPath).Colors();
            Assert.Contains(MushroomLook.SpotsMaterial, colors.Keys);
            foreach (var (name, color) in colors)
            {
                if (name == MushroomLook.CapMaterial)
                    Near(MushroomLook.CapRed, color, $"{id}: {name}");
                else if (name == MushroomLook.SpotsMaterial)
                    Near(MushroomLook.Spot, color, $"{id}: {name}");
                else if (name is MushroomLook.StemMaterial or MushroomLook.GillsMaterial)
                    Near(MushroomLook.Stem, color, $"{id}: {name}");
            }
        }
        Assert.Contains(MushroomLook.CapMaterial, Model(Catalog.Items[Catalog.IndexOf("mushroom_hat")].ModelPath).Colors().Keys);
        // The Spotted Mushroom Dress is a cap: its cloth is the cap red.
        Near(MushroomLook.CapRed, Model(Catalog.Items[Catalog.IndexOf("spotted_dress")].ModelPath).Colors()["Cloth"], "spotted_dress: Cloth");
        // Only the clothes with a mushroom have mushroom materials.
        foreach (var item in Catalog.Items)
            if (Array.IndexOf(MushroomLook.Items, item.Id) < 0)
                Assert.DoesNotContain(Model(item.ModelPath).Colors().Keys, MushroomLook.IsMushroom);
    }

    [Fact]
    public void EveryOtherColorOfTheMushroomClothesStaysAsItWas()
    {
        var expected = new Dictionary<(string, string), Vector3>
        {
            [("mushroom_hoodie", "Cloth")] = new(0.55f, 0.85f, 0.72f),
            [("mushroom_hoodie", "Trim")] = new(0.42f, 0.72f, 0.6f),
            [("mushroom_hoodie", "String")] = new(1f, 0.97f, 0.92f),
            [("mushroom_backpack", "Strap")] = new(0.6f, 0.35f, 0.2f),
            [("spotted_dress", "Collar")] = new(1f, 0.92f, 0.8f),
            [("ladybug_backpack", "Shell")] = new(0.92f, 0.18f, 0.2f),
        };
        foreach (var ((id, material), color) in expected)
            Near(color, Model(Catalog.Items[Catalog.IndexOf(id)].ModelPath).Colors()[material], $"{id}: {material}");
    }

    [Fact]
    public void MushroomCapsGetAGlossyBandAndMushroomsARimLight()
    {
        Assert.Equal(1f, MushroomLook.Gloss(MushroomLook.CapMaterial));
        Assert.Equal(1f, MushroomLook.Gloss(MushroomLook.SpotsMaterial));
        Assert.Equal(0f, MushroomLook.Gloss(MushroomLook.StemMaterial));
        foreach (var name in new[] { MushroomLook.CapMaterial, MushroomLook.SpotsMaterial, MushroomLook.StemMaterial, MushroomLook.GillsMaterial })
            Assert.True(MushroomLook.Rim(name) > 0f, name);
        foreach (var name in new[] { "Cloth", "Trim", "Strap", "Shell", "Hair", "Skin", "" })
            Assert.Equal((0f, 0f), (MushroomLook.Gloss(name), MushroomLook.Rim(name)));
        // The toon shader draws them and the view sets them on the mushroom surfaces.
        string shader = File.ReadAllText(Path.Combine(RepoRoot(), "modules", "Wardrobe", "Godot", "Shaders", "toon.gdshader"));
        Assert.Contains("uniform float gloss", shader);
        Assert.Contains("uniform float rim_light", shader);
        Assert.Matches(@"SPECULAR_LIGHT\s*\+=.*gloss", shader);
        string view = File.ReadAllText(Path.Combine(RepoRoot(), "modules", "Wardrobe", "Godot", "ToonGlb.cs"));
        Assert.Contains("SetShaderParameter(\"gloss\", MushroomLook.Gloss(", view);
        Assert.Contains("SetShaderParameter(\"rim_light\", MushroomLook.Rim(", view);
    }

    [Fact]
    public void TheModelScriptsUseTheSameMushroomColors()
    {
        string kit = File.ReadAllText(Path.Combine(RepoRoot(), "modules", "Wardrobe", "Models", "kit.py"));
        Vector3 Py(string name)
        {
            var m = Regex.Match(kit, name + @" = \(([\d.]+), ([\d.]+), ([\d.]+)\)");
            Assert.True(m.Success, name);
            float F(int i) => float.Parse(m.Groups[i].Value, System.Globalization.CultureInfo.InvariantCulture);
            return new Vector3(F(1), F(2), F(3));
        }
        Near(MushroomLook.CapRed, Py("CAP_RED"), "CAP_RED");
        Near(MushroomLook.Spot, Py("SPOT_WHITE"), "SPOT_WHITE");
        Near(MushroomLook.Stem, Py("STEM_CREAM"), "STEM_CREAM");
    }

    /// <summary>
    /// A .glb read back: each node's name, parent, mesh and world transform (glTF's frame, Godot's),
    /// and its mesh's vertices by material.
    /// </summary>
    private sealed class Glb
    {
        public readonly List<(string Name, int Parent, int Mesh, Matrix4x4 World)> Nodes = new();
        private readonly Dictionary<string, int> _index = new();
        private readonly System.Text.Json.JsonElement _json;
        private readonly byte[] _bin;

        public Glb(string file)
        {
            Assert.True(File.Exists(file), file);
            var bytes = File.ReadAllBytes(file);
            int jsonLength = BitConverter.ToInt32(bytes, 12);
            _json = System.Text.Json.JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes, 20, jsonLength)).RootElement;
            int binStart = 20 + jsonLength;
            _bin = bytes[(binStart + 8)..(binStart + 8 + BitConverter.ToInt32(bytes, binStart))];
            var nodes = _json.GetProperty("nodes");
            var parents = Enumerable.Repeat(-1, nodes.GetArrayLength()).ToArray();
            for (int i = 0; i < parents.Length; i++)
                if (nodes[i].TryGetProperty("children", out var children))
                    foreach (var child in children.EnumerateArray())
                        parents[child.GetInt32()] = i;
            var world = new Matrix4x4?[parents.Length];
            Matrix4x4 World(int i) => world[i] ??= Local(nodes[i]) * (parents[i] < 0 ? Matrix4x4.Identity : World(parents[i]));
            for (int i = 0; i < parents.Length; i++)
            {
                string name = nodes[i].GetProperty("name").GetString()!;
                _index[name] = i;
                Nodes.Add((name, parents[i], nodes[i].TryGetProperty("mesh", out var mesh) ? mesh.GetInt32() : -1, World(i)));
            }
        }

        private static Matrix4x4 Local(System.Text.Json.JsonElement node)
        {
            float[] Floats(string key, params float[] fallback) =>
                node.TryGetProperty(key, out var v) ? v.EnumerateArray().Select(x => x.GetSingle()).ToArray() : fallback;
            var t = Floats("translation", 0, 0, 0);
            var r = Floats("rotation", 0, 0, 0, 1);
            var s = Floats("scale", 1, 1, 1);
            return Matrix4x4.CreateScale(s[0], s[1], s[2]) * Matrix4x4.CreateFromQuaternion(new Quaternion(r[0], r[1], r[2], r[3])) *
                Matrix4x4.CreateTranslation(t[0], t[1], t[2]);
        }

        public bool Has(string name) => _index.ContainsKey(name);

        /// <summary>Each material's base color by name (glTF's default white when it has none).</summary>
        public Dictionary<string, Vector3> Colors()
        {
            var colors = new Dictionary<string, Vector3>();
            foreach (var m in _json.GetProperty("materials").EnumerateArray())
            {
                var c = new Vector3(1f, 1f, 1f);
                if (m.TryGetProperty("pbrMetallicRoughness", out var pbr) && pbr.TryGetProperty("baseColorFactor", out var f))
                    c = new Vector3(f[0].GetSingle(), f[1].GetSingle(), f[2].GetSingle());
                colors[m.GetProperty("name").GetString()!] = c;
            }
            return colors;
        }

        public Vector3 Origin(string name)
        {
            Assert.True(Has(name), $"no node {name}");
            return Nodes[_index[name]].World.Translation;
        }

        /// <summary>The world positions of the node's own mesh's vertices (only those in <paramref name="material"/> when given).</summary>
        public List<Vector3> Vertices(string name, string? material = null)
        {
            Assert.True(Has(name), $"no node {name}");
            var result = new List<Vector3>();
            var node = Nodes[_index[name]];
            if (node.Mesh < 0)
                return result;
            foreach (var primitive in _json.GetProperty("meshes")[node.Mesh].GetProperty("primitives").EnumerateArray())
            {
                string mat = _json.GetProperty("materials")[primitive.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;
                if (material != null && mat != material)
                    continue;
                var accessor = _json.GetProperty("accessors")[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
                var view = _json.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
                int start = (view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0) +
                    (accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
                int stride = view.TryGetProperty("byteStride", out var st) ? st.GetInt32() : 12;
                for (int i = 0; i < accessor.GetProperty("count").GetInt32(); i++)
                {
                    int at = start + i * stride;
                    var local = new Vector3(BitConverter.ToSingle(_bin, at), BitConverter.ToSingle(_bin, at + 4), BitConverter.ToSingle(_bin, at + 8));
                    result.Add(Vector3.Transform(local, node.World));
                }
            }
            return result;
        }

        /// <summary>The vertices of the node and every node under it.</summary>
        public List<Vector3> VerticesUnder(string name)
        {
            var result = Vertices(name);
            for (int i = 0; i < Nodes.Count; i++)
                if (Nodes[i].Parent == _index[name])
                    result.AddRange(VerticesUnder(Nodes[i].Name));
            return result;
        }
    }
}

public class WardrobeModuleTests
{
    /// <summary>
    /// Fake of the shell's bus: delivers messages like the real one and records what the
    /// wardrobe publishes. With <see cref="Coins"/> set it also plays the wallet, answering every
    /// SpendCoins with a SpendResult right away.
    /// </summary>
    private sealed class FakeBus : IMessageBus
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();
        public readonly List<AppearanceState> Appearances = new();
        public readonly List<WardrobeState> States = new();
        public readonly List<SpendCoins> Spends = new();
        public readonly List<ItemReceived> Received = new();
        public readonly List<PreviewYaw> Yaws = new();
        public readonly List<SavePart> Parts = new();
        public readonly List<object> All = new();
        public int? Coins;

        public void Subscribe<T>(IMessageHandler<T> handler) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<object>();
            list.Add(handler);
        }

        public void Publish<T>(in T message) where T : struct
        {
            All.Add(message);
            if (message is SavePart part)
                Parts.Add(part);
            if (message is AppearanceState appearance)
                Appearances.Add(appearance);
            if (message is WardrobeState state)
                States.Add(state);
            if (message is PreviewYaw yaw)
                Yaws.Add(yaw);
            if (message is ItemReceived received)
                Received.Add(received);
            if (message is SpendCoins spend)
            {
                Spends.Add(spend);
                if (Coins is int coins)
                {
                    bool ok = spend.Amount >= 0 && spend.Amount <= coins;
                    if (ok)
                        Coins = coins - spend.Amount;
                    Publish(new SpendResult(spend.RequestId, ok, Coins.Value));
                }
            }
            if (_handlers.TryGetValue(typeof(T), out var list))
                foreach (var handler in list)
                    ((IMessageHandler<T>)handler).Handle(message);
        }
    }

    private static readonly TickContext Step = new(0, 1.0 / 60);

    private static WardrobeModule Started(FakeBus bus, CharacterKind? character = null)
    {
        var wardrobe = new WardrobeModule();
        wardrobe.Start(bus);
        if (character is CharacterKind kind)
            bus.Publish(Player(kind));
        return wardrobe;
    }

    [Fact]
    public void DeviceFollowsActiveDeviceAndTheViewsNotes()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        Assert.Equal(DeviceKind.KeyboardMouse, wardrobe.Device);
        bus.Publish(new ActiveDevice(DeviceKind.PlayStation));
        Assert.Equal(DeviceKind.PlayStation, wardrobe.Device);

        // While a screen is open the view takes the input: its note wins over the Input module's
        // unchanged device, published again every tick.
        bus.Publish(new OpenWardrobe());
        wardrobe.NoteDevice(DeviceKind.KeyboardMouse);
        bus.Publish(new ActiveDevice(DeviceKind.PlayStation));
        wardrobe.Tick(Step);
        Assert.Equal(DeviceKind.KeyboardMouse, wardrobe.Device);
        wardrobe.NoteDevice(DeviceKind.Xbox);
        Assert.Equal(DeviceKind.Xbox, wardrobe.Device);

        // A new device from the Input module is followed at once.
        bus.Publish(new ActiveDevice(DeviceKind.Nintendo));
        Assert.Equal(DeviceKind.Nintendo, wardrobe.Device);

        // Opening a screen shows the Input module's device (the press that opened it went through it).
        wardrobe.Close();
        wardrobe.NoteDevice(DeviceKind.KeyboardMouse);
        bus.Publish(new OpenShop());
        Assert.Equal(DeviceKind.Nintendo, wardrobe.Device);
    }

    private static PlayerState Player(CharacterKind kind) =>
        new(Vector3.Zero, 0f, Vector3.Zero, true, 10, 10, true, kind, Area.Home);

    private static MonsterKilled Kill(int id, MonsterKind kind) =>
        new(id, kind, MonsterRewards.For(kind), Vector3.Zero, 0);

    [Fact]
    public void StartersAreOwnedAndNothingElse()
    {
        var wardrobe = Started(new FakeBus());
        for (int i = 0; i < Catalog.Count; i++)
            Assert.Equal(Catalog.Items[i].Source == Sources.Starter, wardrobe.Owns(i));
        // Every starter outfit can be put on.
        foreach (var outfit in Catalog.StarterOutfits)
            foreach (var id in new[] { outfit.Top, outfit.Bottom, outfit.Shoes, outfit.Head })
                if (id != "")
                    Assert.True(wardrobe.Owns(id), id);
    }

    [Fact]
    public void PublishesAppearanceAndWardrobeStateEveryTick()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        for (int i = 0; i < 3; i++)
            wardrobe.Tick(Step);
        Assert.Equal(3, bus.Appearances.Count);
        Assert.Equal(3, bus.States.Count);
        Assert.All(bus.States, s => Assert.False(s.Open));
        // The boy starts in the first starter outfit.
        var look = bus.Appearances[^1];
        Assert.Equal("striped_tee", look.Top);
        Assert.Equal("shorts", look.Bottom);
        Assert.Equal("sneakers", look.Shoes);
        Assert.Equal("", look.Face);
        Assert.Equal("", look.Back);
    }

    [Theory]
    [InlineData(CharacterKind.Girl)]
    [InlineData(CharacterKind.Boy)]
    public void ANewCharacterHasNoHairUntilAStyleIsPicked(CharacterKind kind)
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, kind);
        wardrobe.Tick(Step);
        Assert.Equal(Looks.NoHair, wardrobe.HairStyle);
        Assert.Equal(Looks.NoHair, bus.Appearances[^1].HairStyle);
        Assert.Equal("", Looks.HairStyles[bus.Appearances[^1].HairStyle].ModelPath);
        // A style can still be picked, and "No hair" picked back.
        bus.Publish(new SetLook(0, 3, 0, 0));
        wardrobe.Tick(Step);
        Assert.Equal(3, bus.Appearances[^1].HairStyle);
        bus.Publish(new SetLook(0, Looks.NoHair, 0, 0));
        wardrobe.Tick(Step);
        Assert.Equal(Looks.NoHair, bus.Appearances[^1].HairStyle);
    }

    [Fact]
    public void TheGirlStartsInTheMushroomDressWithNoBottomShown()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        wardrobe.Tick(Step);
        var look = bus.Appearances[^1];
        Assert.Equal("spotted_dress", look.Top);
        Assert.Equal("", look.Bottom);
        Assert.Equal("mary_janes", look.Shoes);
        Assert.Equal("big_bow", look.Head);
    }

    [Fact]
    public void EquipAndUnequip()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        bus.Publish(new Equip("rain_boots"));
        bus.Publish(new Equip("beanie"));
        wardrobe.Tick(Step);
        Assert.Equal("rain_boots", bus.Appearances[^1].Shoes);
        Assert.Equal("beanie", bus.Appearances[^1].Head);

        bus.Publish(Equip.Unequip(Slot.Head));
        bus.Publish(new Equip("", Slot.Shoes));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Head);
        Assert.Equal("", bus.Appearances[^1].Shoes);
        Assert.Equal("striped_tee", bus.Appearances[^1].Top);
        Assert.Equal(-1, wardrobe.Worn(Slot.Head));
    }

    [Fact]
    public void ItemsNotOwnedOrNotFittingOrUnknownCannotBePutOn()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        bus.Publish(new Equip("mushroom_hat"));      // shop item, not owned
        bus.Publish(new Equip("spotted_dress"));     // owned starter, but a girl's dress
        bus.Publish(new Equip("no_such_item"));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Head);
        Assert.Equal("striped_tee", bus.Appearances[^1].Top);
    }

    [Fact]
    public void ADressHidesTheBottomAndTakingItOffShowsItAgain()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new Equip("striped_tee"));
        bus.Publish(new Equip("overalls"));
        wardrobe.Tick(Step);
        Assert.Equal("overalls", bus.Appearances[^1].Bottom);

        bus.Publish(new Equip("spotted_dress"));
        wardrobe.Tick(Step);
        Assert.Equal("spotted_dress", bus.Appearances[^1].Top);
        Assert.Equal("", bus.Appearances[^1].Bottom);
        Assert.True(wardrobe.BottomHidden);

        // A bottom put on under the dress stays hidden until the dress comes off.
        bus.Publish(new Equip("shorts"));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Bottom);

        bus.Publish(new Equip("overalls_top"));
        wardrobe.Tick(Step);
        Assert.Equal("overalls_top", bus.Appearances[^1].Top);
        Assert.Equal("shorts", bus.Appearances[^1].Bottom);
        Assert.False(wardrobe.BottomHidden);
    }

    [Fact]
    public void SetLookChangesTheLookOnlyWhenInRange()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new SetLook(5, 4, 6, 2));
        wardrobe.Tick(Step);
        var look = bus.Appearances[^1];
        Assert.Equal((5, 4, 6, 2), (look.Skin, look.HairStyle, look.HairColor, look.EyeColor));

        foreach (var bad in new[]
                 {
                     new SetLook(6, 0, 0, 0), new SetLook(0, Looks.HairStyles.Length, 0, 0), new SetLook(0, 0, 8, 0),
                     new SetLook(0, 0, 0, 6), new SetLook(-1, 0, 0, 0),
                 })
            bus.Publish(bad);
        wardrobe.Tick(Step);
        look = bus.Appearances[^1];
        Assert.Equal((5, 4, 6, 2), (look.Skin, look.HairStyle, look.HairColor, look.EyeColor));
        Assert.True(Looks.Valid(look.Skin, look.HairStyle, look.HairColor, look.EyeColor));
    }

    [Fact]
    public void OpenWardrobeAndShopAndClose()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        bus.Publish(new OpenWardrobe());
        wardrobe.Tick(Step);
        Assert.Equal(new WardrobeState(true, false), bus.States[^1]);
        bus.Publish(new OpenShop());
        wardrobe.Tick(Step);
        Assert.Equal(new WardrobeState(true, true), bus.States[^1]);
        wardrobe.Close();
        wardrobe.Tick(Step);
        Assert.Equal(new WardrobeState(false, false), bus.States[^1]);
    }

    [Fact]
    public void PreviewYawIsPublishedOnlyWhileAScreenIsOpenAndStartsFacingTheCamera()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        wardrobe.SetTurnButtons(false, true);
        wardrobe.Tick(Step);
        Assert.Empty(bus.Yaws);                    // closed: nothing, and no turning

        bus.Publish(new OpenWardrobe());
        wardrobe.SetTurnButtons(false, false);
        wardrobe.Tick(Step);
        wardrobe.Tick(Step);
        Assert.Equal(new[] { 0f, 0f }, bus.Yaws.ConvertAll(y => y.Yaw));
        wardrobe.Close();
        wardrobe.Tick(Step);
        Assert.Equal(2, bus.Yaws.Count);

        bus.Publish(new OpenShop());
        wardrobe.Tick(Step);
        Assert.Equal(3, bus.Yaws.Count);           // the shop turns the character too
        Assert.Equal(0f, bus.Yaws[^1].Yaw);
    }

    [Fact]
    public void TurnButtonsTurnAtTheTurnSpeed()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        bus.Publish(new OpenWardrobe());
        float step = WardrobeModule.TurnRadiansPerSecond / 60f;

        wardrobe.SetTurnButtons(false, true);      // E / RB: the front turns to the right
        for (int i = 0; i < 10; i++)
            wardrobe.Tick(Step);
        Assert.Equal(10 * step, bus.Yaws[^1].Yaw, 4);
        for (int i = 1; i < 10; i++)
            Assert.Equal(step, bus.Yaws[i].Yaw - bus.Yaws[i - 1].Yaw, 4);

        wardrobe.SetTurnButtons(true, false);      // Q / LB: back to the left
        for (int i = 0; i < 15; i++)
            wardrobe.Tick(Step);
        Assert.Equal(-5 * step, bus.Yaws[^1].Yaw, 4);

        wardrobe.SetTurnButtons(true, true);       // both: no turn
        wardrobe.Tick(Step);
        Assert.Equal(-5 * step, bus.Yaws[^1].Yaw, 4);
        Assert.Equal(wardrobe.Yaw, bus.Yaws[^1].Yaw);
    }

    [Fact]
    public void TheRightStickTurnsPastItsDeadZoneAndTheButtonsWin()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        bus.Publish(new OpenShop());
        float full = WardrobeModule.TurnRadiansPerSecond / 60f;

        wardrobe.SetTurnStick(0.15f);              // inside the dead zone
        wardrobe.Tick(Step);
        Assert.Equal(0f, bus.Yaws[^1].Yaw);

        wardrobe.SetTurnStick(1f);
        wardrobe.Tick(Step);
        Assert.Equal(full, bus.Yaws[^1].Yaw, 4);

        wardrobe.SetTurnStick(-0.6f);              // half way between the dead zone and full
        wardrobe.Tick(Step);
        Assert.Equal(full - 0.5f * full, bus.Yaws[^1].Yaw, 4);

        wardrobe.SetTurnStick(-1f);
        wardrobe.SetTurnButtons(false, true);      // E held with the stick left: E wins
        wardrobe.Tick(Step);
        Assert.Equal(1.5f * full, bus.Yaws[^1].Yaw, 4);
    }

    [Fact]
    public void MouseDragTurnsAtTheNextTickAndYawWraps()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus);
        wardrobe.Drag(500f);                       // closed: ignored
        bus.Publish(new OpenWardrobe());
        wardrobe.Tick(Step);
        Assert.Equal(0f, bus.Yaws[^1].Yaw);

        wardrobe.Drag(30f);
        wardrobe.Drag(20f);
        Assert.Equal(0f, wardrobe.Yaw);            // applied on the tick
        wardrobe.Tick(Step);
        Assert.Equal(50f * WardrobeModule.DragRadiansPerPixel, bus.Yaws[^1].Yaw, 4);
        wardrobe.Tick(Step);
        Assert.Equal(0.5f, bus.Yaws[^1].Yaw, 4);   // a drag is used once

        wardrobe.Drag(-100f);
        wardrobe.Tick(Step);
        Assert.Equal(-0.5f, bus.Yaws[^1].Yaw, 4);

        // Past half a turn it wraps round to the other side, staying in [-π, π).
        wardrobe.Drag(-300f);
        wardrobe.Tick(Step);
        Assert.Equal(-3.5f + MathF.Tau, bus.Yaws[^1].Yaw, 4);
        wardrobe.Drag(float.NaN);
        wardrobe.Tick(Step);
        Assert.InRange(bus.Yaws[^1].Yaw, -MathF.PI, MathF.PI);

        // Reopening faces the camera again.
        wardrobe.Close();
        bus.Publish(new OpenWardrobe());
        wardrobe.Tick(Step);
        Assert.Equal(0f, bus.Yaws[^1].Yaw);
    }

    [Fact]
    public void TryingOnShowsTheItemAndRevertsWhenNotBought()
    {
        var bus = new FakeBus { Coins = 100 };
        var wardrobe = Started(bus, CharacterKind.Girl);   // the Mushroom Dress, Mary-Janes, Big Bow
        bus.Publish(new Equip("shorts"));                  // kept under the dress
        wardrobe.TryOn("mushroom_hat");                    // shop not open: nothing
        wardrobe.Tick(Step);
        Assert.Equal("big_bow", bus.Appearances[^1].Head);

        bus.Publish(new OpenShop());
        wardrobe.TryOn("mushroom_hat");
        wardrobe.Tick(Step);
        Assert.Equal("mushroom_hat", bus.Appearances[^1].Head);
        Assert.Equal("spotted_dress", bus.Appearances[^1].Top);
        Assert.Equal(Catalog.IndexOf("big_bow"), wardrobe.Worn(Slot.Head));   // still worn underneath

        // Another highlighted item: the hat goes back to the bow.
        wardrobe.TryOn("jeans");
        wardrobe.Tick(Step);
        Assert.Equal("big_bow", bus.Appearances[^1].Head);
        Assert.Equal("", bus.Appearances[^1].Bottom);      // under the dress
        wardrobe.TryOn("sweater_vest");                    // a top instead of the dress shows the bottom
        wardrobe.Tick(Step);
        Assert.Equal(("sweater_vest", "shorts"), (bus.Appearances[^1].Top, bus.Appearances[^1].Bottom));

        // Not enough M¢ for it: still only tried on.
        wardrobe.TryOn("party_dress");
        wardrobe.Buy("party_dress");
        wardrobe.Tick(Step);
        Assert.Equal(BuyResult.NotEnough, wardrobe.LastBuy);
        Assert.Equal("party_dress", bus.Appearances[^1].Top);

        // Nothing highlighted, or a non-shop item: what is worn.
        wardrobe.TryOn("");
        wardrobe.Tick(Step);
        Assert.Equal("spotted_dress", bus.Appearances[^1].Top);
        wardrobe.TryOn("striped_tee");
        wardrobe.Tick(Step);
        Assert.Equal("spotted_dress", bus.Appearances[^1].Top);
        Assert.Equal(-1, wardrobe.TryingOn);

        // Closing the shop takes off what was not bought.
        wardrobe.TryOn("round_glasses");
        wardrobe.Tick(Step);
        Assert.Equal("round_glasses", bus.Appearances[^1].Face);
        wardrobe.Close();
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Face);
        Assert.Equal(-1, wardrobe.TryingOn);
        wardrobe.TryOn("round_glasses");                   // closed: ignored
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Face);
    }

    [Fact]
    public void TryingOnOnlyItemsThatFit()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        bus.Publish(new OpenShop());
        wardrobe.TryOn("flower_dress");                    // girl only
        wardrobe.Tick(Step);
        Assert.Equal("striped_tee", bus.Appearances[^1].Top);
        wardrobe.TryOn("no_such_item");
        Assert.Equal(-1, wardrobe.TryingOn);
    }

    [Fact]
    public void AnItemBoughtWhileTriedOnStaysOn()
    {
        var bus = new FakeBus { Coins = 1000 };
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new OpenShop());
        wardrobe.TryOn("star_sunglasses");
        wardrobe.Buy("star_sunglasses");
        wardrobe.Tick(Step);
        Assert.Equal(BuyResult.Bought, wardrobe.LastBuy);
        wardrobe.Close();
        wardrobe.Tick(Step);
        Assert.Equal("star_sunglasses", bus.Appearances[^1].Face);
        Assert.Equal(Catalog.IndexOf("star_sunglasses"), wardrobe.Worn(Slot.Face));
    }

    [Fact]
    public void BuyingWithEnoughCoinsAddsTheItem()
    {
        var bus = new FakeBus { Coins = 1000 };
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new WalletState(1000));
        bus.Publish(new OpenShop());
        wardrobe.Buy("mushroom_hat");
        Assert.Empty(bus.Spends);                 // sent on the next tick
        wardrobe.Tick(Step);

        var spend = Assert.Single(bus.Spends);
        Assert.Equal(450, spend.Amount);
        Assert.Equal("Red Mushroom-Cap Hat", spend.What);
        Assert.True(wardrobe.Owns("mushroom_hat"));
        Assert.Equal(BuyResult.Bought, wardrobe.LastBuy);
        Assert.Equal(550, wardrobe.Coins);
        var received = Assert.Single(bus.Received);
        Assert.Equal("mushroom_hat", received.ItemId);
        Assert.Equal(WardrobeModule.ShopReason, received.Why);

        // Bought items can be worn, and are not sold twice.
        bus.Publish(new Equip("mushroom_hat"));
        wardrobe.Tick(Step);
        Assert.Equal("mushroom_hat", bus.Appearances[^1].Head);
        Assert.False(wardrobe.CanBuy(Catalog.IndexOf("mushroom_hat")));
        wardrobe.Buy("mushroom_hat");
        wardrobe.Tick(Step);
        Assert.Single(bus.Spends);
    }

    [Fact]
    public void BuyingWithTooFewCoinsAddsNothing()
    {
        var bus = new FakeBus { Coins = 100 };
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new OpenShop());
        wardrobe.Buy("party_dress");
        wardrobe.Tick(Step);
        Assert.Single(bus.Spends);
        Assert.False(wardrobe.Owns("party_dress"));
        Assert.Equal(BuyResult.NotEnough, wardrobe.LastBuy);
        Assert.Empty(bus.Received);
        Assert.Equal(100, bus.Coins);
    }

    [Fact]
    public void OnlyTheOpenShopSellsAndOnlyShopItemsThatFit()
    {
        var bus = new FakeBus { Coins = 5000 };
        var wardrobe = Started(bus, CharacterKind.Boy);
        wardrobe.Buy("jeans");                     // shop not open
        bus.Publish(new OpenWardrobe());
        wardrobe.Buy("jeans");                     // the wardrobe is not the shop
        bus.Publish(new OpenShop());
        wardrobe.Buy(Catalog.SpiderSilkScarf);     // rewards are not for sale
        wardrobe.Buy("flower_dress");              // does not fit the boy
        wardrobe.Buy("sneakers");                  // already owned
        wardrobe.Tick(Step);
        Assert.Empty(bus.Spends);
        Assert.False(wardrobe.Owns("jeans"));
    }

    [Fact]
    public void ASpendResultForSomeoneElseIsIgnored()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new SpendResult(7, true, 0));
        bus.Publish(new SpendResult(WardrobeModule.RequestIdBase + 1, true, 0));
        wardrobe.Tick(Step);
        Assert.Empty(bus.Received);
    }

    [Fact]
    public void FirstTarantulaAndFirstLadybugGiveTheirRewardOnce()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        bus.Publish(Kill(1, MonsterKind.Caterpillar));
        bus.Publish(Kill(2, MonsterKind.Ant));
        Assert.Empty(bus.Received);

        bus.Publish(Kill(3, MonsterKind.Tarantula));
        bus.Publish(Kill(4, MonsterKind.Tarantula));
        Assert.True(wardrobe.Owns(Catalog.SpiderSilkScarf));
        Assert.False(wardrobe.Owns(Catalog.LadybugBackpack));
        var scarf = Assert.Single(bus.Received);
        Assert.Equal(Catalog.SpiderSilkScarf, scarf.ItemId);
        Assert.Equal(WardrobeModule.TarantulaReason, scarf.Why);

        bus.Publish(Kill(5, MonsterKind.Ladybug));
        bus.Publish(Kill(6, MonsterKind.Ladybug));
        Assert.Equal(2, bus.Received.Count);
        Assert.Equal(Catalog.LadybugBackpack, bus.Received[1].ItemId);
        Assert.True(wardrobe.TarantulaRewarded && wardrobe.LadybugRewarded);

        bus.Publish(new Equip(Catalog.LadybugBackpack));
        wardrobe.Tick(Step);
        Assert.Equal(Catalog.LadybugBackpack, bus.Appearances[^1].Back);
    }

    [Fact]
    public void GrantItemGivesOnceAndIgnoresUnknownItems()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new GrantItem("round_glasses", "Millie's quest"));
        bus.Publish(new GrantItem("round_glasses", "Millie's quest"));
        bus.Publish(new GrantItem("sneakers", "already owned"));
        bus.Publish(new GrantItem("no_such_item", "?"));
        var received = Assert.Single(bus.Received);
        Assert.Equal("round_glasses", received.ItemId);
        Assert.Equal("Millie's quest", received.Why);
        Assert.True(wardrobe.Owns("round_glasses"));
    }

    [Fact]
    public void SwitchingCharacterTakesOffWhatDoesNotFit()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new Equip("shorts"));
        bus.Publish(new Equip("spotted_dress"));
        bus.Publish(Player(CharacterKind.Boy));
        wardrobe.Tick(Step);
        Assert.Equal("", bus.Appearances[^1].Top);
        Assert.Equal("shorts", bus.Appearances[^1].Bottom);
    }

    [Fact]
    public void WearOutfitPutsOnAStarterOutfit()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Boy);
        wardrobe.WearOutfit(2);
        wardrobe.Tick(Step);
        var look = bus.Appearances[^1];
        Assert.Equal(("overalls_top", "overalls", "rain_boots", "beanie"), (look.Top, look.Bottom, look.Shoes, look.Head));
        wardrobe.WearOutfit(1); // the dress outfit does not fit the boy
        wardrobe.Tick(Step);
        Assert.Equal("overalls_top", bus.Appearances[^1].Top);
    }

    /// <summary>Everything a save keeps: the look, what is owned and what is put on in each slot.</summary>
    private static (int, int, int, int, bool[], int[]) StateOf(WardrobeModule w)
    {
        var owned = new bool[Catalog.Count];
        for (int i = 0; i < owned.Length; i++)
            owned[i] = w.Owns(i);
        var worn = new int[6];
        for (int s = 0; s < worn.Length; s++)
            worn[s] = w.Worn((Slot)s);
        return (w.Skin, w.HairStyle, w.HairColor, w.EyeColor, owned, worn);
    }

    private static void AssertSameState(WardrobeModule expected, WardrobeModule actual)
    {
        var (a, b) = (StateOf(expected), StateOf(actual));
        Assert.Equal((a.Item1, a.Item2, a.Item3, a.Item4), (b.Item1, b.Item2, b.Item3, b.Item4));
        Assert.Equal(a.Item5, b.Item5);
        Assert.Equal(a.Item6, b.Item6);
        Assert.Equal(expected.Appearance, actual.Appearance);
    }

    [Fact]
    public void SaveAndRestoreRoundTripsTheLookAndOwnedAndWornItemsWithoutSideEffects()
    {
        // A girl who bought the flower clip, got the spider silk scarf, changed her look, wears the
        // flower clip, the scarf and a dress over hidden shorts, and took her shoes off.
        var bus = new FakeBus { Coins = 1000 };
        var original = Started(bus, CharacterKind.Girl);
        bus.Publish(new SetLook(3, 4, 5, 2));
        bus.Publish(new OpenShop());
        original.Buy("flower_clip");
        original.Tick(Step);
        original.Close();
        bus.Publish(Kill(1, MonsterKind.Tarantula));
        bus.Publish(new Equip("flower_clip"));
        bus.Publish(new Equip(Catalog.SpiderSilkScarf));
        bus.Publish(new Equip("shorts"));
        bus.Publish(new Equip("spotted_dress"));
        bus.Publish(Equip.Unequip(Slot.Shoes));
        Assert.True(original.Owns("flower_clip"));
        Assert.True(original.BottomHidden);

        bus.Publish(new CollectSave(42));
        var part = Assert.Single(bus.Parts);
        Assert.Equal(42, part.RequestId);
        Assert.Equal(SaveModules.Wardrobe, part.Module);
        Assert.Contains("\"v\":1", part.Json);

        // A fresh game, with a screen open, restores it.
        var loadBus = new FakeBus { Coins = 1000 };
        var restored = Started(loadBus, CharacterKind.Girl);
        loadBus.Publish(new OpenWardrobe());
        loadBus.All.Clear();
        loadBus.Publish(new RestorePart(SaveModules.Wallet, "{\"v\":1}"));
        loadBus.Publish(new RestorePart(SaveModules.Wardrobe, part.Json));
        AssertSameState(original, restored);
        Assert.True(restored.BottomHidden);
        Assert.Equal(-1, restored.Worn(Slot.Shoes));
        Assert.False(restored.Open);
        // Restoring published nothing: no ItemReceived, no SpendCoins.
        Assert.Equal(2, loadBus.All.Count);

        // The state stays restored: ticks, the player's state and the first tarantula give nothing new.
        restored.Tick(Step);
        loadBus.Publish(Player(CharacterKind.Girl));
        loadBus.Publish(Kill(2, MonsterKind.Tarantula));
        restored.Tick(Step);
        Assert.Empty(loadBus.Received);
        Assert.Empty(loadBus.Spends);
        AssertSameState(original, restored);
        Assert.Equal(original.Appearance, loadBus.Appearances[^1]);

        // Saving the restored game writes the same part.
        loadBus.Publish(new CollectSave(43));
        Assert.Equal(part.Json, Assert.Single(loadBus.Parts).Json);
    }

    [Fact]
    public void ABoysSaveRoundTripsIntoAFreshGame()
    {
        var bus = new FakeBus();
        var original = Started(bus, CharacterKind.Boy);
        original.WearOutfit(2);
        bus.Publish(new GrantItem(Catalog.LadybugBackpack, "quest"));
        bus.Publish(new Equip(Catalog.LadybugBackpack));
        bus.Publish(new SetLook(5, 0, 7, 5));
        bus.Publish(new CollectSave(1));

        var loadBus = new FakeBus();
        var restored = Started(loadBus);
        loadBus.Publish(new RestorePart(SaveModules.Wardrobe, bus.Parts[^1].Json));
        loadBus.Publish(Player(CharacterKind.Boy));
        AssertSameState(original, restored);
        Assert.True(restored.LadybugRewarded);
        Assert.Empty(loadBus.Received);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"v\":1,\"look\":[0,0,0")]
    [InlineData("[1,2,3]")]
    [InlineData("null")]
    [InlineData("{\"v\":2,\"look\":[1,1,1,1],\"owned\":[\"mushroom_hat\"],\"worn\":[\"\",\"\",\"\",\"mushroom_hat\",\"\",\"\"]}")]
    [InlineData("{\"v\":1,\"look\":[99,1,1,1],\"owned\":[\"mushroom_hat\"],\"worn\":[\"\",\"\",\"\",\"mushroom_hat\",\"\",\"\"]}")]
    [InlineData("{\"v\":1,\"look\":[1,1,1,1],\"owned\":[\"mushroom_hat\"],\"worn\":[\"mushroom_hat\"]}")]
    [InlineData("{\"v\":1,\"look\":\"pink\",\"owned\":[],\"worn\":[]}")]
    public void BadOrUnknownSaveDataKeepsTheDefaultsAndLogsOnce(string? json)
    {
        var defaults = Started(new FakeBus(), CharacterKind.Girl);
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        var log = new List<string>();
        wardrobe.Log = log.Add;
        bus.All.Clear();
        bus.Publish(new RestorePart(SaveModules.Wardrobe, json!));
        bus.Publish(new RestorePart(SaveModules.Wardrobe, json!));
        AssertSameState(defaults, wardrobe);
        Assert.Single(log);
        Assert.Contains("Wardrobe", log[0]);
        Assert.Equal(2, bus.All.Count);
        Assert.False(wardrobe.Restore(json));
    }

    [Fact]
    public void RestoreSkipsItemsNoLongerInTheCatalog()
    {
        var wardrobe = Started(new FakeBus(), CharacterKind.Girl);
        Assert.True(wardrobe.Restore("{\"v\":1,\"look\":[1,2,3,4],\"owned\":[\"gone_hat\",\"skirt\"]," +
            "\"worn\":[\"striped_tee\",\"skirt\",\"\",\"gone_hat\",\"\",\"\"]}"));
        Assert.True(wardrobe.Owns("skirt"));
        Assert.Equal(Catalog.IndexOf("skirt"), wardrobe.Worn(Slot.Bottom));
        Assert.Equal(-1, wardrobe.Worn(Slot.Head));
        Assert.Equal((1, 2, 3, 4), (wardrobe.Skin, wardrobe.HairStyle, wardrobe.HairColor, wardrobe.EyeColor));
    }

    /// <summary>
    /// Plays the Input module's <see cref="MenuNav"/> as its contract says: a step on the tick a
    /// direction is first pushed, then one per 0.2 s (12 ticks) while held, never two closer than that.
    /// </summary>
    private sealed class FakeMenuInput
    {
        private const int RepeatTicks = 12;
        private long _tick;
        private long _lastStep = -RepeatTicks;

        public MenuNav Next(int vertical)
        {
            int step = vertical != 0 && _tick - _lastStep >= RepeatTicks ? vertical : 0;
            if (step != 0)
                _lastStep = _tick;
            _tick++;
            return new MenuNav(step, 0);
        }
    }

    /// <summary>
    /// Runs <paramref name="ticks"/> ticks with the screen open: each tick the Input module publishes
    /// its MenuNav (down held while <paramref name="held"/> says so) and the view runs two frames (a
    /// 120 Hz screen), moving the focus by each step it takes. Returns the ticks the focus moved on.
    /// </summary>
    private static List<int> FocusMoves(WardrobeModule wardrobe, FakeBus bus, int ticks, Func<int, bool> held)
    {
        var input = new FakeMenuInput();
        var moves = new List<int>();
        for (int t = 0; t < ticks; t++)
        {
            bus.Publish(input.Next(held(t) ? 1 : 0));
            wardrobe.Tick(Step);
            for (int frame = 0; frame < 2; frame++)
                if (wardrobe.TakeMenuStep(out int vertical, out int horizontal))
                {
                    Assert.Equal((1, 0), (vertical, horizontal));
                    moves.Add(t);
                }
        }
        return moves;
    }

    [Fact]
    public void HeldDirectionMovesTheFocusOneItemPerFifthOfASecond()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new OpenShop());
        // Down held for one second: the first item at once, then one per 0.2 s (12 ticks).
        Assert.Equal(new[] { 0, 12, 24, 36, 48 }, FocusMoves(wardrobe, bus, 60, _ => true));
    }

    [Fact]
    public void ShortFlickMovesTheFocusExactlyOneItem()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        bus.Publish(new OpenWardrobe());
        // The stick pushed for two ticks (33 ms), then let go: one item, however long the screen stays open.
        Assert.Equal(new[] { 0 }, FocusMoves(wardrobe, bus, 60, t => t < 2));
    }

    [Fact]
    public void MenuStepsAreTakenOnceAndOnlyWhileAScreenIsOpen()
    {
        var bus = new FakeBus();
        var wardrobe = Started(bus, CharacterKind.Girl);
        // Closed: steps (walking with W A S D or the stick) are not kept for later.
        bus.Publish(new MenuNav(1, 0));
        bus.Publish(new OpenWardrobe());
        Assert.False(wardrobe.TakeMenuStep(out _, out _));

        // The ticks without a step do not drop one the view has not taken yet.
        bus.Publish(new MenuNav(0, -1));
        bus.Publish(new MenuNav(0, 0));
        Assert.True(wardrobe.TakeMenuStep(out int vertical, out int horizontal));
        Assert.Equal((0, -1), (vertical, horizontal));
        Assert.False(wardrobe.TakeMenuStep(out _, out _));

        // Steps never pile up: only the latest untaken one moves the focus.
        bus.Publish(new MenuNav(-1, 0));
        bus.Publish(new MenuNav(1, 0));
        Assert.True(wardrobe.TakeMenuStep(out vertical, out _));
        Assert.Equal(1, vertical);
        Assert.False(wardrobe.TakeMenuStep(out _, out _));

        // Closing forgets an untaken step.
        bus.Publish(new MenuNav(1, 0));
        wardrobe.Close();
        bus.Publish(new OpenShop());
        Assert.False(wardrobe.TakeMenuStep(out _, out _));
    }
}

public class WardrobePromptsTests
{
    public static readonly TheoryData<DeviceKind> Devices = new()
    {
        DeviceKind.KeyboardMouse, DeviceKind.Xbox, DeviceKind.PlayStation, DeviceKind.Nintendo, DeviceKind.Generic,
    };

    /// <summary>The icon file-name prefixes of a device's buttons (Generic pads show the Xbox ones).</summary>
    private static string[] Families(DeviceKind device) => device switch
    {
        DeviceKind.KeyboardMouse => new[] { "key_", "mouse_" },
        DeviceKind.PlayStation => new[] { "ps_" },
        DeviceKind.Nintendo => new[] { "ns_" },
        _ => new[] { "xbox_" },
    };

    private static readonly string[] AllFamilies = { "key_", "mouse_", "xbox_", "ps_", "ns_" };

    private static string[] IconsIn(string bbcode) =>
        Regex.Matches(bbcode, @"\[img=\d+\](.*?)\[/img\]").Select(m => m.Groups[1].Value).ToArray();

    private static string Glyph(string path) => Path.GetFileNameWithoutExtension(path);

    private static string Words(string bbcode) => Regex.Replace(bbcode, @"\[img=\d+\].*?\[/img\]", "").Replace('\u00A0', ' ');

    private static string RepoFile(string resPath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, resPath.Substring("res://".Length));
    }

    private static void OnlyThatDevice(string bbcode, DeviceKind device)
    {
        var icons = IconsIn(bbcode);
        Assert.NotEmpty(icons);
        var own = Families(device);
        foreach (var icon in icons)
        {
            string glyph = Glyph(icon);
            Assert.True(own.Any(glyph.StartsWith), $"{device} hint shows {glyph}");
            Assert.True(File.Exists(RepoFile(icon)), $"missing icon {icon}");
        }
        foreach (var other in AllFamilies.Except(own))
            Assert.DoesNotContain("/" + other, bbcode);
        // No button names in words, as in the old "Enter / A" and "Esc / B" hints.
        string words = Words(bbcode);
        Assert.DoesNotContain("/", words);
        foreach (var name in new[] { "Esc", "Enter", "Page", "LB", "RB", "LT", "RT", "Cross", "Circle", "stick", " A ", " B ", " Q ", " E " })
            Assert.DoesNotContain(name, " " + words + " ");
    }

    [Theory]
    [MemberData(nameof(Devices))]
    public void WardrobeHintShowsOnlyThatDevicesIcons(DeviceKind device)
    {
        string hint = WardrobePrompts.Wardrobe(device);
        OnlyThatDevice(hint, device);
        Assert.Contains(Prompts.Rich(PromptAction.Confirm, device, WardrobePrompts.IconSize) + "\u00A0Choose", hint);
        Assert.Contains(Prompts.Rich(PromptAction.Back, device, WardrobePrompts.IconSize) + "\u00A0Done", hint);
        Assert.Contains(Prompts.Icon(PromptAction.TurnLeft, device), hint);
        Assert.Contains(Prompts.Icon(PromptAction.TurnRight, device), hint);
        Assert.Contains(WardrobePrompts.TabIcon(-1, device), hint);
        Assert.Contains(WardrobePrompts.TabIcon(1, device), hint);
        Assert.Contains("Tabs", hint);
    }

    [Theory]
    [MemberData(nameof(Devices))]
    public void ShopHintShowsOnlyThatDevicesIcons(DeviceKind device)
    {
        string hint = WardrobePrompts.Shop(device);
        OnlyThatDevice(hint, device);
        Assert.Contains(Prompts.Rich(PromptAction.Confirm, device, WardrobePrompts.IconSize) + "\u00A0Pick,\u00A0then\u00A0Buy", hint);
        Assert.Contains(Prompts.Rich(PromptAction.Back, device, WardrobePrompts.IconSize) + "\u00A0Done", hint);
        Assert.Contains(Prompts.Icon(PromptAction.TurnLeft, device), hint);
        Assert.DoesNotContain("Tabs", hint);
    }

    [Fact]
    public void EachDeviceShowsItsOwnButtons()
    {
        Assert.Contains("[img=32]res://modules/Input/Glyphs/key_enter.svg[/img]\u00A0Choose", WardrobePrompts.Wardrobe(DeviceKind.KeyboardMouse));
        Assert.Contains("[img=32]res://modules/Input/Glyphs/key_esc.svg[/img]\u00A0Done", WardrobePrompts.Wardrobe(DeviceKind.KeyboardMouse));
        Assert.Contains("res://modules/Wardrobe/Godot/Glyphs/key_pgup.svg", WardrobePrompts.Wardrobe(DeviceKind.KeyboardMouse));
        Assert.Contains("mouse_left.svg[/img]\u00A0Drag", WardrobePrompts.Wardrobe(DeviceKind.KeyboardMouse));
        Assert.Contains("xbox_a.svg[/img]\u00A0Choose", WardrobePrompts.Wardrobe(DeviceKind.Xbox));
        Assert.Contains("xbox_b.svg[/img]\u00A0Done", WardrobePrompts.Shop(DeviceKind.Xbox));
        Assert.Contains("xbox_lt.svg", WardrobePrompts.Wardrobe(DeviceKind.Xbox));
        Assert.Contains("xbox_rs.svg", WardrobePrompts.Wardrobe(DeviceKind.Xbox));
        Assert.Contains("ps_cross.svg[/img]\u00A0Choose", WardrobePrompts.Wardrobe(DeviceKind.PlayStation));
        Assert.Contains("ps_circle.svg[/img]\u00A0Done", WardrobePrompts.Shop(DeviceKind.PlayStation));
        Assert.Contains("ps_l2.svg", WardrobePrompts.Wardrobe(DeviceKind.PlayStation));
        Assert.Contains("ns_zr.svg", WardrobePrompts.Wardrobe(DeviceKind.Nintendo));
        Assert.Equal(WardrobePrompts.Wardrobe(DeviceKind.Xbox), WardrobePrompts.Wardrobe(DeviceKind.Generic));
    }

    [Fact]
    public void IconSizeIsUsedForEveryIcon()
    {
        string hint = WardrobePrompts.Wardrobe(DeviceKind.PlayStation, 48);
        Assert.Equal(IconsIn(hint).Length, Regex.Matches(hint, @"\[img=48\]").Count);
        Assert.DoesNotContain("[img=32]", hint);
    }

    [Theory]
    [InlineData("PS5 Controller", "", DeviceKind.PlayStation)]
    [InlineData("Sony Interactive Entertainment Wireless Controller", "", DeviceKind.PlayStation)]
    [InlineData("Nintendo Switch Pro Controller", "", DeviceKind.Nintendo)]
    [InlineData("Xbox Series X Controller", "", DeviceKind.Xbox)]
    [InlineData("Wireless Controller", "030000004c050000cc09000011810000", DeviceKind.PlayStation)]
    [InlineData("Some Pad", "030000005e0400008e02000010010000", DeviceKind.Xbox)]
    [InlineData("Some Pad", "03000000790000000600000010010000", DeviceKind.Generic)]
    [InlineData(null, null, DeviceKind.Generic)]
    public void PadFamilyFromNameOrVendor(string? name, string? guid, DeviceKind expected) =>
        Assert.Equal(expected, WardrobePrompts.PadFamily(name, guid));
}
