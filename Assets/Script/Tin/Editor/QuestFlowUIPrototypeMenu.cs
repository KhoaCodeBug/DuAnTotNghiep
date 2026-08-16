using UnityEditor;
using UnityEngine;

public static class QuestFlowUIPrototypeMenu
{
    [MenuItem("Tools/Quest UI Prototype/Run Automated Self-Test")]
    public static void RunAutomatedSelfTest()
    {
        // Always test a temporary instance so the preview scene is never dirtied by generated children.
        GameObject temporaryHost = new GameObject("Temporary Quest UI Self-Test");
        QuestFlowUIPrototype prototype = temporaryHost.AddComponent<QuestFlowUIPrototype>();

        prototype.EnsureBuiltForTests();
        string[] errors = prototype.ValidatePrototype();

        prototype.SelectQuestForPreview(1);
        if (!prototype.CurrentDetailTitle.Contains("GHÉP LẠI"))
            errors = Append(errors, "Đổi sang nhiệm vụ phụ không cập nhật bảng chi tiết.");

        prototype.SelectTabForPreview(1);
        if (!prototype.IsEmptyStateVisible)
            errors = Append(errors, "Tab Hoàn thành không hiển thị trạng thái trống.");

        prototype.SelectQuestForPreview(0);
        prototype.SelectTabForPreview(0);

        Object.DestroyImmediate(temporaryHost);

        if (errors.Length == 0)
        {
            Debug.Log("[QUEST UI SELF-TEST] PASS — bố cục, nhiệm vụ chính/phụ, điều hướng nhiệm vụ và tab đều hợp lệ.");
            return;
        }

        Debug.LogError("[QUEST UI SELF-TEST] FAIL\n- " + string.Join("\n- ", errors));
    }

    private static string[] Append(string[] source, string value)
    {
        string[] result = new string[source.Length + 1];
        source.CopyTo(result, 0);
        result[result.Length - 1] = value;
        return result;
    }
}
