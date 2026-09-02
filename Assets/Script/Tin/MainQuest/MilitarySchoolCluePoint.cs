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
    private Coroutine searchRoutine;

    public void Configure(MilitaryBaseQuestManager targetManager, int index, string displayName)
    {
        manager = targetManager;
        clueIndex = index;
        clueName = displayName;
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
            AutoUIManager.Instance?.ShowReloadUI(elapsed, searchDuration, GameLocalization.Get("quest.military.inspecting_clue"));
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

    private void OnGUI()
    {
        if (manager == null || !manager.CanInvestigateSchoolClue(clueIndex) ||
            LocalGameplayUIState.BlocksWorldInteractionHints) return;
        LootContainer.DrawMilitaryWaypointDot(transform.position);
        if (searchRoutine != null) return;
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
        string displayName = GameLocalization.Get(clueName, GameLocalization.TranslateLiteral(clueName));
        string prompt = GameLocalization.Get("quest.military.prompt_hold_inspect");
        GUI.Box(new Rect(x, y, 290f, 46f), $"{displayName}\n{prompt}", style);
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
