using UnityEngine;
using UnityEngine.EventSystems;

// Thêm IPointerClickHandler vào danh sách
public class SlotHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public int slotIndex;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.ShowTooltip(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.HideTooltip();
    }

    // HÀM MỚI: Bắt sự kiện Click chuột
    public void OnPointerClick(PointerEventData eventData)
    {
        // Kiểm tra nếu là Chuột Phải (Right Click)
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (AutoUIManager.Instance != null)
            {
                AutoUIManager.Instance.HideTooltip(); // Tắt bảng thông tin
                AutoUIManager.Instance.ShowContextMenu(slotIndex); // Bật Menu Use/Drop
            }
        }
    }
}