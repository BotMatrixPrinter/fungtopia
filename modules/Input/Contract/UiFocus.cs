namespace Fungtopia.Input;

/// <summary>
/// A module publishes this with <see cref="Open"/> true while one of its menus is open and false
/// when it closes. While any owner is open, <see cref="PlayerInput"/> has Move and Look zero and
/// Jump and Fire false; Interact and OpenMap are still reported.
/// </summary>
public readonly struct UiFocus
{
    /// <summary>Any number unique to the menu (for example a hash of its name).</summary>
    public readonly int Owner;

    /// <summary>True while the menu is open, false once it closes.</summary>
    public readonly bool Open;

    public UiFocus(int owner, bool open)
    {
        Owner = owner;
        Open = open;
    }
}
