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
/// Pure progress model for the complete pre-military quest demo.
/// Main-quest clues always reveal an approximate office search area, while the
/// optional route clues reward Map Fragment 1 and the exact office position.
/// </summary>
public sealed class PreMilitaryQuestProgress
{
    public const int RequiredDistinctHouses = 3;
    public const int RequiredRouteClues = 3;

    private readonly HashSet<string> searchedHouseIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> routeClueIds = new HashSet<string>(StringComparer.Ordinal);

    public int SearchedHouseCount => searchedHouseIds.Count;
    public int RouteClueCount => routeClueIds.Count;
    public bool HouseSearchComplete => SearchedHouseCount >= RequiredDistinctHouses;
    public bool MainOfficeClueFound { get; private set; }
    public bool ApproximateOfficeAreaRevealed => HouseSearchComplete && MainOfficeClueFound;
    public bool HasMapFragment1 { get; private set; }
    public bool OfficeDiscovered { get; private set; }
    public bool SideQuestSkipped => OfficeDiscovered && !HasMapFragment1;
    public bool SideQuestResolved => HasMapFragment1 || OfficeDiscovered;
    public bool OfficeInvestigationComplete { get; private set; }
    public bool HasMapFragment2 { get; private set; }
    public bool MainQuestComplete => HasMapFragment2;

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

    /// <summary>
    /// A house counts after at least one LootContainer inside it is opened.
    /// The third distinct house guarantees the main office clue, preventing RNG stalls.
    /// </summary>
    public bool RegisterLootContainerOpenedInHouse(string houseId)
    {
        if (HouseSearchComplete || string.IsNullOrWhiteSpace(houseId))
            return false;

        bool added = searchedHouseIds.Add(houseId);
        if (added && HouseSearchComplete)
            MainOfficeClueFound = true;

        return added;
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
