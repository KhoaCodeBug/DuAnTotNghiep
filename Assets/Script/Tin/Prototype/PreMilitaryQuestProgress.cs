using System;
using System.Collections.Generic;

public enum OfficeKnowledgeLevel
{
    NeighborhoodOnly,
    ApproximateArea,
    ExactLocation,
    Discovered
}

/// <summary>
/// Presentation-safe mirror of the authoritative pre-military stage. It lives
/// in the QuestUI assembly so the journal does not create a circular assembly
/// reference back to MainQuestManager in Assembly-CSharp.
/// </summary>
public enum PreMilitaryQuestStage
{
    NotStarted,
    SearchNeighborhood,
    LocateOffice,
    FindCityMap,
    CityMapFound
}

/// <summary>
/// Presentation-safe mirror of MilitaryBaseQuestManager.Phase.  The Quest UI
/// assembly cannot reference Assembly-CSharp directly, so the runtime bridge
/// sends the phase as primitives and the journal keeps this matching enum.
/// </summary>
public enum RouteBMilitaryPresentationPhase
{
    NotReached,
    Investigating,
    SiegeAndRepair,
    ReadyToEscape,
    Escaped,
    Failed
}

/// <summary>
/// Pure progress model for the complete pre-military quest demo.
/// The opening objective has one source of truth: three physical route-clue
/// documents taken from residential LootContainers.
/// </summary>
public sealed class PreMilitaryQuestProgress
{
    public const int RequiredDistinctHouses = 3;
    public const int RequiredRouteClues = 3;
    public const int MaximumSearchHouses = 6;

    private readonly HashSet<string> routeClueIds = new HashSet<string>(StringComparer.Ordinal);

    public int SearchedHouseCount => 0;
    public int RouteClueCount => routeClueIds.Count;
    public bool HouseSearchComplete => false;
    public bool MainOfficeClueFound { get; private set; }
    public bool ApproximateOfficeAreaRevealed => false;
    public bool HasMapFragment1 { get; private set; }
    public bool OfficeDiscovered { get; private set; }
    public bool SideQuestSkipped => OfficeDiscovered && !HasMapFragment1;
    public bool SideQuestResolved => HasMapFragment1 || OfficeDiscovered;
    public bool OfficeInvestigationComplete { get; private set; }
    public bool HasMapFragment2 { get; private set; }
    public bool MainQuestComplete => HasMapFragment2;
    public bool ArrivalCarRepairUnlocked { get; private set; }
    public bool ArrivalCarRepaired { get; private set; }
    public int ArrivalCarRepairMask { get; private set; }

    /// <summary>
    /// Rebuilds the presentation model from Fusion's authoritative bitmasks.
    /// Synthetic keys are sufficient here because the journal only needs
    /// distinct counts; the actual stable house IDs remain in MainQuestManager.
    /// </summary>
    public void ApplyAuthoritativeSnapshot(int searchedHouseMask, int routeClueMask,
        bool officeDiscovered, bool officeInvestigationComplete, bool hasMapFragment2,
        bool arrivalCarRepairUnlocked = false, bool arrivalCarRepaired = false,
        int arrivalCarRepairMask = 0)
    {
        routeClueIds.Clear();
        _ = searchedHouseMask; // Legacy replicated field; no longer quest progress.
        for (int i = 0; i < RequiredRouteClues; i++)
        {
            if ((routeClueMask & (1 << i)) != 0)
                routeClueIds.Add("NETWORK_CLUE_" + i);
        }

        HasMapFragment1 = RouteClueCount >= RequiredRouteClues;
        MainOfficeClueFound = HasMapFragment1;
        OfficeDiscovered = officeDiscovered;
        OfficeInvestigationComplete = officeInvestigationComplete;
        HasMapFragment2 = hasMapFragment2;
        ArrivalCarRepairUnlocked = arrivalCarRepairUnlocked;
        ArrivalCarRepaired = arrivalCarRepaired;
        ArrivalCarRepairMask = arrivalCarRepairMask;
    }

    public OfficeKnowledgeLevel OfficeKnowledge
    {
        get
        {
            if (OfficeDiscovered) return OfficeKnowledgeLevel.Discovered;
            if (HasMapFragment1) return OfficeKnowledgeLevel.ExactLocation;
            if (ApproximateOfficeAreaRevealed) return OfficeKnowledgeLevel.ApproximateArea;
            return OfficeKnowledgeLevel.NeighborhoodOnly;
        }
    }

    /// <summary>Legacy preview hook. Opening a house no longer advances anything.</summary>
    public bool RegisterLootContainerOpenedInHouse(string houseId)
    {
        _ = houseId;
        return false;
    }

    /// <summary>
    /// Optional clues are collected in parallel while searching houses. Three
    /// unique clues automatically assemble Map Fragment 1 and reveal the exact office.
    /// </summary>
    public bool RegisterRouteClue(string clueId)
    {
        if (SideQuestResolved || string.IsNullOrWhiteSpace(clueId))
            return false;

        bool added = routeClueIds.Add(clueId);
        if (added && RouteClueCount >= RequiredRouteClues)
            HasMapFragment1 = true;

        return added;
    }

    public void RegisterOfficeDiscovered()
    {
        OfficeDiscovered = true;
    }

    /// <summary>
    /// Entering the office is not enough. Investigation completes when the
    /// designated MainQuestSearchCabinet has been searched successfully.
    /// </summary>
    public void RegisterOfficeMapCabinetOpened()
    {
        OfficeInvestigationComplete = true;
    }

    /// <summary>Call only after Map Fragment 2 has entered Quest Items.</summary>
    public void RegisterMapFragment2AddedToInventory()
    {
        HasMapFragment2 = true;
    }
}
