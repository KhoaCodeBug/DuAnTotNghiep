using UnityEngine;

public enum ItemCategory { Ammunition, Medical, Consumable }

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public ItemCategory category;

    [Header("Cài đặt Cộng dồn (Stacking)")]
    public bool isStackable = true;
    public int maxStack = 30; // Ví dụ: 30 viên đạn 1 ô

    [Header("Chỉ số Tác dụng")]
    public float healAmount;     // Lượng máu hồi
    public float staminaRegen;   // Lượng thể lực hồi
    public float useTime;        // Thời gian chờ khi sử dụng (tạo độ khó hardcore)
}