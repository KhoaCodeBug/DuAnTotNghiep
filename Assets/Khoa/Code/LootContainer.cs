using Fusion;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class LootContainer : NetworkBehaviour
{
    [Header("Cài đặt Tủ Đồ")]
    [Tooltip("Khoảng cách tối đa để mở tủ - Đã tăng lên để dễ tương tác hơn")]
    public float interactDistance = 2.5f;

    [Header("Hệ Thống Random Đồ (Chỉ Host xử lý)")]
    [Tooltip("Kéo file Loot Table (ScriptableObject) vào đây")]
    public LootTableSO lootTable;

    [Header("Danh sách đồ hiện tại (Realtime)")]
    public List<InventorySlot> itemsInContainer = new List<InventorySlot>();

    // Biến đánh dấu Host đã random đồ xong chưa (tránh random lại nếu Host restart)
    private bool hasGeneratedLoot = false;

    // Cache lại player để đỡ phải FindObjectsByType liên tục gây giật lag
    private PlayerMovement cachedLocalPlayer;
    private InventorySystem cachedLocalInventory;

    [System.Serializable]
    public class LootSpawnData
    {
        public ItemData itemPrefab;
        [Range(0f, 100f)]
        [Tooltip("Tỉ lệ % xuất hiện của món đồ này")]
        public float dropChance = 30f;
        public int minAmount = 1;
        public int maxAmount = 1;
    }

    // Hàm này của Fusion tự động gọi khi Object được sinh ra trên mạng
    public override void Spawned()
    {
        // CHỈ CÓ HOST MỚI ĐƯỢC QUYỀN TẠO ĐỒ RANDOM
        if (HasStateAuthority && !hasGeneratedLoot)
        {
            GenerateRandomLoot();
        }
    }

    private void GenerateRandomLoot()
    {
        // Nếu tủ không được gán bảng Loot Table nào thì bỏ qua
        if (lootTable == null) return;

        itemsInContainer.Clear();

        // Lấy danh sách quay số từ Loot Table
        foreach (var lootRule in lootTable.lootRules)
        {
            if (lootRule.itemPrefab == null) continue;

            // Quay số từ 0 -> 100
            float roll = Random.Range(0f, 100f);

            // Nếu trúng tỉ lệ %
            if (roll <= lootRule.dropChance)
            {
                int spawnAmount = Random.Range(lootRule.minAmount, lootRule.maxAmount + 1);

                // Gom chung logic cất đồ vào 1 chỗ để tận dụng tính năng gộp Stack
                StoreItemLocal(lootRule.itemPrefab, spawnAmount);
            }
        }

        hasGeneratedLoot = true;
    }

    // Logic cất đồ xài chung cho lúc Random và lúc Player cất vào
    private void StoreItemLocal(ItemData itemData, int amount)
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

        while (amount > 0 && itemsInContainer.Count < 20)
        {
            int amountToStore = Mathf.Min(amount, itemData.maxStack);
            itemsInContainer.Add(new InventorySlot(itemData, amountToStore));
            amount -= amountToStore;
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hitCol = Physics2D.OverlapPoint(mousePos);

            if (hitCol != null && hitCol.gameObject == this.gameObject)
            {
                PlayerMovement localPlayer = GetLocalPlayerCached();
                if (localPlayer != null)
                {
                    float dist = Vector2.Distance(localPlayer.transform.position, transform.position);
                    if (dist <= interactDistance)
                    {
                        if (AutoUIManager.Instance != null)
                        {
                            // Trước khi mở UI, yêu cầu Server gửi danh sách mới nhất về cho chắc ăn
                            RPC_RequestSyncContainerStatus();
                            AutoUIManager.Instance.OpenContainerUI(this);
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
    // ĐỒNG BỘ TRƯỚC KHI MỞ TỦ (FIX LỖI OUTMETA)
    // =========================================================
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSyncContainerStatus()
    {
        // Client xin data -> Host gửi lại danh sách hiện tại cho tất cả
        foreach (var slot in itemsInContainer)
        {
            RPC_SyncAddItem(slot.item.itemName, slot.amount, true);
        }
    }

    // =========================================================
    // 🔥 1. LẤY ĐỒ TỪ TỦ BỎ VÀO BALO
    // =========================================================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestTakeItem(int slotIndex, string requestedItemName, PlayerRef playerTryingToLoot)
    {
        if (slotIndex < 0 || slotIndex >= itemsInContainer.Count) return;

        InventorySlot slot = itemsInContainer[slotIndex];

        if (slot.item.itemName != requestedItemName) return;

        int amount = slot.amount;
        itemsInContainer.RemoveAt(slotIndex);

        RPC_ConfirmLootSuccess(playerTryingToLoot, requestedItemName, amount);
        RPC_SyncRemoveItem(slotIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ConfirmLootSuccess(PlayerRef targetPlayer, string itemName, int amount)
    {
        if (Runner.LocalPlayer == targetPlayer)
        {
            ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
            InventorySystem inv = GetLocalInventoryCached();
            if (inv != null && itemData != null)
            {
                inv.AddItem(itemData, amount);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncRemoveItem(int slotIndex)
    {
        if (!HasStateAuthority)
        {
            if (slotIndex >= 0 && slotIndex < itemsInContainer.Count)
            {
                itemsInContainer.RemoveAt(slotIndex);
            }
        }

        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsContainerOpen(this))
        {
            AutoUIManager.Instance.RefreshContainerUI(this);
        }
    }

    // =========================================================
    // 🔥 2. CẤT ĐỒ TỪ BALO VÀO TỦ
    // =========================================================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StoreItem(string itemName, int amount)
    {
        ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
        if (itemData == null) return;

        StoreItemLocal(itemData, amount); // Gọi hàm local trên server
        RPC_SyncAddItem(itemName, amount, false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncAddItem(string itemName, int amount, bool isFullSync)
    {
        if (!HasStateAuthority)
        {
            if (isFullSync) itemsInContainer.Clear(); // Nếu là lệnh đồng bộ toàn bộ thì clear mảng cũ

            ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
            if (itemData != null)
            {
                StoreItemLocal(itemData, amount); // Tái sử dụng logic thêm đồ
            }
        }

        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsContainerOpen(this))
        {
            AutoUIManager.Instance.RefreshContainerUI(this);
        }
    }

    // =========================================================
    // CÁC HÀM TỐI ƯU HIỆU NĂNG (CACHE)
    // =========================================================
    private PlayerMovement GetLocalPlayerCached()
    {
        if (cachedLocalPlayer != null) return cachedLocalPlayer;

        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.Object != null && p.HasInputAuthority)
            {
                cachedLocalPlayer = p;
                return p;
            }
        }
        return null;
    }

    private InventorySystem GetLocalInventoryCached()
    {
        if (cachedLocalInventory != null) return cachedLocalInventory;

        var inventories = FindObjectsByType<InventorySystem>(FindObjectsSortMode.None);
        foreach (var inv in inventories)
        {
            if (inv.Object != null && inv.HasInputAuthority)
            {
                cachedLocalInventory = inv;
                return inv;
            }
        }
        return null;
    }
}