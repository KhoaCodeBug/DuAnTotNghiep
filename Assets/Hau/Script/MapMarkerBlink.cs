using UnityEngine;

public class MapMarkerBlink : MonoBehaviour
{
    public float speed = 3f;
    public float minAlpha = 0.3f;
    public float maxAlpha = 1f;

    private SpriteRenderer sr;

    void Start()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(Time.time * speed) + 1) / 2);

        if (sr != null)
        {
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
        transform.forward = Camera.main.transform.forward;
    }
}