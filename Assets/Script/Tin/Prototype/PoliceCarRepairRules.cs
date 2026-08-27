using System;

public enum PoliceCarRepairAction
{
    RepairEngine,
    RepairHood,
    AddFuel,
    ReplaceBattery,
    ReplaceTire
}

[Flags]
public enum PoliceCarRepairState
{
    None = 0,
    EngineRepaired = 1 << 0,
    HoodRepaired = 1 << 1,
    FuelAdded = 1 << 2,
    BatteryReplaced = 1 << 3,
    TireReplaced = 1 << 4,
    RequiredComplete = EngineRepaired | HoodRepaired | FuelAdded | BatteryReplaced | TireReplaced
}

public static class PoliceCarRepairRules
{
    public const int RequiredActionCount = 5;

    public static bool TryGetAction(string partId, out PoliceCarRepairAction action)
    {
        switch (partId)
        {
            case "engine": action = PoliceCarRepairAction.RepairEngine; return true;
            case "hood": action = PoliceCarRepairAction.RepairHood; return true;
            case "fuel": action = PoliceCarRepairAction.AddFuel; return true;
            case "battery": action = PoliceCarRepairAction.ReplaceBattery; return true;
            case "front_left": action = PoliceCarRepairAction.ReplaceTire; return true;
            default: action = default; return false;
        }
    }

    public static PoliceCarRepairState GetStateBit(PoliceCarRepairAction action) => action switch
    {
        PoliceCarRepairAction.RepairEngine => PoliceCarRepairState.EngineRepaired,
        PoliceCarRepairAction.RepairHood => PoliceCarRepairState.HoodRepaired,
        PoliceCarRepairAction.AddFuel => PoliceCarRepairState.FuelAdded,
        PoliceCarRepairAction.ReplaceBattery => PoliceCarRepairState.BatteryReplaced,
        PoliceCarRepairAction.ReplaceTire => PoliceCarRepairState.TireReplaced,
        _ => PoliceCarRepairState.None
    };

    public static ArrivalCarItemKind GetRequiredItem(PoliceCarRepairAction action) => action switch
    {
        PoliceCarRepairAction.RepairEngine => ArrivalCarItemKind.Toolbox,
        PoliceCarRepairAction.RepairHood => ArrivalCarItemKind.Hammer,
        PoliceCarRepairAction.AddFuel => ArrivalCarItemKind.FuelCan,
        PoliceCarRepairAction.ReplaceBattery => ArrivalCarItemKind.Battery,
        _ => ArrivalCarItemKind.Tire
    };

    public static bool UsesTimedArrivalCarInteraction(PoliceCarRepairAction action) =>
        action == PoliceCarRepairAction.AddFuel || action == PoliceCarRepairAction.ReplaceTire;

    public static float GetTimedInteractionDurationSeconds(PoliceCarRepairAction action) => action switch
    {
        PoliceCarRepairAction.AddFuel => ArrivalCarRepairRules.FuelRepairDurationSeconds,
        PoliceCarRepairAction.ReplaceTire => ArrivalCarRepairRules.TireRepairDurationSeconds,
        _ => 0f
    };

    public static ArrivalCarRepairAction ToArrivalCarRepairAction(PoliceCarRepairAction action) => action switch
    {
        PoliceCarRepairAction.AddFuel => ArrivalCarRepairAction.AddFuel,
        PoliceCarRepairAction.ReplaceTire => ArrivalCarRepairAction.ReplaceTire,
        PoliceCarRepairAction.ReplaceBattery => ArrivalCarRepairAction.ReplaceBattery,
        _ => ArrivalCarRepairAction.RepairCore
    };

    public static bool IsApplied(int stateMask, PoliceCarRepairAction action) =>
        (((PoliceCarRepairState)stateMask) & GetStateBit(action)) != 0;

    public static bool IsComplete(int stateMask) =>
        (((PoliceCarRepairState)stateMask) & PoliceCarRepairState.RequiredComplete) ==
        PoliceCarRepairState.RequiredComplete;

    public static int CountApplied(int stateMask)
    {
        int count = 0;
        foreach (PoliceCarRepairAction action in Enum.GetValues(typeof(PoliceCarRepairAction)))
            if (IsApplied(stateMask, action)) count++;
        return count;
    }
}
