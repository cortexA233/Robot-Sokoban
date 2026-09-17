using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Sokoban.Art.Editor
{
    /// <summary>Opt-in native Test Runner evidence when the MCP job omits its summary.</summary>
    [InitializeOnLoad]
    public static class StationKitTestEvidence
    {
        const string Key="Sokoban.StationKit.TestEvidence";
        static TestRunnerApi api;
        static readonly Recorder recorder=new Recorder();
        static StationKitTestEvidence() { if(SessionState.GetBool(Key,false)) Register(); }
        public static void Begin() { SessionState.SetBool(Key,true); Register(); }
        static void Register()
        {
            if(api) return;
            api=ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(recorder);
        }
        [Serializable] public class Case { public string name,state,message,stackTrace; public double seconds; }
        [Serializable] public class Report { public string unityVersion,state,completedAt; public int passed,failed,skipped; public double seconds; public List<Case> tests; }
        sealed class Recorder : ICallbacks
        {
            readonly List<Case> tests=new List<Case>();
            public void RunStarted(ITestAdaptor testsToRun) { tests.Clear(); }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if(result.HasChildren) return;
                tests.Add(new Case{name=result.FullName,state=result.ResultState,message=result.Message,stackTrace=result.StackTrace,seconds=result.Duration});
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                if(!SessionState.GetBool(Key,false)) return;
                var report=new Report{unityVersion=Application.unityVersion,state=result.ResultState,completedAt=DateTime.UtcNow.ToString("o"),
                    passed=result.PassCount,failed=result.FailCount,skipped=result.SkipCount,seconds=result.Duration,tests=tests};
                Directory.CreateDirectory("Logs/StationKitGameplay");
                File.WriteAllText("Logs/StationKitGameplay/Tests.json",JsonUtility.ToJson(report,true)+"\n");
                SessionState.SetBool(Key,false);
            }
        }
    }
}
