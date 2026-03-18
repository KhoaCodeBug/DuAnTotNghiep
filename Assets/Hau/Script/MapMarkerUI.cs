using UnityEngine;
using UnityEngine.UI;

public class MapMarkerUI : MonoBehaviour
{
    public Transform target;           // object ngoài world
    public RectTransform mapRect;      // Map UI
    public RectTransform marker;       // chấm đỏ

    [Header("World Range")]
    public Vector2 worldMin;
    public Vector2 worldMax;

    private Image img;

    void Start()
    {
        if (marker != null)
            img = marker.GetComponent<Image>();
    }

    void Update()
    {
        // 🔥 KHÔNG BAO GIỜ CHO NULL
        if (target == null)
        {
            Debug.LogError("❌ target NULL");
            return;
        }

        if (mapRect == null)
        {
            Debug.LogError("❌ mapRect NULL");
            return;
        }

        if (marker == null)
        {
            Debug.LogError("❌ marker NULL");
            return;
        }

        // 🔥 convert world → %
        float percentX = Mathf.InverseLerp(worldMin.x, worldMax.x, target.position.x);
        float percentY = Mathf.InverseLerp(worldMin.y, worldMax.y, target.position.y);

        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        float posX = (percentX - 0.5f) * mapRect.sizeDelta.x;
        float posY = (percentY - 0.5f) * mapRect.sizeDelta.y;

        marker.anchoredPosition = new Vector2(posX, posY);
        // 🔥 NHẤP NHÁY
        if (img != null)
        {
            float t = (Mathf.Sin(Time.time * 3f) + 1) / 2;
            img.color = new Color(1, 0, 0, t);
        }
    }
}