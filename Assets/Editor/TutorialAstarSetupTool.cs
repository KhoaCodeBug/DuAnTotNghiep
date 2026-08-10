using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Copies the proven isometric Grid Graph configuration from Main into the
/// standalone tutorial, scans it against the tutorial colliders, and saves it.
/// </summary>
public static class TutorialAstarSetupTool
{
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string TutorialScenePath = "Assets/Scenes/Intro_Cinematic.unity";

    [MenuItem("Tools/Intro/Sync A* From Main")]
    public static void SyncFromMain()
    {
        Scene originalScene = SceneManager.GetActiveScene();
        string originalPath = originalScene.path;

        if (originalScene.isDirty)
        {
            Debug.LogError("[Tutorial A*] Hãy lưu scene đang mở trước khi đồng bộ A*.");
            return;
        }

        try
        {
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            AstarPath source = Object.FindFirstObjectByType<AstarPath>();
            if (source == null)
                throw new System.InvalidOperationException("Không tìm thấy AstarPath trong Main.");

            // ComponentUtility copies the serialized component, but AstarData's
            // graph list is runtime state. Keep the serialized graph settings so
            // they can be explicitly deserialized after changing scenes.
            byte[] graphSettings = source.data.GetData();
            if (graphSettings == null || graphSettings.Length == 0)
                graphSettings = source.data.SerializeGraphs();
            graphSettings = (byte[])graphSettings.Clone();

            if (!ComponentUtility.CopyComponent(source))
                throw new System.InvalidOperationException("Không copy được cấu hình AstarPath từ Main.");

            Scene tutorialScene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);
            AstarPath target = Object.FindFirstObjectByType<AstarPath>();
            if (target == null)
            {
                GameObject graphObject = new GameObject("A* Tutorial");
                SceneManager.MoveGameObjectToScene(graphObject, tutorialScene);
                if (!ComponentUtility.PasteComponentAsNew(graphObject))
                    throw new System.InvalidOperationException("Không tạo được AstarPath trong tutorial.");
                target = graphObject.GetComponent<AstarPath>();
            }
            else
            {
                if (!ComponentUtility.PasteComponentValues(target))
                    throw new System.InvalidOperationException("Không cập nhật được AstarPath trong tutorial.");
                target.gameObject.name = "A* Tutorial";
            }

            target.scanOnStartup = true;
            AstarPath.active = target;
            target.data.FindGraphTypes();
            target.data.SetData(graphSettings);
            target.data.Awake();
            EditorUtility.SetDirty(target);
            target.Scan();

            GridGraph grid = target.data.gridGraph;
            if (grid == null || grid.nodes == null || grid.nodes.Length == 0)
                throw new System.InvalidOperationException("Grid Graph scan xong nhưng không tạo được node.");

            EditorSceneManager.MarkSceneDirty(tutorialScene);
            EditorSceneManager.SaveScene(tutorialScene);
            Debug.Log($"[Tutorial A*] Đã đồng bộ và scan {grid.nodes.Length:N0} node " +
                      $"({grid.width}x{grid.depth}, nodeSize {grid.nodeSize:0.###}).");
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[Tutorial A*] Đồng bộ thất bại: {exception.Message}\n{exception.StackTrace}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(originalPath))
                EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
        }
    }
}
