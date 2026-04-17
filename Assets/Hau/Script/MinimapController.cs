using UnityEngine;
using Fusion;
using System.Collections.Generic;

public class MinimapController : MonoBehaviour
{
    [Header("UI Prefabs (Dấu chấm)")]
    public RectTransform mapRect;
    public RectTransform otherPlayerPrefab;
    public RectTransform enemyPrefab;

    [Header("Dấu chấm của bạn (Kéo sẵn vào)")]
    public RectTransform localPlayerIcon;

    [Header("Cài đặt")]
    public float mapScale = 5f;
    public float mapRadius = 90f;

    private Transform localPlayer;
    private Dictionary<Transform, RectTransform> iconMap = new Dictionary<Transform, RectTransform>();

    void Start()
    {
        // Ẩn icon của mình lúc đầu, sẽ hiện khi tìm thấy nhân vật
        if (localPlayerIcon != null) localPlayerIcon.gameObject.SetActive(false);
    }

    void Update()
    {
        AutoRegisterObjects();

        if (localPlayer == null) return;

        UpdateLocalPlayer();
        UpdateAllIcons();
    }

    void AutoRegisterObjects()
    {
        // 1. QUÉT TÌM MỌI THỨ CÓ TAG "Player"
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        // 2. IN RA BÁO CÁO (Để xem Unity có bị mù không)
        Debug.Log("🔍 Đã quét Tag 'Player' trên map. Tìm thấy: " + players.Length + " người!");

        foreach (GameObject pObj in players)
        {
            NetworkObject netObj = pObj.GetComponent<NetworkObject>();
            if (netObj == null) continue;

            // 3. Phân loại Mình và Người Khác
            // (Thêm HasStateAuthority để phòng trường hợp bạn dùng Fusion Shared Mode)
            bool isMe = netObj.HasInputAuthority || (netObj.Runner.Topology == Topologies.Shared && netObj.HasStateAuthority);

            if (isMe)
            {
                localPlayer = pObj.transform;
            }
            else
            {
                CreateIconIfMissing(pObj.transform, otherPlayerPrefab);
            }
        }

        // Quét Quái (Enemy) - Giữ nguyên
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var e in enemies)
        {
            CreateIconIfMissing(e.transform, enemyPrefab);
        }
    }
    void CreateIconIfMissing(Transform target, RectTransform prefab)
    {
        if (target == null || iconMap.ContainsKey(target)) return;

        Debug.Log("✅ Đang tạo icon cho đối tượng: " + target.gameObject.name);

        RectTransform newIcon = Instantiate(prefab, mapRect);
        newIcon.gameObject.SetActive(true);
        newIcon.localScale = Vector3.one; // Chống lỗi teo nhỏ
        newIcon.anchoredPosition3D = Vector3.zero; // Chống lỗi rớt trục Z

        iconMap.Add(target, newIcon);
    }

    void UpdateLocalPlayer()
    {
        if (localPlayerIcon == null)
        {
            Debug.LogError("⚠️ BẠN CHƯA KÉO LOCAL PLAYER ICON VÀO SCRIPT!");
            return;
        }

        localPlayerIcon.gameObject.SetActive(true);
        localPlayerIcon.anchoredPosition = Vector2.zero;
        localPlayerIcon.localScale = Vector3.one;
        localPlayerIcon.SetAsLastSibling(); // Ép nổi lên trên cùng nền map
    }

    void UpdateAllIcons()
    {
        List<Transform> toRemove = new List<Transform>();

        foreach (var item in iconMap)
        {
            Transform target = item.Key;
            RectTransform icon = item.Value;

            if (target == null)
            {
                if (icon != null) Destroy(icon.gameObject);
                toRemove.Add(target);
                continue;
            }

            Vector2 offset = new Vector2(
                target.position.x - localPlayer.position.x,
                target.position.y - localPlayer.position.y
            );

            Vector2 mapPos = offset * mapScale;

            if (mapPos.magnitude > mapRadius)
            {
                mapPos = mapPos.normalized * mapRadius;
            }

            icon.anchoredPosition = mapPos;
        }

        foreach (var t in toRemove) iconMap.Remove(t);
    }
}