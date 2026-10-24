using System;
using Fungtopia.Map;
using Fungtopia.Pet;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.Home.View;

/// <summary>
/// Draws the home from the Home Core: the furniture placed in the house, the catalog book on its
/// stand, the wardrobe, the pets at home (wandering, napping, playing), and while decorating the floor grid,
/// the held item at the cursor over a green (fits) or red (does not) footprint. Its
/// <see cref="HomePanels"/> show the prompts, the shop's catalog and the decorating help. Reads
/// the shop's and decorating's own buttons and hands them to the Core (Interact comes through
/// the Input module).
/// </summary>
public partial class HomeView : Node3D, IModuleView
{
    private HomeModule? _home;
    private readonly Node3D?[] _furniture = new Node3D?[HomeModule.MaxItems];
    private readonly FurnitureKind[] _furnitureKind = new FurnitureKind[HomeModule.MaxItems];
    private readonly HomePetModel?[] _pets = new HomePetModel?[3];
    private MeshInstance3D _grid = null!;
    private MeshInstance3D _footprint = null!;
    private StandardMaterial3D _footprintMaterial = null!;
    private HomePanels _panels = null!;
    private float _time;

    public void Bind(IModule module) => _home = (HomeModule)module;

    /// <summary>The Core drawn (the preview binds its own).</summary>
    public HomeModule? Home => _home;

    public override void _Ready()
    {
        var book = HomeModels.Instance(HomeModels.Furniture, "CatalogBook");
        if (book != null)
        {
            var p = HomeRoom.BookPosition;
            book.Position = new Vector3(p.X, p.Y, p.Z);
            book.Rotation = new Vector3(0f, MathF.PI, 0f); // facing north, into the room
            AddChild(book);
        }
        // The wardrobe comes with the house: always there, against the north wall, doors facing into the room.
        var wardrobe = HomeModels.Instance(HomeModels.Furniture, "Wardrobe");
        if (wardrobe != null)
        {
            var p = HomeRoom.WardrobePosition;
            wardrobe.Position = new Vector3(p.X, p.Y, p.Z);
            AddChild(wardrobe);
        }

        var room = Areas.Home;
        _grid = new MeshInstance3D
        {
            Name = "Grid",
            Mesh = new PlaneMesh { Size = new Vector2(room.MaxX - room.MinX, room.MaxZ - room.MinZ) },
            MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://modules/Home/Godot/Shaders/grid.gdshader") },
            Position = new Vector3(room.Center.X, Areas.HomeFloorY + 0.012f, room.Center.Y),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        AddChild(_grid);
        _footprintMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.5f, 1f, 0.55f, 0.45f),
        };
        _footprint = new MeshInstance3D
        {
            Name = "Footprint",
            Mesh = new PlaneMesh { Size = Vector2.One },
            MaterialOverride = _footprintMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        AddChild(_footprint);

        for (int i = 0; i < _pets.Length; i++)
        {
            _pets[i] = HomePetModel.Create((PetKind)i);
            _pets[i]!.Visible = false;
            AddChild(_pets[i]);
        }

        _panels = new HomePanels();
        AddChild(_panels);
    }

    public override void _ExitTree() => HomeModels.Release();

    public override void _Process(double delta)
    {
        if (_home == null)
            return;
        float dt = (float)delta;
        _time += dt;
        var items = _home.Items;
        for (int i = 0; i < items.Length; i++)
        {
            var node = Furniture(i, items[i].Kind);
            if (node == null)
                continue;
            bool held = _home.Decorating && _home.Held == i;
            node.Visible = items[i].Placed || held;
            if (items[i].Placed)
                node.Transform = Pose(items[i].Center, items[i].Yaw, 0f);
        }

        bool decorating = _home.Decorating;
        _grid.Visible = decorating;
        _footprint.Visible = decorating;
        if (decorating)
            DrawCursor(items);

        var pets = _home.Pets;
        for (int i = 0; i < _pets.Length; i++)
        {
            var model = _pets[i];
            if (model == null)
                continue;
            var pet = pets[i];
            model.Visible = pet.Present;
            if (pet.Present)
                model.Show(pet, dt);
        }

        _panels.Refresh(_home);
    }

    private void DrawCursor(ReadOnlySpan<FurnitureItem> items)
    {
        int w = 1, d = 1;
        if (_home!.Held >= 0)
        {
            var kind = items[_home.Held].Kind;
            w = Fungtopia.Home.Furniture.Width(kind, _home.CursorRotation);
            d = Fungtopia.Home.Furniture.Depth(kind, _home.CursorRotation);
            var node = Furniture(_home.Held, kind);
            var center = HomeRoom.CellsCenter(_home.CursorColumn, _home.CursorRow, w, d);
            // The held item floats a little above the floor, bobbing.
            if (node != null)
                node.Transform = Pose(center, _home.CursorRotation * MathF.PI * 0.5f, 0.12f + 0.05f * MathF.Sin(_time * 4f));
            _footprintMaterial.AlbedoColor = _home.CanPlaceHeld ? new Color(0.5f, 1f, 0.55f, 0.45f) : new Color(1f, 0.4f, 0.4f, 0.5f);
        }
        else
        {
            bool over = _home.ItemAt(_home.CursorColumn, _home.CursorRow) >= 0;
            _footprintMaterial.AlbedoColor = over ? new Color(1f, 0.85f, 0.3f, 0.55f) : new Color(1f, 1f, 1f, 0.35f);
        }
        var c = HomeRoom.CellsCenter(_home.CursorColumn, _home.CursorRow, w, d);
        _footprint.Transform = new Transform3D(Basis.FromScale(new Vector3(w * HomeRoom.Cell, 1f, d * HomeRoom.Cell)),
            new Vector3(c.X, c.Y + 0.02f, c.Z));
    }

    private static Transform3D Pose(System.Numerics.Vector3 center, float yaw, float lift) =>
        new(new Basis(Vector3.Up, yaw), new Vector3(center.X, center.Y + lift, center.Z));

    /// <summary>The node drawing item <paramref name="index"/>, made when first needed.</summary>
    private Node3D? Furniture(int index, FurnitureKind kind)
    {
        if (_furniture[index] != null && _furnitureKind[index] == kind)
            return _furniture[index];
        _furniture[index]?.QueueFree();
        var node = HomeModels.Instance(HomeModels.Furniture, kind.ToString());
        _furniture[index] = node;
        _furnitureKind[index] = kind;
        if (node != null)
            AddChild(node);
        return node;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_home == null || !(_home.ShopOpen || _home.Decorating))
            return;
        bool back = Pressed(e, Key.Q, JoyButton.B) || Pressed(e, Key.Backspace, JoyButton.B);
        if (back)
        {
            _home.Back();
            return;
        }
        int up = Pressed(e, Key.Up, JoyButton.DpadUp) ? 1 : 0, down = Pressed(e, Key.Down, JoyButton.DpadDown) ? 1 : 0;
        if (_home.ShopOpen)
        {
            if (up + down > 0)
                _home.MoveShopSelection(down - up);
            return;
        }
        int left = Pressed(e, Key.Left, JoyButton.DpadLeft) ? 1 : 0, right = Pressed(e, Key.Right, JoyButton.DpadRight) ? 1 : 0;
        if (up + down + left + right > 0)
            _home.MoveCursor(right - left, down - up);
        if (Pressed(e, Key.R, JoyButton.RightShoulder))
            _home.Rotate();
        if (Pressed(e, Key.Tab, JoyButton.LeftShoulder))
            _home.NextItem(1);
    }

    private static bool Pressed(InputEvent e, Key key, JoyButton button) => e switch
    {
        InputEventKey k => k.Pressed && (k.PhysicalKeycode == key || k.Keycode == key),
        InputEventJoypadButton j => j.Pressed && j.ButtonIndex == button,
        _ => false,
    };
}
