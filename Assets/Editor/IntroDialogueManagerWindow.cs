#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class IntroDialogueManagerWindow : EditorWindow
{
    private const string AssetPath = "Assets/Resources/IntroDialogue/IntroOpeningDialogue.asset";
    private IntroDialogueSequence sequence;
    private SerializedObject serializedSequence;
    private SerializedProperty lines;

    [MenuItem("Tools/Intro/Dialogue Manager")]
    public static void Open()
    {
        GetWindow<IntroDialogueManagerWindow>("Intro Dialogue");
    }

    private void OnEnable() => LoadOrCreate();

    private void LoadOrCreate()
    {
        sequence = AssetDatabase.LoadAssetAtPath<IntroDialogueSequence>(AssetPath);
        if (sequence == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Resources/IntroDialogue");
            sequence = CreateInstance<IntroDialogueSequence>();
            AssetDatabase.CreateAsset(sequence, AssetPath);
            AssetDatabase.SaveAssets();
        }

        serializedSequence = new SerializedObject(sequence);
        lines = serializedSequence.FindProperty("lines");
    }

    private void OnGUI()
    {
        if (sequence == null || serializedSequence == null) LoadOrCreate();

        EditorGUILayout.LabelField("Intro Cinematic Dialogue", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Mỗi lần bấm E sẽ chuyển sang câu tiếp theo. Câu cuối hiện nhắc bấm E để rời xe.", MessageType.Info);

        serializedSequence.Update();
        EditorGUILayout.PropertyField(lines, new GUIContent("Lines"), true);
        serializedSequence.ApplyModifiedProperties();

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Save Dialogue", GUILayout.Height(32f)))
        {
            EditorUtility.SetDirty(sequence);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif