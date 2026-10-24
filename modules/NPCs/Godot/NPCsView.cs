using System;
using Fungtopia.Shell;
using Godot;

namespace Fungtopia.NPCs.View;

/// <summary>
/// Draws the NPCs module: Dr. Jeremy, Millie, Lucy, Abby and Pip (behind the food stall's counter,
/// on its floor) at their spots, turned smoothly to the Core's facing, with their idle and talking
/// animation (<see cref="NpcFigure"/>), and the small "E / X: Talk" (Pip: "E / X: Food") prompt
/// floating over the one the player is near.
/// </summary>
public partial class NPCsView : Node3D, IModuleView
{
    private static readonly Color Ink = new(0.24f, 0.13f, 0.12f);

    /// <summary>The food stall's plank floor above the ground at Pip's spot (as the Town module draws it), which Pip stands on.</summary>
    private const float StallFloor = 0.05f;

    private NPCsModule? _npcs;
    private readonly NpcFigure[] _figures = new NpcFigure[NPCsModule.Count];
    private PanelContainer _prompt = null!;
    private Label _promptLabel = null!;

    public void Bind(IModule module) => _npcs = (NPCsModule)module;

    public override void _Ready()
    {
        for (int i = 0; i < NPCsModule.Count; i++)
        {
            var figure = NpcFigure.Create(i);
            var p = NPCsModule.Spots[i].Position;
            figure.Position = new Vector3(p.X, p.Y + (i == NPCsModule.Pip ? StallFloor : 0f), p.Z);
            figure.Rotation = new Vector3(0f, NPCsModule.Spots[i].Yaw, 0f);
            AddChild(figure);
            _figures[i] = figure;
        }
        var layer = new CanvasLayer { Layer = 11 };
        AddChild(layer);
        (_prompt, _promptLabel) = BuildPrompt();
        layer.AddChild(_prompt);
    }

    public override void _Process(double delta)
    {
        if (_npcs == null)
            return;
        // Ease toward the Core's facing (it turns on the fixed tick; this smooths the frames between).
        float blend = 1f - MathF.Exp(-(float)delta * 20f);
        for (int i = 0; i < NPCsModule.Count; i++)
        {
            var figure = _figures[i];
            float yaw = figure.Rotation.Y;
            figure.Rotation = new Vector3(0f, Mathf.LerpAngle(yaw, _npcs.Yaw[i], blend), 0f);
            figure.Talking = _npcs.Talking == i;
            figure.Noticing = _npcs.Noticing == i;
        }
        PlacePrompt(_npcs.PromptNpc, _npcs.PromptText);
    }

    private void PlacePrompt(int npc, string text)
    {
        _prompt.Visible = false;
        if (npc < 0)
            return;
        if (_promptLabel.Text != text)
            _promptLabel.Text = text;
        var camera = GetViewport().GetCamera3D();
        var size = _prompt.GetCombinedMinimumSize();
        var screen = GetViewport().GetVisibleRect().Size;
        var figure = _figures[npc];
        var top = figure.Position + new Vector3(0f, figure.Height + 0.25f, 0f);
        Vector2 at;
        if (camera != null && !camera.IsPositionBehind(top))
            at = camera.UnprojectPosition(top) - new Vector2(size.X * 0.5f, size.Y);
        else
            at = new Vector2((screen.X - size.X) * 0.5f, screen.Y * 0.7f);
        _prompt.Position = at.Clamp(Vector2.Zero, screen - size);
        _prompt.Visible = true;
    }

    /// <summary>A cream rounded pill with an ink border, like the signposts' prompt.</summary>
    public static (PanelContainer Panel, Label Label) BuildPrompt()
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
        var label = new Label { Text = NPCsModule.TalkPrompt };
        label.AddThemeFontSizeOverride("font_size", 26);
        label.AddThemeColorOverride("font_color", Ink);
        label.AddThemeFontOverride("font", new FontVariation { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.7f });
        panel.AddChild(label);
        return (panel, label);
    }
}
