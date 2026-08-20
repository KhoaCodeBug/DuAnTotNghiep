using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(QuestMapRevealTuningTool))]
public sealed class QuestMapRevealTuningToolEditor : Editor
{
    private SerializedProperty beforeCenter;
    private SerializedProperty beforeSize;
    private SerializedProperty afterCenter;
    private SerializedProperty afterSize;

    private void OnEnable()
    {
        beforeCenter = serializedObject.FindProperty("beforeQuestCenter");
        beforeSize = serializedObject.FindProperty("beforeQuestSize");
        afterCenter = serializedObject.FindProperty("afterQuestCenter");
        afterSize = serializedObject.FindProperty("afterQuestSize");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "PLAY MODE: mở bản đồ bằng M, sau đó chỉnh các thanh bên dưới. " +
            "Map cập nhật trực tiếp. AFTER QUEST là ô văn phòng mở thêm, không phải vùng gộp.",
            MessageType.Info);

        DrawRegion("BEFORE QUEST  •  KHU SPAWN", beforeCenter, beforeSize, new Color(1f, 0.68f, 0.08f));
        EditorGUILayout.Space(8f);
        DrawRegion("AFTER QUEST  •  KHU VĂN PHÒNG", afterCenter, afterSize, new Color(0.72f, 0.32f, 1f));
        EditorGUILayout.Space(10f);
        DrawPreview();

        if (GUILayout.Button("RESET VỀ GIÁ TRỊ BAN ĐẦU", GUILayout.Height(28f)))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(target, "Reset quest map reveal layout");
            ((QuestMapRevealTuningTool)target).ResetToDefaults();
            EditorUtility.SetDirty(target);
            serializedObject.Update();
        }

        if (serializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(target);
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }

    private static void DrawRegion(string title, SerializedProperty center, SerializedProperty size, Color accent)
    {
        Rect titleRect = EditorGUILayout.GetControlRect(false, 24f);
        EditorGUI.DrawRect(titleRect, new Color(accent.r, accent.g, accent.b, 0.18f));
        EditorGUI.LabelField(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 8f, titleRect.height),
            title, EditorStyles.boldLabel);

        Vector2 centerValue = center.vector2Value;
        Vector2 sizeValue = size.vector2Value;
        centerValue.x = EditorGUILayout.Slider("Center X", centerValue.x, 0f, 1f);
        centerValue.y = EditorGUILayout.Slider("Center Y", centerValue.y, 0f, 1f);
        sizeValue.x = EditorGUILayout.Slider("Width", sizeValue.x, 0.02f, 1f);
        sizeValue.y = EditorGUILayout.Slider("Height", sizeValue.y, 0.02f, 1f);
        center.vector2Value = centerValue;
        size.vector2Value = sizeValue;
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("SƠ ĐỒ HAI VÙNG (đã xoay giống map trong game)", EditorStyles.boldLabel);
        Rect canvas = GUILayoutUtility.GetRect(10f, 190f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(canvas, new Color(0.025f, 0.075f, 0.065f, 1f));
        DrawPreviewRegion(canvas, ToRect(beforeCenter.vector2Value, beforeSize.vector2Value),
            new Color(1f, 0.68f, 0.08f), "BEFORE");
        DrawPreviewRegion(canvas, ToRect(afterCenter.vector2Value, afterSize.vector2Value),
            new Color(0.72f, 0.32f, 1f), "AFTER");
    }

    private static Rect ToRect(Vector2 center, Vector2 size)
    {
        return new Rect(center - size * 0.5f, size);
    }

    private static void DrawPreviewRegion(Rect canvas, Rect normalized, Color color, string label)
    {
        // The runtime art is rotated -90 degrees: local Y becomes display X,
        // while local X becomes the top-to-bottom preview axis.
        Rect display = new Rect(
            canvas.x + normalized.y * canvas.width,
            canvas.y + normalized.x * canvas.height,
            normalized.height * canvas.width,
            normalized.width * canvas.height);
        EditorGUI.DrawRect(display, new Color(color.r, color.g, color.b, 0.20f));
        DrawBorder(display, color, 2f);
        EditorGUI.LabelField(new Rect(display.x + 5f, display.y + 3f, display.width - 10f, 18f), label,
            EditorStyles.miniBoldLabel);
    }

    private static void DrawBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}
