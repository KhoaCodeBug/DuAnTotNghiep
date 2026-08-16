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

    [Header("Weapon Loot (Chỉ Host xử lý)")]
    [Range(0f, 100f)]
    [Tooltip("Cơ hội một Loot Container sinh tối đa một weapon ngẫu nhiên từ Resources/Items.")]
    public float bonusWeaponDropChance = 25f;

    [Header("Danh sách đồ hiện tại (Realtime)")]
    public List<InventorySlot> itemsInContainer = new List<InventorySlot>();

    private bool hasGeneratedLoot = false;
    private PlayerMovement cachedLocalPlayer;
    private InventorySystem cachedLocalInventory;
    private int lastOpenFrame = -1;

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
        itemsInContainer.Clear();

        if (lootTable != null)
        {
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
        }

        // The existing loot table does not contain AK47/S12K.  Roll one
        // optional weapon separately so a player who starts with one weapon
        // can still discover a different second weapon in the world.
        TryGenerateBonusWeapon();
        hasGeneratedLoot = true;
    }

    /// <summary>
    /// Adds one guaranteed, visible quest clue to this real container. Only
    /// State Authority mutates the canonical contents; normal synchronization
    /// sends the generated item to clients by its stable display name.
    /// </summary>
    public bool EnsureQuestClueItem(QuestRouteClueKind kind)
    {
        if (!HasStateAuthority) return false;

        ItemData clue = QuestRouteClueItemCatalog.GetOrCreate(kind);
        foreach (InventorySlot slot in itemsInContainer)
        {
            if (slot != null && QuestRouteClueItemCatalog.TryGetKind(slot.item, out QuestRouteClueKind existing) && existing == kind)
                return true;
        }

        if (itemsInContainer.Count >= 20) itemsInContainer.RemoveAt(itemsInContainer.Count - 1);
        StoreItemLocal(clue, 1);
        RPC_SyncAddItem(clue.itemName, 1, false);
        return true;
    }

    private void TryGenerateBonusWeapon()
    {
        if (Random.Range(0f, 100f) > bonusWeaponDropChance) return;

        List<ItemData> weaponPool = new List<ItemData>();
        foreach (ItemData item in Resources.LoadAll<ItemData>("Items"))
        {
            if (item != null && item.category == ItemCategory.Weapon && !string.IsNullOrWhiteSpace(item.name))
            {
                weaponPool.Add(item);
            }
        }

        if (weaponPool.Count == 0) return;

        ItemData selectedWeapon = weaponPool[Random.Range(0, weaponPool.Count)];
        StoreItemLocal(selectedWeapon, 1);
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

        Collider2D myCollider = GetComponent<Collider2D>();
        Vector2 closestPoint = myCollider.ClosestPoint(playerPos);
        float dist = Vector2.Distance(playerPos, closestPoint);

        bool isBlockedByWall = false;
        if (obstacleLayer.value != 0)
        {
            isBlockedByWall = Physics2D.Linecast(playerPos, closestPoint, obstacleLayer);
        }

        bool canInteract = (dist <= interactDistance) && !isBlockedByWall;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = canInteract ? highlightColor : originalColor;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            if (Camera.main == null) return;
            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z = Mathf.Abs(Camera.main.transform.position.z - transform.position.z);
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

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

            if (clickedThisCabinet)
                TryOpenForLocalPlayer();
        }
    }

    /// <summary>
    /// Opens this container for the input-authority player after applying the
    /// exact same distance and wall checks as the normal world click.  It is
    /// also used by the tutorial's small-target click assist.
    /// </summary>
    public bool TryOpenForLocalPlayer()
    {
        if (lastOpenFrame == Time.frameCount) return true;
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()) return false;

        PlayerMovement localPlayer = GetLocalPlayerCached();
        Collider2D myCollider = GetComponent<Collider2D>();
        if (localPlayer == null || myCollider == null) return false;

        Vector2 playerPos = localPlayer.transform.position;
        Vector2 closestPoint = myCollider.ClosestPoint(playerPos);
        float distance = Vector2.Distance(playerPos, closestPoint);
        bool blockedByWall = obstacleLayer.value != 0 && Physics2D.Linecast(playerPos, closestPoint, obstacleLayer);
        if (distance > interactDistance || blockedByWall)
        {
            Debug.Log(distance > interactDistance
                ? "Đứng xa quá không với tới tủ đồ!"
                : "Có bức tường chắn ngang rồi, không mở được!");
            return false;
        }

        if (Runner == null || AutoUIManager.Instance == null) return false;
        lastOpenFrame = Time.frameCount;
        RPC_RequestSyncContainerStatus(Runner.LocalPlayer);
        AutoUIManager.Instance.OpenContainerUI(this);
        PreMilitaryQuestRuntimeBridge.NotifyContainerOpened(this);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }

    // =========================================================
    // CÁC HÀM RPC ĐỒNG BỘ MẠNG (ĐÃ FIX TÌNH TRẠNG MẤT ITEM CLIENT)
    // =========================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSyncContainerStatus(PlayerRef requestingPlayer)
    {
        RPC_ClearClientContainer(requestingPlayer);

        foreach (var slot in itemsInContainer)
        {
            RPC_SyncAddItemToTarget(requestingPlayer, slot.item.itemName, slot.amount);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ClearClientContainer([RpcTarget] PlayerRef targetPlayer)
    {
        if (Runner.LocalPlayer == targetPlayer && !HasStateAuthority)
        {
            itemsInContainer.Clear();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncAddItemToTarget([RpcTarget] PlayerRef targetPlayer, string itemName, int amount)
    {
        if (Runner.LocalPlayer == targetPlayer && !HasStateAuthority)
        {
            ItemData itemData = ItemDataLoader.LoadItem(itemName);
            if (itemData != null) StoreItemLocal(itemData, amount);
        }

        if (Runner.LocalPlayer == targetPlayer && AutoUIManager.Instance != null && AutoUIManager.Instance.IsContainerOpen(this))
        {
            AutoUIManager.Instance.RefreshContainerUI(this);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestTakeItem(int slotIndex, string requestedItemName, PlayerRef playerTryingToLoot, RpcInfo info = default)
    {
        // A client may request loot only for its own PlayerRef.  RpcInfo.Source
        // is normally the caller, including a Host local call; PlayerRef.None
        // is allowed only for a local-server invocation.
        if (info.Source != PlayerRef.None && info.Source != playerTryingToLoot)
        {
            Debug.LogWarning($"[LOOT SERVER] Rejected spoofed loot request: source={info.Source}, requested={playerTryingToLoot}.");
            return;
        }
        if (slotIndex < 0 || slotIndex >= itemsInContainer.Count)
        {
            Debug.LogWarning($"[LOOT SERVER] Rejected invalid container slot {slotIndex} from {playerTryingToLoot}.");
            return;
        }
        InventorySlot slot = itemsInContainer[slotIndex];
        if (slot == null || slot.item == null || slot.amount <= 0)
        {
            Debug.LogWarning($"[LOOT SERVER] Rejected empty container slot {slotIndex} from {playerTryingToLoot}.");
            return;
        }

        // Kiểm tra an toàn: Nếu tên item ở vị trí này không đúng với cái Client xin thì từ chối.
        if (slot.item.itemName != requestedItemName)
        {
            Debug.LogWarning($"[LOOT SERVER] Rejected item mismatch at slot {slotIndex}: requested={requestedItemName}, actual={slot.item.itemName}.");
            return;
        }

        if (!TryGetServerInventory(playerTryingToLoot, out InventorySystem playerInventory))
        {
            RPC_NotifyLootDenied(playerTryingToLoot, "Không tìm thấy túi đồ hợp lệ của người chơi trên Host.");
            return;
        }

        // Weapons are a two-weapon loadout system.  The host owns this check,
        // so a client cannot bypass it by editing its local inventory/UI.
        if (slot.item.category == ItemCategory.Weapon)
        {
            if (playerInventory.GetWeaponItemCount() >= 2)
            {
                RPC_NotifyLootDenied(playerTryingToLoot, "Bạn đã có đủ 2 vũ khí, không thể loot thêm.");
                return;
            }

            if (playerInventory.HasWeapon(slot.item.name) || playerInventory.HasWeapon(slot.item.itemName))
            {
                RPC_NotifyLootDenied(playerTryingToLoot, "Bạn đã có vũ khí này; hãy tìm khẩu khác trong Loot Container.");
                return;
            }
        }

        int amount = slot.amount;
        // Canonical transaction: add on State Authority first.  Only remove
        // from the container if the inventory accepted the full stack.  The
        // inventory's existing RPC sync then updates the owning client.
        if (!playerInventory.AddItem(slot.item, amount))
        {
            RPC_NotifyLootDenied(playerTryingToLoot, "Túi đồ không đủ chỗ để nhận vật phẩm này.");
            return;
        }

        bool isRouteClue = QuestRouteClueItemCatalog.TryGetKind(slot.item, out QuestRouteClueKind routeClueKind);

        itemsInContainer.RemoveAt(slotIndex);
        RPC_SyncRemoveItem(slotIndex);
        if (isRouteClue)
        {
            RPC_NotifyQuestClueLooted(playerTryingToLoot, (int)routeClueKind,
                QuestRouteClueItemCatalog.GetClueId(routeClueKind),
                QuestRouteClueItemCatalog.GetDisplayName(routeClueKind));
        }
        Debug.Log($"[LOOT SERVER] Granted {amount}x {slot.item.itemName} to {playerTryingToLoot}.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyQuestClueLooted([RpcTarget] PlayerRef targetPlayer, int kindValue,
        string clueId, string displayName)
    {
        if (Runner.LocalPlayer != targetPlayer) return;
        PreMilitaryQuestRuntimeBridge.NotifyRouteClueLooted(clueId, displayName);
    }

    private bool TryGetServerInventory(PlayerRef player, out InventorySystem inventory)
    {
        inventory = null;

        // Fast path for the usual HostModeSpawner flow.
        if (HostModeSpawner.Instance != null && HostModeSpawner.Instance.TryGetPlayerInventory(player, out inventory))
        {
            return true;
        }

        // Reliable fallback for late joins, respawns, or scenes that spawned a
        // player through a different path than HostModeSpawner's dictionary.
        foreach (InventorySystem candidate in FindObjectsByType<InventorySystem>(FindObjectsSortMode.None))
        {
            if (candidate != null && candidate.Object != null && candidate.Object.IsValid && candidate.Object.InputAuthority == player)
            {
                inventory = candidate;
                return true;
            }
        }

        return false;
    }

    // Client-side UX only.  The same rule is revalidated by State Authority in
    // RPC_RequestTakeItem, so changing this UI cannot bypass the limit.
    public bool CanLocalPlayerLootItem(ItemData itemData)
    {
        if (itemData == null || itemData.category != ItemCategory.Weapon) return true;

        InventorySystem inventory = GetLocalInventoryCached();
        if (inventory == null) return false;
        if (inventory.GetWeaponItemCount() >= 2) return false;
        return !inventory.HasWeapon(itemData.name) && !inventory.HasWeapon(itemData.itemName);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyLootDenied([RpcTarget] PlayerRef targetPlayer, string reason)
    {
        if (Runner.LocalPlayer == targetPlayer)
        {
            Debug.LogWarning($"[LOOT] {reason}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ConfirmLootSuccess(PlayerRef targetPlayer, string itemName, int amount)
    {
        if (Runner.LocalPlayer == targetPlayer)
        {
            ItemData itemData = ItemDataLoader.LoadItem(itemName);
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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StoreItem(string itemName, int amount)
    {
        ItemData itemData = ItemDataLoader.LoadItem(itemName);
        if (itemData == null) return;

        StoreItemLocal(itemData, amount);
        RPC_SyncAddItem(itemName, amount, false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncAddItem(string itemName, int amount, bool isFullSync)
    {
        if (!HasStateAuthority)
        {
            // Vẫn giữ isFullSync phòng hờ các trường hợp ép nạp mới khác
            if (isFullSync) itemsInContainer.Clear();
            ItemData itemData = ItemDataLoader.LoadItem(itemName);
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
