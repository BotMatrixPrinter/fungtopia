using System;
using System.Collections.Generic;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Townsfolk.View;

/// <summary>
/// Draws Pet City's townsfolk, their pets, the roaming pets and the butterflies from the Core,
/// smoothly between ticks (each one between its previous and current tick's position and yaw), with
/// simple procedural animation: folk walk with real strides (thighs and knees), swinging their
/// floating hands, gesture while chatting (looking each other in the eyes) and sit on benches; cats and dogs trot, sniff, nap and chase;
/// crows hop, peck and flap; butterflies flutter. Nothing is drawn (or animated) while the Core is
/// not <see cref="TownsfolkModule.Active"/> (the player far from Pet City).
/// </summary>
public partial class TownsfolkView : Node3D, IModuleView
{
    private sealed class FolkFigure
    {
        public Node3D Root = null!;
        public Node3D Model = null!;
        public Figures.Part Body, Head, Skirt, ArmL, ArmR, HandL, HandR, LegL, LegR, ShinL, ShinR;
        public float Scale, Cycle, Time;
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
            ["Hair"] = Figures.ToColor(look.HairColor),
            ["Outfit"] = Figures.ToColor(look.Outfit),
            ["Trim"] = Figures.ToColor(look.Trim),
            ["Trousers"] = Figures.ToColor(look.TrousersColor),
            // Trousers down to the shoes, or bare legs under a dress.
            ["Legs"] = Figures.ToColor(look.Trousers ? look.TrousersColor : look.Skin),
            ["Hat"] = Figures.ToColor(look.HatColor),
        };
        var model = Figures.Make("folk.glb", tints);
        model.Scale = Vector3.One * look.Scale;
        foreach (var hair in Enum.GetValues<HairStyle>())
            if (model.FindChild("Hair" + hair, true, false) is Node3D node)
                node.Visible = hair == look.Hair;
        foreach (var hat in Enum.GetValues<HatStyle>())
            if (hat != HatStyle.None && model.FindChild("Hat" + hat, true, false) is Node3D node)
                node.Visible = hat == look.Hat;
        if (model.FindChild("Trousers", true, false) is Node3D trousers)
            trousers.Visible = look.Trousers;
        if (model.FindChild("Skirt", true, false) is Node3D skirt)
            skirt.Visible = !look.Trousers;
        var root = new Node3D { Name = $"Folk{i}" };
        root.AddChild(model);
        AddChild(root);
        return new FolkFigure
        {
            Root = root, Model = model, Scale = look.Scale,
            Body = Figures.Find(model, "Body"), Head = Figures.Find(model, "Head"), Skirt = Figures.Find(model, "Skirt"),
            ArmL = Figures.Find(model, "ArmL"), ArmR = Figures.Find(model, "ArmR"),
            HandL = Figures.Find(model, "HandL"), HandR = Figures.Find(model, "HandR"),
            LegL = Figures.Find(model, "LegL"), LegR = Figures.Find(model, "LegR"),
            ShinL = Figures.Find(model, "ShinL"), ShinR = Figures.Find(model, "ShinR"),
            Cycle = i * 0.29f, Time = i * 1.37f,
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
        Place(fig.Root, f.Previous, f.Position, f.PreviousYaw, f.Yaw, alpha);
        // Real strides: the planted foot moves back under the hips as fast as the body goes on.
        fig.Cycle += Body.CycleRate(f.Speed, fig.Scale) * dt;
        var gait = Body.Legs(fig.Cycle, f.Speed);
        float walk = Body.Walk(f.Speed);
        float s = MathF.Sin(fig.Cycle * MathF.Tau);
        float breath = MathF.Sin(t * 2.2f);
        bool chatting = f.State == FolkState.Chatting;
        float talk = chatting && f.Speaking ? 1f : 0f;
        float listen = chatting && !f.Speaking ? 1f : 0f;
        float nod = MathF.Sin(t * (talk > 0f ? 8f : 2.6f)) * (0.09f * talk + 0.06f * listen);
        float look = f.State == FolkState.Browsing ? MathF.Sin(t * 0.8f) * 0.25f : MathF.Sin(t * 0.45f) * 0.1f;
        // Chatting, they look each other in the eyes, up or down to the other's height.
        float pitch = f.State == FolkState.Browsing ? 0.12f : 0f;
        if (chatting && f.Partner >= 0)
        {
            ref readonly var other = ref all[f.Partner];
            float apart = new Vector2(other.Position.X - f.Position.X, other.Position.Z - f.Position.Z).Length();
            pitch = Body.LookPitch(fig.Scale, other.Look.Scale, apart);
            look *= 0.3f;
        }

        if (sitting)
        {
            // On the seat: hips down on it (a little back, toward the backrest), thighs forward, knees bent, feet on the ground.
            var sit = Body.Sit(fig.Scale);
            fig.Model.Position = new Vector3(0f, sit.Lift, sit.Back);
            fig.Body.Pose(Vector3.Zero, new Vector3(0.06f, 0f, 0f),
                new Vector3(1f + breath * 0.01f, 1f - breath * 0.012f, 1f + breath * 0.01f));
            fig.Head.Pose(new Vector3(0f, breath * 0.004f, 0f), new Vector3(-nod - 0.05f, look, MathF.Sin(t * 0.9f) * 0.04f), Vector3.One);
            float swingL = MathF.Sin(t * 1.3f) * 0.06f, swingR = MathF.Sin(t * 1.1f + 0.7f) * 0.06f;
            fig.LegL.Pose(Vector3.Zero, new Vector3(sit.Hip, 0f, 0.04f));
            fig.LegR.Pose(Vector3.Zero, new Vector3(sit.Hip, 0f, -0.04f));
            fig.ShinL.Pose(Vector3.Zero, new Vector3(sit.Knee + swingL, 0f, 0f));
            fig.ShinR.Pose(Vector3.Zero, new Vector3(sit.Knee + swingR, 0f, 0f));
            // A skirt drapes over the lap rather than down through the seat.
            fig.Skirt.Pose(new Vector3(0f, 0.06f, -0.08f), Vector3.Zero, new Vector3(1.05f, 0.5f, 1.35f));
            // Hands in the lap, on the thighs (the elbows bent).
            float rest = breath * 0.004f;
            if (fig.HandL.Node != null)
                fig.HandL.Node.Position = new Vector3(-0.11f, Body.HipY + 0.12f + rest, -0.2f);
            if (fig.HandR.Node != null)
                fig.HandR.Node.Position = new Vector3(0.11f, Body.HipY + 0.12f + rest, -0.24f);
            return;
        }

        // Walking, the body sinks a little as the legs spread (the lower foot on the ground) and sways with the steps.
        fig.Model.Position = new Vector3(0f, -gait.Drop * fig.Scale, 0f);
        fig.Body.Pose(Vector3.Zero, new Vector3(-0.03f * walk, s * 0.06f * walk, s * 0.025f * walk),
            new Vector3(1f + breath * 0.01f, 1f - breath * 0.012f, 1f + breath * 0.01f));
        fig.Head.Pose(new Vector3(0f, breath * 0.004f, 0f),
            new Vector3(-nod + pitch, look, s * 0.03f * walk + MathF.Sin(t * 0.9f) * 0.04f), Vector3.One);
        fig.Skirt.Pose(Vector3.Zero, Vector3.Zero, Vector3.One);
        fig.LegL.Pose(Vector3.Zero, new Vector3(gait.HipL, 0f, 0f));
        fig.LegR.Pose(Vector3.Zero, new Vector3(gait.HipR, 0f, 0f));
        fig.ShinL.Pose(Vector3.Zero, new Vector3(gait.KneeL, 0f, 0f));
        fig.ShinR.Pose(Vector3.Zero, new Vector3(gait.KneeR, 0f, 0f));

        // The hands swing against the legs; talking, the right hand goes up and about, and now and then the left too.
        float swing = -gait.HipL * 1.1f;
        float idle = MathF.Sin(t * 2.2f + 0.6f) * 0.05f;
        float gesture = talk * (0.9f + MathF.Sin(t * 5.3f) * 0.35f);
        float otherHand = talk * MathF.Max(0f, MathF.Sin(t * 1.7f)) * 0.8f;
        fig.ArmL.Pose(Vector3.Zero, new Vector3(swing + idle + otherHand, 0f, -idle * 0.4f + otherHand * 0.15f));
        fig.ArmR.Pose(Vector3.Zero, new Vector3(-swing - idle + gesture, 0f, idle * 0.4f - gesture * 0.15f));
        // Gesturing, the elbows bend: the hands come up in front of the chest.
        Figures.FollowArm(fig.ArmL, fig.HandL, 1f - otherHand * 0.5f);
        Figures.FollowArm(fig.ArmR, fig.HandR, 1f - gesture * 0.45f);
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
