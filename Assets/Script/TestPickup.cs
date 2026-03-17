using UnityEngine;

public class TestPickup : MonoBehaviour
{
    public InventorySystem inventory;
    public ItemData itemToPickup; // Kéo vật phẩm Dan9mm vào đây
    public int amount; // Số lượng muốn nhặt

    void Update()
    {
        // Bấm phím P để nhặt đồ test
        if (Input.GetKeyDown(KeyCode.P))
        {
            inventory.AddItem(itemToPickup, amount);
            Debug.Log("Vừa nhặt: " + amount + " " + itemToPickup.itemName);
        }
    }
}