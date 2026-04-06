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
    // 🔥 1. LẤY ĐỒ TỪ TỦ BỎ VÀO BALO (DRAG HOẶC CLICK)
    // =========================================================

    // Yêu cầu lấy đồ gửi từ bất kỳ ai lên Server
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

        // Phóng thanh cho tất cả Client: "Đồng bộ lại danh sách tủ đồ đi tụi bây!"
        RPC_SyncRemoveItem(slotIndex);
    }

    // Server cho phép thêm đồ vào Balo của người chơi
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ConfirmLootSuccess(PlayerRef targetPlayer, string itemName, int amount)
    {
        // Chỉ thằng nào lấy mới được thêm đồ
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

    // Đồng bộ thao tác xóa đồ trên tất cả máy Client
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncRemoveItem(int slotIndex)
    {
        // Client tự xóa đồ để khớp với Server
        if (!HasStateAuthority)
        {
            if (slotIndex >= 0 && slotIndex < itemsInContainer.Count)
            {
                itemsInContainer.RemoveAt(slotIndex);
            }
        }

        // Vẽ lại giao diện
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsContainerOpen(this))
        {
            AutoUIManager.Instance.RefreshContainerUI(this);
        }
    }

    // =========================================================
    // 🔥 2. CẤT ĐỒ TỪ BALO VÀO TỦ (DRAG HOẶC CLICK)
    // =========================================================

    // Gửi yêu cầu cất đồ lên Server
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StoreItem(string itemName, int amount)
    {
        ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
        if (itemData == null) return;

        // Xử lý logic gộp đồ (Stack) trên Server
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
                        RPC_SyncAddItem(itemName, amount); // Báo Client cập nhật
                        return;
                    }
                    else
                    {
                        slot.amount += spaceLeft;
                        amount -= spaceLeft;
                    }
                }
            }
        }

        // Nếu còn dư hoặc đồ không stack được, tạo ô mới
        while (amount > 0 && itemsInContainer.Count < 20) // Giả sử tủ chứa tối đa 20 ô
        {
            int amountToStore = Mathf.Min(amount, itemData.maxStack);
            itemsInContainer.Add(new InventorySlot(itemData, amountToStore));
            amount -= amountToStore;
        }

        RPC_SyncAddItem(itemName, amount); // Báo Client cập nhật
    }

    // Đồng bộ thao tác thêm đồ trên tất cả máy Client
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncAddItem(string itemName, int amount)
    {
        // Client làm động tác thêm đồ Y CHANG Server để đồng bộ danh sách
        if (!HasStateAuthority)
        {
            ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
            if (itemData != null)
            {
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
                                amount = 0;
                                break;
                            }
                            else
                            {
                                slot.amount += spaceLeft;
                                amount -= spaceLeft;
                            }
                        }
                    }
                }

                while (amount > 0 && itemsInContainer.Count < 20)
                {
                    int amountToStore = Mathf.Min(amount, itemData.maxStack);
                    itemsInContainer.Add(new InventorySlot(itemData, amountToStore));
                    amount -= amountToStore;
                }
            }
        }

        // Vẽ lại giao diện sau khi đồng bộ
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsContainerOpen(this))
        {
            AutoUIManager.Instance.RefreshContainerUI(this);
        }
    }

    // =========================================================
    // Các hàm tìm Local Player (Đã tinh chỉnh chống lỗi)
    // =========================================================
    private PlayerMovement GetLocalPlayer()
    {
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object != null && p.HasInputAuthority) return p;
        }
        return null;
    }

    private InventorySystem FindLocalInventory()
    {
        var inventories = FindObjectsByType<InventorySystem>(FindObjectsSortMode.None);
        foreach (var inv in inventories)
        {
            if (inv.Object != null && inv.HasInputAuthority) return inv;
        }
        return null;
    }
}