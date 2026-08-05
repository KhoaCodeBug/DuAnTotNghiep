using UnityEngine;

public enum ItemCategory { Ammunition, Medical, Consumable, Weapon, Backpack }

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