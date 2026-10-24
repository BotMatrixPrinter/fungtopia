namespace Fungtopia.Shell;

/// <summary>
/// Implemented by the root node of a module's Godot scene (modules/&lt;Name&gt;/Godot/Main.tscn).
/// The shell hands it its own module's Core so it can draw it and feed it input.
/// </summary>
public interface IModuleView
{
    void Bind(IModule module);
}
