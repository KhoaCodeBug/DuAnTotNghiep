using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemCategory { Ammunition, Medical, Consumable, Weapon, Backpack, QuestItem }

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public ItemCategory category;

    [Header("Cài đặt Balo")]
    public int backpackLevel = 1; // Cấp 1 đến 5 (PUBG Style)
    public int backpackSlotsBonus = 5;

    // Chỉ số thời gian để dùng vật phẩm này (Tính bằng giây)
    [Tooltip("Ví dụ: Băng gạc = 3 giây, Nước = 1.5 giây, Đạn = 4 giây (Thời gian nạp đạn)")]
    public float useTime = 1f;

    // Cài đặt rớt đồ (Tùy chọn)
    public GameObject specificDropPrefab;

    // Cài đặt Cộng dồn (Stacking)
    public bool isStackable = true;
    public int maxStack = 30;

    // Chỉ số Tác dụng (Y tế)
    public float healAmount;

    // Chỉ số Dinh dưỡng
    public float hungerRestore;
    public float thirstRestore;

    // Cài đặt Buff (Cho Nhu yếu phẩm)
    public float buffDuration;
    public float speedMultiplier = 1.5f;
    public float maxStaminaBoost = 50f;

    // Chỉ Số Vũ Khí (Bắn Súng)
    public float weaponDamage = 34f;
    public float fireRate = 0.1f;
    public int magazineCapacity = 30;
    public int pelletCount = 1;
    public float spreadAngle = 2f;

    // Tầm Bắn & Tiếng Nổ
    public float weaponRange = 15f;
    public float shootNoiseRadius = 20f;
    public float soundVolumeMultiplier = 1f;
    public ItemData ammoTypeRequired;

    // Âm Thanh Riêng Của Vũ Khí
    public AudioClip customSingleShootSFX;
    public AudioClip customAutoShootSFX;
    public AudioClip customReloadSFX;
    public AudioClip customDryFireSFX;
}

/// <summary>
/// Single source of truth for inventory capacity.  The five hotbar slots are
/// always present; backpack upgrades affect only the storage area.
/// </summary>
public static class BackpackCapacityRules
{
    public const int HotbarSlots = 5;
    public const int BaseBackpackSlots = 15;
    public const int MaxBackpackSlots = 50;
    public const int MaxBackpackLevel = 5;
    public const int InitialTotalSlots = HotbarSlots + BaseBackpackSlots;
    public const int MaxTotalSlots = HotbarSlots + MaxBackpackSlots;

    public static int ClampLevel(int level) => Mathf.Clamp(level, 0, MaxBackpackLevel);

    public static int GetBackpackSlots(int level)
    {
        return ClampLevel(level) switch
        {
            0 => BaseBackpackSlots,
            1 => 20,
            2 => 25,
            3 => 30,
            4 => 40,
            _ => MaxBackpackSlots
        };
    }

    public static int GetTotalSlots(int level) => HotbarSlots + GetBackpackSlots(level);
    public static int GetBonusSlots(int level) => GetBackpackSlots(level) - BaseBackpackSlots;

    public static int GetLevelForBackpackSlots(int backpackSlots)
    {
        int normalized = Mathf.Clamp(backpackSlots, BaseBackpackSlots, MaxBackpackSlots);
        if (normalized >= 50) return 5;
        if (normalized >= 40) return 4;
        if (normalized >= 30) return 3;
        if (normalized >= 25) return 2;
        if (normalized >= 20) return 1;
        return 0;
    }

    public static int GetLevelForTotalSlots(int totalSlots)
    {
        return GetLevelForBackpackSlots(Mathf.Clamp(totalSlots - HotbarSlots,
            BaseBackpackSlots, MaxBackpackSlots));
    }

    public static int ClampTotalSlots(int totalSlots)
    {
        return Mathf.Clamp(totalSlots, InitialTotalSlots, MaxTotalSlots);
    }

    public static int GetStorageSlots(ItemData backpack)
    {
        if (backpack == null || backpack.category != ItemCategory.Backpack)
            return BaseBackpackSlots;

        int level = ClampLevel(backpack.backpackLevel);
        int configured = BaseBackpackSlots + Mathf.Max(0, backpack.backpackSlotsBonus);
        return Mathf.Clamp(Mathf.Max(GetBackpackSlots(level), configured),
            BaseBackpackSlots, MaxBackpackSlots);
    }

    public static int GetTotalSlots(ItemData backpack) => HotbarSlots + GetStorageSlots(backpack);
}

/// <summary>
/// Runtime definitions for all backpack tiers.  Keeping these IDs stable lets
/// loot, quest rewards, respawn snapshots and Fusion RPCs resolve the same item
/// on both Host and late-joining clients even though no asset is required.
/// </summary>
public static class BackpackItemCatalog
{
    public const string BackpackIdPrefix = "BackpackLevel";
    public const string MilitaryLevel3Id = "MilitaryBackpackLevel3";

    private static readonly Dictionary<string, ItemData> Items =
        new Dictionary<string, ItemData>(StringComparer.OrdinalIgnoreCase);

    public static ItemData GetOrCreate(int level, bool military = false)
    {
        level = Mathf.Clamp(level, 1, BackpackCapacityRules.MaxBackpackLevel);
        string id = military && level == 3 ? MilitaryLevel3Id : BackpackIdPrefix + level;
        if (Items.TryGetValue(id, out ItemData existing) && existing != null)
        {
            // If existing item was created with the 32x32 fallback solid square,
            // upgrade to authored art if available in Resources.
            if (existing.icon != null && existing.icon.rect.width <= 32)
            {
                Sprite refreshed = CreateIcon(level, military);
                if (refreshed != null && refreshed.rect.width > 32)
                {
                    existing.icon = refreshed;
                }
            }
            return existing;
        }

        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.name = id;
        item.itemName = GetDisplayName(level, military);
        item.category = ItemCategory.Backpack;
        item.isStackable = false;
        item.maxStack = 1;
        item.backpackLevel = level;
        item.backpackSlotsBonus = BackpackCapacityRules.GetBonusSlots(level);
        item.icon = CreateIcon(level, military);
        item.hideFlags = HideFlags.DontSave;
        Items[id] = item;
        // Also index the display name because containers send a stable
        // display identifier when syncing to a client.
        Items[item.itemName] = item;
        return item;
    }

    public static void ResetCache()
    {
        Items.Clear();
    }

    public static bool TryLoad(string identifier, out ItemData item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(identifier)) return false;
        if (Items.TryGetValue(identifier, out item) && item != null) return true;

        if (string.Equals(identifier, MilitaryLevel3Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(identifier, "Balo quân sự cấp 3", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(identifier, "Level-3 military backpack", StringComparison.OrdinalIgnoreCase))
        {
            item = GetOrCreate(3, true);
            return true;
        }

        for (int level = 1; level <= BackpackCapacityRules.MaxBackpackLevel; level++)
        {
            if (string.Equals(identifier, BackpackIdPrefix + level, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identifier, GetDisplayName(level, false), StringComparison.OrdinalIgnoreCase))
            {
                item = GetOrCreate(level);
                return true;
            }
        }

        return false;
    }

    public static string GetDisplayName(int level, bool military = false)
    {
        level = Mathf.Clamp(level, 1, BackpackCapacityRules.MaxBackpackLevel);
        return military && level == 3 ? "Balo quân sự cấp 3" : $"Balo dã chiến cấp {level}";
    }

    public static string GetLocalizedDisplayName(ItemData item)
    {
        if (item == null) return string.Empty;
        int level = Mathf.Clamp(item.backpackLevel, 1, BackpackCapacityRules.MaxBackpackLevel);
        bool military = string.Equals(item.name, MilitaryLevel3Id, StringComparison.OrdinalIgnoreCase);
        return GameLocalization.IsVietnamese
            ? GetDisplayName(level, military)
            : (military ? "Level-3 military backpack" : $"Level-{level} field backpack");
    }

    private static Sprite CreateIcon(int level, bool military)
    {
        // Backpack PNGs live in Resources so runtime-created catalog items use
        // the same art on Host, client and late joiner.
        string resourceName = (military && level == 3 ? MilitaryLevel3Id : BackpackIdPrefix + level);
        string resourcePath = "Backpacks/" + BackpackIdPrefix + level;

        // 1. First attempt: Load as Sprite directly (native Sprite import type)
        Sprite authoredSprite = Resources.Load<Sprite>(resourcePath);

        // 2. If imported with Multiple spriteMode (SpriteSheet), Resources.Load<Sprite>
        // returns null while Resources.LoadAll<Sprite> returns all sub-sprites.
        if (authoredSprite == null)
        {
            Sprite[] allSprites = Resources.LoadAll<Sprite>(resourcePath);
            if (allSprites != null && allSprites.Length > 0)
            {
                for (int i = 0; i < allSprites.Length; i++)
                {
                    if (allSprites[i] != null)
                    {
                        authoredSprite = allSprites[i];
                        break;
                    }
                }
            }
        }

        // 3. Fallback: Load as Texture2D and create runtime Sprite
        if (authoredSprite == null)
        {
            Texture2D authoredTexture = Resources.Load<Texture2D>(resourcePath);
            if (authoredTexture != null)
            {
                authoredSprite = Sprite.Create(authoredTexture,
                    new Rect(0f, 0f, authoredTexture.width, authoredTexture.height),
                    new Vector2(0.5f, 0.5f), Mathf.Max(authoredTexture.width, authoredTexture.height));
            }
        }

        if (authoredSprite != null)
        {
            return authoredSprite;
        }

        // 4. Generated solid-color fallback for headless tests or missing assets
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = resourceName + "_ICON",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 border = new Color32(25, 30, 32, 255);
        Color32 accent = military
            ? new Color32(84, 143, 75, 255)
            : Color.HSVToRGB(Mathf.Lerp(0.28f, 0.08f, (level - 1) / 4f), 0.7f, 0.95f);
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        for (int y = 5; y < 28; y++)
        for (int x = 4; x < 28; x++)
            pixels[y * size + x] = x == 4 || x == 27 || y == 5 || y == 27 ? border : accent;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
