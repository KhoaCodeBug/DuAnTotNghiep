using UnityEngine;
using Fusion; // Vẫn cần Fusion để kiểm tra NetworkObject

[RequireComponent(typeof(Collider2D))]
public class ItemPickup : MonoBehaviour // 🔥 ĐÃ ĐỔI: Trở lại làm MonoBehaviour bình thường
{
    public ItemData item;
    public int amount = 1;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            InventorySystem inventory = collision.GetComponent<InventorySystem>();

            // 🔥 ĐÃ FIX: Chỉ máy tính của người chơi đó mới được quyền lụm (Chặn Host lụm giùm)
            if (inventory != null && inventory.HasInputAuthority)
            {
                bool pickedUp = inventory.AddItem(item, amount);

                if (pickedUp)
                {
                    Debug.Log("Đã lụm: " + item.itemName);

                    // 1. Tắt hình ảnh và va chạm ngay lập tức trên máy mình để tạo cảm giác mượt mà (Không bị lag delay)
                    Collider2D col = GetComponent<Collider2D>();
                    if (col != null) col.enabled = false;
                    SpriteRenderer sr = GetComponent<SpriteRenderer>();
                    if (sr != null) sr.enabled = false;

                    // 2. Kiểm tra xem cục đồ này có danh tính mạng hay không
                    NetworkObject netObj = GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsValid)
                    {
                        // Đồ xịn (rớt từ quái hoặc ném ra bằng mạng) -> Nhờ túi đồ gọi Server xóa
                        inventory.RPC_RequestDespawnItem(netObj);
                    }
                    else
                    {
                        // Đồ dỏm (8 món bạn đặt tay vào Scene để test) -> Tự hủy ngay trên máy
                        Destroy(gameObject);
                    }
                }
                else
                {
                    Debug.Log("Túi đã đầy, không thể lụm thêm " + item.itemName);
                }
            }
        }
    }
}