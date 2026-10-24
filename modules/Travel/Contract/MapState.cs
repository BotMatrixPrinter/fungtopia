namespace Fungtopia.Travel;

/// <summary>
/// Whether the full-screen travel map is open, published by the Travel module once every tick.
/// While it is open the HUD hides and the player should stand still.
/// </summary>
public readonly struct MapState
{
    public readonly bool Open;

    public MapState(bool open)
    {
        Open = open;
    }
}
