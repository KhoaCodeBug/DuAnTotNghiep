using Fusion;
using UnityEngine;

// Tự động gắn kèm script nhặt đồ cũ của bạn
[RequireComponent(typeof(ItemPickup))]
public class NetworkDropItem : NetworkBehaviour
{
    // Biến mạng: Giữ tên File của món đồ và số lượng
    [Networked] public NetworkString<_64> NetItemName { get; set; }
    [Networked] public int NetAmount { get; set; }

    public override void Spawned()
    {
        // 1. Lấy tên file từ trên mạng đưa về
        string fileName = NetItemName.ToString();

        // 2. Lục lọi trong thư mục Resources/Items/ để tìm file ItemData tương ứng
        ItemData loadedItem = Resources.Load<ItemData>("Items/" + fileName);

        if (loadedItem != null)
        {
            // 3. Bơm dữ liệu vào script ItemPickup để ai đi ngang qua cũng nhặt được
            ItemPickup pickup = GetComponent<ItemPickup>();
            pickup.item = loadedItem;
            pickup.amount = NetAmount;

            // 4. Thay quần áo (đổi hình ảnh Sprite rớt dưới đất)
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = loadedItem.icon;
        }
        else
        {
            Debug.LogError("CẢNH BÁO: Không tìm thấy file đồ nào tên là [" + fileName + "] trong thư mục Resources/Items!");
        }
    }
}