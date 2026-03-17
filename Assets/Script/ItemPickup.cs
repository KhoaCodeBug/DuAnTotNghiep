using UnityEngine;

[RequireComponent(typeof(Collider2D))] // Bắt buộc phải có Collider để xét va chạm
public class ItemPickup : MonoBehaviour
{
    public ItemData item;   // Món đồ chứa bên trong
    public int amount = 1;  // Số lượng

    // Hàm này tự động chạy khi có 1 Collider khác chạm vào cục đồ này
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Kiểm tra xem vật thể chạm vào có Tag là "Player" không
        if (collision.CompareTag("Player"))
        {
            // Lấy túi đồ của Player
            InventorySystem inventory = collision.GetComponent<InventorySystem>();

            if (inventory != null)
            {
                // Gọi hàm nhặt đồ. Hàm AddItem sẽ trả về TRUE nếu nhặt được, FALSE nếu túi đầy
                bool pickedUp = inventory.AddItem(item, amount);

                // Nếu túi CÒN CHỖ và lụm thành công -> Xóa cục đồ trên mặt đất đi
                if (pickedUp)
                {
                    Debug.Log("Đã lụm: " + item.itemName);
                    Destroy(gameObject);
                }
                else
                {
                    Debug.Log("Túi đã đầy, không thể lụm thêm " + item.itemName);
                }
            }
        }
    }
}