using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IntroRoadLoopBaker
{
    [MenuItem("Tools/Intro/Bake Trailer Road Loop")]
    private static void Bake()
    {
        IntroTutorialDirector director = Object.FindFirstObjectByType<IntroTutorialDirector>(FindObjectsInactive.Include);
        if (director == null)
        {
            Debug.LogError("Open Intro_Cinematic before baking the trailer road loop.");
            return;
        }

        IntroRoadLooper looper = director.GetComponent<IntroRoadLooper>();
        if (looper == null) looper = Undo.AddComponent<IntroRoadLooper>(director.gameObject);

        if (looper.LoopRoot != null)
        {
            Undo.DestroyObjectImmediate(looper.LoopRoot);
            looper.ClearPreparedChunksReference();
        }

        if (!looper.Prepare())
        {
            Debug.LogError("Trailer road loop bake failed. Check Grid, CarStart and CarStop.");
            return;
        }

        EditorUtility.SetDirty(looper);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        EditorSceneManager.SaveScene(director.gameObject.scene);
        Debug.Log("Trailer road loop baked successfully into Intro_Cinematic.");
    }
}
