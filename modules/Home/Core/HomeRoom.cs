using System;
using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.Home;

/// <summary>
/// The home interior's floor grid: <see cref="Areas.Home"/> (12 m x 10 m) in 0.5 m cells, cell
/// (0, 0) at its north-west corner (min X, min Z). Some cells are kept clear: the way from the
/// door (in the south wall) to where you appear on entering, the catalog book's stand, and the
/// big wardrobe that stands in the north-east corner from the start.
/// </summary>
public static class HomeRoom
{
    /// <summary>Side of a grid cell, meters.</summary>
    public const float Cell = 0.5f;

    /// <summary>Cells along X (24) and Z (20).</summary>
    public static readonly int Columns = (int)MathF.Round((Areas.Home.MaxX - Areas.Home.MinX) / Cell);
    public static readonly int Rows = (int)MathF.Round((Areas.Home.MaxZ - Areas.Home.MinZ) / Cell);

    /// <summary>The floor's height.</summary>
    public static float FloorY => Areas.HomeFloorY;

    // The door's way in: 2 m wide around the door, from the south wall to 0.5 m past HomeInterior.
    public static readonly int DoorMinColumn = ColumnAt(Places.HomeExit.Position.X - 1f);
    public static readonly int DoorMaxColumn = ColumnAt(Places.HomeExit.Position.X + 1f) - 1;
    public static readonly int DoorMinRow = RowAt(Places.HomeInterior.Position.Z - 0.5f);
    public static readonly int DoorMaxRow = Rows - 1;

    /// <summary>The catalog book's stand: two by two cells against the south wall, east of the door.</summary>
    public static readonly int BookColumn = DoorMaxColumn + 4;
    public static readonly int BookRow = Rows - 2;
    public const int BookSize = 2;

    /// <summary>The catalog book's stand: the middle of its cells, on the floor.</summary>
    public static Vector3 BookPosition => CellsCenter(BookColumn, BookRow, BookSize, BookSize);

    /// <summary>
    /// The wardrobe: fixed against the north wall in the north-east corner (east of the window),
    /// three cells wide and two deep; never moved, removed or covered by furniture.
    /// </summary>
    public static readonly int WardrobeColumn = Columns - 4;
    public const int WardrobeRow = 0;
    public const int WardrobeWidth = 3;
    public const int WardrobeDepth = 2;

    /// <summary>The wardrobe's footprint: the middle of its cells, on the floor (its doors face south, into the room).</summary>
    public static Vector3 WardrobePosition => CellsCenter(WardrobeColumn, WardrobeRow, WardrobeWidth, WardrobeDepth);

    /// <summary>Where to stand to use the wardrobe: the middle of its front, on the floor.</summary>
    public static Vector3 WardrobeFront => CellsCenter(WardrobeColumn, WardrobeRow + WardrobeDepth, WardrobeWidth, 0);

    /// <summary>Whether a cell is under the wardrobe.</summary>
    public static bool UnderWardrobe(int column, int row) =>
        column >= WardrobeColumn && column < WardrobeColumn + WardrobeWidth && row >= WardrobeRow && row < WardrobeRow + WardrobeDepth;

    public static int ColumnAt(float x) => (int)MathF.Floor((x - Areas.Home.MinX) / Cell);

    public static int RowAt(float z) => (int)MathF.Floor((z - Areas.Home.MinZ) / Cell);

    /// <summary>
    /// Where a ray (for example from the camera through the mouse) meets the floor plane at
    /// <see cref="FloorY"/>: false when it runs parallel to the floor, points away from it, or is not finite.
    /// </summary>
    public static bool TryFloorPoint(Vector3 origin, Vector3 direction, out Vector3 point)
    {
        point = default;
        float t = (FloorY - origin.Y) / direction.Y;
        if (MathF.Abs(direction.Y) < 1e-6f || !(t >= 0f) || !float.IsFinite(t))
            return false;
        point = new Vector3(origin.X + direction.X * t, FloorY, origin.Z + direction.Z * t);
        return float.IsFinite(point.X) && float.IsFinite(point.Z);
    }

    /// <summary>
    /// The north-west cell of a <paramref name="width"/> x <paramref name="depth"/> footprint whose
    /// middle is as near as the grid allows to <paramref name="point"/> on the floor (a 1 x 1 footprint
    /// is the cell under the point), kept inside the room (clamped at the walls). Pure.
    /// </summary>
    public static (int Column, int Row) CellUnder(Vector3 point, int width = 1, int depth = 1)
    {
        width = Math.Clamp(width, 1, Columns);
        depth = Math.Clamp(depth, 1, Rows);
        float x = (point.X - Areas.Home.MinX) / Cell - (width - 1) * 0.5f;
        float z = (point.Z - Areas.Home.MinZ) / Cell - (depth - 1) * 0.5f;
        // Clamp before converting, so points far outside (or not finite) stay inside too.
        int column = (int)MathF.Floor(Math.Clamp(float.IsFinite(x) ? x : 0f, 0f, Columns - width));
        int row = (int)MathF.Floor(Math.Clamp(float.IsFinite(z) ? z : 0f, 0f, Rows - depth));
        return (column, row);
    }

    /// <summary>The middle (on the floor) of the rectangle of cells starting at (column, row).</summary>
    public static Vector3 CellsCenter(int column, int row, int width, int depth) =>
        new(Areas.Home.MinX + (column + width * 0.5f) * Cell, FloorY, Areas.Home.MinZ + (row + depth * 0.5f) * Cell);

    /// <summary>Whether a cell is inside the room.</summary>
    public static bool Inside(int column, int row) => column >= 0 && row >= 0 && column < Columns && row < Rows;

    /// <summary>Whether a cell must stay clear (the door's way in, the catalog book's stand, the wardrobe).</summary>
    public static bool KeptClear(int column, int row) =>
        (column >= DoorMinColumn && column <= DoorMaxColumn && row >= DoorMinRow && row <= DoorMaxRow)
        || (column >= BookColumn && column < BookColumn + BookSize && row >= BookRow && row < BookRow + BookSize)
        || UnderWardrobe(column, row);
}
