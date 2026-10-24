using System.Collections.Generic;
using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// The shared frame of the wardrobe and shop screens, styled like the start screen's "Make your
/// look": on the right half of the screen only (the character stands on the left), a big cream
/// heading, the rounded cream panel (header, body, a footer with the controls hint) and big
/// mushroom buttons under it (Done). The view does the mouse hit-testing and focus moves.
/// </summary>
public abstract partial class ScreenPanel : Control
{
    public const float Width = 800f;

    /// <summary>The space kept right of the panel at 1080p.</summary>
    public const float Margin = 70f;

    protected readonly Label Title;
    protected readonly VBoxContainer Header = new() { Alignment = BoxContainer.AlignmentMode.Center };
    protected readonly VBoxContainer Body = new();
    protected readonly Label Hint;
    protected readonly HBoxContainer Footer = new();
    protected readonly HBoxContainer Buttons = new() { Alignment = BoxContainer.AlignmentMode.Center };
    protected readonly CapButton DoneButton = new("Done", WardrobeStyle.Green, new Vector2(240, 104), 40);
    protected readonly WardrobeModule Core;
    protected readonly IconStudio Icons;

    protected ScreenPanel(WardrobeModule core, IconStudio icons, string title, string hint)
    {
        Core = core;
        Icons = icons;
        MouseFilter = MouseFilterEnum.Ignore;
        var page = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        page.AddThemeConstantOverride("separation", 10);
        AddChild(page);

        Title = WardrobeStyle.Heading(title, 58);
        page.AddChild(Title);

        var panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", WardrobeStyle.PanelBox());
        page.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        panel.AddChild(column);
        Header.AddThemeConstantOverride("separation", 10);
        column.AddChild(Header);
        Body.AddThemeConstantOverride("separation", 12);
        Body.SizeFlagsVertical = SizeFlags.ExpandFill;
        column.AddChild(Body);

        Footer.AddThemeConstantOverride("separation", 16);
        Hint = WardrobeStyle.Label(hint, 18, WardrobeStyle.Soft);
        Hint.HorizontalAlignment = HorizontalAlignment.Left;
        Hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Footer.AddChild(Hint);
        column.AddChild(Footer);

        Buttons.AddThemeConstantOverride("separation", 40);
        DoneButton.Pressed += () => Core.Close();
        Buttons.AddChild(DoneButton);
        page.AddChild(Buttons);
    }

    /// <summary>Called every frame while shown: rebuilds what changed.</summary>
    public abstract void Refresh();

    /// <summary>Page Up / Page Down, the triggers: the previous or next tab (when the screen has tabs).</summary>
    public virtual void StepTab(int direction)
    {
    }

    /// <summary>The control to focus when the screen opens.</summary>
    public abstract Control FirstFocus();

    /// <summary>The screen's scale for a screen of <paramref name="screen"/> size: 1 at 1080p, never past the right half.</summary>
    public static float ScaleFor(Vector2 screen) => Mathf.Min(screen.Y / 1080f, screen.X * 0.5f / (Width + Margin));

    /// <summary>Places the screen on the right half of a screen of <paramref name="screen"/> size, the left half left free.</summary>
    public void Place(Vector2 screen)
    {
        float scale = ScaleFor(screen);
        Scale = new Vector2(scale, scale);
        Size = new Vector2(Width, screen.Y / scale - 36f);
        Position = new Vector2(screen.X - (Width + Margin) * scale, 18f * scale);
    }

    /// <summary>A square card button with an icon (or a big word) and a caption.</summary>
    protected Button CardButton(Texture2D? icon, string caption, string? word, Color fill, Vector2 size, string? extra = null,
        Color? extraColor = null, float iconSize = 0f)
    {
        var button = new Button { CustomMinimumSize = size, FocusMode = FocusModeEnum.All };
        WardrobeStyle.Style(button, fill, 28);
        if (fill == WardrobeStyle.Mint)
            button.AddThemeStyleboxOverride("normal", WardrobeStyle.Box(fill, 28, 7, WardrobeStyle.Ring.Darkened(0.1f)));
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.KeepSize, 8);
        button.AddChild(box);
        if (iconSize <= 0f)
            iconSize = size.X - 36f;
        if (icon != null)
            box.AddChild(new TextureRect
            {
                Texture = icon, CustomMinimumSize = new Vector2(iconSize, iconSize), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            });
        else if (word != null)
        {
            var big = WardrobeStyle.Label(word, 30, WardrobeStyle.Soft);
            big.CustomMinimumSize = new Vector2(iconSize, iconSize * 0.8f);
            box.AddChild(big);
        }
        var label = WardrobeStyle.Label(caption, 16, WardrobeStyle.Ink);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(size.X - 20f, 0);
        box.AddChild(label);
        if (extra != null)
            box.AddChild(WardrobeStyle.Label(extra, 18, extraColor ?? WardrobeStyle.Ink));
        return button;
    }

    protected static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>Focuses the <paramref name="index"/>-th button among <paramref name="buttons"/> (clamped), if any.</summary>
    protected static void FocusAt(List<Button> buttons, int index)
    {
        if (buttons.Count > 0)
            buttons[Mathf.Clamp(index, 0, buttons.Count - 1)].GrabFocus();
    }
}
