using System;

public enum ArrivalCarItemKind
{
    Toolbox,
    Hammer,
    FuelCan,
    Battery,
    Tire
}

public enum ArrivalCarRepairAction
{
    RepairCore,
    AddFuel,
    ReplaceBattery,
    ReplaceTire
}

[Flags]
public enum ArrivalCarRepairState
{
    None = 0,
    CoreRepaired = 1 << 0,
    FuelAdded = 1 << 1,
    BatteryReplaced = 1 << 2,
    TireReplaced = 1 << 3,

    RequiredComplete = CoreRepaired | FuelAdded | BatteryReplaced | TireReplaced
}

/// <summary>
/// Pure rules shared by the network transaction, runtime UI and EditMode tests.
/// Route A car preparation runs alongside Route B and must never replace its story stage.
/// </summary>
public static class ArrivalCarRepairRules
{
    // These durations follow the supplied repair recordings exactly. Keeping
    // them in the shared rules lets Fusion authority and the local clock use
    // the same timeline without adding serialized data to Main.unity.
    public const float FuelRepairDurationSeconds = 4.884875f;
    public const float TireRepairDurationSeconds = 3.8f;
    public const float HammerRepairDurationSeconds = 9.432f;

    public static bool TryGetAction(string partId, out ArrivalCarRepairAction action)
    {
        switch (partId)
        {
            case "engine":
            case "hood":
                action = ArrivalCarRepairAction.RepairCore;
                return true;
            case "fuel":
                action = ArrivalCarRepairAction.AddFuel;
                return true;
            case "battery":
                action = ArrivalCarRepairAction.ReplaceBattery;
                return true;
            // The opening car has exactly one unusable tire. Healthy tires are
            // inspection-only and must not consume the single replacement.
            case "front_left":
                action = ArrivalCarRepairAction.ReplaceTire;
                return true;
            default:
                action = default;
                return false;
        }
    }

    public static ArrivalCarRepairState GetStateBit(ArrivalCarRepairAction action) => action switch
    {
        ArrivalCarRepairAction.RepairCore => ArrivalCarRepairState.CoreRepaired,
        ArrivalCarRepairAction.AddFuel => ArrivalCarRepairState.FuelAdded,
        ArrivalCarRepairAction.ReplaceBattery => ArrivalCarRepairState.BatteryReplaced,
        ArrivalCarRepairAction.ReplaceTire => ArrivalCarRepairState.TireReplaced,
        _ => ArrivalCarRepairState.None
    };

    public static bool IsApplied(int stateMask, ArrivalCarRepairAction action) =>
        (((ArrivalCarRepairState)stateMask) & GetStateBit(action)) != 0;

    public static bool IsRequiredRepairComplete(int stateMask) =>
        (((ArrivalCarRepairState)stateMask) & ArrivalCarRepairState.RequiredComplete) ==
        ArrivalCarRepairState.RequiredComplete;

    public static bool ConsumesInstalledPart(ArrivalCarRepairAction action) =>
        action == ArrivalCarRepairAction.AddFuel ||
        action == ArrivalCarRepairAction.ReplaceBattery ||
        action == ArrivalCarRepairAction.ReplaceTire;

    public static float GetRepairDurationSeconds(ArrivalCarRepairAction action) => action switch
    {
        ArrivalCarRepairAction.AddFuel => FuelRepairDurationSeconds,
        ArrivalCarRepairAction.ReplaceTire => TireRepairDurationSeconds,
        // The engine/hood and battery are both inside the engine bay and use
        // the supplied hammer recording.
        _ => HammerRepairDurationSeconds
    };
}
