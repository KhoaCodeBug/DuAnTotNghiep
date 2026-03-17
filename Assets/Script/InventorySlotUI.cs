using UnityEngine;
using UnityEngine.UI;
using TMPro; // Dùng dòng này nếu bạn xài TextMeshPro, nếu xài Text thường thì xóa đi

public class InventorySlotUI : MonoBehaviour
{
    public Image itemIcon;
    public TextMeshProUGUI amountText; // Nếu xài Text thường thì đổi thành: public Text amountText;

    // Hàm này sẽ được gọi để cập nhật thông tin hiển thị lên ô
    public void UpdateSlot(InventorySlot slot)
    {
        if (slot != null && slot.item != null)
        {
            // Bật icon và gán hình ảnh của vật phẩm vào
            itemIcon.gameObject.SetActive(true);
            itemIcon.sprite = slot.item.icon;

            // Nếu số lượng > 1 thì mới hiện số, còn 1 cái thì ẩn số đi cho đẹp
            if (slot.amount > 1)
            {
                amountText.gameObject.SetActive(true);
                amountText.text = slot.amount.ToString();
            }
            else
            {
                amountText.gameObject.SetActive(false);
            }
        }
        else
        {
            // Nếu ô trống thì tắt icon và chữ đi
            itemIcon.gameObject.SetActive(false);
            amountText.gameObject.SetActive(false);
        }
    }
}