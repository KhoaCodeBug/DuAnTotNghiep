/// <summary>
/// Process-local flag for the standalone, solo tutorial. It deliberately never
/// travels over Fusion so a multiplayer room cannot inherit tutorial rules.
/// </summary>
public static class TutorialSession
{
    public static bool IsActive { get; private set; }

    public static void Begin() => IsActive = true;

    public static void End()
    {
        IsActive = false;
        TutorialInputGate.Clear();
    }
}
