using System;
using System.Collections.Generic;
using Godot;

namespace Fungtopia.Dialogue.View;

/// <summary>
/// The speech bubble, drawn in code: a rounded cream bubble with a soft drop shadow and an ink
/// outline, the speaker's name tab (a little spotted pink mushroom cap on a cream stem band) at its
/// top left, and the bouncing "continue" arrow at its bottom right.
/// </summary>
public partial class SpeechBubble : Control
{
    public const float Corner = 46f;
    public const float Outline = 5f;
    private const float BodyTop = 0.5f;
    public const float TabHeight = 86f;

    private static readonly Vector3[] Spots =
    {
        new(0.1f, 0.68f, 0.09f), new(0.24f, 0.3f, 0.12f), new(0.5f, 0.12f, 0.09f), new(0.75f, 0.27f, 0.11f), new(0.9f, 0.66f, 0.08f),
    };

    private Vector2[] _body = Array.Empty<Vector2>();
    private Vector2[] _shadow = Array.Empty<Vector2>();
    private Vector2[] _bodyOutline = Array.Empty<Vector2>();
    private Vector2[] _cap = Array.Empty<Vector2>();
    private Vector2[] _capOutline = Array.Empty<Vector2>();
    private Vector2 _builtSize;
    private float _builtTab;
    private readonly Vector2[] _arrow = new Vector2[3];
    private readonly Vector2[] _arrowOutline = new Vector2[4];

    /// <summary>Width of the name tab in pixels (fits the speaker's name).</summary>
    public float TabWidth { get; set; } = 220f;

    public bool ArrowVisible { get; set; }

    /// <summary>The arrow's bounce, 0 (rest) to 1 (lowest).</summary>
    public float ArrowBounce { get; set; }

    public SpeechBubble() => MouseFilter = MouseFilterEnum.Ignore;

    /// <summary>The rectangle of the bubble's body, below the tab.</summary>
    public Rect2 Body => new(0f, TabHeight * BodyTop, Size.X, Size.Y - TabHeight * BodyTop);

    /// <summary>The rectangle of the tab's band where the name sits.</summary>
    public Rect2 TabBand => new(Corner * 0.6f, 0f, TabWidth, TabHeight);

    public override void _Draw()
    {
        if (_builtSize != Size || _builtTab != TabWidth)
            Build();

        DrawColoredPolygon(_shadow, new Color(DialogueStyle.Ink, 0.22f));
        DrawColoredPolygon(_body, DialogueStyle.Paper);
        var body = Body;
        DrawPolyline(_bodyOutline, DialogueStyle.Ink, Outline, true);

        // The name tab: a mushroom cap.
        var tab = TabBand;
        DrawColoredPolygon(_cap, DialogueStyle.Cap);
        foreach (var spot in Spots)
            DrawCircle(tab.Position + new Vector2(spot.X * tab.Size.X, spot.Y * tab.Size.Y), spot.Z * tab.Size.Y, DialogueStyle.Spot);
        DrawPolyline(_capOutline, DialogueStyle.Ink, Outline, true);

        if (ArrowVisible)
        {
            var tip = new Vector2(body.End.X - 64f, body.End.Y - 30f + ArrowBounce * 10f);
            _arrow[0] = tip + new Vector2(-18f, -22f);
            _arrow[1] = tip + new Vector2(18f, -22f);
            _arrow[2] = tip;
            DrawColoredPolygon(_arrow, DialogueStyle.Cap);
            _arrowOutline[0] = _arrow[0];
            _arrowOutline[1] = _arrow[1];
            _arrowOutline[2] = _arrow[2];
            _arrowOutline[3] = _arrow[0];
            DrawPolyline(_arrowOutline, DialogueStyle.Ink, 4f, true);
        }
    }

    private void Build()
    {
        _builtSize = Size;
        _builtTab = TabWidth;
        var body = Body;
        var points = new List<Vector2>();
        AddCorner(points, new Vector2(body.End.X - Corner, body.Position.Y + Corner), -MathF.PI * 0.5f);
        AddCorner(points, new Vector2(body.End.X - Corner, body.End.Y - Corner), 0f);
        AddCorner(points, new Vector2(body.Position.X + Corner, body.End.Y - Corner), MathF.PI * 0.5f);
        AddCorner(points, new Vector2(body.Position.X + Corner, body.Position.Y + Corner), MathF.PI);
        _body = points.ToArray();
        _shadow = new Vector2[_body.Length];
        for (int i = 0; i < _body.Length; i++)
            _shadow[i] = _body[i] + new Vector2(0f, 10f);
        points.Add(points[0]);
        _bodyOutline = points.ToArray();

        // A dome over the band: a half ellipse with a flat, slightly rounded underside.
        var tab = TabBand;
        points.Clear();
        const int arc = 24;
        for (int i = 0; i <= arc; i++)
        {
            float a = MathF.PI + MathF.PI * i / arc;
            points.Add(new Vector2(tab.Position.X + tab.Size.X * 0.5f + MathF.Cos(a) * tab.Size.X * 0.5f,
                tab.End.Y - 8f + MathF.Sin(a) * (tab.Size.Y - 8f)));
        }
        for (int i = 1; i < 8; i++)
        {
            float t = i / 8f;
            points.Add(new Vector2(tab.End.X - t * tab.Size.X, tab.End.Y - 8f + MathF.Sin(MathF.PI * t) * 8f));
        }
        _cap = points.ToArray();
        points.Add(points[0]);
        _capOutline = points.ToArray();
    }

    private static void AddCorner(List<Vector2> points, Vector2 center, float start)
    {
        for (int i = 0; i <= 8; i++)
        {
            float a = start + MathF.PI * 0.5f * i / 8;
            points.Add(center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * Corner);
        }
    }
}
