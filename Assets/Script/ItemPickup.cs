using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public Item item;         // Món đồ sẽ nhận được
    public int amount = 1;    // Số lượng nhận được khi nhặt

    // Hàm này tự chạy khi có vật thể khác chạm vào
    void OnTriggerEnter(Collider other)
    {
        // Đảm bảo chỉ có Player mới nhặt được đồ
        if (other.CompareTag("Player"))
        {
            PickUp();
        }
    }

    void PickUp()
    {
        // Gọi hàm Add từ balo của người chơi
        bool wasPickedUp = Inventory.instance.Add(item, amount);

        // Nếu nhặt thành công (túi chưa đầy), thì xóa cục đồ dưới đất đi
        if (wasPickedUp)
        {
            Destroy(gameObject);
        }
    }
}