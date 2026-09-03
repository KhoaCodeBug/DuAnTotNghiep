using UnityEngine;
using Fusion; // Vẫn cần Fusion để kiểm tra NetworkObject

[RequireComponent(typeof(Collider2D))]
public class ItemPickup : MonoBehaviour // 🔥 ĐÃ ĐỔI: Trở lại làm MonoBehaviour bình thường
{
    public ItemData item;
    public int amount = 1;
    [Range(FlashlightController.MinimumLootBattery01, 1f)]
    public float flashlightBattery01 = 1f;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            InventorySystem inventory = collision.GetComponent<InventorySystem>();

            // 🔥 ĐÃ FIX: Chỉ máy tính của người chơi đó mới được quyền lụm (Chặn Host lụm giùm)
            if (inventory != null && inventory.HasInputAuthority)
            {
                NetworkObject netObj = GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsValid)
                {
                    // Networked loot is granted and despawned atomically by State
                    // Authority. Do not add it optimistically on the client.
                    inventory.RPC_RequestPickupItem(netObj);

                    enabled = false;
                    Collider2D networkCol = GetComponent<Collider2D>();
                    if (networkCol != null) networkCol.enabled = false;
                    SpriteRenderer networkRenderer = GetComponent<SpriteRenderer>();
                    if (networkRenderer != null) networkRenderer.enabled = false;
                    return;
                }

                bool pickedUp = inventory.AddItem(item, amount, flashlightBattery01);

                if (pickedUp)
                {
                    Debug.Log("Đã lụm: " + item.itemName);

                    // 🔥 PHÁT ÂM THANH LỤM VẬT PHẨM (item_pickup.wav)
                    AudioClip pickupSFX = Resources.Load<AudioClip>("Sound/Actions/item_pickup");
                    if (pickupSFX != null)
                    {
                        if (AutoUIManager.Instance != null) AutoUIManager.Instance.PlayItemPickupSound();
                    }

                    // 1. Tắt hình ảnh và va chạm ngay lập tức trên máy mình để tạo cảm giác mượt mà (Không bị lag delay)
                    Collider2D col = GetComponent<Collider2D>();
                    if (col != null) col.enabled = false;
                    SpriteRenderer sr = GetComponent<SpriteRenderer>();
                    if (sr != null) sr.enabled = false;

                    // Đồ đặt trực tiếp trong scene không có NetworkObject.
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
