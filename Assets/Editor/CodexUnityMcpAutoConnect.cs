using System;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CodexUnityMcpAutoConnect
{
    private const string AutoConnectDoneKey = "CodexUnityMcpAutoConnect.Done";
    private const string UseHttpTransportKey = "MCPForUnity.UseHttpTransport";
    private const string HttpBaseUrlKey = "MCPForUnity.HttpUrl";
    private const string HttpScopeKey = "MCPForUnity.HttpTransportScope";
    private const string AutoStartOnLoadKey = "MCPForUnity.AutoStartOnLoad";
    private const string SetupCompletedKey = "MCPForUnity.SetupCompleted";
    private const string ServerUrl = "http://127.0.0.1:8080";

    static CodexUnityMcpAutoConnect()
    {
        EditorApplication.delayCall += StartOnce;
    }

    private static async void StartOnce()
    {
        if (SessionState.GetBool(AutoConnectDoneKey, false))
        {
            return;
        }

        SessionState.SetBool(AutoConnectDoneKey, true);

        try
        {
            EditorPrefs.SetBool(UseHttpTransportKey, true);
            EditorPrefs.SetString(HttpBaseUrlKey, ServerUrl);
            EditorPrefs.SetString(HttpScopeKey, "local");
            EditorPrefs.SetBool(AutoStartOnLoadKey, true);
            EditorPrefs.SetBool(SetupCompletedKey, true);

            bool started = await MCPServiceLocator.Bridge.StartAsync();
            Debug.Log(started
                ? "[Codex] MCP for Unity bridge connected to http://127.0.0.1:8080."
                : "[Codex] MCP for Unity bridge did not connect yet. Open Window > MCP for Unity and press Connect if needed.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Codex] MCP for Unity auto-connect failed: " + ex.Message);
        }
    }
}
