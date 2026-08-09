using UnityEditor;
using UnityEngine;

/// <summary>
/// Designer-facing editor for every title/body/objective used by the first
/// tutorial phase. It edits only the ScriptableObject asset, never code.
/// </summary>
public sealed class TutorialPhaseOneTextManagerWindow : EditorWindow
{
    private const string TextAssetPath = "Assets/Resources/Tutorial/TutorialPhaseOneText.asset";
    private TutorialPhaseOneText textAsset;
    private SerializedObject serializedText;
    private Vector2 scroll;

    [MenuItem("Tools/Intro/Phase 1 Text Manager")]
    private static void Open()
    {
        GetWindow<TutorialPhaseOneTextManagerWindow>("Tutorial Text").minSize = new Vector2(560f, 500f);
    }

    private void OnEnable() => LoadAsset();

    private void OnGUI()
    {
        EditorGUILayout.LabelField("PHASE 1 — TUTORIAL TEXT", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Sửa toàn bộ lời hiện trên box, objective và marker ở đây. Thay đổi được lưu vào asset, không cần mở code.", MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reload")) LoadAsset();
            GUI.enabled = textAsset != null;
            if (GUILayout.Button("Select Asset")) Selection.activeObject = textAsset;
            GUI.enabled = true;
        }

        if (textAsset == null)
        {
            EditorGUILayout.HelpBox("Không tìm thấy TutorialPhaseOneText.asset. Hãy tạo lại asset ở Resources/Tutorial.", MessageType.Error);
            return;
        }

        serializedText.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSection("DI CHUYỂN", "moveTitle", "moveBrief", "moveObjective");
        DrawSection("TẦM NHÌN", "zoomTitle", "zoomBrief", "zoomObjective");
        DrawSection("QUAN SÁT", "aimTitle", "aimBrief", "aimObjective");
        DrawSection("ĐÓI VÀ KHÁT", "needsTitle", "needsBrief", "needsFocusTitle", "needsFocusBody");
        DrawSection("NGÔI NHÀ & TỦ BẾP", "houseTitle", "houseBrief", "houseObjective", "houseMarker", "cabinetTitle", "cabinetBrief", "lootObjective", "cabinetMarker");
        DrawSection("DÙNG ĐỒ", "consumeTitle", "consumeBrief", "consumeObjective");
        DrawSection("TRANG BỊ & NẠP ĐẠN", "weaponTitle", "weaponBrief", "weaponObjective", "reloadTitle", "reloadBrief", "reloadObjective");
        DrawSection("ZOMBIE ĐẦU TIÊN", "leaveHouseTitle", "leaveHouseBrief", "leaveHouseObjective", "noiseTitle", "noiseBrief", "noiseObjective", "sneakTitle", "sneakBrief", "sneakObjective", "meleeTitle", "meleeBrief", "meleeObjective");
        DrawSection("HOÀN THÀNH HIỆN TẠI", "completeTitle", "completeBrief");
        EditorGUILayout.EndScrollView();

        if (serializedText.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(textAsset);
            AssetDatabase.SaveAssets();
        }
    }

    private void DrawSection(string heading, params string[] propertyNames)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
        foreach (string propertyName in propertyNames)
        {
            SerializedProperty property = serializedText.FindProperty(propertyName);
            if (property != null) EditorGUILayout.PropertyField(property, true);
        }
    }

    private void LoadAsset()
    {
        textAsset = AssetDatabase.LoadAssetAtPath<TutorialPhaseOneText>(TextAssetPath);
        serializedText = textAsset != null ? new SerializedObject(textAsset) : null;
    }
}
