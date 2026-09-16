using MCPForUnity.Editor.Services;
using UnityEditor;

namespace Sokoban.Editor
{
    // CoplayDev 10.2.0 creates its test service lazily. An EditMode test that enters
    // Play Mode reloads the domain, losing the callback registration before the
    // test finishes. Recreate the service after every reload so MCP receives the
    // completion callback and finalizes the job persisted in SessionState.
    [InitializeOnLoad]
    internal static class SokobanMcpTestCallbacks
    {
        static SokobanMcpTestCallbacks()
        {
            _ = MCPServiceLocator.Tests;
        }
    }
}
