using Fusion;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))] // Bắt buộc có Collider để chuột click trúng
public class LootContainer : NetworkBehaviour
{
    [Header("Cài đặt Tủ Đồ")]
    [Tooltip("Khoảng cách tối đa để mở tủ")]
    public float interactDistance = 1.5f;

    [Header("Danh sách đồ trong tủ (Host quản lý)")]
    public List<InventorySlot> itemsInContainer = new List<InventorySlot>();

    private void Update()
    {
        // 1. Kiểm tra Click chuột trái
        if (Input.GetMouseButtonDown(0))
        {
            // Tránh click xuyên qua UI (Đang bấm UI tự nhiên trúng cái tủ sau lưng)
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            // 2. Bắn tia Raycast từ chuột xuống thế giới 2D
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hitCol = Physics2D.OverlapPoint(mousePos);

            // 3. Nếu chuột click TRÚNG CÁI TỦ NÀY
            if (hitCol != null && hitCol.gameObject == this.gameObject)
            {
                PlayerMovement localPlayer = GetLocalPlayer();
                if (localPlayer != null)
                {
                    // 4. KIỂM TRA KHOẢNG CÁCH
                    float dist = Vector2.Distance(localPlayer.transform.position, transform.position);
                    if (dist <= interactDistance)
                    {
                        if (AutoUIManager.Instance != null)
                        {
                            AutoUIManager.Instance.OpenContainerUI(this);
                            Debug.Log("Mở tủ đồ thành công!");
                        }
                    }
                    else
                    {
                        Debug.Log("Đứng xa quá không với tới tủ đồ!");
                    }
                }
            }
        }
    }

    // =========================================================
    // 🔥 FIX QUAN TRỌNG NHẤT: Đổi RpcSources.InputAuthority thành RpcSources.All
    // Cái tủ là của chung, nên AI CŨNG CÓ QUYỀN (All) gọi lên cho Server (StateAuthority)
    // =========================================================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestTakeItem(int slotIndex, string requestedItemName, PlayerRef playerTryingToLoot)
    {
        // BƯỚC BẢO MẬT: Chống 2 người cùng lấy 1 món đồ!
        if (slotIndex < 0 || slotIndex >= itemsInContainer.Count) return;

        InventorySlot slot = itemsInContainer[slotIndex];

        // Nếu thằng A vừa lấy mất tiêu, ô này bị đôn món đồ khác lên -> Tên không khớp -> Bác bỏ!
        if (slot.item.itemName != requestedItemName)
        {
            Debug.Log("Món đồ đã bị người khác lấy trước!");
            return;
        }

        int amount = slot.amount;

        // Xóa món đó khỏi tủ đồ của Server
        itemsInContainer.RemoveAt(slotIndex);

        // Báo riêng cho cái thằng vừa click: "Lụm thành công rồi, nhét vô túi đi!"
        RPC_ConfirmLootSuccess(playerTryingToLoot, requestedItemName, amount);

        // Phóng thanh cho tất cả mọi người đang dòm cái tủ: "Cập nhật giao diện đi tụi bây!"
        RPC_UpdateContainerUIForAll();
    }

    // Lệnh này Host gọi, gửi về tất cả Client, nhưng chỉ Client nào đúng ID mới được thêm đồ vào túi
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ConfirmLootSuccess(PlayerRef targetPlayer, string itemName, int amount)
    {
        // Kiểm tra xem máy này có phải là máy của thằng được cho đồ không?
        if (Runner.LocalPlayer == targetPlayer)
        {
            ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
            InventorySystem inv = FindLocalInventory();
            if (inv != null && itemData != null)
            {
                inv.AddItem(itemData, amount);
                Debug.Log($"Lục tủ được: {amount}x {itemName}");
            }
        }
    }

    // Lệnh này bắt mọi máy tính (đang mở cái tủ này) phải vẽ lại UI
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_UpdateContainerUIForAll()
    {
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsContainerOpen(this))
        {
            AutoUIManager.Instance.RefreshContainerUI(this);
        }
    }

    // Các hàm tìm Local Player
    private PlayerMovement GetLocalPlayer()
    {
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var p in players) { if (p.HasInputAuthority) return p; }
        return null;
    }

    private InventorySystem FindLocalInventory()
    {
        var players = FindObjectsByType<InventorySystem>(FindObjectsSortMode.None);
        foreach (var p in players) { if (p.HasInputAuthority) return p; }
        return null;
    }
    // =========================================================
    // 🔥 MỚI: HÀM NHẬN ĐỒ TỪ NGƯỜI CHƠI CẤT VÀO TỦ
    // =========================================================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StoreItem(string itemName, int amount)
    {
        // 1. Tải thông tin món đồ (Phải đảm bảo file ItemData nằm trong thư mục Resources/Items)
        ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
        if (itemData == null) return;

        // 2. Kiểm tra xem trong tủ có món này chưa để cộng dồn (Stack)
        if (itemData.isStackable)
        {
            foreach (var slot in itemsInContainer)
            {
                if (slot.item.itemName == itemData.itemName && slot.amount < itemData.maxStack)
                {
                    int spaceLeft = itemData.maxStack - slot.amount;
                    if (amount <= spaceLeft)
                    {
                        slot.amount += amount;
                        RPC_UpdateContainerUIForAll();
                        return; // Xong việc
                    }
                    else
                    {
                        slot.amount += spaceLeft;
                        amount -= spaceLeft;
                    }
                }
            }
        }

        // 3. Nếu tủ chưa có món này, hoặc các stack cũ đã đầy -> Tạo ô mới
        while (amount > 0 && itemsInContainer.Count < 20) // Giả sử tủ chứa tối đa 20 ô
        {
            int amountToStore = Mathf.Min(amount, itemData.maxStack);
            itemsInContainer.Add(new InventorySlot(itemData, amountToStore));
            amount -= amountToStore;
        }

        // 4. Báo mọi người đang dòm tủ cập nhật lại hình ảnh
        RPC_UpdateContainerUIForAll();
    }
}