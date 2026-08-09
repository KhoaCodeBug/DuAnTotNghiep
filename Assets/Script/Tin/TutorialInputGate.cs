/// <summary>
/// Local tutorial restrictions. This is intentionally separate from
/// Time.timeScale: Fusion's simulation keeps running and multiplayer is never
/// affected because only the tutorial scene enables it.
/// </summary>
public static class TutorialInputGate
{
    public static bool MovementLocked { get; private set; }
    public static bool SurvivalFrozen { get; private set; }
    public static bool CameraZoomLocked { get; private set; }
    public static bool FireLocked { get; private set; }

    public static void Configure(bool movementLocked, bool survivalFrozen)
    {
        MovementLocked = movementLocked;
        SurvivalFrozen = survivalFrozen;
    }

    public static void SetCameraZoomLocked(bool locked) => CameraZoomLocked = locked;
    public static void SetFireLocked(bool locked) => FireLocked = locked;

    public static void Clear()
    {
        MovementLocked = false;
        SurvivalFrozen = false;
        CameraZoomLocked = false;
        FireLocked = false;
    }
}
