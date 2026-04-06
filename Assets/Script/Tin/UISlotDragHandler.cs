using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UISlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int slotIndex;
    public bool isFromInventory;

    private GameObject dragIcon;
    private Canvas mainCanvas;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!AutoUIManager.Instance.HasItemAt(slotIndex, isFromInventory)) return;

        mainCanvas = GetComponentInParent<Canvas>();

        Image sourceImg = transform.Find("ItemIcon")?.GetComponent<Image>();
        if (sourceImg == null || sourceImg.sprite == null) return;

        dragIcon = new GameObject("DragIcon");
        dragIcon.transform.SetParent(mainCanvas.transform, false);
        dragIcon.transform.SetAsLastSibling();

        Image img = dragIcon.AddComponent<Image>();
        img.sprite = sourceImg.sprite;
        img.raycastTarget = false; // Phải tắt cái này chuột mới xuyên qua được

        RectTransform rt = dragIcon.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60, 60);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIcon == null) return;

        // Cho hình bay theo chuột
        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(mainCanvas.transform as RectTransform, eventData.position, mainCanvas.worldCamera, out pos);
        dragIcon.transform.localPosition = pos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIcon != null) Destroy(dragIcon);

        GameObject target = eventData.pointerCurrentRaycast.gameObject;
        if (target == null) return;

        // 🔥 BẮT ĐẦU FIX: Dò ngược lên các object cha xem có trúng vùng Balo hay Tủ không
        Transform current = target.transform;
        bool dropToContainer = false;
        bool dropToInventory = false;

        while (current != null)
        {
            if (current.name.Contains("ContSlot") || current.name.Contains("ContainerPanel")) dropToContainer = true;
            if (current.name.Contains("Slot_") || current.name.Contains("InventoryPanel")) dropToInventory = true;
            current = current.parent;
        }

        // Thực thi lệnh dựa theo vùng thả chuột
        if (isFromInventory && dropToContainer)
        {
            AutoUIManager.Instance.DragItemToContainer(slotIndex);
        }
        else if (!isFromInventory && dropToInventory)
        {
            AutoUIManager.Instance.DragItemToInventory(slotIndex);
        }
    }
}