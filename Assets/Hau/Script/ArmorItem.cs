using UnityEngine;

[CreateAssetMenu(fileName = "NewArmor", menuName = "Items/Armor")]
public class ArmorItem : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public int armorValue = 1;
}