using System;
using System.Numerics;
using Fungtopia.Map;

namespace Fungtopia.Home;

/// <summary>
/// The home interior's floor grid: <see cref="Areas.Home"/> (12 m x 10 m) in 0.5 m cells, cell
/// (0, 0) at its north-west corner (min X, min Z). Some cells are kept clear: the way from the
/// door (in the south wall) to where you appear on entering, and the catalog book's stand.
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

    public static int ColumnAt(float x) => (int)MathF.Floor((x - Areas.Home.MinX) / Cell);

    public static int RowAt(float z) => (int)MathF.Floor((z - Areas.Home.MinZ) / Cell);

    /// <summary>The middle (on the floor) of the rectangle of cells starting at (column, row).</summary>
    public static Vector3 CellsCenter(int column, int row, int width, int depth) =>
        new(Areas.Home.MinX + (column + width * 0.5f) * Cell, FloorY, Areas.Home.MinZ + (row + depth * 0.5f) * Cell);

    /// <summary>Whether a cell is inside the room.</summary>
    public static bool Inside(int column, int row) => column >= 0 && row >= 0 && column < Columns && row < Rows;

    /// <summary>Whether a cell must stay clear (the door's way in, the catalog book's stand).</summary>
    public static bool KeptClear(int column, int row) =>
        (column >= DoorMinColumn && column <= DoorMaxColumn && row >= DoorMinRow && row <= DoorMaxRow)
        || (column >= BookColumn && column < BookColumn + BookSize && row >= BookRow && row < BookRow + BookSize);
}
