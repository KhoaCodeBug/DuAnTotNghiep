using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MilitarySchoolCluePoint : MonoBehaviour
{
    [SerializeField, Min(0.2f)] private float interactionDistance = 1.25f;
    [SerializeField, Min(0.1f)] private float searchDuration = 0.9f;

    private MilitaryBaseQuestManager manager;
    private int clueIndex;
    private string clueName;
    private SpriteRenderer marker;
    private Coroutine searchRoutine;

    public void Configure(MilitaryBaseQuestManager targetManager, int index, string displayName)
    {
        manager = targetManager;
        clueIndex = index;
        clueName = displayName;
        BuildMarker();
    }

    private void Update()
    {
        if (manager == null || !manager.CanInvestigateSchoolClue(clueIndex) ||
            LocalGameplayUIState.BlocksWorldInteractionHints)
        {
            CancelSearch();
            return;
        }

        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        if (player == null || Vector2.Distance(player.transform.position, transform.position) > interactionDistance)
        {
            CancelSearch();
            return;
        }

        if (searchRoutine == null && Input.GetKeyDown(KeyCode.E))
            searchRoutine = StartCoroutine(SearchRoutine(player));
    }

    private IEnumerator SearchRoutine(PlayerMovement player)
    {
        if (AutoUIManager.Instance != null) AutoUIManager.Instance.isDoingAction = true;
        float elapsed = 0f;
        while (elapsed < searchDuration)
        {
            if (player == null || !Input.GetKey(KeyCode.E) ||
                Vector2.Distance(player.transform.position, transform.position) > interactionDistance)
            {
                EndSearchPresentation();
                searchRoutine = null;
                yield break;
            }

            elapsed = Mathf.Min(searchDuration, elapsed + Time.unscaledDeltaTime);
            AutoUIManager.Instance?.ShowReloadUI(elapsed, searchDuration, "ĐANG KIỂM TRA MANH MỐI...");
            yield return null;
        }

        EndSearchPresentation();
        searchRoutine = null;
        manager.RequestInvestigateSchoolClue(clueIndex);
    }

    private void CancelSearch()
    {
        if (searchRoutine == null) return;
        StopCoroutine(searchRoutine);
        searchRoutine = null;
        EndSearchPresentation();
    }

    private static void EndSearchPresentation()
    {
        if (AutoUIManager.Instance == null) return;
        AutoUIManager.Instance.HideReloadUI();
        AutoUIManager.Instance.isDoingAction = false;
    }

    private void BuildMarker()
    {
        if (marker != null) return;
        marker = gameObject.AddComponent<SpriteRenderer>();
        marker.sprite = CreateMarkerSprite();
        marker.color = Color.white;
        marker.sortingOrder = 35;
    }

    private void OnGUI()
    {
        if (manager == null || !manager.CanInvestigateSchoolClue(clueIndex) || searchRoutine != null ||
            LocalGameplayUIState.BlocksWorldInteractionHints) return;
        PlayerMovement player = PlayerMovement.LocalPlayerInstance;
        Camera camera = Camera.main;
        if (player == null || camera == null ||
            Vector2.Distance(player.transform.position, transform.position) > interactionDistance) return;

        Vector3 screen = camera.WorldToScreenPoint(transform.position);
        if (screen.z <= 0f) return;
        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(1f, 0.86f, 0.38f);
        float x = Mathf.Clamp(screen.x - 145f, 8f, Screen.width - 298f);
        float y = Mathf.Clamp(Screen.height - screen.y - 66f, 8f, Screen.height - 54f);
        GUI.Box(new Rect(x, y, 290f, 46f), $"{clueName}\nGIỮ [E] ĐỂ KIỂM TRA", style);
    }

    private static Sprite CreateMarkerSprite()
    {
        const int size = 10;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "MILITARY_CLUE_MARKER_RUNTIME",
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.DontSave
        };
        Color32[] pixels = new Color32[size * size];
        Color32 fill = new Color32(255, 205, 38, 255);
        Color32 edge = new Color32(119, 83, 5, 255);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float radius = Vector2.Distance(new Vector2(x, y), center);
            pixels[y * size + x] = radius > 4.5f
                ? new Color32(0, 0, 0, 0)
                : radius > 3.65f ? edge : fill;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 20f);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}

[DisallowMultipleComponent]
public sealed class MilitarySchoolRoofExitTrigger : MonoBehaviour
{
    private MilitaryBaseQuestManager manager;

    public void Configure(MilitaryBaseQuestManager targetManager) => manager = targetManager;

    private void OnTriggerExit2D(Collider2D other)
    {
        if (manager == null) return;
        PlayerMovement player = other != null ? other.GetComponentInParent<PlayerMovement>() : null;
        if (player == null || player.Object == null || !player.Object.IsValid || !player.Object.HasInputAuthority)
            return;
        manager.RequestHandleSchoolRoofExit();
    }
}
