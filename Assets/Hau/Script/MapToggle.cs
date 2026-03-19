using UnityEngine;

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
    public float zOffset = -50;
    public float zoomSpeed = 50f;
    public float minZoom = 20f;
    public float maxZoom = 200f;
    public float dragSpeed = 0.5f;
    public GameObject[] markers;
    private bool isOpen = false;
    private Vector3 lastMousePos;

    void Update()
    {
        // 🔥 Mở / đóng map
        if (Input.GetKeyDown(KeyCode.M))
        {
            isOpen = !isOpen;
            mapUI.SetActive(isOpen);
            playerIcon.SetActive(isOpen);

            if (isOpen)
            {
                SnapToPlayer();
            }
        }

        if (!isOpen) return;

        HandleZoom();
        HandleDrag();
    }

    void LateUpdate()
    {
        // 🔥 Follow nhẹ (giữ player gần center nếu không drag)
        if (isOpen && !Input.GetMouseButton(0))
        {
            mapCamera.transform.position = Vector3.Lerp(
                mapCamera.transform.position,
                new Vector3(player.position.x, player.position.y, zOffset),
                Time.deltaTime * 5f
            );
        }
    }

    void SnapToPlayer()
    {
        mapCamera.transform.position = new Vector3(
            player.position.x,
            player.position.y,
            zOffset
        );

        mapCamera.orthographicSize = defaultZoom;
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0)
        {
            mapCamera.orthographicSize -= scroll * zoomSpeed;
            mapCamera.orthographicSize = Mathf.Clamp(
                mapCamera.orthographicSize,
                minZoom,
                maxZoom
            );
        }
    }

    void HandleDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;

            Vector3 move = new Vector3(-delta.x, -delta.y, 0) * dragSpeed * Time.deltaTime;

            mapCamera.transform.Translate(move);

            lastMousePos = Input.mousePosition;
        }
        foreach (var m in markers)
        {
            m.SetActive(isOpen);
        }
    }
}