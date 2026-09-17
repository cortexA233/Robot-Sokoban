using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Setup;
using UnityEditor;
using UnityEngine;

namespace Sokoban.Editor
{
    /// <summary>Applies the local MCP connection documented in Docs/Debug/UnityMcp.md.</summary>
    public static class SokobanMcpSetup
    {
        public const string ServerUrl = "http://127.0.0.1:8087";

        [MenuItem("Tools/Sokoban/Configure CoplayDev MCP")]
        public static void Configure()
        {
            // MCP for Unity stores transport preferences in Unity's user-level EditorPrefs.
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
            EditorPrefs.SetString("MCPForUnity.HttpTransportScope", "local");
            HttpEndpointUtility.SaveLocalBaseUrl(ServerUrl);
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
            EditorPrefs.SetBool("MCPForUnity.ProjectScopedTools.LocalHttp", true);
            EditorConfigurationCache.Instance.Refresh();
            SetupWindowService.MarkSetupCompleted();

            Debug.Log("Sokoban MCP configured at " + ServerUrl + "/mcp. " +
                      "Open Window > MCP for Unity to manage the connection.");
        }
    }
}
