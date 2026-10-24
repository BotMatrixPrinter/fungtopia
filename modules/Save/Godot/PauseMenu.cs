using System;
using System.Collections.Generic;
using Fungtopia.Map;
using Godot;

namespace Fungtopia.Save.View;

/// <summary>Which screen of the pause menu shows.</summary>
public enum MenuPage
{
    Main,
    Slots,
    Choice,
}

/// <summary>
/// Draws the pause menu (a cream panel with a spotted mushroom cap on a dimmed screen) and says
/// which item is at a point. It keeps no game state: <see cref="SaveView"/> tells it the page,
/// the highlighted item and the slot list.
/// </summary>
public partial class PauseMenu : Control
{
    public static readonly Color Ink = new(0.27f, 0.17f, 0.23f);
    public static readonly Color Cream = new(1f, 0.95f, 0.86f);
    public static readonly Color Spot = new(1f, 0.97f, 0.93f);
    public static readonly Color Pink = new(0.95f, 0.49f, 0.55f);
    public static readonly Color Red = new(0.9f, 0.25f, 0.27f);
    public static readonly Color Gold = new(1f, 0.78f, 0.3f);
    public static readonly Color Blue = new(0.45f, 0.62f, 0.95f);
    public static readonly Color Green = new(0.5f, 0.78f, 0.42f);
    public static readonly Color Lilac = new(0.72f, 0.56f, 0.92f);
    public static readonly Color Grey = new(0.62f, 0.56f, 0.58f);

    private static readonly string[] MainItems = { "Resume", "Save", "Load", "Quit to Title" };
    private static readonly Color[] MainColors = { Green, Gold, Blue, Pink };

    private static Font? _font;

    /// <summary>A rounded, heavy font when the system has one, else the boldest fallback.</summary>
    public static Font Font => _font ??= new SystemFont
    {
        FontNames = new[] { "Nunito", "Fredoka", "Baloo 2", "Varela Round", "Comic Neue", "Quicksand", "DejaVu Sans" },
        FontWeight = 800,
        Antialiasing = TextServer.FontAntialiasing.Gray,
    };

    private readonly StyleBoxFlat _box = new() { CornerDetail = 12, AntiAliasing = true };
    private readonly List<Rect2> _items = new();
    private float _time;

    public MenuPage Page { get; set; }

    public int Highlight { get; set; }

    /// <summary>The slot picked on the Slots page (shown on the Choice page).</summary>
    public int ChosenSlot { get; set; } = 1;

    public SaveSlots Slots { get; set; }

    public int CurrentSlot { get; set; }

    /// <summary>0..1, how far the panel has popped in.</summary>
    public float Appear { get; set; } = 1f;

    public PauseMenu()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Number of items on a page (the last is always Back, except on Main).</summary>
    public static int ItemCount(MenuPage page) => page switch
    {
        MenuPage.Main => MainItems.Length,
        MenuPage.Slots => SaveSlots.Count + 1,
        _ => 3,
    };

    /// <summary>The item under <paramref name="point"/> (as last drawn), or -1.</summary>
    public int ItemAt(Vector2 point)
    {
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].HasPoint(point))
                return i;
        return -1;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        if (size.X < 64f || size.Y < 64f)
            return;
        _items.Clear();
        float ease = 1f - (1f - Appear) * (1f - Appear);
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.12f, 0.06f, 0.14f, 0.55f * ease));

        float scale = Mathf.Min(size.X / 1920f, size.Y / 1080f);
        float pop = 0.85f + 0.15f * ease + 0.04f * Mathf.Sin(Mathf.Clamp(Appear, 0f, 1f) * Mathf.Pi);
        DrawSetTransform(size * 0.5f, 0f, Vector2.One * scale * pop);
        var panelSize = Page == MenuPage.Slots ? new Vector2(1180f, 760f) : new Vector2(Page == MenuPage.Main ? 720f : 840f, Page == MenuPage.Main ? 720f : 600f);
        var panel = new Rect2(-panelSize * 0.5f + new Vector2(0f, 40f), panelSize);
        Box(panel, Cream, Ink, 8, 48, 16);
        DrawCap(new Vector2(0f, panel.Position.Y + 22f), panelSize.X * 0.78f, 120f, Red, 4);

        string title = Page switch
        {
            MenuPage.Main => "Paused",
            MenuPage.Slots => "Load a Game",
            _ => $"Slot {ChosenSlot}",
        };
        Text(title, new Vector2(0f, panel.Position.Y + 76f), 60, Spot, 14);

        switch (Page)
        {
            case MenuPage.Main:
                for (int i = 0; i < MainItems.Length; i++)
                    Pill(i, new Vector2(0f, panel.Position.Y + 220f + i * 128f), new Vector2(460f, 100f), MainItems[i], MainColors[i], true, scale * pop, size);
                break;
            case MenuPage.Slots:
                for (int i = 0; i < SaveSlots.Count; i++)
                    SlotCard(i, new Vector2((i - 1) * 370f, panel.Position.Y + 390f), Slots.Get(i + 1), scale * pop, size);
                Pill(SaveSlots.Count, new Vector2(0f, panel.End.Y - 80f), new Vector2(260f, 80f), "Back", Lilac, true, scale * pop, size);
                break;
            default:
                var slot = Slots.Get(Math.Clamp(ChosenSlot, 1, SaveSlots.Count));
                Pill(0, new Vector2(0f, panel.Position.Y + 220f), new Vector2(560f, 120f), "Manual save", Gold, slot.Manual.Ok, scale * pop, size,
                    Describe(slot.Manual));
                Pill(1, new Vector2(0f, panel.Position.Y + 370f), new Vector2(560f, 120f), "Autosave", Green, slot.Auto.Ok, scale * pop, size,
                    Describe(slot.Auto));
                Pill(2, new Vector2(0f, panel.End.Y - 80f), new Vector2(260f, 80f), "Back", Lilac, true, scale * pop, size);
                break;
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    /// <summary>"Day 3 · 120 M¢ · 14:05" for a save, or why it cannot be loaded.</summary>
    public static string Describe(SaveInfo info) => info.State switch
    {
        SaveFileState.None => "Empty",
        SaveFileState.Unreadable => "Unreadable",
        _ => $"Day {info.Day} · {info.Coins} M¢ · {When(info.SavedAt)}",
    };

    public static string When(DateTime utc)
    {
        var local = utc.ToLocalTime();
        return local.Date == DateTime.Now.Date ? local.ToString("HH:mm") : local.ToString("d MMM HH:mm");
    }

    public static string AreaName(Area area) => area switch
    {
        Area.Town => "Mushroom Town",
        Area.Road => "On the road",
        Area.Forest => "Mushroom Forest",
        Area.PetCity => "Pet City",
        Area.Home => "Home",
        Area.DeepForest => "Deep Forest",
        _ => area.ToString(),
    };

    private void SlotCard(int index, Vector2 center, SlotInfo info, float scale, Vector2 screen)
    {
        var size = new Vector2(330f, 420f);
        var rect = new Rect2(center - size * 0.5f, size);
        bool lit = Highlight == index;
        float s = lit ? 1.04f + Mathf.Sin(_time * 5f) * 0.02f : 1f;
        rect = new Rect2(center - size * s * 0.5f, size * s);
        AddItem(rect, scale, screen);
        var newest = info.Newest;
        Color cap = index switch { 0 => Pink, 1 => Blue, _ => Gold };
        Box(rect, newest.Ok ? Spot : Cream.Darkened(0.06f), lit ? Gold.Darkened(0.2f) : Ink, lit ? 8 : 5, 32, lit ? 12 : 6);
        DrawCap(new Vector2(rect.GetCenter().X, rect.Position.Y + 96f), rect.Size.X * 0.7f, 70f, cap, index + 1);
        Text($"Slot {info.Slot}", new Vector2(rect.GetCenter().X, rect.Position.Y + 128f), 40, Ink, 0);
        if (info.Slot == CurrentSlot && CurrentSlot != 0)
            Text("playing", new Vector2(rect.GetCenter().X, rect.Position.Y + 166f), 24, Pink.Darkened(0.2f), 0);
        float y = rect.Position.Y + 214f;
        if (newest.Ok)
        {
            Text(newest.CharacterName, new Vector2(rect.GetCenter().X, y), 36, Ink, 0);
            Text($"Day {newest.Day} · {newest.Coins} M¢", new Vector2(rect.GetCenter().X, y + 46f), 28, Ink, 0);
            Text(AreaName(newest.Area), new Vector2(rect.GetCenter().X, y + 84f), 26, Ink.Lightened(0.25f), 0);
            Text($"Save: {(info.Manual.Ok ? When(info.ManualSavedAt) : info.Manual.Exists ? "unreadable" : "-")}",
                new Vector2(rect.GetCenter().X, y + 132f), 22, Ink.Lightened(0.3f), 0);
            Text($"Auto: {(info.Auto.Ok ? When(info.AutoSavedAt) : info.Auto.Exists ? "unreadable" : "-")}",
                new Vector2(rect.GetCenter().X, y + 162f), 22, Ink.Lightened(0.3f), 0);
        }
        else
        {
            Text(info.Exists ? "Unreadable" : "Empty", new Vector2(rect.GetCenter().X, y + 50f), 36, info.Exists ? Red : Grey, 0);
            if (info.Exists)
                Text("(the file is damaged)", new Vector2(rect.GetCenter().X, y + 96f), 22, Ink.Lightened(0.3f), 0);
        }
    }

    private void Pill(int index, Vector2 center, Vector2 size, string label, Color color, bool enabled, float scale, Vector2 screen, string? sub = null)
    {
        bool lit = Highlight == index;
        float s = lit ? 1.05f + Mathf.Sin(_time * 5f) * 0.025f : 1f;
        var rect = new Rect2(center - size * s * 0.5f, size * s);
        AddItem(rect, scale, screen);
        var fill = enabled ? (lit ? color.Lightened(0.15f) : color) : Grey;
        Box(rect, fill, lit ? Gold.Darkened(0.25f) : Ink, lit ? 8 : 5, (int)(rect.Size.Y * 0.5f), lit ? 10 : 6);
        // Two little spots, like a mushroom cap.
        DrawCircle(rect.Position + new Vector2(rect.Size.Y * 0.42f, rect.Size.Y * 0.36f), rect.Size.Y * 0.11f, new Color(Spot, 0.85f));
        DrawCircle(rect.End - new Vector2(rect.Size.Y * 0.5f, rect.Size.Y * 0.36f), rect.Size.Y * 0.08f, new Color(Spot, 0.85f));
        if (sub == null)
        {
            Text(label, rect.GetCenter(), 44, Spot, 10);
        }
        else
        {
            Text(label, rect.GetCenter() - new Vector2(0f, 20f), 42, Spot, 10);
            Text(sub, rect.GetCenter() + new Vector2(0f, 30f), 26, Spot, 7);
        }
        if (lit)
        {
            float bob = Mathf.Sin(_time * 6f) * 6f;
            var tip = new Vector2(rect.Position.X - 22f + bob, rect.GetCenter().Y);
            var arrow = new[] { tip, tip + new Vector2(-30f, -22f), tip + new Vector2(-30f, 22f) };
            DrawColoredPolygon(arrow, Gold);
            DrawPolyline(new[] { arrow[0], arrow[1], arrow[2], arrow[0] }, Ink, 4f, true);
        }
    }

    /// <summary>Remembers an item's rectangle in screen coordinates for <see cref="ItemAt"/>.</summary>
    private void AddItem(Rect2 local, float scale, Vector2 screen) =>
        _items.Add(new Rect2(screen * 0.5f + local.Position * scale, local.Size * scale));

    private void Box(Rect2 rect, Color fill, Color border, int borderWidth, int radius, int shadow)
    {
        _box.BgColor = fill;
        _box.BorderColor = border;
        _box.SetBorderWidthAll(borderWidth);
        _box.SetCornerRadiusAll(radius);
        _box.ShadowColor = new Color(Ink, 0.35f);
        _box.ShadowSize = shadow;
        _box.ShadowOffset = new Vector2(0, shadow * 0.8f);
        DrawStyleBox(_box, rect);
    }

    private void Text(string text, Vector2 center, int size, Color color, int outline)
    {
        if (text.Length == 0)
            return;
        var font = Font;
        var extent = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        var origin = center + new Vector2(-extent.X * 0.5f, font.GetAscent(size) - extent.Y * 0.5f);
        if (outline > 0)
            DrawStringOutline(font, origin, text, HorizontalAlignment.Left, -1, size, outline, Ink);
        DrawString(font, origin, text, HorizontalAlignment.Left, -1, size, color);
    }

    /// <summary>A spotted mushroom cap standing on <paramref name="baseCenter"/>, with an ink outline.</summary>
    private void DrawCap(Vector2 baseCenter, float width, float height, Color color, int seed)
    {
        const int n = 24;
        var points = new Vector2[n + 2];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Pi + Mathf.Pi * i / n;
            float flare = 1f + 0.08f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a)), 6f);
            points[i] = baseCenter + new Vector2(Mathf.Cos(a) * width * 0.5f * flare, Mathf.Sin(a) * height);
        }
        points[n + 1] = points[0];
        DrawColoredPolygon(points[..(n + 1)], color);
        for (int i = 0; i < 3; i++)
        {
            float u = ((seed * 7 + i * 5) % 9) / 9f;
            float x = (i - 1) * width * 0.3f + (u - 0.5f) * width * 0.08f;
            float y = -height * (i == 1 ? 0.62f : 0.38f);
            DrawCircle(baseCenter + new Vector2(x, y), height * (i == 1 ? 0.17f : 0.13f), Spot);
        }
        DrawPolyline(points, Ink, Mathf.Max(3f, height * 0.08f), true);
    }
}
