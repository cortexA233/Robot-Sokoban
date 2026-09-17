using MCPForUnity.Editor.Services;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Sokoban.Editor
{
    // CoplayDev 10.2.0 creates its test service lazily. An EditMode test that enters
    // Play Mode reloads the domain, losing the callback registration before the
    // test finishes. Recreate the service after every reload so MCP receives the
    // completion callback and finalizes the job persisted in SessionState.
    [InitializeOnLoad]
    internal static class SokobanMcpTestCallbacks
    {
        private static readonly TestRunnerApi EvidenceApi;
        static SokobanMcpTestCallbacks()
        {
            _ = MCPServiceLocator.Tests;
            EvidenceApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            EvidenceApi.RegisterCallbacks(new EvidenceCallbacks(), 1000);
        }

        // The upstream job may retain progress but lose its result after a PlayMode
        // domain reload. Keep Unity's complete result tree independently, including
        // failures and counts, rather than reconstructing a pass from progress.
        private sealed class EvidenceCallbacks : ICallbacks
        {
            private const string PathKey = "Sokoban.TestEvidence.Path";
            public void RunStarted(ITestAdaptor testsToRun) => SessionState.SetString(PathKey,
                "Library/SokobanTestResults/Run-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + ".xml");
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string path = SessionState.GetString(PathKey, "Library/SokobanTestResults/Recovered.xml");
                Directory.CreateDirectory("Library/SokobanTestResults");
                string xml = result.ToXml().OuterXml;
                File.WriteAllText(path, xml);
                File.WriteAllText("Library/SokobanTestResults/Latest.xml", xml);
            }
        }
    }
}
