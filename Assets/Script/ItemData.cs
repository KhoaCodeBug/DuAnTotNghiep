using UnityEngine;

public enum ItemCategory { Ammunition, Medical, Consumable }

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public ItemCategory category;

    [Header("Cài đặt rớt đồ (Tùy chọn)")]
    public GameObject specificDropPrefab;

    [Header("Cài đặt Cộng dồn (Stacking)")]
    public bool isStackable = true;
    public int maxStack = 30;

    [Header("Chỉ số Tác dụng (Y tế)")]
    public float healAmount;

    [Header("Chỉ số Dinh dưỡng")]
    public float hungerRestore; // Lượng đói hồi lại
    public float thirstRestore; // Lượng khát hồi lại

    [Header("Cài đặt Buff (Cho Nhu yếu phẩm)")]
    public float buffDuration;
    public float speedMultiplier = 1.5f;
    public float maxStaminaBoost = 50f;
}