using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Labels the three hospital investigation points that the runtime positional
/// ordering selects while their final desk/radio/container art is unavailable.
/// Editor-only: it does not create runtime objects or change network state.
/// </summary>
[InitializeOnLoad]
public static class OfficeMissionPointGizmo
{
    private static readonly string[] Labels =
    {
        "[1] BÀN ĐIỀU PHỐI",
        "[2] RADIO",
        "[3] TỦ HỒ SƠ / BẢN ĐỒ"
    };

    private static readonly Color[] Colors =
    {
        new Color(0.1f, 0.95f, 0.8f, 1f),
        new Color(1f, 0.6f, 0.05f, 1f),
        new Color(0.75f, 0.25f, 1f, 1f)
    };

    static OfficeMissionPointGizmo()
    {
        SceneView.duringSceneGui -= DrawMissionPoints;
        SceneView.duringSceneGui += DrawMissionPoints;
    }

    private static void DrawMissionPoints(SceneView sceneView)
    {
        List<MainQuestSearchCabinet> order = BuildEditorOrder();
        if (order.Count < 3) return;

        for (int stepIndex = 0; stepIndex < 3; stepIndex++)
        {
            MainQuestSearchCabinet point = order[stepIndex];
            if (point == null || !point.gameObject.scene.IsValid()) continue;

            Color color = Colors[stepIndex];
            Vector3 position = point.transform.position;
            Handles.color = color;
            Handles.DrawSolidDisc(position, Vector3.forward, 0.22f);
            Handles.DrawWireDisc(position, Vector3.forward, point.interactionDistance);

            if (stepIndex + 1 < order.Count && order[stepIndex + 1] != null)
                Handles.DrawDottedLine(position, order[stepIndex + 1].transform.position, 5f);

            Vector2 guiPosition = HandleUtility.WorldToGUIPoint(position + Vector3.up * 0.45f);
            Rect labelRect = new Rect(guiPosition.x - 105f, guiPosition.y - 42f, 210f, 42f);
            GUIStyle labelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = color;
            string coordinates = $"({position.x:0.00}, {position.y:0.00})";
            Handles.BeginGUI();
            GUI.Box(labelRect, Labels[stepIndex] + "\n" + coordinates, labelStyle);
            Handles.EndGUI();
        }
    }

    private static List<MainQuestSearchCabinet> BuildEditorOrder()
    {
        MainQuestSearchCabinet[] found = UnityEngine.Object.FindObjectsByType<MainQuestSearchCabinet>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<MainQuestSearchCabinet> candidates = new List<MainQuestSearchCabinet>(found);
        List<MainQuestSearchCabinet> result = new List<MainQuestSearchCabinet>(3);
        TakePoint(candidates, result, (left, right) => left.transform.position.x > right.transform.position.x);
        TakePoint(candidates, result, (left, right) => left.transform.position.y < right.transform.position.y);
        TakePoint(candidates, result, (left, right) => left.transform.position.y > right.transform.position.y);
        return result;
    }

    private static void TakePoint(List<MainQuestSearchCabinet> candidates,
        List<MainQuestSearchCabinet> result, Func<MainQuestSearchCabinet, MainQuestSearchCabinet, bool> prefer)
    {
        if (candidates.Count == 0) return;
        MainQuestSearchCabinet selected = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
            if (prefer(candidates[i], selected))
                selected = candidates[i];
        candidates.Remove(selected);
        result.Add(selected);
    }
}
