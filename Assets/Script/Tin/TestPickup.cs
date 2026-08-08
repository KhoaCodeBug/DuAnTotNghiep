using UnityEngine;

public class TestPickup : MonoBehaviour
{
    public InventorySystem inventory;
    public ItemData itemToPickup; // Kéo vật phẩm Dan9mm vào đây
    public int amount; // Số lượng muốn nhặt

    [Tooltip("Legacy test only. Disabled by default so it cannot conflict with the developer cheat hotkey.")]
    public bool enableLegacyPickupHotkey = false;

    void Update()
    {
        // This component is still attached to the player prefabs and was using
        // P as a test pickup shortcut.  It ran at the same time as the cheat
        // menu, which is why each press of P granted its configured item
        // (currently the energy drink).  Keep it opt-in and move it to F10.
        if (enableLegacyPickupHotkey && Input.GetKeyDown(KeyCode.F10) && inventory != null && itemToPickup != null)
        {
            inventory.AddItem(itemToPickup, amount);
            Debug.Log("Vừa nhặt: " + amount + " " + itemToPickup.itemName);
        }
    }
}
