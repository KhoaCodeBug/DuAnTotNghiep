using Fusion;
using UnityEngine;

/// <summary>Networked state for the personal hotbar flashlight.</summary>
[DisallowMultipleComponent]
public sealed class FlashlightController : NetworkBehaviour
{
    public const string ItemId = "Flashlight";
    /// <summary>One full real-time game day (DayNightManager.realMinutesPerDay = 10).</summary>
    public const float FullBatterySeconds = 600f;
    public const float MinimumLootBattery01 = 0.25f;

    [Networked] public NetworkBool IsFlashlightOn { get; private set; }
    [Networked] public NetworkBool IsEquippedInHotbar { get; private set; }
    [Networked] public float Battery01 { get; private set; }
    [Networked] private int ActiveHotbarSlot { get; set; } = -1;

    private InventorySystem inventory;
    private bool lastReportedHotbarState;
    private float cachedBattery01 = 1f;
    private bool cachedActive;

    private bool NetworkStateAvailable => Object != null && Object.IsValid && Runner != null && Runner.IsRunning;
    public bool IsFlashlightActive => NetworkStateAvailable && ReadAndCacheActive();
    public float DisplayBattery01 => NetworkStateAvailable ? ReadAndCacheBattery() : cachedBattery01;

    /// <summary>Returns the locally replicated charge for one specific hotbar flashlight.</summary>
    public float GetDisplayBattery01(int hotbarSlot)
    {
        inventory ??= GetComponent<InventorySystem>();
        return inventory != null && inventory.TryGetFlashlightBattery(hotbarSlot, out float battery01)
            ? battery01
            : 0f;
    }

    public override void Spawned()
    {
        inventory = GetComponent<InventorySystem>();
        if (HasStateAuthority)
        {
            Battery01 = 0f;
            ActiveHotbarSlot = -1;
            IsFlashlightOn = false;
            IsEquippedInHotbar = false;
        }
        cachedBattery01 = 0f;
        cachedActive = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        inventory ??= GetComponent<InventorySystem>();
        if (!IsActiveSlotValid())
        {
            IsEquippedInHotbar = false;
            IsFlashlightOn = false;
            ActiveHotbarSlot = -1;
            Battery01 = 0f;
            return;
        }

        IsEquippedInHotbar = true;
        if (!IsFlashlightOn) return;

        Battery01 = Mathf.Max(0f, Battery01 - Runner.DeltaTime / FullBatterySeconds);
        inventory.SetFlashlightBatteryLocal(ActiveHotbarSlot, Battery01);
        if (Battery01 <= 0f) IsFlashlightOn = false;
    }

    public override void Render()
    {
        if (!NetworkStateAvailable) return;
        cachedBattery01 = Battery01;
        cachedActive = IsFlashlightOn && IsEquippedInHotbar && Battery01 > 0.0001f;
        inventory ??= GetComponent<InventorySystem>();
        if (ActiveHotbarSlot >= 0)
            inventory?.SetFlashlightBatteryLocal(ActiveHotbarSlot, Battery01);
    }

    private void Update()
    {
        if (!NetworkStateAvailable || !HasInputAuthority) return;
        inventory ??= GetComponent<InventorySystem>();
        bool inHotbar = HasFlashlightInHotbar();
        if (inHotbar == lastReportedHotbarState) return;
        lastReportedHotbarState = inHotbar;
        if (HasStateAuthority) SetHotbarState(inHotbar);
        else RPC_SetHotbarState(inHotbar);
    }

    public bool TryToggleFromHotbar(int selectedSlot)
    {
        if (!NetworkStateAvailable || !HasInputAuthority || selectedSlot < 0 || selectedSlot >= 5 || !IsFlashlightAt(selectedSlot)) return false;
        if (HasStateAuthority) Toggle(selectedSlot);
        else RPC_RequestToggle(selectedSlot);
        return true;
    }

    private bool HasFlashlightInHotbar()
    {
        if (inventory == null) return false;
        for (int i = 0; i < Mathf.Min(5, inventory.slots.Count); i++)
            if (IsFlashlightAt(i)) return true;
        return false;
    }

    private bool IsFlashlightAt(int index)
    {
        if (inventory == null || index >= inventory.slots.Count) return false;
        InventorySlot slot = inventory.slots[index];
        return slot != null && slot.item != null && slot.amount > 0 &&
               (slot.item.name == ItemId || slot.item.itemName == ItemId);
    }

    private void SetHotbarState(bool inHotbar)
    {
        IsEquippedInHotbar = inHotbar;
        if (!inHotbar) IsFlashlightOn = false;
    }

    private bool IsActiveSlotValid() => inventory != null && ActiveHotbarSlot >= 0 &&
        ActiveHotbarSlot < 5 && inventory.IsFlashlightInHotbarSlot(ActiveHotbarSlot);

    private void Toggle(int selectedSlot)
    {
        if (inventory == null || !inventory.TryGetFlashlightBattery(selectedSlot, out float slotBattery01)) return;
        ActiveHotbarSlot = selectedSlot;
        IsEquippedInHotbar = true;
        Battery01 = slotBattery01;
        if (Battery01 <= 0f) return;
        IsFlashlightOn = !IsFlashlightOn;
    }

    /// <summary>Called only by authoritative inventory death cleanup.</summary>
    public void AuthorityClearFlashlightState()
    {
        if (!HasStateAuthority) return;
        IsFlashlightOn = false;
        IsEquippedInHotbar = false;
        Battery01 = 0f;
        ActiveHotbarSlot = -1;
    }

    private float ReadAndCacheBattery()
    {
        cachedBattery01 = Battery01;
        return cachedBattery01;
    }

    private bool ReadAndCacheActive()
    {
        cachedActive = IsFlashlightOn && IsEquippedInHotbar && Battery01 > 0.0001f;
        return cachedActive;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetHotbarState(bool inHotbar) => SetHotbarState(inHotbar);

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestToggle(int selectedSlot) => Toggle(selectedSlot);
}
