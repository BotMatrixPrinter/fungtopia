namespace Fungtopia.NPCs;

/// <summary>
/// Millie's current quest, published by the NPCs module once every tick (for the HUD).
/// </summary>
public readonly struct QuestState
{
    /// <summary>A quest has been given and not yet paid out.</summary>
    public readonly bool Active;

    /// <summary>What to do, e.g. "Defeat 5 Mushroom Caterpillars"; empty when not <see cref="Active"/>.</summary>
    public readonly string Title;

    /// <summary>How far along it is, 0 up to <see cref="Goal"/>.</summary>
    public readonly int Progress;

    /// <summary>The count (monsters, or M¢ earned) that completes it; 0 when not <see cref="Active"/>.</summary>
    public readonly int Goal;

    /// <summary>Done: talk to Millie for the reward.</summary>
    public readonly bool Complete;

    public QuestState(bool active, string title, int progress, int goal, bool complete)
    {
        Active = active;
        Title = title;
        Progress = progress;
        Goal = goal;
        Complete = complete;
    }
}
