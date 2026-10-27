using System;
using Fungtopia.Map;
using Fungtopia.Pet;
using Fungtopia.Shell;
using Godot;
using GodotInput = Godot.Input;

namespace Fungtopia.Home.View;

/// <summary>
/// Draws the home from the Home Core: the furniture placed in the house, the catalog book on its
/// stand, the wardrobe, the pets at home (wandering, napping, playing), and in placement mode the floor grid,
/// the held item at the cursor over a green (fits) or red (does not) footprint, or with nothing held
/// the placed item under the cursor highlighted. Its <see cref="HomePanels"/> show the prompts, the
/// shop's catalog, the decorating picker and the decorating help. Reads
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
    private bool _mouseMoved, _wasPlacing;

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
            bool held = _home.Decorating && !_home.Picking && _home.Held == i;
            node.Visible = items[i].Placed || held;
            if (items[i].Placed)
                node.Transform = Pose(items[i].Center, items[i].Yaw, 0f);
        }

        UpdateMouse();
        bool decorating = _home.Decorating && !_home.Picking;
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

    /// <summary>
    /// While decorating the mouse cursor shows (the Input module captures it again when the last menu
    /// closes); the mouse, once moved, highlights the picker line under it or moves the grid cursor to
    /// the floor under it. Entering placement mode by mouse puts the cursor under it at once.
    /// </summary>
    private void UpdateMouse()
    {
        bool placing = _home!.Decorating && !_home.Picking;
        if (_home.Decorating && GodotInput.MouseMode != GodotInput.MouseModeEnum.Visible)
            GodotInput.MouseMode = GodotInput.MouseModeEnum.Visible;
        if (placing && (_mouseMoved || (!_wasPlacing && _home.MouseActive)))
            PointAtMouse();
        else if (_home.Decorating && _home.Picking && _mouseMoved)
            _home.HoverPicker(_panels.PickerLineAtMouse());
        _mouseMoved = false;
        _wasPlacing = placing;
    }

    private void PointAtMouse()
    {
        if (TryMouseOnFloor(out var point))
            _home!.PointAt(point);
    }

    /// <summary>The point on the floor under the mouse: the ray from the current camera through it, onto the floor plane.</summary>
    private bool TryMouseOnFloor(out System.Numerics.Vector3 point)
    {
        point = default;
        var camera = GetViewport().GetCamera3D();
        if (camera == null)
            return false;
        var mouse = GetViewport().GetMousePosition();
        var o = camera.ProjectRayOrigin(mouse);
        var d = camera.ProjectRayNormal(mouse);
        return HomeRoom.TryFloorPoint(new System.Numerics.Vector3(o.X, o.Y, o.Z), new System.Numerics.Vector3(d.X, d.Y, d.Z), out point);
    }

    public override void _Input(InputEvent e)
    {
        if (_home == null || !_home.Decorating)
            return;
        switch (e)
        {
            case InputEventMouseMotion motion when motion.Relative.Length() >= 2f:
                _mouseMoved = true;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }:
                // Not marked handled: the Input module still sees the click (the device used last).
                if (_home.Picking)
                    _home.ClickPicker(_panels.PickerLineAtMouse());
                else if (TryMouseOnFloor(out var point))
                    _home.ClickFloor(point);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle, Pressed: true }:
                _home.MouseRotate();
                break;
        }
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
            // The placed item under the cursor is highlighted: its whole footprint glows yellow.
            int over = _home.Highlighted;
            if (over >= 0)
            {
                var item = items[over];
                (w, d) = (item.Width, item.Depth);
                var c0 = HomeRoom.CellsCenter(item.Column, item.Row, w, d);
                _footprint.Transform = new Transform3D(Basis.FromScale(new Vector3(w * HomeRoom.Cell, 1f, d * HomeRoom.Cell)),
                    new Vector3(c0.X, c0.Y + 0.02f, c0.Z));
                _footprintMaterial.AlbedoColor = new Color(1f, 0.85f, 0.3f, 0.55f);
                return;
            }
            _footprintMaterial.AlbedoColor = new Color(1f, 1f, 1f, 0.35f);
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
        // E, X and A (while a panel holds UiFocus) come through the Input module as Interact, so A is
        // not also a Confirm here (that would buy or place twice); menu directions come as MenuNav.
        if (Pressed(e, Key.Q, JoyButton.B) || Pressed(e, Key.Escape, JoyButton.B))
            _home.Back();
        else if (e is InputEventKey && (Pressed(e, Key.Enter, JoyButton.A) || Pressed(e, Key.KpEnter, JoyButton.A)))
            _home.Confirm();
        else if (_home.Decorating && _home.Held >= 0 && Pressed(e, Key.R, JoyButton.RightShoulder))
            _home.Rotate();
        // Put away: R with nothing held, or the gamepad's left face button (X / Square / Nintendo Y). That
        // button also arrives as Interact, but only after this, on a cell now empty: it picks nothing up.
        else if (_home.Decorating && _home.Held < 0 && Pressed(e, Key.R, JoyButton.X))
            _home.PutAway();
    }

    private static bool Pressed(InputEvent e, Key key, JoyButton button) => e switch
    {
        InputEventKey k => k.Pressed && !k.Echo && (k.PhysicalKeycode == key || k.Keycode == key),
        InputEventJoypadButton j => j.Pressed && j.ButtonIndex == button,
        _ => false,
    };
}
