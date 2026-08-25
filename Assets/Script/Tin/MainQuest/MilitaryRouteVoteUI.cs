using UnityEngine;

/// <summary>Local presentation for the authoritative unanimous Route-B vote.</summary>
public sealed class MilitaryRouteVoteUI : MonoBehaviour
{
    private static MilitaryRouteVoteUI instance;

    private MilitaryBaseQuestManager manager;
    private int voteId;
    private int approvedCount;
    private int requiredCount;
    private bool submittedApproval;
    private bool visible;

    public static bool IsVisible => instance != null && instance.visible;

    public static void Show(MilitaryBaseQuestManager owner, int id, int approved, int required)
    {
        if (instance == null)
        {
            GameObject host = new GameObject("Military Route Unanimous Vote UI");
            instance = host.AddComponent<MilitaryRouteVoteUI>();
        }
        instance.manager = owner;
        instance.voteId = id;
        instance.approvedCount = approved;
        instance.requiredCount = Mathf.Max(1, required);
        instance.submittedApproval = false;
        instance.visible = true;
        QuestFlowUIPrototype.Instance?.CloseAllQuestOverlays();
        AutoUIManager.Instance?.SetQuestOverlayOpen(true);
    }

    public static void UpdateProgress(int id, int approved, int required)
    {
        if (instance == null || !instance.visible || instance.voteId != id) return;
        instance.approvedCount = Mathf.Max(0, approved);
        instance.requiredCount = Mathf.Max(1, required);
    }

    public static void Close(int id = -1)
    {
        if (instance == null || (id >= 0 && instance.voteId != id)) return;
        instance.visible = false;
        instance.manager = null;
        AutoUIManager.Instance?.SetQuestOverlayOpen(false);
    }

    private void Update()
    {
        if (!visible || manager == null) return;
        if (!submittedApproval && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Y)))
            Submit(true);
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.X))
            Submit(false);
    }

    private void Submit(bool approve)
    {
        if (manager == null) return;
        if (approve) submittedApproval = true;
        manager.RequestSubmitMilitaryRouteVote(voteId, approve);
    }

    private void OnGUI()
    {
        if (!visible) return;

        int oldDepth = GUI.depth;
        Color oldColor = GUI.color;
        GUI.depth = -4500;
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        float width = Mathf.Min(760f, Screen.width - 32f);
        float height = 390f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panel, GUIContent.none);

        GUIStyle eyebrow = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        eyebrow.normal.textColor = new Color(1f, 0.68f, 0.16f);
        GUI.Label(new Rect(panel.x + 30f, panel.y + 28f, panel.width - 60f, 28f),
            "ĐIỂM KHÔNG THỂ QUAY LẠI  //  BIỂU QUYẾT TOÀN ĐỘI", eyebrow);

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        title.normal.textColor = Color.white;
        GUI.Label(new Rect(panel.x + 38f, panel.y + 70f, panel.width - 76f, 62f),
            "TIẾP TỤC TUYẾN CỐT TRUYỆN QUÂN SỰ?", title);

        GUIStyle body = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 16,
            wordWrap = true
        };
        body.normal.textColor = new Color(0.82f, 0.86f, 0.84f);
        GUI.Label(new Rect(panel.x + 58f, panel.y + 142f, panel.width - 116f, 90f),
            "Nếu toàn đội đồng ý, Flow cốt truyện chính sẽ bắt đầu và không thể quay lại tuyến tự do trong session này. " +
            "Chỉ cần một người từ chối, biểu quyết sẽ hủy và xe có thể được kiểm tra lại sau.", body);

        GUIStyle counter = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        counter.normal.textColor = submittedApproval
            ? new Color(0.42f, 1f, 0.55f)
            : new Color(1f, 0.83f, 0.32f);
        string status = submittedApproval
            ? $"ĐÃ ĐỒNG Ý  •  ĐANG CHỜ {approvedCount}/{requiredCount}"
            : $"PHIẾU ĐỒNG Ý: {approvedCount}/{requiredCount}";
        GUI.Label(new Rect(panel.x + 40f, panel.y + 240f, panel.width - 80f, 36f), status, counter);

        if (!submittedApproval && GUI.Button(new Rect(panel.x + panel.width - 330f, panel.y + 304f, 280f, 54f),
                "[ENTER / Y]  ĐỒNG Ý"))
            Submit(true);
        if (GUI.Button(new Rect(panel.x + 50f, panel.y + 304f, 230f, 54f), "[ESC / N]  TỪ CHỐI"))
            Submit(false);

        GUI.color = oldColor;
        GUI.depth = oldDepth;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
