using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable, fill-free highlight for a vehicle part on a UI diagram.
/// It uses a restrained outline pulse so the source artwork remains visible.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class VehiclePartPulseHighlight : MonoBehaviour
{
    [SerializeField] private Color highlightColor = new Color(1f, 0.2f, 0.14f, 1f);
    [SerializeField, Min(1f)] private float borderThickness = 2f;
    [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.28f;
    [SerializeField, Range(0f, 1f)] private float maximumAlpha = 0.92f;
    [SerializeField, Min(0.1f)] private float pulsesPerSecond = 0.85f;
    // Keep the highlight exactly on the source artwork frame. Only alpha pulses;
    // scaling would make the outline spill outside the white component box.
    [SerializeField, Range(0f, 0.08f)] private float scaleAmount;

    private readonly Image[] borderImages = new Image[4];
    private RectTransform rectTransform;
    private Vector3 restingScale;

    public void Configure(Color color, float thickness = 2f)
    {
        highlightColor = color;
        borderThickness = Mathf.Max(1f, thickness);
        EnsureBorder();
        LayoutBorder();
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        restingScale = rectTransform.localScale;
        EnsureBorder();
        LayoutBorder();
    }

    private void OnEnable()
    {
        rectTransform = GetComponent<RectTransform>();
        restingScale = rectTransform.localScale;
    }

    private void OnDisable()
    {
        if (rectTransform != null) rectTransform.localScale = restingScale;
    }

    private void Update()
    {
        if (rectTransform == null) return;

        float wave = (Mathf.Sin(Time.unscaledTime * pulsesPerSecond * Mathf.PI * 2f) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minimumAlpha, maximumAlpha, wave);
        Color animatedColor = highlightColor;
        animatedColor.a *= alpha;

        for (int i = 0; i < borderImages.Length; i++)
            if (borderImages[i] != null) borderImages[i].color = animatedColor;

        rectTransform.localScale = restingScale * (1f + scaleAmount * wave);
    }

    private void EnsureBorder()
    {
        for (int i = 0; i < borderImages.Length; i++)
        {
            if (borderImages[i] != null) continue;
            string sideName = i == 0 ? "Top" : i == 1 ? "Bottom" : i == 2 ? "Left" : "Right";
            GameObject side = new GameObject(sideName + " Border", typeof(RectTransform), typeof(Image));
            side.transform.SetParent(transform, false);
            borderImages[i] = side.GetComponent<Image>();
            borderImages[i].raycastTarget = false;
        }
    }

    private void LayoutBorder()
    {
        SetSide(borderImages[0].rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -borderThickness), new Vector2(0f, 0f));
        SetSide(borderImages[1].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, borderThickness));
        SetSide(borderImages[2].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(borderThickness, 0f));
        SetSide(borderImages[3].rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-borderThickness, 0f), new Vector2(0f, 0f));
    }

    private static void SetSide(RectTransform side, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        side.anchorMin = anchorMin;
        side.anchorMax = anchorMax;
        side.offsetMin = offsetMin;
        side.offsetMax = offsetMax;
    }
}
