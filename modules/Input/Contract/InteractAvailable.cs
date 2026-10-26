namespace Fungtopia.Input;

/// <summary>
/// A module publishes this with <see cref="Available"/> true while it offers an interaction (its
/// Interact prompt shows, or a dialogue is open) and false when that ends. Tracked per owner like
/// <see cref="UiFocus"/>. While any owner is available (or any <see cref="UiFocus"/> owner is open),
/// the gamepad's bottom face button gives <see cref="PlayerInput.Interact"/> instead of Jump.
/// </summary>
public readonly struct InteractAvailable
{
    /// <summary>Any number unique to the interaction's source (for example its UiFocus owner or a hash of its name).</summary>
    public readonly int Owner;

    /// <summary>True while the interaction is offered, false once it ends.</summary>
    public readonly bool Available;

    public InteractAvailable(int owner, bool available)
    {
        Owner = owner;
        Available = available;
    }
}
