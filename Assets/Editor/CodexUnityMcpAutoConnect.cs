using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CodexUnityMcpAutoConnect
{
    private static int remainingAttempts = 12;
    private static double nextAttemptTime;

    static CodexUnityMcpAutoConnect()
    {
        EditorApplication.delayCall += StartRetryLoop;
    }

    private static void StartRetryLoop()
    {
        nextAttemptTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += RetryConnect;
    }

    private static void RetryConnect()
    {
        if (remainingAttempts <= 0)
        {
            EditorApplication.update -= RetryConnect;
            return;
        }

        if (EditorApplication.timeSinceStartup < nextAttemptTime) return;

        remainingAttempts--;
        nextAttemptTime = EditorApplication.timeSinceStartup + 5d;
        Connect();
    }

    private static void Connect()
    {
        try
        {
            EditorConfigurationCache.Instance.SetUseHttpTransport(true);
            EditorConfigurationCache.Instance.SetHttpBaseUrl("http://127.0.0.1:8080");
            EditorConfigurationCache.Instance.SetHttpTransportScope("local");
            EditorConfigurationCache.Instance.SetUvxPathOverride("C:/Users/lehoa/AppData/Roaming/Python/Python312/Scripts/uvx.exe");
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
            EditorPrefs.SetBool("MCPForUnity.SetupCompleted", true);

            _ = MCPServiceLocator.Bridge.StartAsync();
            Debug.Log("[Codex] MCP for Unity bridge start requested at http://127.0.0.1:8080.");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Codex] Could not start MCP for Unity bridge: {ex.Message}");
        }
    }
}
