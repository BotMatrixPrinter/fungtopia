using System;
using System.Numerics;

namespace Fungtopia.Map;

/// <summary>An axis-aligned box of the home interior: its name, center and full size, in meters.</summary>
public readonly struct HomeBox
{
    public readonly string Name;
    public readonly Vector3 Center;
    public readonly Vector3 Size;

    public HomeBox(string name, Vector3 center, Vector3 size)
    {
        Name = name;
        Center = center;
        Size = size;
    }

    public Vector3 Min => Center - Size * 0.5f;
    public Vector3 Max => Center + Size * 0.5f;
}

/// <summary>
/// One round window of the home interior: <see cref="OnWall"/> is its center in its wall's inner
/// face plane, <see cref="Inward"/> the unit normal of that face into the room, and the depths are
/// meters in front of the face along it. The glass is a thin disc from <see cref="GlassBack"/> to
/// <see cref="GlassFront"/>; the frame ring (a torus flattened along its axis by
/// <see cref="HomeLayout.FrameDepthScale"/>) from <see cref="FrameBack"/> to <see cref="FrameFront"/>.
/// </summary>
public readonly struct HomeWindow
{
    public readonly Vector3 OnWall;
    public readonly Vector3 Inward;

    public HomeWindow(Vector3 onWall, Vector3 inward)
    {
        OnWall = onWall;
        Inward = inward;
    }

    public float GlassBack => HomeLayout.GlassGap;
    public float GlassFront => HomeLayout.GlassGap + HomeLayout.GlassThickness;
    public float GlassCenter => (GlassBack + GlassFront) * 0.5f;
    public float FrameBack => GlassFront + HomeLayout.FrameGap;
    public float FrameCenter => FrameBack + HomeLayout.FrameHalfDepth;
    public float FrameFront => FrameBack + 2f * HomeLayout.FrameHalfDepth;

    /// <summary>The point <paramref name="depth"/> meters in front of the wall on the window's axis.</summary>
    public Vector3 At(float depth) => OnWall + Inward * depth;
}

/// <summary>
/// The player's home interior as the Map's view draws it: a room exactly <see cref="Areas.Home"/>
/// (the walls' inner faces on its edges) with its floor at <see cref="Areas.HomeFloorY"/>. Every
/// piece stands clear of the others: no two faces facing the same way lie within
/// <see cref="MinGap"/> of each other (pieces only meet face to face), so nothing z-fights.
/// </summary>
public static class HomeLayout
{
    public const float Wall = 0.3f, Height = 4f, DoorWidth = 1.6f, DoorHeight = 2.4f;

    /// <summary>The least distance between two surfaces of different pieces that could z-fight.</summary>
    public const float MinGap = 0.02f;

    /// <summary>Windows: glass radius, frame ring radii and the windows' centers above the floor.</summary>
    public const float GlassRadius = 0.62f, FrameInnerRadius = 0.6f, FrameOuterRadius = 0.78f, WindowY = 2.1f;

    /// <summary>The glass disc stands GlassGap in front of the wall, the frame FrameGap in front of the glass.</summary>
    public const float GlassGap = 0.03f, GlassThickness = 0.01f, FrameGap = 0.025f;

    /// <summary>The frame torus is squashed along its axis by this, so it stays close to the wall.</summary>
    public const float FrameDepthScale = 0.5f;

    public const float FrameHalfDepth = (FrameOuterRadius - FrameInnerRadius) * 0.5f * FrameDepthScale;

    public const float KnobRadius = 0.08f;

    private static readonly AreaBounds Room = Areas.Home;
    private static readonly float Y = Areas.HomeFloorY;
    private static readonly float W = Room.MaxX - Room.MinX, D = Room.MaxZ - Room.MinZ;
    private static readonly float Cx = Room.Center.X, Cz = Room.Center.Y;

    /// <summary>The door's center x, in the middle of the south wall at <see cref="Places.HomeExit"/>.</summary>
    public static readonly float DoorX = Places.HomeExit.Position.X;

    private static readonly float SouthZ = Room.MaxZ + Wall * 0.5f, SideWidth = (W - DoorWidth) * 0.5f;
    private const float DoorThickness = 0.12f, Skirting = 0.06f, SkirtingHeight = 0.2f;

    public static readonly HomeBox Floor = new("Floor", new(Cx, Y - 0.1f, Cz), new(W + 2f * Wall, 0.2f, D + 2f * Wall));
    public static readonly HomeBox WallNorth = new("WallNorth", new(Cx, Y + Height * 0.5f, Room.MinZ - Wall * 0.5f), new(W + 2f * Wall, Height, Wall));
    public static readonly HomeBox WallWest = new("WallWest", new(Room.MinX - Wall * 0.5f, Y + Height * 0.5f, Cz), new(Wall, Height, D));
    public static readonly HomeBox WallEast = new("WallEast", new(Room.MaxX + Wall * 0.5f, Y + Height * 0.5f, Cz), new(Wall, Height, D));
    public static readonly HomeBox WallSouthWest = new("WallSouthWest", new(Room.MinX + SideWidth * 0.5f - Wall * 0.5f, Y + Height * 0.5f, SouthZ), new(SideWidth + Wall, Height, Wall));
    public static readonly HomeBox WallSouthEast = new("WallSouthEast", new(Room.MaxX - SideWidth * 0.5f + Wall * 0.5f, Y + Height * 0.5f, SouthZ), new(SideWidth + Wall, Height, Wall));
    public static readonly HomeBox Lintel = new("WallSouthLintel", new(DoorX, Y + (Height + DoorHeight) * 0.5f, SouthZ), new(DoorWidth, Height - DoorHeight, Wall));
    public static readonly HomeBox Door = new("Door", new(DoorX, Y + DoorHeight * 0.5f, SouthZ), new(DoorWidth - 0.1f, DoorHeight - 0.05f, DoorThickness));

    // The skirting along the north wall runs its whole width; the side ones stop where it starts,
    // so no two boards overlap (their tops would z-fight in the corners).
    public static readonly HomeBox SkirtingNorth = new("SkirtingNorth", new(Cx, Y + SkirtingHeight * 0.5f, Room.MinZ + Skirting * 0.5f), new(W, SkirtingHeight, Skirting));
    public static readonly HomeBox SkirtingWest = new("SkirtingWest", new(Room.MinX + Skirting * 0.5f, Y + SkirtingHeight * 0.5f, Cz + Skirting * 0.5f), new(Skirting, SkirtingHeight, D - Skirting));
    public static readonly HomeBox SkirtingEast = new("SkirtingEast", new(Room.MaxX - Skirting * 0.5f, Y + SkirtingHeight * 0.5f, Cz + Skirting * 0.5f), new(Skirting, SkirtingHeight, D - Skirting));

    private static readonly HomeBox[] BoxArray =
    {
        Floor, WallNorth, WallWest, WallEast, WallSouthWest, WallSouthEast, Lintel, Door,
        SkirtingNorth, SkirtingWest, SkirtingEast,
    };

    /// <summary>Every box of the room: floor, walls, lintel, door and skirting boards.</summary>
    public static ReadOnlySpan<HomeBox> Boxes => BoxArray;

    /// <summary>The door knob's center: a sphere sunk into the door's inner face, mostly standing out of it.</summary>
    public static readonly Vector3 KnobCenter = new(DoorX + 0.5f, Y + 1.1f, Door.Min.Z - KnobRadius * 0.5f);

    private static readonly HomeWindow[] WindowArray =
    {
        new(new(Cx - 3f, Y + WindowY, Room.MinZ), Vector3.UnitZ),
        new(new(Cx + 3f, Y + WindowY, Room.MinZ), Vector3.UnitZ),
        new(new(Room.MinX, Y + WindowY, Cz), Vector3.UnitX),
        new(new(Room.MaxX, Y + WindowY, Cz), -Vector3.UnitX),
    };

    /// <summary>The four round windows: two in the north wall, one in the west and one in the east wall.</summary>
    public static ReadOnlySpan<HomeWindow> Windows => WindowArray;
}
