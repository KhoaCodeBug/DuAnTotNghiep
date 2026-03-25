using System.Collections.Generic;
using UnityEngine;
using Fusion; // Bắt buộc dùng mạng

[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;

    public InventorySlot(ItemData item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }
    public void AddAmount(int value) { amount += value; }
}

public class InventorySystem : NetworkBehaviour
{
    [Header("Cài đặt Ba lô")]
    public int maxSlots = 20;

    [Header("Cài đặt Nhặt Đồ (Mới)")]
    public float pickupRadius = 0.5f; // Tầm quét nhặt đồ dưới chân

    [Header("Danh sách các ô đang chứa đồ")]
    public List<InventorySlot> slots = new List<InventorySlot>();

    [Header("Cài đặt Rớt Đồ")]
    public GameObject droppedItemPrefab;

    // 🔥 MẮT THẦN QUÉT ĐỒ: Bỏ qua lỗi vật lý mạng, tự đi tìm đồ dưới chân
    // 🔥 MẮT THẦN QUÉT ĐỒ: Bỏ qua lỗi vật lý mạng, tự đi tìm đồ dưới chân
    public override void FixedUpdateNetwork()
    {
        // 1. Chỉ quét đồ trên máy tính của người đang chơi nhân vật này
        if (!HasInputAuthority) return;

        // 🔥 2. ĐÃ FIX: CHỐT CHẶN CHỐNG NHẶT ĐỒ Ở TƯƠNG LAI (Chống Resimulation)
        // Dòng này ép Clone không được phép "cầm đèn chạy trước ô tô", giải quyết triệt để vụ lụm đồ từ xa!
        if (!Runner.IsForward) return;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, pickupRadius);
        foreach (Collider2D col in colliders)
        {
            ItemPickup pickup = col.GetComponent<ItemPickup>();

            // Nhặt nếu thấy đồ và đồ chưa bị tắt
            if (pickup != null && pickup.isActiveAndEnabled)
            {
                bool pickedUp = AddItem(pickup.item, pickup.amount);
                if (pickedUp)
                {
                    Debug.Log("Đã lụm: " + pickup.item.itemName);

                    // Tắt cục đồ ngay lập tức trên màn hình của mình cho mượt
                    pickup.enabled = false;
                    col.enabled = false;
                    SpriteRenderer sr = pickup.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.enabled = false;

                    // Nhờ Server xóa cục đồ trên toàn mạng
                    NetworkObject netObj = pickup.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsValid)
                    {
                        RPC_RequestDespawnItem(netObj);
                    }
                    else
                    {
                        Destroy(pickup.gameObject); // Xóa đồ test tự đặt
                    }
                }
            }
        }
    }

    // 🔥 MỚI: Thêm hàm này xuống cuối cùng của file InventorySystem (Dưới cùng, trước dấu ngoặc nhọn kết thúc class)
    // Hàm này giúp bạn NHÌN THẤY vòng tròn nhặt đồ trong Unity
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }

    public bool AddItem(ItemData itemToAdd, int amountToAdd)
    {
        if (!HasInputAuthority) return false;

        if (itemToAdd.isStackable)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.item == itemToAdd && slot.amount < itemToAdd.maxStack)
                {
                    int spaceLeft = itemToAdd.maxStack - slot.amount;
                    if (amountToAdd <= spaceLeft)
                    {
                        slot.AddAmount(amountToAdd);
                        UpdateUI();
                        return true;
                    }
                    else
                    {
                        slot.AddAmount(spaceLeft);
                        amountToAdd -= spaceLeft;
                    }
                }
            }
        }

        while (amountToAdd > 0 && slots.Count < maxSlots)
        {
            int amountToStore = Mathf.Min(amountToAdd, itemToAdd.maxStack);
            slots.Add(new InventorySlot(itemToAdd, amountToStore));
            amountToAdd -= amountToStore;
        }

        UpdateUI();

        if (amountToAdd > 0)
        {
            Debug.Log("Ba lô đầy! Không thể chứa hết " + itemToAdd.itemName);
            return false;
        }

        return true;
    }

    public void UseItem(int index)
    {
        if (!HasInputAuthority) return;
        if (index < 0 || index >= slots.Count) return;

        InventorySlot slot = slots[index];
        ItemData item = slot.item;
        bool itemUsed = false;

        PlayerHealth health = GetComponent<PlayerHealth>();
        PlayerStamina stamina = GetComponent<PlayerStamina>();
        PlayerSurvival survival = GetComponent<PlayerSurvival>();

        if (item.category == ItemCategory.Medical)
        {
            if (health != null && health.currentHealth < health.maxHealth)
            {
                health.Heal(item.healAmount);
                itemUsed = true;
            }
            else Debug.Log("⚠️ Máu đang đầy!");
        }
        else if (item.category == ItemCategory.Consumable)
        {
            if (survival != null)
            {
                if (item.hungerRestore > 0) survival.RestoreHunger(item.hungerRestore);
                if (item.thirstRestore > 0) survival.RestoreThirst(item.thirstRestore);
                itemUsed = true;
            }

            if (stamina != null && item.buffDuration > 0)
            {
                stamina.ApplyEnergyBuff(item.buffDuration, item.speedMultiplier, item.maxStaminaBoost);
                itemUsed = true;
            }

            if (itemUsed) Debug.Log("Đã nốc xong: " + item.itemName);
        }
        else if (item.category == ItemCategory.Ammunition)
        {
            Debug.Log("⚠️ Đạn dược không thể sử dụng trực tiếp!");
        }

        if (itemUsed)
        {
            slot.amount--;
            if (slot.amount <= 0) slots.RemoveAt(index);
            UpdateUI();
        }
    }

    public void DropItem(int index)
    {
        if (!HasInputAuthority) return;

        if (index < 0 || index >= slots.Count) return;
        InventorySlot slot = slots[index];
        ItemData itemToDrop = slot.item;

        slot.amount--;
        if (slot.amount <= 0) slots.RemoveAt(index);
        UpdateUI();

        if (droppedItemPrefab != null)
        {
            // 🔥 LẤY TÊN FILE của món đồ để gửi cho Server
            string fileName = itemToDrop.name;
            RPC_RequestDropItem(fileName, 1, transform.position);
        }
    }

    // Yêu cầu Server đẻ đồ ra cho mọi người cùng thấy
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestDropItem(NetworkString<_64> fileName, int dropAmount, Vector3 dropPos)
    {
        Vector2 randomOffset = Random.insideUnitCircle * 0.4f;
        Vector3 spawnPos = dropPos + new Vector3(randomOffset.x, randomOffset.y, 0f);

        // Runner.Spawn sẽ đẻ cục đồ rỗng ra trên toàn Server
        Runner.Spawn(droppedItemPrefab, spawnPos, Quaternion.identity, null, (runner, obj) =>
        {
            // 🔥 TRƯỚC KHI hiện ra màn hình, nhét cái Tên File và Số lượng vào cục đồ
            NetworkDropItem netDrop = obj.GetComponent<NetworkDropItem>();
            if (netDrop != null)
            {
                netDrop.NetItemName = fileName;
                netDrop.NetAmount = dropAmount;
            }
        });
    }

    private void UpdateUI()
    {
        if (!HasInputAuthority) return;
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.RefreshUI(this.slots);
    }

    public int GetItemCount(ItemData itemToCount)
    {
        if (itemToCount == null) return 0;
        int total = 0;
        foreach (var slot in slots) { if (slot.item != null && slot.item.itemName == itemToCount.itemName) total += slot.amount; }
        return total;
    }

    public int ConsumeItem(ItemData itemToConsume, int amountNeeded)
    {
        if (itemToConsume == null) return 0;
        if (!HasInputAuthority) return 0;

        int amountExtracted = 0;
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i].item != null && slots[i].item.itemName == itemToConsume.itemName)
            {
                int availableInSlot = slots[i].amount;
                int amountToTakeFromSlot = Mathf.Min(availableInSlot, amountNeeded - amountExtracted);

                slots[i].amount -= amountToTakeFromSlot;
                amountExtracted += amountToTakeFromSlot;

                if (slots[i].amount <= 0) slots.RemoveAt(i);
                if (amountExtracted >= amountNeeded) break;
            }
        }

        UpdateUI();
        return amountExtracted;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestDespawnItem(NetworkObject itemNetObj)
    {
        if (itemNetObj != null && itemNetObj.IsValid)
        {
            Runner.Despawn(itemNetObj);
        }
    }
}