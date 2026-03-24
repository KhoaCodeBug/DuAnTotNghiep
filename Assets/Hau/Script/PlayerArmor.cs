using System.Collections.Generic;
using UnityEngine;

public class PlayerArmor : MonoBehaviour
{
    public int maxVisibleArmor = 8; // Hiển thị tối đa 8 icon
    public List<ArmorItem> armorInventory = new List<ArmorItem>();

    public void AddArmor(ArmorItem newArmor)
    {
        if (newArmor == null) return;

        for (int i = 0; i < newArmor.armorValue; i++)
        {
            if (armorInventory.Count >= maxVisibleArmor)
                break;

            armorInventory.Add(newArmor);
        }

        UpdateArmorUI();
    }

    public void UpdateArmorUI()
    {
        // Lấy danh sách sprite từ inventory
        List<Sprite> armorSprites = new List<Sprite>();
        foreach (var a in armorInventory)
        {
            armorSprites.Add(a.icon);
        }

        AutoUIManager.Instance.SetArmorIcons(armorSprites);
    }

    public void RemoveArmor(int index)
    {
        if (index >= 0 && index < armorInventory.Count)
        {
            armorInventory.RemoveAt(index);
            UpdateArmorUI();
        }
    }

    public void ClearArmor()
    {
        armorInventory.Clear();
        UpdateArmorUI();
    }
}