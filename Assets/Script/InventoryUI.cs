using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; } // Lại dùng Singleton cho dễ gọi

    public Transform gridContainer; // Kéo GridContainer vào đây
    public GameObject slotPrefab;   // Kéo Prefab Slot từ dưới Project lên đây

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // Hàm này sẽ vẽ lại toàn bộ Ba lô
    public void RefreshUI(InventorySystem inventory)
    {
        // 1. Xóa sạch các ô cũ đang hiển thị trên màn hình
        foreach (Transform child in gridContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. Duyệt qua túi đồ của Player, có bao nhiêu món thì đẻ ra bấy nhiêu ô UI
        foreach (InventorySlot slot in inventory.slots)
        {
            // Tạo ra 1 ô Prefab làm con của GridContainer
            GameObject slotGO = Instantiate(slotPrefab, gridContainer);

            // Lấy script UI và đẩy dữ liệu vào để nó tự đổi hình ảnh/số lượng
            InventorySlotUI slotUI = slotGO.GetComponent<InventorySlotUI>();
            if (slotUI != null)
            {
                slotUI.UpdateSlot(slot);
            }
        }
    }
}