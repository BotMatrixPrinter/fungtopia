namespace Fungtopia.Town;

/// <summary>
/// Published by Town only when its street lamps switch: on at nightfall, off at dawn
/// (following <c>Fungtopia.DayNight.TimeOfDay.IsNight</c>). Never per tick.
/// </summary>
public readonly struct TownLights
{
    /// <summary>Whether the lamps are lit.</summary>
    public readonly bool On;

    public TownLights(bool on)
    {
        On = on;
    }
}
