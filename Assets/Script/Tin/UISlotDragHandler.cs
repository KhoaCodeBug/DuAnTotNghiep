using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UISlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int slotIndex;
    public bool isFromInventory; // Tick vào nếu là ô Balo, bỏ Tick nếu là ô Tủ đồ

    private GameObject dragIcon;
    private Canvas mainCanvas;

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Kiểm tra thông qua Manager xem ô này thực sự có đồ không
        if (!AutoUIManager.Instance.HasItemAt(slotIndex, isFromInventory)) return;

        mainCanvas = GetComponentInParent<Canvas>();

        // Tìm cái Image con tên là ItemIcon
        Image sourceImg = transform.Find("ItemIcon")?.GetComponent<Image>();
        if (sourceImg == null || sourceImg.sprite == null) return;

        dragIcon = new GameObject("DragIcon");
        dragIcon.transform.SetParent(mainCanvas.transform, false);
        dragIcon.transform.SetAsLastSibling();

        Image img = dragIcon.AddComponent<Image>();
        img.sprite = sourceImg.sprite;
        img.raycastTarget = false;

        RectTransform rt = dragIcon.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60, 60);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIcon == null) return;
        dragIcon.transform.position = Input.mousePosition; // Cho bóng bay theo chuột
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIcon != null) Destroy(dragIcon);

        // Kiểm tra xem lúc buông chuột ra, bên dưới là object nào?
        GameObject target = eventData.pointerCurrentRaycast.gameObject;
        if (target == null) return;

        // Nếu kéo từ Balo thả trúng vùng Tủ đồ (hoặc ngược lại)
        if (isFromInventory && (target.name.Contains("ContSlot") || target.name.Contains("Container")))
        {
            AutoUIManager.Instance.DragItemToContainer(slotIndex);
        }
        else if (!isFromInventory && (target.name.Contains("Slot_") || target.name.Contains("Inventory")))
        {
            AutoUIManager.Instance.DragItemToInventory(slotIndex);
        }
    }
}