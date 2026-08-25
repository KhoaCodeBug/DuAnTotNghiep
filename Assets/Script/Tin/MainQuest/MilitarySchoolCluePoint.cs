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
        if (manager == null || !manager.CanCollectSchoolClue(clueIndex) ||
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
        manager.RequestCollectSchoolClue(clueIndex);
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
        marker.color = new Color(0.98f, 0.78f, 0.2f, 0.9f);
        marker.sortingOrder = 35;
    }

    private void OnGUI()
    {
        if (manager == null || !manager.CanCollectSchoolClue(clueIndex) || searchRoutine != null ||
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
        Texture2D texture = new Texture2D(18, 14, TextureFormat.RGBA32, false)
        {
            name = "MILITARY_CLUE_MARKER_RUNTIME",
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.DontSave
        };
        Color32[] pixels = new Color32[18 * 14];
        Color32 paper = new Color32(232, 216, 164, 255);
        Color32 ink = new Color32(72, 66, 53, 255);
        for (int y = 0; y < 14; y++)
        for (int x = 0; x < 18; x++)
        {
            bool inside = x >= 1 && x <= 16 && y >= 1 && y <= 12;
            bool line = inside && y >= 4 && y <= 10 && y % 2 == 0 && x >= 4 && x <= 13;
            pixels[y * 18 + x] = !inside ? new Color32(0, 0, 0, 0) : line ? ink : paper;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 18f, 14f), new Vector2(0.5f, 0.5f), 18f);
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
        manager.RequestConfirmSchoolRoofExit();
    }
}
