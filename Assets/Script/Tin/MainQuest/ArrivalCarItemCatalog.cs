using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stable runtime item definitions for Route A car preparation. They are
/// intentionally separate from the military escape-vehicle items.
/// </summary>
public static class ArrivalCarItemCatalog
{
    public const string ToolboxId = "ArrivalCarToolbox";
    public const string HammerId = "ArrivalCarHammer";
    public const string FuelCanId = "ArrivalCarFuelCan";
    public const string BatteryId = "ArrivalCarBattery";
    public const string TireId = "ArrivalCarTire";

    private static readonly Dictionary<ArrivalCarItemKind, ItemData> Items = new();

    public static ItemData GetOrCreate(ArrivalCarItemKind kind)
    {
        if (Items.TryGetValue(kind, out ItemData existing) && existing != null) return existing;

        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.name = GetId(kind);
        item.itemName = GetDisplayName(kind);
        item.category = ItemCategory.QuestItem;
        item.isStackable = false;
        item.maxStack = 1;
        item.icon = CreateIcon(kind);
        item.hideFlags = HideFlags.DontSave;
        Items[kind] = item;
        return item;
    }

    public static bool TryLoad(string identifier, out ItemData item)
    {
        item = null;
        if (!TryGetKind(identifier, out ArrivalCarItemKind kind)) return false;
        item = GetOrCreate(kind);
        return true;
    }

    public static bool TryGetKind(string identifier, out ArrivalCarItemKind kind)
    {
        foreach (ArrivalCarItemKind candidate in Enum.GetValues(typeof(ArrivalCarItemKind)))
        {
            if (Matches(candidate, identifier))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    public static bool TryGetKind(ItemData item, out ArrivalCarItemKind kind)
    {
        if (item != null && (TryGetKind(item.name, out kind) || TryGetKind(item.itemName, out kind)))
            return true;
        kind = default;
        return false;
    }

    public static string GetId(ArrivalCarItemKind kind) => kind switch
    {
        ArrivalCarItemKind.Toolbox => ToolboxId,
        ArrivalCarItemKind.Hammer => HammerId,
        ArrivalCarItemKind.FuelCan => FuelCanId,
        ArrivalCarItemKind.Battery => BatteryId,
        _ => TireId
    };

    public static string GetDisplayName(ArrivalCarItemKind kind) => kind switch
    {
        ArrivalCarItemKind.Toolbox => "Bộ dụng cụ sửa xe",
        ArrivalCarItemKind.Hammer => "Búa sửa chữa",
        ArrivalCarItemKind.FuelCan => "Can nhiên liệu",
        ArrivalCarItemKind.Battery => "Ắc quy xe",
        _ => "Lốp xe"
    };

    public static ArrivalCarItemKind[] GetRequiredItems(ArrivalCarRepairAction action) => action switch
    {
        ArrivalCarRepairAction.RepairCore => new[] { ArrivalCarItemKind.Toolbox, ArrivalCarItemKind.Hammer },
        ArrivalCarRepairAction.AddFuel => new[] { ArrivalCarItemKind.FuelCan },
        ArrivalCarRepairAction.ReplaceBattery => new[] { ArrivalCarItemKind.Battery },
        ArrivalCarRepairAction.ReplaceTire => new[] { ArrivalCarItemKind.Tire },
        _ => Array.Empty<ArrivalCarItemKind>()
    };

    private static bool Matches(ArrivalCarItemKind kind, string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return false;
        if (string.Equals(identifier, GetId(kind), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(identifier, GetDisplayName(kind), StringComparison.OrdinalIgnoreCase))
            return true;

        return kind switch
        {
            ArrivalCarItemKind.Toolbox => EqualsAny(identifier, "Toolbox", "Bộ dụng cụ"),
            ArrivalCarItemKind.Hammer => EqualsAny(identifier, "Hammer", "Búa"),
            ArrivalCarItemKind.FuelCan => EqualsAny(identifier, "FuelCanister", "Can nhiên liệu"),
            ArrivalCarItemKind.Battery => EqualsAny(identifier, "CarBattery", "Ắc quy"),
            ArrivalCarItemKind.Tire => EqualsAny(identifier, "CarTire", "Lốp"),
            _ => false
        };
    }

    private static bool EqualsAny(string value, params string[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
            if (string.Equals(value, candidates[i], StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static Sprite CreateIcon(ArrivalCarItemKind kind)
    {
        string resourcePath = kind switch
        {
            ArrivalCarItemKind.Toolbox => "Story/CarUI/Toolbox",
            ArrivalCarItemKind.Hammer => "Story/CarUI/Hammer",
            ArrivalCarItemKind.FuelCan => "Story/CarUI/GasCan",
            ArrivalCarItemKind.Battery => "Story/CarUI/CarBattery",
            _ => "Story/CarUI/CarTire"
        };
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null) return null;
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), Mathf.Max(texture.width, texture.height));
        sprite.name = GetId(kind) + "_ICON";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
