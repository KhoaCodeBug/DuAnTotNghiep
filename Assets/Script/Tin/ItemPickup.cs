using UnityEngine;
using Fusion; // Bắt buộc dùng mạng

[RequireComponent(typeof(Collider2D))]
public class ItemPickup : NetworkBehaviour // 🔥 ĐÃ ĐỔI sang NetworkBehaviour
{
    public ItemData item;
    public int amount = 1;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            InventorySystem inventory = collision.GetComponent<InventorySystem>();

            // 🔥 ĐÃ FIX: CHỈ CHO PHÉP MÁY ĐANG ĐIỀU KHIỂN NHÂN VẬT ĐÓ ĐƯỢC QUYỀN LỤM ĐỒ
            if (inventory != null && inventory.HasInputAuthority)
            {
                bool pickedUp = inventory.AddItem(item, amount);

                if (pickedUp)
                {
                    Debug.Log("Đã lụm: " + item.itemName);

                    // Xin Server xóa cục đồ này trên mọi máy tính
                    if (Object != null && Object.IsValid)
                        RPC_RequestDespawn();
                    else
                        Destroy(gameObject); // Dự phòng cho đồ offline chưa gắn NetworkObject
                }
                else
                {
                    Debug.Log("Túi đã đầy, không thể lụm thêm " + item.itemName);
                }
            }
        }
    }

    // Lệnh xin Server xóa đồ để mọi người cùng thấy nó biến mất
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestDespawn()
    {
        if (Object != null && Object.IsValid)
            Runner.Despawn(Object);
    }
}