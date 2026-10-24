using System;
using Fungtopia.Map;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.PetCity.View;

/// <summary>
/// Draws Pet City from <see cref="PetCityLayout"/>: every Pet City building of the Map exactly on
/// its footprint (tall colorful city blocks, Lucy's Pet Store with its counter at
/// <see cref="Places.LucySpot"/> and its pets on display); and every Pet City prop of the Map (<see cref="PetCityLayout.CityProps"/>: lamps, benches,
/// planters, the statue) exactly where, as turned and as large as <see cref="Props.All"/> says. At night (<see cref="PetCityModule.WindowsLit"/>) the windows, the sign and the lamps glow
/// and the store's light shines brighter. The pets on display breathe and bob a little.
/// </summary>
public partial class PetCityView : Node3D, IModuleView
{
    /// <summary>The city blocks' wall and roof colors, one pair per style.</summary>
    private static readonly (Color Wall, Color Roof)[] Styles =
    {
        (new Color(0.62f, 0.86f, 0.74f), new Color(0.94f, 0.38f, 0.44f)),
        (new Color(0.78f, 0.7f, 0.96f), new Color(0.98f, 0.62f, 0.36f)),
        (new Color(1f, 0.84f, 0.48f), new Color(0.5f, 0.6f, 0.96f)),
        (new Color(0.62f, 0.8f, 0.98f), new Color(0.96f, 0.5f, 0.72f)),
    };

    private static readonly string[] Pets = { "Kitten", "Puppy", "Bunny", "Hamster", "MushroomCrow" };

    private PetCityModule? _city;
    private readonly Node3D?[] _pets = new Node3D?[Pets.Length];
    private readonly Vector3[] _petRest = new Vector3[Pets.Length];
    private OmniLight3D? _storeLight;
    private bool _lit;
    private float _time;

    public void Bind(IModule module) => _city = (PetCityModule)module;

    /// <summary>The Core drawn (the preview binds its own).</summary>
    public PetCityModule? City => _city;

    public override void _Ready()
    {
        foreach (var piece in PetCityLayout.Pieces)
        {
            switch (piece.Kind)
            {
                case PieceKind.CityBlock: AddCityBlock(piece); break;
                case PieceKind.PetStore: AddPetStore(piece); break;
            }
        }
        foreach (var prop in PetCityLayout.CityProps)
            AddProp(prop);
        _lit = _city?.WindowsLit ?? false;
        Light(_lit);
    }

    public override void _ExitTree() => PetCityModels.Release();

    public override void _Process(double delta)
    {
        _time += (float)delta;
        bool lit = _city?.WindowsLit ?? false;
        if (lit != _lit)
        {
            _lit = lit;
            Light(lit);
        }
        for (int i = 0; i < _pets.Length; i++)
        {
            var pet = _pets[i];
            if (pet == null)
                continue;
            float t = _time * (i == 4 ? 2.2f : 1.6f) + i * 1.3f;
            float breathe = 1f + 0.035f * MathF.Sin(t * 2f);
            pet.Scale = new Vector3(1f, breathe, 1f);
            pet.Position = _petRest[i] + new Vector3(0f, i == 4 ? 0.04f * MathF.Max(0f, MathF.Sin(t)) : 0f, 0f);
            if (i == 4)
                pet.Rotation = new Vector3(0f, 0.35f * MathF.Sin(_time * 0.7f), 0f);
        }
    }

    private void Light(bool night)
    {
        PetCityModels.SetNight(night);
        if (_storeLight != null)
            _storeLight.LightEnergy = night ? 4f : 1.6f;
    }

    /// <summary>The model's transform: standing on the high side of its footing (the plinth fills in below).</summary>
    private static Transform3D Placed(in Piece piece, Vector3 scale) =>
        new(new Basis(Vector3.Up, piece.Yaw).Scaled(scale), new Vector3(piece.Position.X, piece.Footing.High, piece.Position.Z));

    /// <summary>
    /// A stone plinth under a building, from its footing's base (in the ground) a little above its
    /// high side, slightly wider than its footprint and flaring wider at the bottom, so no edge floats.
    /// </summary>
    private void AddPlinth(in Piece piece, string name, Color color)
    {
        float height = piece.Footing.Extension + PetCityLayout.PlinthAbove;
        float w = piece.Size.X + 2f * PetCityLayout.PlinthOverhang, d = piece.Size.Z + 2f * PetCityLayout.PlinthOverhang;
        var top = new BoxMesh { Size = new Vector3(w, height, d) };
        var plinth = new MeshInstance3D
        {
            Name = name,
            Mesh = top,
            MaterialOverride = PetCityModels.Toon(color),
            Transform = new Transform3D(new Basis(Vector3.Up, piece.Yaw),
                new Vector3(piece.Position.X, piece.Position.Y + height * 0.5f, piece.Position.Z)),
        };
        AddChild(plinth);
        // The skirt: a lower, wider step that widens into the ground.
        float skirt = Planting.Sink + PetCityLayout.PlinthAbove * 0.5f;
        plinth.AddChild(new MeshInstance3D
        {
            Name = "Skirt",
            Mesh = new BoxMesh { Size = new Vector3(w + 0.5f, skirt, d + 0.5f) },
            MaterialOverride = PetCityModels.Toon(color.Darkened(0.12f)),
            Position = new Vector3(0f, (skirt - height) * 0.5f, 0f),
        });
    }

    private void AddCityBlock(in Piece piece)
    {
        var style = Styles[piece.Style];
        AddPlinth(piece, $"Plinth{piece.BuildingIndex}", style.Wall.Darkened(0.35f));
        var root = new Node3D
        {
            Name = $"CityBlock{piece.BuildingIndex}",
            Transform = Placed(piece, new Vector3(piece.Size.X / 12f, piece.Size.Y / PetCityLayout.ModelHeight(piece.Stories), piece.Size.Z / 12f)),
        };
        AddChild(root);
        var tower = PetCityModels.Instance($"Tower{piece.Stories}", style.Wall, style.Roof);
        if (tower != null)
            root.AddChild(tower);
        var roof = PetCityModels.Instance(piece.Style % 2 == 0 ? "RoofCap" : "RoofTerrace", style.Wall, style.Roof);
        if (roof != null)
        {
            roof.Position = new Vector3(0f, PetCityLayout.GroundFloorHeight + piece.Stories * PetCityLayout.StoryHeight, 0f);
            root.AddChild(roof);
        }
    }

    private void AddPetStore(in Piece piece)
    {
        var scale = new Vector3(piece.Size.X / 18f, piece.Size.Y / 11f, piece.Size.Z / 12f);
        AddPlinth(piece, "PetStorePlinth", new Color(0.86f, 0.7f, 0.78f));
        var root = new Node3D { Name = "PetStore", Transform = Placed(piece, scale) };
        AddChild(root);
        foreach (var name in new[] { "PetStore", "PetStoreInside" })
        {
            var part = PetCityModels.Instance(name);
            if (part != null)
                root.AddChild(part);
        }
        for (int i = 0; i < Pets.Length; i++)
        {
            _pets[i] = PetCityModels.Instance(Pets[i]);
            if (_pets[i] == null)
                continue;
            _petRest[i] = _pets[i]!.Position;
            root.AddChild(_pets[i]);
        }

        // The counter at Lucy's spot, on the store's floor.
        const float floor = 0.45f;
        var counter = PetCityModels.Instance("Counter");
        if (counter != null)
        {
            var at = PetCityLayout.Counter;
            counter.Transform = new Transform3D(new Basis(Vector3.Up, at.Yaw),
                new Vector3(at.Position.X, piece.Footing.High + floor * scale.Y, at.Position.Z));
            AddChild(counter);
        }

        _storeLight = new OmniLight3D
        {
            Name = "StoreLight",
            LightColor = new Color(1f, 0.82f, 0.6f),
            OmniRange = 12f,
            OmniAttenuation = 0.5f,
            Position = new Vector3(0f, 4.5f, -1.5f),
        };
        root.AddChild(_storeLight);
    }

    private void AddProp(in CityProp prop)
    {
        var model = PetCityLayout.PropModel(prop.Kind);
        if (model == null)
        {
            GD.PushWarning($"Fungtopia: PetCity: no model for a {prop.Kind} (prop {prop.PropIndex}).");
            return;
        }
        var (name, radius, height) = model.Value;
        var node = PetCityModels.Instance(name);
        if (node == null)
            return;
        float across = prop.Radius / radius;
        node.Name = $"{name}{prop.PropIndex}";
        // From the base in the ground up to its full height above the high side: the stem reaches down.
        float tall = PetCityLayout.DrawnHeight(prop);
        node.Transform = new Transform3D(new Basis(Vector3.Up, prop.Yaw).Scaled(new Vector3(across, tall / height, across)),
            new Vector3(prop.Position.X, prop.Position.Y, prop.Position.Z));
        AddChild(node);
        float flare = PetCityLayout.FlareRadius(prop);
        if (flare > 0f)
        {
            // A root flare: a collar widening from the base's own radius into the ground.
            float h = prop.Footing.Extension + PetCityLayout.FlareAbove;
            AddChild(new MeshInstance3D
            {
                Name = $"Flare{prop.PropIndex}",
                Mesh = new CylinderMesh { TopRadius = prop.Radius * 1.02f, BottomRadius = flare, Height = h, RadialSegments = 24, Rings = 1 },
                MaterialOverride = PetCityModels.Toon(prop.Kind == PropKind.Statue ? new Color(0.62f, 0.66f, 0.64f) : new Color(0.78f, 0.62f, 0.5f)),
                Position = new Vector3(prop.Position.X, prop.Position.Y + h * 0.5f, prop.Position.Z),
            });
        }
    }
}
