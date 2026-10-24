using System;
using Fungtopia.Map;
using Fungtopia.Shell;
using Godot;
using GodotInput = Godot.Input;

namespace Fungtopia.Travel.View;

/// <summary>
/// Draws the Travel module: a wooden signpost at each of the four <see cref="TravelModule.Signposts"/>
/// with three arrow boards pointing at the three places, the "[Interact icon] Map" prompt (the
/// active device's button) over a near signpost, the full-screen <see cref="MapCanvas"/> while the map is open (mouse,
/// Enter / Space / A to confirm, Esc / B to go back go to the Core; arrows, W A S D, the D-pad and the
/// left stick reach it only as Input's paced <c>MenuNav</c>) and the fade around a trip.
/// </summary>
public partial class TravelView : Node3D, IModuleView
{
    private static readonly Color Ink = new(0.24f, 0.13f, 0.12f);
    private static readonly Color FadeColor = new(0.1f, 0.05f, 0.1f);

    /// <summary>Board k points at this destination (top: Pet City, middle: Forest, bottom: Town).</summary>
    private static readonly int[] BoardDestination = { 2, 1, 0 };

    private TravelModule? _travel;
    private MapCanvas _map = null!;
    private CanvasLayer _mapLayer = null!;
    private PanelContainer _prompt = null!;
    private RichTextLabel _promptLabel = null!;
    private string _promptText = "";
    private ColorRect _fade = null!;
    private bool _wasOpen;
    private int _hover = -1;

    public void Bind(IModule module) => _travel = (TravelModule)module;

    public override void _Ready()
    {
        for (int i = 0; i < TravelModule.Signposts.Length; i++)
            AddChild(BuildSignpost(TravelModule.Signposts[i]));

        var promptLayer = new CanvasLayer { Layer = 11 };
        AddChild(promptLayer);
        _prompt = BuildPrompt(out _promptLabel);
        promptLayer.AddChild(_prompt);

        _mapLayer = new CanvasLayer { Layer = 20, Visible = false };
        AddChild(_mapLayer);
        _map = new MapCanvas { Travel = _travel };
        _mapLayer.AddChild(_map);

        var fadeLayer = new CanvasLayer { Layer = 21 };
        AddChild(fadeLayer);
        _fade = new ColorRect { Color = new Color(FadeColor, 0f), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        fadeLayer.AddChild(_fade);
    }

    public override void _Process(double delta)
    {
        if (_travel == null)
            return;
        bool open = _travel.Open;
        if (open != _wasOpen)
        {
            _wasOpen = open;
            _mapLayer.Visible = open;
            _hover = -1;
            // The map needs the mouse pointer; walking needs it captured again.
            GodotInput.MouseMode = open ? GodotInput.MouseModeEnum.Visible : GodotInput.MouseModeEnum.Captured;
        }
        _fade.Visible = _travel.Fade > 0f;
        _fade.Color = new Color(FadeColor, _travel.Fade);
        PlacePrompt();
    }

    public override void _Input(InputEvent e)
    {
        if (_travel == null || !_travel.Open)
            return;
        var screen = GetViewport().GetVisibleRect().Size;
        switch (e)
        {
            case InputEventMouseMotion motion:
            {
                int hit = Hit(motion.Position, screen);
                if (hit != _hover && hit >= 0)
                    _travel.Hover(hit);
                _hover = hit;
                break;
            }
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click:
            {
                // Handled here so the Input module does not take this click to capture the mouse.
                int hit = Hit(click.Position, screen);
                if (hit >= 0)
                    _travel.Pick(hit);
                GetViewport().SetInputAsHandled();
                break;
            }
            case InputEventKey { Keycode: Key.Enter or Key.KpEnter or Key.Space, Pressed: true, Echo: false }:
            case InputEventJoypadButton { ButtonIndex: JoyButton.A, Pressed: true }:
                _travel.Confirm();
                GetViewport().SetInputAsHandled();
                break;
            case InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false }:
            case InputEventJoypadButton { ButtonIndex: JoyButton.B, Pressed: true }:
                _travel.Close();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private static int Hit(Vector2 position, Vector2 screen) => MapLayout.Hit(MapLayout.ToCanvas(
        new System.Numerics.Vector2(position.X, position.Y), new System.Numerics.Vector2(screen.X, screen.Y)));

    private void PlacePrompt()
    {
        _prompt.Visible = false;
        if (_travel == null || !_travel.ShowPrompt)
            return;
        // Follows the active device; reset to its new (minimum) size when the text changes.
        string text = _travel.Prompt;
        if (!ReferenceEquals(text, _promptText))
        {
            _promptLabel.Text = _promptText = text;
            _prompt.ResetSize();
        }
        var camera = GetViewport().GetCamera3D();
        var size = _prompt.GetCombinedMinimumSize();
        var screen = GetViewport().GetVisibleRect().Size;
        Vector2 at;
        var p = TravelModule.Signposts[_travel.NearSignpost].Position;
        var top = new Vector3(p.X, p.Y + 2.85f, p.Z);
        if (camera != null && !camera.IsPositionBehind(top))
            at = camera.UnprojectPosition(top) - new Vector2(size.X * 0.5f, size.Y);
        else
            at = new Vector2((screen.X - size.X) * 0.5f, screen.Y * 0.7f);
        _prompt.Position = at.Clamp(Vector2.Zero, screen - size);
        _prompt.Visible = true;
    }

    private static PanelContainer BuildPrompt(out RichTextLabel label)
    {
        var box = new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.97f, 0.9f), BorderColor = Ink, AntiAliasing = true,
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 8, ContentMarginBottom = 10,
            ShadowColor = new Color(0f, 0f, 0f, 0.25f), ShadowSize = 6, ShadowOffset = new Vector2(0f, 4f),
        };
        box.SetBorderWidthAll(4);
        box.SetCornerRadiusAll(22);
        var panel = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", box);
        // The button icon is inline BBCode; FitContent sizes the pill to the icon and the word.
        label = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, AutowrapMode = TextServer.AutowrapMode.Off,
            ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("normal_font_size", 28);
        label.AddThemeColorOverride("default_color", Ink);
        label.AddThemeFontOverride("normal_font", new FontVariation { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.7f });
        panel.AddChild(label);
        return panel;
    }

    /// <summary>A signpost at <paramref name="place"/>, turned to its facing, its boards turned to their places.</summary>
    public static Node3D BuildSignpost(Place place)
    {
        var model = SignpostModel.Load();
        model.Position = new Vector3(place.Position.X, place.Position.Y, place.Position.Z);
        model.Rotation = new Vector3(0f, place.Yaw, 0f);
        for (int k = 0; k < BoardDestination.Length; k++)
        {
            if (model.FindChild($"Board{k}", true, false) is not Node3D board)
                continue;
            int destination = BoardDestination[k];
            var target = Target(TravelModule.Destinations[destination]);
            float dx = target.X - place.Position.X, dz = target.Y - place.Position.Z;
            // Turning +X by yaw a gives (cos a, 0, -sin a).
            float worldYaw = MathF.Atan2(-dz, dx);
            board.Rotation = new Vector3(0f, worldYaw - place.Yaw, 0f);
            AddBoardLabel(board, MapLayout.Name(destination), false);
            AddBoardLabel(board, MapLayout.Name(destination), true);
        }
        return model;
    }

    private static System.Numerics.Vector2 Target(Area area) => area switch
    {
        Area.Town => Buildings.TownSquare,
        Area.PetCity => Buildings.CityPlaza,
        _ => System.Numerics.Vector2.Zero,
    };

    private static void AddBoardLabel(Node3D board, string text, bool back)
    {
        const float halfThick = 0.03f;
        var label = new Label3D
        {
            Text = text,
            FontSize = 44,
            PixelSize = 0.0024f,
            Modulate = Ink,
            OutlineSize = 0,
            DoubleSided = false,
            Shaded = false,
            Font = new FontVariation { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.6f },
            Position = new Vector3(0.6f, 0f, back ? -halfThick - 0.006f : halfThick + 0.006f),
            Rotation = new Vector3(0f, back ? MathF.PI : 0f, 0f),
        };
        board.AddChild(label);
    }
}
