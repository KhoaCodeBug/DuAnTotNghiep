using UnityEngine;
using System.Collections;
using UnityEngine.Tilemaps;

public class RoofVisibility : MonoBehaviour
{
    public Tilemap[] roofTilemaps;

    private int counter = 0;
    private Coroutine fadeCoroutine;

    public float fadeDuration = 0.3f;
    public float targetAlpha = 0f;

    public void EnterRoof()
    {
        counter++;

        if (counter == 1)
        {
            Fade(false); // fade out
        }
    }

    public void ExitRoof()
    {
        counter--;

        if (counter <= 0)
        {
            counter = 0;
            Fade(true); // fade in
        }
    }

    void Fade(bool fadeIn)
    {
        if (roofTilemaps == null || roofTilemaps.Length == 0)
            return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeRoutine(fadeIn));
    }

    IEnumerator FadeRoutine(bool fadeIn)
    {
        float start = roofTilemaps[0].color.a;
        float end = fadeIn ? 1f : targetAlpha;

        float t = 0;

        while (t < fadeDuration)
        {
            float a = Mathf.Lerp(start, end, t / fadeDuration);

            foreach (var tilemap in roofTilemaps)
            {
                if (tilemap == null) continue;

                Color c = tilemap.color;
                c.a = a;
                tilemap.color = c;
            }

            t += Time.deltaTime;
            yield return null;
        }

        // set final
        foreach (var tilemap in roofTilemaps)
        {
            if (tilemap == null) continue;

            Color c = tilemap.color;
            c.a = end;
            tilemap.color = c;
        }
    }
}