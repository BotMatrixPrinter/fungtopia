using System;
using System.Collections.Generic;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Townsfolk.View;

/// <summary>
/// Draws Pet City's townsfolk, their pets, the roaming pets and the butterflies from the Core,
/// smoothly between ticks (each one between its previous and current tick's position and yaw), with
/// simple procedural animation: folk bob as they walk, swinging their floating hands and tube
/// legs, gesture while chatting and sit on benches (legs bent over the edge); cats and dogs trot, sniff, nap and chase;
/// crows hop, peck and flap; butterflies flutter. Nothing is drawn (or animated) while the Core is
/// not <see cref="TownsfolkModule.Active"/> (the player far from Pet City).
/// </summary>
public partial class TownsfolkView : Node3D, IModuleView
{
    private sealed class FolkFigure
    {
        public Node3D Root = null!;
        public Figures.Part Body, Head, ArmL, ArmR, HandL, HandR, LegL, LegR, ShinL, ShinR;
        public float Phase, Time, Scale;
    }

    private sealed class PetFigure
    {
        public Node3D Root = null!;
        public Node3D Model = null!;
        public Figures.Part Body, Head, Tail, LegFL, LegFR, LegBL, LegBR, WingL, WingR, LegL, LegR;
        public float Phase, Time, Nap;
    }

    private sealed class ButterflyFigure
    {
        public Node3D Root = null!;
        public Figures.Part WingL, WingR;
        public float Time;
    }

    private TownsfolkModule? _town;
    private readonly List<FolkFigure> _folk = new();
    private readonly List<PetFigure> _pets = new();
    private readonly List<ButterflyFigure> _butterflies = new();
    private bool _built;

    public void Bind(IModule module) => _town = (TownsfolkModule)module;

    /// <summary>The Core drawn (the preview binds its own).</summary>
    public TownsfolkModule? Town => _town;

    public override void _Ready()
    {
        Visible = false;
        string start = OS.GetEnvironment("TOWNSFOLK_START").ToLowerInvariant();
        if (start == "plaza" && _town != null)
            _town.TeleportPlayerOnce(TownsfolkModule.ScreenshotSpot, TownsfolkModule.ScreenshotYaw);
    }

    public override void _ExitTree() => Figures.Release();

    private void Build(TownsfolkModule town)
    {
        _built = true;
        var folk = town.Folk;
        for (int i = 0; i < folk.Length; i++)
            _folk.Add(MakeFolk(folk[i].Look, i));
        var pets = town.Pets;
        for (int i = 0; i < pets.Length; i++)
            _pets.Add(MakePet(pets[i], i));
        var butterflies = town.Butterflies;
        for (int i = 0; i < butterflies.Length; i++)
            _butterflies.Add(MakeButterfly(butterflies[i], i));
    }

    private FolkFigure MakeFolk(in Look look, int i)
    {
        var tints = new Dictionary<string, Color>
        {
            ["Skin"] = Figures.ToColor(look.Skin),
            ["Outfit"] = Figures.ToColor(look.Outfit),
            ["Legs"] = Figures.ToColor(look.Legs),
            ["Bow"] = Figures.ToColor(look.AccessoryColor),
            ["Hat"] = Figures.ToColor(look.HatColor),
        };
        var model = Figures.Make("folk.glb", tints);
        model.Scale = Vector3.One * look.Scale;
        foreach (var shape in Enum.GetValues<BodyShape>())
            if (model.FindChild(shape.ToString(), true, false) is Node3D node)
                node.Visible = shape == look.Shape;
        foreach (var hat in Enum.GetValues<HatStyle>())
            if (hat != HatStyle.None && model.FindChild("Hat" + hat, true, false) is Node3D node)
                node.Visible = hat == look.Hat;
        if (model.FindChild("BowTie", true, false) is Node3D bowTie)
            bowTie.Visible = look.Accessory == Accessory.BowTie;
        if (model.FindChild("Bow", true, false) is Node3D bow)
            bow.Visible = look.Accessory == Accessory.Bow;
        var root = new Node3D { Name = $"Folk{i}" };
        root.AddChild(model);
        AddChild(root);
        return new FolkFigure
        {
            Root = root,
            Body = Figures.Find(model, "Body"), Head = Figures.Find(model, "Head"),
            ArmL = Figures.Find(model, "ArmL"), ArmR = Figures.Find(model, "ArmR"),
            HandL = Figures.Find(model, "HandL"), HandR = Figures.Find(model, "HandR"),
            LegL = Figures.Find(model, "LegL"), LegR = Figures.Find(model, "LegR"),
            ShinL = Figures.Find(model, "ShinL"), ShinR = Figures.Find(model, "ShinR"),
            Time = i * 1.37f, Scale = look.Scale,
        };
    }

    private PetFigure MakePet(in Critter pet, int i)
    {
        var color = Figures.ToColor(pet.Color);
        var tints = pet.Kind == CritterKind.Crow
            ? new Dictionary<string, Color> { ["Feather"] = color }
            : new Dictionary<string, Color> { ["Fur"] = color, ["FurDark"] = color.Darkened(0.25f) };
        string file = pet.Kind switch { CritterKind.Cat => "cat.glb", CritterKind.Dog => "dog.glb", _ => "crow.glb" };
        var model = Figures.Make(file, tints);
        model.Scale = Vector3.One * pet.Scale;
        var root = new Node3D { Name = $"{pet.Kind}{i}" };
        root.AddChild(model);
        AddChild(root);
        return new PetFigure
        {
            Root = root, Model = model,
            Body = Figures.Find(model, "Body"), Head = Figures.Find(model, "Head"), Tail = Figures.Find(model, "Tail"),
            LegFL = Figures.Find(model, "LegFL"), LegFR = Figures.Find(model, "LegFR"),
            LegBL = Figures.Find(model, "LegBL"), LegBR = Figures.Find(model, "LegBR"),
            WingL = Figures.Find(model, "WingL"), WingR = Figures.Find(model, "WingR"),
            LegL = Figures.Find(model, "LegL"), LegR = Figures.Find(model, "LegR"),
            Time = i * 0.91f,
        };
    }

    private ButterflyFigure MakeButterfly(in Butterfly butterfly, int i)
    {
        var model = Figures.Make("butterfly.glb", new Dictionary<string, Color> { ["Wing"] = Figures.ToColor(butterfly.Color) }, false);
        var root = new Node3D { Name = $"Butterfly{i}" };
        root.AddChild(model);
        AddChild(root);
        return new ButterflyFigure
        {
            Root = root, WingL = Figures.Find(model, "WingL"), WingR = Figures.Find(model, "WingR"), Time = i * 0.37f,
        };
    }

    public override void _Process(double delta)
    {
        var town = _town;
        if (town == null || !town.Active)
        {
            Visible = false;
            return;
        }
        if (!_built)
            Build(town);
        Visible = true;
        float dt = (float)delta;
        float alpha = (float)Engine.GetPhysicsInterpolationFraction();
        var folk = town.Folk;
        for (int i = 0; i < _folk.Count; i++)
            DrawFolk(_folk[i], folk[i], folk, alpha, dt);
        var pets = town.Pets;
        for (int i = 0; i < _pets.Count; i++)
            DrawPet(_pets[i], pets[i], alpha, dt);
        float out_ = town.ButterfliesOut;
        var butterflies = town.Butterflies;
        for (int i = 0; i < _butterflies.Count; i++)
            DrawButterfly(_butterflies[i], butterflies[i], out_, alpha, dt);
    }

    private static Vector3 G(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    private static void Place(Node3D root, System.Numerics.Vector3 previous, System.Numerics.Vector3 position,
        float previousYaw, float yaw, float alpha, float lift = 0f)
    {
        var at = G(previous).Lerp(G(position), alpha);
        // A jump (coming out of a door, standing up from a bench) is not smoothed.
        if (G(previous).DistanceTo(G(position)) > 0.5f)
            at = G(position);
        root.Position = at + new Vector3(0f, lift, 0f);
        root.Rotation = new Vector3(0f, Figures.LerpAngle(previousYaw, yaw, alpha), 0f);
    }

    private static void DrawFolk(FolkFigure fig, in Folk f, ReadOnlySpan<Folk> all, float alpha, float dt)
    {
        fig.Root.Visible = f.Visible;
        if (!f.Visible)
            return;
        fig.Time += dt;
        float t = fig.Time;
        bool sitting = f.State == FolkState.Sitting;
        Place(fig.Root, f.Previous, f.Position, f.PreviousYaw, f.Yaw, alpha, sitting ? Stride.SitLift(fig.Scale) : 0f);
        // A stride every 0.7 m or so.
        float walk = Math.Clamp(f.Speed / 1.2f, 0f, 1.4f);
        fig.Phase += f.Speed * dt * 9f;
        float s = MathF.Sin(fig.Phase);
        float bob = MathF.Abs(MathF.Cos(fig.Phase)) * 0.05f * walk;
        float breath = MathF.Sin(t * 2.2f);
        bool chatting = f.State == FolkState.Chatting;
        float talk = chatting && f.Speaking ? 1f : 0f;
        float listen = chatting && !f.Speaking ? 1f : 0f;
        float nod = MathF.Sin(t * (talk > 0f ? 8f : 2.6f)) * (0.09f * talk + 0.06f * listen);
        float look = f.State == FolkState.Browsing ? MathF.Sin(t * 0.8f) * 0.25f : MathF.Sin(t * 0.45f) * 0.1f;

        fig.Body.Pose(new Vector3(0f, bob, 0f), new Vector3(0f, 0f, s * 0.05f * walk),
            new Vector3(1f + breath * 0.012f, 1f - breath * 0.015f, 1f + breath * 0.012f));
        fig.Head.Pose(new Vector3(0f, bob + breath * 0.006f, 0f),
            new Vector3(-nod + (f.State == FolkState.Browsing ? 0.12f : 0f), look, s * 0.06f * walk + MathF.Sin(t * 0.9f) * 0.04f), Vector3.One);

        float swing = s * 0.55f * walk;
        float idle = MathF.Sin(t * 2.2f + 0.6f) * 0.07f;
        LegPose legs;
        if (sitting)
        {
            // Hands on the lap, thighs along the seat, shins bent down over its edge, swinging a little.
            fig.ArmL.Pose(Vector3.Zero, new Vector3(0.6f, 0f, 0.15f));
            fig.ArmR.Pose(Vector3.Zero, new Vector3(0.6f, 0f, -0.15f));
            legs = Stride.Sit(t);
        }
        else
        {
            // Talking: the right hand goes up and about, and now and then the left too.
            float gesture = talk * (0.9f + MathF.Sin(t * 5.3f) * 0.35f);
            float other = talk * MathF.Max(0f, MathF.Sin(t * 1.7f)) * 0.8f;
            fig.ArmL.Pose(new Vector3(0f, bob, 0f), new Vector3(swing + idle + other, 0f, -idle * 0.5f - other * 0.6f));
            fig.ArmR.Pose(new Vector3(0f, bob, 0f), new Vector3(-swing - idle + gesture, 0f, idle * 0.5f + gesture * 0.7f));
            legs = Stride.Walk(fig.Phase, walk);
        }
        fig.LegL.Pose(Vector3.Zero, new Vector3(legs.HipL, 0f, 0f));
        fig.LegR.Pose(Vector3.Zero, new Vector3(legs.HipR, 0f, 0f));
        fig.ShinL.Pose(Vector3.Zero, new Vector3(legs.KneeL, 0f, 0f));
        fig.ShinR.Pose(Vector3.Zero, new Vector3(legs.KneeR, 0f, 0f));
        Figures.FollowArm(fig.ArmL, fig.HandL);
        Figures.FollowArm(fig.ArmR, fig.HandR);
    }

    private static void DrawPet(PetFigure fig, in Critter p, float alpha, float dt)
    {
        fig.Root.Visible = p.Visible;
        if (!p.Visible)
            return;
        fig.Time += dt;
        float t = fig.Time;
        Place(fig.Root, p.Previous, p.Position, p.PreviousYaw, p.Yaw, alpha);
        if (p.Kind == CritterKind.Crow)
        {
            DrawCrow(fig, p, t, dt);
            return;
        }
        float run = Math.Clamp(p.Speed / 1.3f, 0f, 2.6f);
        fig.Phase += p.Speed * dt * 14f;
        float s = MathF.Sin(fig.Phase);
        float trot = MathF.Min(run, 1f);
        fig.Nap = Mathf.MoveToward(fig.Nap, p.State == CritterState.Nap ? 1f : 0f, dt * 1.5f);
        float sit = p.State == CritterState.Sit ? 1f : 0f;
        float sniff = p.State == CritterState.Sniff ? 1f : 0f;
        float breath = MathF.Sin(t * (fig.Nap > 0.5f ? 1.4f : 3f));
        float bob = MathF.Abs(MathF.Cos(fig.Phase)) * 0.04f * trot;
        // Napping: lying down, legs tucked under, head on the paws, slow breathing.
        fig.Model.Position = new Vector3(0f, -0.1f * fig.Nap * p.Scale + bob, 0f);
        fig.Body.Pose(Vector3.Zero, new Vector3(sit * 0.35f + s * 0.04f * trot, 0f, 0f),
            new Vector3(1f + breath * 0.02f, 1f + breath * 0.03f * (1f + fig.Nap), 1f));
        float headDown = -0.55f * sniff * (0.8f + 0.2f * MathF.Sin(t * 9f)) - 0.35f * fig.Nap;
        fig.Head.Pose(new Vector3(0f, -0.05f * fig.Nap, 0f),
            new Vector3(headDown + sit * -0.2f, MathF.Sin(t * (sniff > 0f ? 3f : 0.6f)) * (0.15f + 0.2f * sniff) * (1f - fig.Nap), 0f), Vector3.One);
        float leg = s * 0.7f * trot;
        float tuck = fig.Nap * 1.3f;
        fig.LegFL.Pose(Vector3.Zero, new Vector3(leg - tuck, 0f, 0f));
        fig.LegBR.Pose(Vector3.Zero, new Vector3(leg + tuck + sit * 1.2f, 0f, 0f));
        fig.LegFR.Pose(Vector3.Zero, new Vector3(-leg - tuck, 0f, 0f));
        fig.LegBL.Pose(Vector3.Zero, new Vector3(-leg + tuck + sit * 1.2f, 0f, 0f));
        float wag = p.Kind == CritterKind.Dog ? 9f : 3f;
        fig.Tail.Pose(Vector3.Zero, new Vector3(0f, MathF.Sin(t * wag) * (0.5f - 0.35f * fig.Nap), 0f));
    }

    private static void DrawCrow(PetFigure fig, in Critter p, float t, float dt)
    {
        bool flying = p.State == CritterState.Fly;
        bool hopping = p.State == CritterState.Hop && p.Speed > 0f;
        fig.Phase += dt * 9f;
        float hop = hopping ? MathF.Abs(MathF.Sin(fig.Phase)) * 0.12f : 0f;
        fig.Model.Position = new Vector3(0f, hop, 0f);
        float flap = flying ? MathF.Sin(t * 18f) * 0.9f + 0.2f : hopping ? MathF.Sin(t * 14f) * 0.15f : 0f;
        fig.WingL.Pose(Vector3.Zero, new Vector3(0f, 0f, flap));
        fig.WingR.Pose(Vector3.Zero, new Vector3(0f, 0f, -flap));
        float peck = p.State == CritterState.Peck ? MathF.Max(0f, MathF.Sin(t * 5f)) * 0.9f : 0f;
        fig.Head.Pose(Vector3.Zero, new Vector3(-peck, p.State == CritterState.Perch ? MathF.Sin(t * 0.8f) * 0.6f : 0f, 0f), Vector3.One);
        fig.Body.Pose(Vector3.Zero, new Vector3(flying ? -0.3f : -peck * 0.3f, 0f, 0f), Vector3.One);
        fig.Tail.Pose(Vector3.Zero, new Vector3(MathF.Sin(t * 3f) * 0.1f, 0f, 0f));
        float tuck = flying ? 0.9f : 0f;
        fig.LegL.Pose(Vector3.Zero, new Vector3(-tuck, 0f, 0f));
        fig.LegR.Pose(Vector3.Zero, new Vector3(-tuck, 0f, 0f));
    }

    private static void DrawButterfly(ButterflyFigure fig, in Butterfly b, float out_, float alpha, float dt)
    {
        fig.Root.Visible = out_ > 0f;
        if (out_ <= 0f)
            return;
        fig.Time += dt;
        var at = G(b.Previous).Lerp(G(b.Position), alpha);
        fig.Root.Position = at;
        var v = G(b.Velocity);
        if (v.X * v.X + v.Z * v.Z > 1e-4f)
            fig.Root.Rotation = new Vector3(0f, MathF.Atan2(-v.X, -v.Z), 0f);
        fig.Root.Scale = Vector3.One * (out_ * 1.8f);
        float flap = MathF.Sin(fig.Time * 26f) * 1.1f;
        fig.WingL.Pose(Vector3.Zero, new Vector3(0f, 0f, -flap));
        fig.WingR.Pose(Vector3.Zero, new Vector3(0f, 0f, flap));
    }
}
