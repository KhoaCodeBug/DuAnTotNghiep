using Fusion;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class LootContainer : NetworkBehaviour
{
    public const int DefaultMaxSlots = 20;

    [Header("Cài đặt Tủ Đồ")]
    [Tooltip("Khoảng cách tối đa để mở tủ (Bấm vào tủ ở Scene để xem vòng tròn vàng)")]
    public float interactDistance = 2.5f;
    [SerializeField, Min(1)] private int maxSlots = DefaultMaxSlots;
    [SerializeField, Range(0, 4)]
    [Tooltip("Số ô chừa cho vật phẩm nhiệm vụ sau khi random loot thường.")]
    private int reservedQuestSlots = 2;

    public int MaxSlots => Mathf.Max(1, maxSlots);
    private int RandomLootSlotLimit => Mathf.Max(1, MaxSlots - reservedQuestSlots);

    [Header("Chống Loot Xuyên Tường")]
    [Tooltip("Chọn layer của các bức tường hoặc vật cản (Wall)")]
    public LayerMask obstacleLayer;

    [Header("Hiệu ứng (UX)")]
    public Color highlightColor = new Color(1f, 0.8f, 0.8f, 1f);
    private Color originalColor;
    private SpriteRenderer spriteRenderer;

    [Header("Hệ Thống Random Đồ (Chỉ Host xử lý)")]
    public LootTableSO lootTable;

    [Header("Route B Military Repair Loot")]
    [SerializeField]
    [Tooltip("Only the military siege may open or transact with this container.")]
    private bool militaryRepairLootContainer;

    [Header("Weapon Loot (Chỉ Host xử lý)")]
    [Range(0f, 100f)]
    [Tooltip("Cơ hội một Loot Container sinh tối đa một weapon ngẫu nhiên từ Resources/Items.")]
    public float bonusWeaponDropChance = 25f;

    [Header("Danh sách đồ hiện tại (Realtime)")]
    public List<InventorySlot> itemsInContainer = new List<InventorySlot>();

    // Each physical cabinet rolls quest loot at most once on State Authority.
    // Opening a house or an ordinary cabinet never advances quest progress.
    [Networked] private NetworkBool RouteClueRollResolved { get; set; }
    [Networked] private NetworkBool MilitaryLootHasItems { get; set; }

    private bool hasGeneratedLoot = false;
    private PlayerMovement cachedLocalPlayer;
    private InventorySystem cachedLocalInventory;
    private int lastOpenFrame = -1;

    public bool IsMilitaryRepairLootContainer => militaryRepairLootContainer;
    public bool IsMilitaryLootVip => militaryRepairLootContainer && name.StartsWith("LootQuanSuVjp");
    public bool ShouldShowMilitaryWaypoint => Object != null && Object.IsValid &&
        IsGameplayAvailable && MilitaryLootHasItems;
    public bool IsGameplayAvailable => !militaryRepairLootContainer ||
        (MilitaryBaseQuestManager.Instance != null &&
         MilitaryBaseQuestManager.Instance.CurrentPhase == MilitaryBaseQuestManager.Phase.SiegeAndRepair &&
         !MilitaryBaseQuestManager.Instance.IsMilitaryIntroCinematicActive);

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
        if (HasStateAuthority && !hasGeneratedLoot && !militaryRepairLootContainer)
        {
            GenerateRandomLoot();
        }
    }

    /// <summary>Authority-only setup API used before a Route B container becomes playable.</summary>
    public void AuthorityClearContents()
    {
        if (!HasStateAuthority || !militaryRepairLootContainer) return;
        itemsInContainer.Clear();
        hasGeneratedLoot = true;
        MilitaryLootHasItems = false;
        RPC_ClearMilitaryContainerForRetry();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ClearMilitaryContainerForRetry()
    {
        if (!HasStateAuthority) itemsInContainer.Clear();
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsContainerOpen(this))
            AutoUIManager.Instance.CloseContainerUI();
    }

    public bool AuthorityAddConfiguredItem(ItemData itemData, int amount)
    {
        if (!HasStateAuthority || !militaryRepairLootContainer || !hasGeneratedLoot ||
            !StoreItemLocal(itemData, amount)) return false;
        MilitaryLootHasItems = true;
        return true;
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
                    StoreItemLocal(lootRule.itemPrefab, spawnAmount, RandomLootSlotLimit);
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

        if (!StoreItemLocal(clue, 1))
        {
            Debug.LogWarning($"[QUEST LOOT] Container '{name}' is full; clue '{clue.itemName}' was not inserted.");
            return false;
        }
        RPC_SyncAddItem(clue.itemName, 1, false);
        return true;
    }

    /// <summary>Adds one stable arrival-car quest item to authoritative loot.</summary>
    public bool EnsureArrivalCarItem(ArrivalCarItemKind kind)
    {
        if (!HasStateAuthority) return false;

        ItemData item = ArrivalCarItemCatalog.GetOrCreate(kind);
        foreach (InventorySlot slot in itemsInContainer)
        {
            if (slot != null && ArrivalCarItemCatalog.TryGetKind(slot.item, out ArrivalCarItemKind existing) &&
                existing == kind)
                return true;
        }

        if (!StoreItemLocal(item, 1))
        {
            Debug.LogWarning($"[ARRIVAL CAR] Container '{name}' is full; '{item.itemName}' was not inserted.");
            return false;
        }
        RPC_SyncAddItem(item.itemName, 1, false);
        return true;
    }

    public bool ContainsArrivalCarItem(ArrivalCarItemKind kind)
    {
        foreach (InventorySlot slot in itemsInContainer)
        {
            if (slot != null && slot.amount > 0 &&
                ArrivalCarItemCatalog.TryGetKind(slot.item, out ArrivalCarItemKind existing) && existing == kind)
                return true;
        }

        return false;
    }

    public bool AuthorityTryBeginRouteClueRoll()
    {
        if (!HasStateAuthority || RouteClueRollResolved)
            return false;

        RouteClueRollResolved = true;
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
        StoreItemLocal(selectedWeapon, 1, RandomLootSlotLimit);
    }

    public bool CanStoreItem(ItemData itemData, int amount)
    {
        return CanStoreItem(itemData, amount, MaxSlots);
    }

    private bool CanStoreItem(ItemData itemData, int amount, int slotLimit)
    {
        if (itemData == null || amount <= 0) return false;

        int remaining = amount;
        int stackLimit = itemData.isStackable ? Mathf.Max(1, itemData.maxStack) : 1;
        if (itemData.isStackable)
        {
            foreach (InventorySlot slot in itemsInContainer)
            {
                if (slot == null || slot.item == null || slot.item.itemName != itemData.itemName) continue;
                remaining -= Mathf.Max(0, stackLimit - slot.amount);
                if (remaining <= 0) return true;
            }
        }

        int availableNewSlots = Mathf.Max(0, slotLimit - itemsInContainer.Count);
        return remaining <= availableNewSlots * stackLimit;
    }

    private bool StoreItemLocal(ItemData itemData, int amount, int slotLimit = -1)
    {
        int effectiveSlotLimit = slotLimit > 0 ? Mathf.Min(slotLimit, MaxSlots) : MaxSlots;
        if (!CanStoreItem(itemData, amount, effectiveSlotLimit)) return false;

        int stackLimit = itemData.isStackable ? Mathf.Max(1, itemData.maxStack) : 1;
        if (itemData.isStackable)
        {
            foreach (var slot in itemsInContainer)
            {
                if (slot != null && slot.item != null && slot.item.itemName == itemData.itemName &&
                    slot.amount < stackLimit)
                {
                    int spaceLeft = stackLimit - slot.amount;
                    if (amount <= spaceLeft)
                    {
                        slot.amount += amount;
                        return true;
                    }
                    else
                    {
                        slot.amount += spaceLeft;
                        amount -= spaceLeft;
                    }
                }
            }
        }

        while (amount > 0 && itemsInContainer.Count < effectiveSlotLimit)
        {
            int amountToStore = Mathf.Min(amount, stackLimit);
            itemsInContainer.Add(new InventorySlot(itemData, amountToStore));
            amount -= amountToStore;
        }

        return amount == 0;
    }

    private void Update()
    {
        if (!IsGameplayAvailable)
        {
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsContainerOpen(this))
                AutoUIManager.Instance.CloseContainerUI();
            return;
        }

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
        if (!IsGameplayAvailable) return false;
        if (lastOpenFrame == Time.frameCount) return true;
        if (AutoUIManager.Instance != null && AutoUIManager.Instance.IsAnyMenuOpen()) return false;

        PlayerMovement localPlayer = GetLocalPlayerCached();
        Collider2D myCollider = GetComponent<Collider2D>();
        if (localPlayer == null || myCollider == null) return false;

        Vector2 playerPos = localPlayer.transform.position;
        Vector2 closestPoint = myCollider.ClosestPoint(playerPos);
        float distance = Vector2.Distance(playerPos, closestPoint);
        bool blockedByWall = obstacleLayer.value != 0 && Physics2D.Linecast(playerPos, closestPoint, obstacleLayer);
        if (!CanPlayerOpenFrom(playerPos))
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
        return true;
    }

    /// <summary>
    /// Shared interaction validation used by both the local click path and the
    /// authoritative quest request. Keeping these checks identical prevents a
    /// client from advancing a house while standing too far away or behind a wall.
    /// </summary>
    public bool CanPlayerOpenFrom(Vector3 playerPosition)
    {
        if (!IsGameplayAvailable) return false;
        Collider2D containerCollider = GetComponent<Collider2D>();
        if (containerCollider == null) return false;

        Vector2 playerPoint = playerPosition;
        Vector2 closestPoint = containerCollider.ClosestPoint(playerPoint);
        if (Vector2.Distance(playerPoint, closestPoint) > interactDistance) return false;
        return obstacleLayer.value == 0 || !Physics2D.Linecast(playerPoint, closestPoint, obstacleLayer);
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
    public void RPC_RequestSyncContainerStatus(PlayerRef requestingPlayer, RpcInfo info = default)
    {
        // Resolve quest loot on State Authority before this container's
        // canonical slots are sent to the opener.  Keeping both operations in
        // one RPC removes the race where the UI could render first and the pity
        // clue was inserted by a second request afterwards.
        if (info.Source != PlayerRef.None && info.Source != requestingPlayer)
        {
            Debug.LogWarning($"[LOOT SERVER] Rejected spoofed container sync: source={info.Source}, requested={requestingPlayer}.");
            return;
        }
        if (!AuthorityValidateMilitaryLootRequest(requestingPlayer, out string denial))
        {
            RPC_NotifyLootDenied(requestingPlayer, denial);
            return;
        }
        MainQuestManager.Instance?.AuthorityRegisterOpenedContainer(this, requestingPlayer);

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
        if (!AuthorityValidateMilitaryLootRequest(playerTryingToLoot, out string denial))
        {
            RPC_NotifyLootDenied(playerTryingToLoot, denial);
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
        if (militaryRepairLootContainer && itemsInContainer.Count == 0) MilitaryLootHasItems = false;
        RPC_SyncRemoveItem(slotIndex);
        if (isRouteClue)
        {
            MainQuestManager.Instance?.AuthorityRegisterRouteClue(routeClueKind, playerTryingToLoot);
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
    public void RPC_StoreItem(string itemName, int amount, PlayerRef playerTryingToStore, RpcInfo info = default)
    {
        if (info.Source != PlayerRef.None && info.Source != playerTryingToStore)
        {
            Debug.LogWarning($"[LOOT SERVER] Rejected spoofed store request: source={info.Source}, requested={playerTryingToStore}.");
            return;
        }
        if (!AuthorityValidateMilitaryLootRequest(playerTryingToStore, out string denial))
        {
            RPC_NotifyLootDenied(playerTryingToStore, denial);
            return;
        }

        ItemData itemData = ItemDataLoader.LoadItem(itemName);
        if (itemData == null || amount <= 0) return;
        if (!TryGetServerInventory(playerTryingToStore, out InventorySystem playerInventory))
        {
            RPC_NotifyLootDenied(playerTryingToStore, "Không tìm thấy túi đồ hợp lệ để cất vật phẩm.");
            return;
        }

        if (playerInventory.GetItemCount(itemData) < amount)
        {
            RPC_NotifyLootDenied(playerTryingToStore, "Vật phẩm trong túi đã thay đổi; yêu cầu cất đồ bị hủy.");
            return;
        }

        if (!CanStoreItem(itemData, amount))
        {
            RPC_NotifyLootDenied(playerTryingToStore, $"Tủ đã đầy ({MaxSlots} ô).");
            return;
        }

        int consumed = playerInventory.ConsumeItem(itemData, amount);
        if (consumed != amount || !StoreItemLocal(itemData, amount))
        {
            if (consumed > 0) playerInventory.AddItem(itemData, consumed);
            RPC_NotifyLootDenied(playerTryingToStore, "Không thể hoàn tất giao dịch cất đồ; vật phẩm đã được hoàn lại.");
            return;
        }

        if (militaryRepairLootContainer) MilitaryLootHasItems = true;
        RPC_SyncAddItem(itemName, amount, false);
    }

    private void OnGUI()
    {
        if (!ShouldShowMilitaryWaypoint || LocalGameplayUIState.BlocksWorldInteractionHints) return;
        Camera camera = Camera.main;
        if (camera == null) return;
        Vector3 screenPoint = camera.WorldToScreenPoint(transform.position + new Vector3(0f, 0.65f, 0f));
        if (screenPoint.z <= 0f) return;

        GUIStyle markerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = IsMilitaryLootVip ? 32 : 27,
            fontStyle = FontStyle.Bold
        };
        markerStyle.normal.textColor = new Color(1f, 0.88f, 0.1f, 1f);
        string marker = IsMilitaryLootVip ? "◆" : "●";
        GUI.Label(new Rect(screenPoint.x - 22f, Screen.height - screenPoint.y - 22f, 44f, 44f), marker,
            markerStyle);
    }

    private bool AuthorityValidateMilitaryLootRequest(PlayerRef player, out string reason)
    {
        reason = string.Empty;
        if (!militaryRepairLootContainer) return true;
        if (!HasStateAuthority || !IsGameplayAvailable)
        {
            reason = "Thùng tiếp tế sửa xe chỉ khả dụng trong giai đoạn phòng thủ.";
            return false;
        }
        if (!TryGetServerInventory(player, out InventorySystem inventory) || inventory == null)
        {
            reason = "Không tìm thấy Player authoritative cho giao dịch loot.";
            return false;
        }

        PlayerHealth health = inventory.GetComponent<PlayerHealth>();
        if (health != null && (health.isDead || health.isTransforming))
        {
            reason = "Không thể dùng thùng tiếp tế trong trạng thái hiện tại.";
            return false;
        }
        if (!CanPlayerOpenFrom(inventory.transform.position))
        {
            reason = "Bạn đang quá xa hoặc có vật cản giữa Player và thùng tiếp tế.";
            return false;
        }
        return true;
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
