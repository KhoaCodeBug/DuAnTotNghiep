using Fusion;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class LootContainer : NetworkBehaviour
{
    [Header("Cài đặt Tủ Đồ")]
    [Tooltip("Khoảng cách tối đa để mở tủ (Bấm vào tủ ở Scene để xem vòng tròn vàng)")]
    public float interactDistance = 2.5f;

    [Header("Chống Loot Xuyên Tường")]
    [Tooltip("Chọn layer của các bức tường hoặc vật cản (Wall)")]
    public LayerMask obstacleLayer;

    [Header("Hiệu ứng (UX)")]
    public Color highlightColor = new Color(1f, 0.8f, 0.8f, 1f);
    private Color originalColor;
    private SpriteRenderer spriteRenderer;

    [Header("Hệ Thống Random Đồ (Chỉ Host xử lý)")]
    public LootTableSO lootTable;

    [Header("Danh sách đồ hiện tại (Realtime)")]
    public List<InventorySlot> itemsInContainer = new List<InventorySlot>();

    private bool hasGeneratedLoot = false;
    private PlayerMovement cachedLocalPlayer;
    private InventorySystem cachedLocalInventory;

    [System.Serializable]
    public class LootSpawnData
    {
        public ItemData itemPrefab;
        [Range(0f, 100f)]
        public float dropChance = 30f;
        public int minAmount = 1;
        public int maxAmount = 1;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    public override void Spawned()
    {
        if (HasStateAuthority && !hasGeneratedLoot)
        {
            GenerateRandomLoot();
        }
    }

    private void GenerateRandomLoot()
    {
        if (lootTable == null) return;
        itemsInContainer.Clear();

        foreach (var lootRule in lootTable.lootRules)
        {
            if (lootRule.itemPrefab == null) continue;
            float roll = Random.Range(0f, 100f);
            if (roll <= lootRule.dropChance)
            {
                int spawnAmount = Random.Range(lootRule.minAmount, lootRule.maxAmount + 1);
                StoreItemLocal(lootRule.itemPrefab, spawnAmount);
            }
        }
        hasGeneratedLoot = true;
    }

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
        PlayerMovement localPlayer = GetLocalPlayerCached();
        if (localPlayer == null) return;

        Vector2 playerPos = localPlayer.transform.position;

        // CẢI TIẾN 1: Tính khoảng cách từ Player đến CÁI MÉP TỦ GẦN NHẤT
        Collider2D myCollider = GetComponent<Collider2D>();
        Vector2 closestPoint = myCollider.ClosestPoint(playerPos);
        float dist = Vector2.Distance(playerPos, closestPoint);

        // Kiểm tra xuyên tường
        bool isBlockedByWall = false;
        if (obstacleLayer.value != 0)
        {
            isBlockedByWall = Physics2D.Linecast(playerPos, closestPoint, obstacleLayer);
        }

        // Điều kiện gộp: Phải đủ gần mép tủ VÀ không bị tường chắn
        bool canInteract = (dist <= interactDistance) && !isBlockedByWall;

        // ĐỔI MÀU
        if (spriteRenderer != null)
        {
            spriteRenderer.color = canInteract ? highlightColor : originalColor;
        }

        // KIỂM TRA CLICK CHUỘT
        if (Input.GetMouseButtonDown(0))
        {
            // Chống click xuyên UI
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // CẢI TIẾN 2: Quét xuyên thấu mọi Collider tại điểm click (Chống lỗi click nhầm tường/sàn)
            Collider2D[] hits = Physics2D.OverlapPointAll(mousePos);
            bool clickedThisCabinet = false;
            foreach (var hit in hits)
            {
                if (hit.gameObject == this.gameObject)
                {
                    clickedThisCabinet = true;
                    break;
                }
            }

            // Nếu click chuẩn xác trúng cái tủ này
            if (clickedThisCabinet)
            {
                if (canInteract)
                {
                    if (AutoUIManager.Instance != null)
                    {
                        RPC_RequestSyncContainerStatus();
                        AutoUIManager.Instance.OpenContainerUI(this);
                    }
                }
                else
                {
                    if (dist > interactDistance)
                        Debug.Log("Đứng xa quá không với tới tủ đồ!");
                    else if (isBlockedByWall)
                        Debug.Log("Có bức tường chắn ngang rồi, không mở được!");
                }
            }
        }
    }

    // =========================================================
    // VẼ VÒNG TRÒN VÀNG TRONG EDITOR ĐỂ CANH KHOẢNG CÁCH (UX CHO DEV)
    // =========================================================
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }

    // =========================================================
    // CÁC HÀM RPC ĐỒNG BỘ MẠNG (ĐÃ FIX QUYỀN TRUY CẬP PROXY)
    // =========================================================

    [Rpc(RpcSources.Proxies | RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestSyncContainerStatus()
    {
        foreach (var slot in itemsInContainer)
        {
            RPC_SyncAddItem(slot.item.itemName, slot.amount, true);
        }
    }

    [Rpc(RpcSources.Proxies | RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)]
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
    public void RPC_ConfirmLootSuccess(PlayerRef targetPlayer, string itemName, int amount)
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

    [Rpc(RpcSources.Proxies | RpcSources.InputAuthority | RpcSources.StateAuthority, RpcTargets.StateAuthority)]
    public void RPC_StoreItem(string itemName, int amount)
    {
        ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
        if (itemData == null) return;

        StoreItemLocal(itemData, amount);
        RPC_SyncAddItem(itemName, amount, false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncAddItem(string itemName, int amount, bool isFullSync)
    {
        if (!HasStateAuthority)
        {
            if (isFullSync) itemsInContainer.Clear();
            ItemData itemData = Resources.Load<ItemData>("Items/" + itemName);
            if (itemData != null) StoreItemLocal(itemData, amount);
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