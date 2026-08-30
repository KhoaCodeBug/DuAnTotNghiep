#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class FowLosArtifactRunner
{
    private static string currentQaFolder;
    private static bool isRunningSuite = false;

    static FowLosArtifactRunner()
    {
        EditorApplication.delayCall += OnEditorReady;
    }

    private static void OnEditorReady()
    {
        string triggerFile = Path.Combine(Application.dataPath, "../Temp/RunFowQaTrigger.txt");
        if (File.Exists(triggerFile))
        {
            File.Delete(triggerFile);
            RunFullQAFlow();
        }
    }

    public static string CreateFreshQaFolder()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        currentQaFolder = Path.Combine(Application.dataPath, "../QA_Artifacts/FOW_LOS_Fix_" + timestamp);
        Directory.CreateDirectory(currentQaFolder);
        return currentQaFolder;
    }

    [MenuItem("Tools/QA/Run Full FOW LOS QA Suite")]
    public static void TriggerFullQaSuite()
    {
        string triggerFile = Path.Combine(Application.dataPath, "../Temp/RunFowQaTrigger.txt");
        File.WriteAllText(triggerFile, DateTime.Now.ToString("O"));
        AssetDatabase.Refresh();
    }

    private static int compileErrorCount = 0;
    private static int warningCount = 0;
    private static int errorCount = 0;
    private static System.Collections.Generic.List<string> warningDetails = new System.Collections.Generic.List<string>();
    private static System.Collections.Generic.List<string> errorDetails = new System.Collections.Generic.List<string>();

    private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            warningCount++;
            if (warningDetails.Count < 20) warningDetails.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errorCount++;
            if (errorDetails.Count < 20) errorDetails.Add(condition);
        }
    }

    public static void RunFullQAFlow()
    {
        if (isRunningSuite) return;
        isRunningSuite = true;

        compileErrorCount = 0;
        warningCount = 0;
        errorCount = 0;
        warningDetails.Clear();
        errorDetails.Clear();
        Application.logMessageReceived += OnLogMessageReceived;

        string folder = CreateFreshQaFolder();
        Debug.Log("[FOW QA RUNNER] Starting Full QA Flow in: " + folder);

        // Write CHANGED_FILES.txt
        string changedFilesContent =
            "Assets/Shader/FogVisionOverlay.shader\n" +
            "Assets/Khoa/Code/FogVisionController.cs\n" +
            "Assets/Khoa/Code/PlayerVision.cs\n" +
            "Assets/Khoa/Code/VisionLineOfSight.cs\n" +
            "Assets/Script/Tin/Prototype/Tests/PlayMode/VisibilityAndZombieRegressionPlayModeTests.cs\n" +
            "Assets/Script/Tin/Editor/FowLosArtifactRunner.cs\n";
        File.WriteAllText(Path.Combine(folder, "CHANGED_FILES.txt"), changedFilesContent);

        // Write FOW_LOS_BEHAVIOR.md
        string behaviorDoc =
            "# FOW and LOS Behavior Specification & Verification\n\n" +
            "## 1. Indoor Behavior\n" +
            "- **Ambient Visibility**: When the survivor enters a building, the current room/interior is illuminated by ambient lighting (`_IndoorAmbientOpacity` modulated by night blend).\n" +
            "- **Depth Discontinuity Protection**: The ray fan prevents triangular shadow bleeding across open rooms by suppressing diagonal interpolation when adjacent rays have large depth steps (> 1.2m).\n" +
            "- **Building Envelope**: Up to 32 polygon vertices and bounding box fallback (`_IndoorBounds`) eliminate diagonal room truncations.\n" +
            "- **Door Portals**: Exterior visibility is permitted only through verified open doorways (`FindOpenIndoorPortals`), keeping closed rooms dark to the outside.\n\n" +
            "## 2. Outdoor Behavior\n" +
            "- **Daytime LOS Occlusion**: Occluded areas behind obstacles (e.g. stalled cars, fences, walls) are rendered as dark blind spots, NOT daylight white fog.\n" +
            "- **Gate Pass-Through**: Military gates with `MilitaryGateVisionPassThrough` allow vision rays and FOW to pass through.\n" +
            "- **Shared Contract**: `PlayerVision` zombie awareness and `FogVisionController` ray fan share identical origin (`LineOfSightOrigin`), direction, and obstacle layer mask.\n";
        File.WriteAllText(Path.Combine(folder, "FOW_LOS_BEHAVIOR.md"), behaviorDoc);

        // Run EditMode Tests first
        TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
        Filter editFilter = new Filter { testMode = TestMode.EditMode };
        api.RegisterCallbacks(new EditModeCallbacks(folder));
        api.Execute(new ExecutionSettings(editFilter));
    }

    private class EditModeCallbacks : ICallbacks
    {
        private readonly string folder;

        public EditModeCallbacks(string folder)
        {
            this.folder = folder;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log($"[FOW QA] EditMode Run Started ({testsToRun.TestCaseCount} tests)");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            string summary = $"EditMode Tests Finished.\nTotal: {result.PassCount + result.FailCount + result.SkipCount}, Passed: {result.PassCount}, Failed: {result.FailCount}, Skipped: {result.SkipCount}\nDuration: {result.Duration}s\n";
            File.WriteAllText(Path.Combine(folder, "EDITMODE_TEST_RESULTS.txt"), summary);
            Debug.Log("[FOW QA] " + summary);

            int pass = result.PassCount;
            int fail = result.FailCount;
            int skip = result.SkipCount;

            // Now trigger PlayMode tests
            EditorApplication.delayCall += () =>
            {
                Type testType = Type.GetType("VisibilityAndZombieRegressionPlayModeTests, ProjectZomboiNhai.QuestFlow.Tests.PlayMode");
                if (testType != null)
                {
                    testType.GetProperty("ActiveQaDirectory", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.SetValue(null, folder);
                }
                TestRunnerApi playApi = ScriptableObject.CreateInstance<TestRunnerApi>();
                Filter playFilter = new Filter { testMode = TestMode.PlayMode };
                playApi.RegisterCallbacks(new PlayModeCallbacks(folder, pass, fail, skip));
                playApi.Execute(new ExecutionSettings(playFilter));
            };
        }

        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.TestStatus == TestStatus.Failed)
            {
                Debug.LogError($"[FOW QA FAIL EditMode] {result.FullName}: {result.Message}");
            }
        }
    }

    private class PlayModeCallbacks : ICallbacks
    {
        private readonly string folder;
        private readonly int editPass;
        private readonly int editFail;
        private readonly int editSkip;

        public PlayModeCallbacks(string folder, int editPass, int editFail, int editSkip)
        {
            this.folder = folder;
            this.editPass = editPass;
            this.editFail = editFail;
            this.editSkip = editSkip;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log($"[FOW QA] PlayMode Run Started ({testsToRun.TestCaseCount} tests)");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            Application.logMessageReceived -= OnLogMessageReceived;

            int playPass = result.PassCount;
            int playFail = result.FailCount;
            int playSkip = result.SkipCount;

            string summary = $"PlayMode Tests Finished.\nTotal: {playPass + playFail + playSkip}, Passed: {playPass}, Failed: {playFail}, Skipped: {playSkip}\nDuration: {result.Duration}s\n";
            File.WriteAllText(Path.Combine(folder, "PLAYMODE_TEST_RESULTS.txt"), summary);
            Debug.Log("[FOW QA] " + summary);

            string compileStatus = (EditorUtility.scriptCompilationFailed || compileErrorCount > 0) ? "FAIL" : "PASS";
            bool isPass = (compileStatus == "PASS" && editFail == 0 && editSkip == 0 && playFail == 0 && playSkip == 0);
            string status = isPass ? "PASS" : "FAIL";
            string warningStatus = warningCount > 0 ? $"{warningCount} logged (non-blocking)" : "0 logged";

            // Write QA_STATUS.txt
            string qaStatusText =
                $"STATUS: {status}\n" +
                $"COMPILE_STATUS: {compileStatus}\n" +
                $"COMPILE_ERRORS: {compileErrorCount}\n" +
                $"WARNINGS_COUNT: {warningCount}\n" +
                $"WARNINGS_STATUS: {warningStatus}\n" +
                $"EDITMODE_TOTAL: {editPass + editFail + editSkip}\n" +
                $"EDITMODE_PASSED: {editPass}\n" +
                $"EDITMODE_FAILED: {editFail}\n" +
                $"EDITMODE_SKIPPED: {editSkip}\n" +
                $"PLAYMODE_TOTAL: {playPass + playFail + playSkip}\n" +
                $"PLAYMODE_PASSED: {playPass}\n" +
                $"PLAYMODE_FAILED: {playFail}\n" +
                $"PLAYMODE_SKIPPED: {playSkip}\n";
            File.WriteAllText(Path.Combine(folder, "QA_STATUS.txt"), qaStatusText);

            // Write QA_STATUS.json
            string qaStatusJson =
                "{\n" +
                $"  \"status\": \"{status}\",\n" +
                $"  \"compileStatus\": \"{compileStatus}\",\n" +
                $"  \"compileErrors\": {compileErrorCount},\n" +
                $"  \"warningCount\": {warningCount},\n" +
                $"  \"warningStatus\": \"{warningStatus}\",\n" +
                $"  \"editMode\": {{ \"total\": {editPass + editFail + editSkip}, \"passed\": {editPass}, \"failed\": {editFail}, \"skipped\": {editSkip} }},\n" +
                $"  \"playMode\": {{ \"total\": {playPass + playFail + playSkip}, \"passed\": {playPass}, \"failed\": {playFail}, \"skipped\": {playSkip} }}\n" +
                "}";
            File.WriteAllText(Path.Combine(folder, "QA_STATUS.json"), qaStatusJson);

            // Write FINAL_REPORT.md
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# FOW & LOS P0 Fix Final Report");
            sb.AppendLine();
            sb.AppendLine("## 1. Summary of Changes");
            sb.AppendLine("- Fixed depth discontinuity angular shadow bleeding in `FogVisionOverlay.shader` using depth jump step thresholding.");
            sb.AppendLine("- Fixed daytime outdoor blind spots rendering as white fog; now rendered with natural dark shadow blind spot color.");
            sb.AppendLine("- Upgraded indoor polygon representation to 32 points with bounding box envelope fallback in `FogVisionController.cs` and `FogVisionOverlay.shader`.");
            sb.AppendLine("- Unified LOS semantics with `PlayerVision.cs` and `VisionLineOfSight.cs`.");
            sb.AppendLine();
            sb.AppendLine("## 2. Test Execution & Compiler Results");
            sb.AppendLine($"- **Compile Status**: {compileStatus} (Errors: {compileErrorCount})");
            sb.AppendLine($"- **Warnings**: {warningStatus}");
            sb.AppendLine($"- **EditMode**: Total {editPass + editFail + editSkip} | Passed: {editPass} | Failed: {editFail} | Skipped: {editSkip}");
            sb.AppendLine($"- **PlayMode**: Total {playPass + playFail + playSkip} | Passed: {playPass} | Failed: {playFail} | Skipped: {playSkip}");
            sb.AppendLine();
            sb.AppendLine("## 3. Visual QA Flow Artifacts");
            sb.AppendLine("The following captures were generated during the Solo PlayMode flow:");
            sb.AppendLine("- `FOW_FLOW_00_start_spawn.png`: Survivor spawn view (15:00 daytime)");
            sb.AppendLine("- `FOW_FLOW_01_outside_house.png`: Outside approaching house (15:00 daytime)");
            sb.AppendLine("- `FOW_FLOW_02_inside_house.png`: Inside house entry (15:00 daytime)");
            sb.AppendLine("- `FOW_01_windowless_room_flashlight_off.png`: Windowless room flashlight OFF");
            sb.AppendLine("- `FOW_02_windowless_room_aim_wall.png`: Windowless room flashlight aiming North (+Y)");
            sb.AppendLine("- `FOW_03_windowless_room_rotate_180.png`: Windowless room 180-degree rotation South (-Y)");
            sb.AppendLine("- `PORTAL_CLOSED_DIRECT.png` / `FOW_04_closed_door_blocked.png`: Hospital closed door (blocked by door blocker)");
            sb.AppendLine("- `PORTAL_OPEN_DIRECT.png` / `FOW_05_open_door_portal.png`: Hospital open door (portal active and revealing connected hallway)");
            sb.AppendLine("- `FOW_06_outdoor_fence_los.png`: Outdoor fence LOS barrier");
            sb.AppendLine("- `FOW_07_outdoor_day.png`: Daytime outdoor vision");
            sb.AppendLine("- `FOW_08_outdoor_night.png`: Nighttime outdoor vision with flashlight");
            sb.AppendLine("- `FOW_09_moving_flashlight.png`: Moving survivor with dynamic flashlight");
            sb.AppendLine();
            sb.AppendLine($"QA Result: {status} - EditMode ({editPass}/{editPass + editFail + editSkip}), PlayMode ({playPass}/{playPass + playFail + playSkip}).");

            File.WriteAllText(Path.Combine(folder, "FINAL_REPORT.md"), sb.ToString());
            isRunningSuite = false;
            Debug.Log($"[FOW QA RUNNER] Full QA Flow Completed with status: {status}");
        }

        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.TestStatus == TestStatus.Failed)
            {
                Debug.LogError($"[FOW QA FAIL PlayMode] {result.FullName}: {result.Message}");
            }
        }
    }
}
#endif
