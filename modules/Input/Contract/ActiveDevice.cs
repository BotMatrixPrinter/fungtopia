namespace Fungtopia.Input;

/// <summary>The kind of device the player used last: keyboard and mouse, or a gamepad family.</summary>
public enum DeviceKind
{
    KeyboardMouse,
    Xbox,
    PlayStation,
    Nintendo,
    /// <summary>Any other gamepad; its prompts use the Xbox-style buttons.</summary>
    Generic,
}

/// <summary>
/// Published every tick by the Input module: the device the player used last (any key, mouse button,
/// mouse motion or wheel, or any gamepad button or stick / trigger past the dead zone). Show prompts
/// for this device only, with <see cref="Prompts"/>.
/// </summary>
public readonly struct ActiveDevice
{
    public readonly DeviceKind Kind;

    public ActiveDevice(DeviceKind kind) => Kind = kind;

    /// <summary>True for every gamepad family, false for keyboard and mouse.</summary>
    public bool IsGamepad => Kind != DeviceKind.KeyboardMouse;
}
