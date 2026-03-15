using UnityEngine;

// Dòng này giúp bạn tạo vật phẩm bằng chuột phải trong Editor
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class Item : ScriptableObject
{
    public string itemName = "Tên Vật Phẩm";
    public Sprite icon = null;               // Hình ảnh
    public bool isStackable = false;         // Có cho phép cộng dồn không? (VD: Đạn)

    // Chức năng dùng đồ (sẽ phát triển sau)
    public virtual void Use()
    {
        Debug.Log("Đang sử dụng: " + itemName);
    }
}