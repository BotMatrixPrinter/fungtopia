using System.Collections.Generic;
using Godot;

namespace Fungtopia.Wardrobe.View;

/// <summary>
/// The shared frame of the wardrobe and shop screens: a rounded cream panel on the right of the
/// screen (the character shows on the left, behind it) with a pink header, a body and a footer
/// with the controls hint and a Done button. The view does the mouse hit-testing and focus moves.
/// </summary>
public abstract partial class ScreenPanel : PanelContainer
{
    public const float Width = 900f;

    protected readonly VBoxContainer Header = new() { Alignment = BoxContainer.AlignmentMode.Center };
    protected readonly VBoxContainer Body = new();
    protected readonly Label Hint;
    protected readonly HBoxContainer Footer = new();
    protected readonly Button DoneButton = new() { Text = "Done", CustomMinimumSize = new Vector2(180, 64) };
    protected readonly WardrobeModule Core;
    protected readonly IconStudio Icons;

    protected ScreenPanel(WardrobeModule core, IconStudio icons, string hint)
    {
        Core = core;
        Icons = icons;
        AddThemeStyleboxOverride("panel", WardrobeStyle.Box(WardrobeStyle.Cream, 46, 6, null, 26));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        AddChild(column);

        var cap = new PanelContainer();
        cap.AddThemeStyleboxOverride("panel", WardrobeStyle.Box(WardrobeStyle.Cap, 36, 5, null, 14));
        cap.AddChild(Header);
        column.AddChild(cap);

        Body.AddThemeConstantOverride("separation", 14);
        Body.SizeFlagsVertical = SizeFlags.ExpandFill;
        column.AddChild(Body);

        Footer.AddThemeConstantOverride("separation", 16);
        var footer = Footer;
        Hint = WardrobeStyle.Label(hint, 19, WardrobeStyle.Soft);
        Hint.HorizontalAlignment = HorizontalAlignment.Left;
        Hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        footer.AddChild(Hint);
        WardrobeStyle.Style(DoneButton, WardrobeStyle.Mint, 28, 30);
        DoneButton.Pressed += () => Core.Close();
        footer.AddChild(DoneButton);
        column.AddChild(footer);
    }

    /// <summary>Called every frame while shown: rebuilds what changed.</summary>
    public abstract void Refresh();

    /// <summary>Q / E, LB / RB: the previous or next tab (when the screen has tabs).</summary>
    public virtual void StepTab(int direction)
    {
    }

    /// <summary>The control to focus when the screen opens.</summary>
    public abstract Control FirstFocus();

    /// <summary>Places the panel on the right of a screen of <paramref name="screen"/> size.</summary>
    public void Place(Vector2 screen)
    {
        float scale = Mathf.Min(1f, screen.Y / 1080f);
        Scale = new Vector2(scale, scale);
        Size = new Vector2(Width, screen.Y / scale - 90f);
        Position = new Vector2(screen.X - (Width + 50f) * scale, 45f * scale);
    }

    /// <summary>A square card button with an icon (or a big word) and a caption.</summary>
    protected Button CardButton(Texture2D? icon, string caption, string? word, Color fill, Vector2 size, string? extra = null,
        Color? extraColor = null, float iconSize = 0f)
    {
        var button = new Button { CustomMinimumSize = size, FocusMode = FocusModeEnum.All };
        WardrobeStyle.Style(button, fill, 26);
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
