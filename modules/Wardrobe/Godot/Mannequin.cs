using System.Collections.Generic;
using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// For previews only: the Player module's chibi rig (its boy.glb, read as a file) dressed from an
/// <see cref="AppearanceState"/>: skin and eye colors, the hair style in its color, and each worn
/// item's objects parented to the rig part of the same name with no offset
/// (<see cref="Attach"/>). The rig's own hair, bow tie and back bow are hidden and its shirt and
/// trousers are drawn as skin, so the clothes show. The game's character is drawn by the Player
/// module; this shows how the pieces fit it.
/// </summary>
public partial class Mannequin : Node3D
{
    private const string Rig = "res://modules/Player/Models/boy.glb";

    private static readonly StandardMaterial3D s_hidden = new()
    {
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = new Color(0, 0, 0, 0),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };

    private readonly List<Node> _worn = new();
    private Node3D? _rig;

    public override void _Ready()
    {
        _rig = ToonGlb.Load(Rig);
        if (_rig == null)
            return;
        AddChild(_rig);
        Hide(_rig, "Hair");
        Hide(_rig, "Ribbon");
        Hide(_rig, "Spots");
        Hide(_rig, "Wood");
        if (_rig.FindChild("BackBow", true, false) is Node3D bow)
            bow.Visible = false;
    }

    public void Apply(in AppearanceState look)
    {
        if (_rig == null)
            return;
        foreach (var node in _worn)
            node.QueueFree();
        _worn.Clear();
        var skin = WardrobeStyle.ToColor(Looks.SkinTones[look.Skin]);
        foreach (var name in new[] { "Skin", "Cloth", "Legs" })
            ToonGlb.Tint(_rig, name, skin);
        ToonGlb.Tint(_rig, "Eye", WardrobeStyle.ToColor(Looks.EyeColors[look.EyeColor]));
        Put(Looks.HairStyles[look.HairStyle].ModelPath, WardrobeStyle.ToColor(Looks.HairColors[look.HairColor]));
        bool shoes = false;
        foreach (Slot slot in System.Enum.GetValues<Slot>())
            if (Catalog.TryGet(look.Worn(slot), out var item))
            {
                Put(item.ModelPath, null);
                shoes |= slot == Slot.Shoes;
            }
        ToonGlb.Tint(_rig, "Shoe", new Color(0.42f, 0.24f, 0.18f));
        if (shoes)
            Hide(_rig, "Shoe");
    }

    private void Put(string path, Color? hair)
    {
        var model = ToonGlb.Load(path);
        if (model == null || _rig == null)
            return;
        if (hair is Color tint)
            ToonGlb.Tint(model, "Hair", tint);
        foreach (var child in model.GetChildren())
        {
            if (child is not Node3D piece || _rig.FindChild(piece.Name, true, false) is not Node3D part)
                continue;
            model.RemoveChild(piece);
            piece.Owner = null;
            piece.Name = "Worn" + piece.Name;
            piece.Transform = Transform3D.Identity;
            part.AddChild(piece);
            _worn.Add(piece);
        }
        model.QueueFree();
    }

    private static void Hide(Node node, string material)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
                if (mesh.GetSurfaceOverrideMaterial(i) is ShaderMaterial m && (string)m.GetMeta("name", "") == material)
                    mesh.SetSurfaceOverrideMaterial(i, s_hidden);
        foreach (var child in node.GetChildren())
            Hide(child, material);
    }
}
