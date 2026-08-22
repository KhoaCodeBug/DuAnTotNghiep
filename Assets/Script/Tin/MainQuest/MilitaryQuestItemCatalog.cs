using System;
using System.Collections.Generic;
using UnityEngine;

public enum MilitaryQuestItemKind
{
    ArmoryKey,
    Battery,
    FuelCanister,
    RepairKit,
    LevelThreeBackpack
}

/// <summary>
/// Runtime quest-item definitions. This follows the existing route-clue catalog
/// pattern so InventorySystem RPCs can resolve the same identifiers on Host and clients.
/// </summary>
public static class MilitaryQuestItemCatalog
{
    public const string ArmoryKeyId = "MilitaryArmoryKey";
    public const string BatteryId = "MilitaryBattery";
    public const string FuelCanisterId = "FuelCanister";
    public const string RepairKitId = "MilitaryRepairKit";
    public const string LevelThreeBackpackId = "MilitaryBackpackLevel3";

    private static readonly Dictionary<MilitaryQuestItemKind, ItemData> Items = new();

    public static ItemData GetOrCreate(MilitaryQuestItemKind kind)
    {
        if (Items.TryGetValue(kind, out ItemData existing) && existing != null) return existing;

        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.name = GetId(kind);
        item.itemName = GetDisplayName(kind);
        item.category = kind == MilitaryQuestItemKind.LevelThreeBackpack
            ? ItemCategory.Backpack
            : ItemCategory.QuestItem;
        item.isStackable = false;
        item.maxStack = 1;
        item.backpackLevel = kind == MilitaryQuestItemKind.LevelThreeBackpack ? 3 : 1;
        item.backpackSlotsBonus = kind == MilitaryQuestItemKind.LevelThreeBackpack ? 15 : 5;
        item.icon = CreateIcon(kind);
        item.hideFlags = HideFlags.DontSave;
        Items[kind] = item;
        return item;
    }

    public static bool TryLoad(string identifier, out ItemData item)
    {
        item = null;
        if (!TryGetKind(identifier, out MilitaryQuestItemKind kind)) return false;
        item = GetOrCreate(kind);
        return true;
    }

    public static bool TryGetKind(string identifier, out MilitaryQuestItemKind kind)
    {
        foreach (MilitaryQuestItemKind candidate in Enum.GetValues(typeof(MilitaryQuestItemKind)))
        {
            if (string.Equals(identifier, GetId(candidate), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identifier, GetDisplayName(candidate), StringComparison.OrdinalIgnoreCase))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    public static string GetId(MilitaryQuestItemKind kind) => kind switch
    {
        MilitaryQuestItemKind.ArmoryKey => ArmoryKeyId,
        MilitaryQuestItemKind.Battery => BatteryId,
        MilitaryQuestItemKind.FuelCanister => FuelCanisterId,
        MilitaryQuestItemKind.RepairKit => RepairKitId,
        _ => LevelThreeBackpackId
    };

    public static string GetDisplayName(MilitaryQuestItemKind kind) => kind switch
    {
        MilitaryQuestItemKind.ArmoryKey => "Chìa khóa kho quân nhu",
        MilitaryQuestItemKind.Battery => "Ắc quy quân sự",
        MilitaryQuestItemKind.FuelCanister => "Can nhiên liệu",
        MilitaryQuestItemKind.RepairKit => "Bộ sửa chữa quân sự",
        _ => "Balo quân sự cấp 3"
    };

    public static string GetLocalizedDisplayName(MilitaryQuestItemKind kind) =>
        GameLocalization.IsVietnamese ? GetDisplayName(kind) : kind switch
        {
            MilitaryQuestItemKind.ArmoryKey => "Armory key",
            MilitaryQuestItemKind.Battery => "Military battery",
            MilitaryQuestItemKind.FuelCanister => "Fuel canister",
            MilitaryQuestItemKind.RepairKit => "Military repair kit",
            _ => "Level-3 military backpack"
        };

    private static Sprite CreateIcon(MilitaryQuestItemKind kind)
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = GetId(kind) + "_ICON",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 dark = new Color32(30, 38, 40, 255);
        Color32 accent = kind switch
        {
            MilitaryQuestItemKind.ArmoryKey => new Color32(245, 190, 45, 255),
            MilitaryQuestItemKind.Battery => new Color32(90, 210, 235, 255),
            MilitaryQuestItemKind.FuelCanister => new Color32(216, 65, 51, 255),
            MilitaryQuestItemKind.RepairKit => new Color32(235, 235, 220, 255),
            _ => new Color32(84, 143, 75, 255)
        };
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        for (int y = 6; y < 27; y++)
        for (int x = 5; x < 27; x++)
            pixels[y * size + x] = x == 5 || x == 26 || y == 6 || y == 26 ? dark : accent;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
