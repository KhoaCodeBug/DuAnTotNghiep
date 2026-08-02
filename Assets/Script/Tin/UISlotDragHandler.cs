using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UISlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum Location { Inventory, Hotbar, Container }
    public Location slotLocation = Location.Inventory;
    public int slotIndex; // Real index in localInventory.slots (0..4 for Hotbar, 5..19 for Inventory)

    private GameObject dragIcon;
    private Canvas mainCanvas;

    public void OnBeginDrag(PointerEventData eventData)
    {
        bool hasItem = false;
        Sprite iconSprite = null;

        if (slotLocation == Location.Inventory || slotLocation == Location.Hotbar)
        {
            hasItem = AutoUIManager.Instance.HasItemAtRealIndex(slotIndex);
            Image sourceImg = transform.Find("ItemIcon")?.GetComponent<Image>();
            if (sourceImg == null) sourceImg = transform.Find("Icon")?.GetComponent<Image>();
            if (sourceImg != null) iconSprite = sourceImg.sprite;
        }
        else if (slotLocation == Location.Container)
        {
            hasItem = AutoUIManager.Instance.HasItemInContainerAt(slotIndex);
            Image sourceImg = transform.Find("ItemIcon")?.GetComponent<Image>();
            if (sourceImg != null) iconSprite = sourceImg.sprite;
        }

        if (!hasItem || iconSprite == null) return;

        mainCanvas = GetComponentInParent<Canvas>();
        if (mainCanvas == null) return;

        dragIcon = new GameObject("DragIcon");
        dragIcon.transform.SetParent(mainCanvas.transform, false);
        dragIcon.transform.SetAsLastSibling();

        Image img = dragIcon.AddComponent<Image>();
        img.sprite = iconSprite;
        img.raycastTarget = false;

        RectTransform rt = dragIcon.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60, 60);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIcon == null || mainCanvas == null) return;
        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(mainCanvas.transform as RectTransform, eventData.position, mainCanvas.worldCamera, out pos);
        dragIcon.transform.localPosition = pos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragIcon != null) Destroy(dragIcon);

        GameObject target = eventData.pointerCurrentRaycast.gameObject;
        if (target == null) return;

        Transform current = target.transform;
        UISlotDragHandler targetHandler = null;

        while (current != null)
        {
            targetHandler = current.GetComponent<UISlotDragHandler>();
            if (targetHandler != null) break;
            current = current.parent;
        }

        if (targetHandler != null)
        {
            // Drop onto specific slot
            int targetIndex = targetHandler.slotIndex;
            Location targetLoc = targetHandler.slotLocation;

            if ((slotLocation == Location.Inventory || slotLocation == Location.Hotbar) &&
                (targetLoc == Location.Inventory || targetLoc == Location.Hotbar))
            {
                // Swap/move between Inventory and Hotbar or within Hotbar
                AutoUIManager.Instance.SwapPlayerSlots(slotIndex, targetIndex);
            }
            else if (slotLocation == Location.Inventory && targetLoc == Location.Container)
            {
                AutoUIManager.Instance.DragItemToContainer(slotIndex);
            }
            else if (slotLocation == Location.Container && (targetLoc == Location.Inventory || targetLoc == Location.Hotbar))
            {
                AutoUIManager.Instance.DragItemToInventory(slotIndex);
            }
        }
        else
        {
            // Dropped onto panel area (not on specific slot)
            Transform areaCheck = target.transform;
            bool onInventoryPanel = false;
            bool onHotbarPanel = false;
            bool onContainerPanel = false;

            while (areaCheck != null)
            {
                if (areaCheck.name.Contains("InventoryPanel") || areaCheck.name.Contains("InvSlot") || areaCheck.name.Contains("SlotGrid")) onInventoryPanel = true;
                if (areaCheck.name.Contains("HotbarPanel") || areaCheck.name.Contains("HotbarSlot")) onHotbarPanel = true;
                if (areaCheck.name.Contains("ContainerPanel") || areaCheck.name.Contains("ContSlot")) onContainerPanel = true;
                areaCheck = areaCheck.parent;
            }

            if ((slotLocation == Location.Hotbar && onInventoryPanel) || (slotLocation == Location.Inventory && onHotbarPanel))
            {
                AutoUIManager.Instance.MoveToFirstAvailableSlot(slotIndex, onHotbarPanel ? Location.Hotbar : Location.Inventory);
            }
            else if (slotLocation == Location.Container && (onInventoryPanel || onHotbarPanel))
            {
                AutoUIManager.Instance.DragItemToInventory(slotIndex);
            }
            else if ((slotLocation == Location.Inventory || slotLocation == Location.Hotbar) && onContainerPanel)
            {
                AutoUIManager.Instance.DragItemToContainer(slotIndex);
            }
        }
    }
}