using System.Collections.Generic;
using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// Renders the little icons of clothes and hair styles: each model once, in its own small
/// transparent <see cref="SubViewport"/> with its own world, a soft light and a camera framing it
/// three-quarters from the front. Icons are made on first use and kept.
/// </summary>
public partial class IconStudio : Node
{
    public const int Size = 160;

    private readonly Dictionary<string, SubViewport> _icons = new();

    /// <summary>The icon of the model at <paramref name="path"/>; <paramref name="hair"/> tints its "Hair" material.</summary>
    public Texture2D Icon(string path, Color? hair = null)
    {
        string key = hair is Color c ? path + "#" + c.ToHtml() : path;
        if (_icons.TryGetValue(key, out var existing))
            return existing.GetTexture();
        var viewport = new SubViewport
        {
            Size = new Vector2I(Size, Size),
            TransparentBg = true,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        AddChild(viewport);
        _icons[key] = viewport;
        // "No hair" has no model: its icon is the plain round bald head.
        var model = string.IsNullOrEmpty(path) ? BaldHead() : ToonGlb.Load(path);
        if (model == null)
            return viewport.GetTexture();
        if (hair is Color tint)
            ToonGlb.Tint(model, "Hair", tint);
        // An item for both bodies has a body object for each: the icon shows the Girl's cone one.
        if (model.FindChild(Attach.BodyGirl, false, false) != null && model.FindChild(Attach.BodyBoy, false, false) is Node boy)
        {
            model.RemoveChild(boy);
            boy.QueueFree();
        }
        viewport.AddChild(model);
        viewport.AddChild(new DirectionalLight3D { LightEnergy = 1.2f, RotationDegrees = new Vector3(-35f, -25f, 0f) });
        viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.75f, 0.68f, 0.8f),
                AmbientLightEnergy = 0.9f,
            },
        });
        var box = ToonGlb.Bounds(model);
        var center = box.GetCenter();
        float extent = Mathf.Max(box.Size.X, Mathf.Max(box.Size.Y, box.Size.Z));
        // Three-quarters from the front (Godot's +Z is behind the character), a little from above.
        var dir = new Vector3(0.55f, 0.35f, 1f).Normalized();
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = extent * 1.35f + 0.05f,
            Near = 0.01f,
            Far = 20f,
            Current = true,
        };
        viewport.AddChild(camera);
        camera.LookAtFromPosition(center + dir * 4f, center, Vector3.Up);
        return viewport.GetTexture();
    }

    /// <summary>A plain round head (the Player's 0.19 m ball at its height), for "No hair".</summary>
    private static Node3D BaldHead()
    {
        var root = new Node3D();
        root.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.19f, Height = 0.38f },
            Position = new Vector3(0f, 1.11f, 0f),
            MaterialOverride = ToonGlb.Material(WardrobeStyle.ToColor(Looks.SkinTones[1])),
        });
        return root;
    }
}
