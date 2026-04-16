using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Loot Table", menuName = "Survival Game/Loot Table")]
public class LootTableSO : ScriptableObject
{
    [Header("Danh sách đồ rớt")]
    public List<LootContainer.LootSpawnData> lootRules = new List<LootContainer.LootSpawnData>();
}