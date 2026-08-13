using Fusion;
using UnityEngine;

/// <summary>Networked state for the personal hotbar flashlight.</summary>
[DisallowMultipleComponent]
public sealed class FlashlightController : NetworkBehaviour
{
    public const string ItemId = "Flashlight";
    public const float FullBatterySeconds = 300f;

    [Networked] public NetworkBool IsFlashlightOn { get; private set; }
    [Networked] public NetworkBool IsEquippedInHotbar { get; private set; }
    [Networked] public float Battery01 { get; private set; }

    private InventorySystem inventory;
    private bool lastReportedHotbarState;
    private float cachedBattery01 = 1f;
    private bool cachedActive;

    private bool NetworkStateAvailable => Object != null && Object.IsValid && Runner != null && Runner.IsRunning;
    public bool IsFlashlightActive => NetworkStateAvailable && ReadAndCacheActive();
    public float DisplayBattery01 => NetworkStateAvailable ? ReadAndCacheBattery() : cachedBattery01;

    public override void Spawned()
    {
        inventory = GetComponent<InventorySystem>();
        if (HasStateAuthority) Battery01 = 1f;
        cachedBattery01 = HasStateAuthority ? 1f : 0f;
        cachedActive = false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (!IsEquippedInHotbar && IsFlashlightOn) IsFlashlightOn = false;
        if (!IsFlashlightOn) return;

        Battery01 = Mathf.Max(0f, Battery01 - Runner.DeltaTime / FullBatterySeconds);
        if (Battery01 <= 0f) IsFlashlightOn = false;
    }

    public override void Render()
    {
        if (!NetworkStateAvailable) return;
        cachedBattery01 = Battery01;
        cachedActive = IsFlashlightOn && IsEquippedInHotbar && Battery01 > 0.0001f;
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
        if (HasStateAuthority) Toggle();
        else RPC_RequestToggle();
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

    private void Toggle()
    {
        if (!IsEquippedInHotbar || Battery01 <= 0f) return;
        IsFlashlightOn = !IsFlashlightOn;
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
    private void RPC_RequestToggle() => Toggle();
}
