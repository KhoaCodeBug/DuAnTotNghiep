using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private readonly Dictionary<Transform, RectTransform> iconMap = new Dictionary<Transform, RectTransform>();
    private Canvas minimapCanvas;
    private Camera minimapCamera;
    private bool isUnlocked = true;

    private void Awake()
    {
        minimapCanvas = GetComponent<Canvas>();
        minimapCamera = GetComponentInParent<Camera>();
        // Khóa ngay từ Awake để minimap không lóe lên trước khi state quest được spawn.
        if (SceneManager.GetActiveScene().name == "Main") SetMapUnlocked(false);
    }

    private void Start()
    {
        if (localPlayerIcon != null) localPlayerIcon.gameObject.SetActive(false);
    }

    public void SetMapUnlocked(bool unlocked)
    {
        if (isUnlocked == unlocked && minimapCanvas != null && minimapCanvas.enabled == unlocked) return;

        isUnlocked = unlocked;
        if (minimapCanvas != null) minimapCanvas.enabled = unlocked;
        if (minimapCamera != null) minimapCamera.enabled = unlocked;

        if (!unlocked)
        {
            if (localPlayerIcon != null) localPlayerIcon.gameObject.SetActive(false);
            foreach (RectTransform icon in iconMap.Values)
                if (icon != null) icon.gameObject.SetActive(false);
        }
        else
        {
            foreach (RectTransform icon in iconMap.Values)
                if (icon != null) icon.gameObject.SetActive(true);
        }
    }

    private void Update()
    {
        if (!isUnlocked) return;

        AutoRegisterObjects();
        if (localPlayer == null) return;
        UpdateLocalPlayer();
        UpdateAllIcons();
    }

    private void AutoRegisterObjects()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject playerObject in players)
        {
            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsValid || networkObject.Runner == null) continue;

            bool isMe = networkObject.HasInputAuthority ||
                        (networkObject.Runner.Topology == Topologies.Shared && networkObject.HasStateAuthority);
            if (isMe) localPlayer = playerObject.transform;
            else CreateIconIfMissing(playerObject.transform, otherPlayerPrefab);
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
            CreateIconIfMissing(enemy.transform, enemyPrefab);
    }

    private void CreateIconIfMissing(Transform target, RectTransform prefab)
    {
        if (target == null || prefab == null || mapRect == null || iconMap.ContainsKey(target)) return;

        RectTransform newIcon = Instantiate(prefab, mapRect);
        newIcon.gameObject.SetActive(true);
        newIcon.localScale = Vector3.one;
        newIcon.anchoredPosition3D = Vector3.zero;
        iconMap.Add(target, newIcon);
    }

    private void UpdateLocalPlayer()
    {
        if (localPlayerIcon == null) return;
        localPlayerIcon.gameObject.SetActive(true);
        localPlayerIcon.anchoredPosition = Vector2.zero;
        localPlayerIcon.localScale = Vector3.one;
        localPlayerIcon.SetAsLastSibling();
    }

    private void UpdateAllIcons()
    {
        List<Transform> toRemove = new List<Transform>();
        foreach (KeyValuePair<Transform, RectTransform> item in iconMap)
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
                target.position.y - localPlayer.position.y);
            Vector2 mapPosition = offset * mapScale;
            if (mapPosition.magnitude > mapRadius)
                mapPosition = mapPosition.normalized * mapRadius;
            icon.anchoredPosition = mapPosition;
        }

        foreach (Transform target in toRemove) iconMap.Remove(target);
    }
}
