using UnityEngine;
using UnityEngine.SceneManagement;

public class MapController : MonoBehaviour
{
    [Header("UI")]
    public GameObject mapUI;
    public GameObject playerIcon;

    [Header("Refs")]
    public Transform player;
    public Camera mapCamera;
    public float defaultZoom = 60f;

    [Header("Settings")]
    public float zOffset = -50f;
    public float zoomSpeed = 50f;
    public float minZoom = 20f;
    public float maxZoom = 200f;
    public float dragSpeed = 0.5f;
    public GameObject[] markers;

    private bool isOpen;
    private bool isUnlocked = true;
    private Vector3 lastMousePos;

    private void Awake()
    {
        // Main bắt đầu mà chưa có bản đồ; MainQuestManager sẽ mở lại sau khi loot được.
        if (SceneManager.GetActiveScene().name == "Main") SetMapUnlocked(false);
    }

    public void SetMapUnlocked(bool unlocked)
    {
        isUnlocked = unlocked;
        if (isUnlocked) return;

        isOpen = false;
        if (mapUI != null) mapUI.SetActive(false);
        if (playerIcon != null) playerIcon.SetActive(false);
        if (markers == null) return;
        for (int i = 0; i < markers.Length; i++)
            if (markers[i] != null) markers[i].SetActive(false);
    }

    private void Update()
    {
        if (RouteBRadioBroadcastUI.BlocksLocalGameplayInput ||
            VehicleRepairSkillCheckUI.BlocksGameplayInput ||
            MilitaryRouteBEscapePresentation.BlocksGameplayInput) return;
        if (!isUnlocked) return;

        if (Input.GetKeyDown(KeyCode.M))
        {
            isOpen = !isOpen;
            if (mapUI != null) mapUI.SetActive(isOpen);
            if (playerIcon != null) playerIcon.SetActive(isOpen);
            if (isOpen) SnapToPlayer();
        }

        if (!isOpen) return;
        HandleZoom();
        HandleDrag();
    }

    private void LateUpdate()
    {
        if (!isUnlocked || !isOpen || Input.GetMouseButton(0) || mapCamera == null || player == null) return;
        mapCamera.transform.position = Vector3.Lerp(
            mapCamera.transform.position,
            new Vector3(player.position.x, player.position.y, zOffset),
            Time.deltaTime * 5f);
    }

    private void SnapToPlayer()
    {
        if (mapCamera == null || player == null) return;
        mapCamera.transform.position = new Vector3(player.position.x, player.position.y, zOffset);
        mapCamera.orthographicSize = defaultZoom;
    }

    private void HandleZoom()
    {
        if (mapCamera == null) return;
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll == 0f) return;

        mapCamera.orthographicSize -= scroll * zoomSpeed;
        mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize, minZoom, maxZoom);
    }

    private void HandleDrag()
    {
        if (Input.GetMouseButtonDown(0)) lastMousePos = Input.mousePosition;
        if (Input.GetMouseButton(0) && mapCamera != null)
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            Vector3 move = new Vector3(-delta.x, -delta.y, 0f) * dragSpeed * Time.deltaTime;
            mapCamera.transform.Translate(move);
            lastMousePos = Input.mousePosition;
        }

        if (markers == null) return;
        foreach (GameObject marker in markers)
            if (marker != null) marker.SetActive(isOpen);
    }
}
