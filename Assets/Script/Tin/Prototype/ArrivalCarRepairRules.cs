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
}
