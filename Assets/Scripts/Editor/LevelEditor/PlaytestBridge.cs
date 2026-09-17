using System;
using System.IO;
using Sokoban.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sokoban.Editor
{
    [InitializeOnLoad]
    public static class PlaytestBridge
    {
        private const string ActiveKey = "Sokoban.Playtest.Active";
        private const string PreviousStartSceneKey = "Sokoban.Playtest.StartScene";
        private const string Snapshot = "Library/SokobanDrafts/Playtest.json";
        private const string Recording = "Library/SokobanDrafts/Recording.json";
        static PlaytestBridge() { EditorApplication.playModeStateChanged += OnPlayMode; }

        public static void Play(LevelDocument document, Action<ValidationReport> showValidation = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出当前试玩。");
            var report = LevelValidator.Validate(document.level);
            showValidation?.Invoke(report);
            if (!report.IsValid)
            {
                if (showValidation == null) throw new InvalidOperationException("关卡有校验错误：\n" + string.Join("\n", report.Issues));
                return;
            }
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Bootstrap.unity");
            if (!scene) throw new InvalidOperationException("Bootstrap 场景缺失，请运行 Prepare Gameplay Assets。");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            document.Backup();
            LevelJson.AtomicWrite(Snapshot, LevelJson.Write(document.level.Copy()));
            if (File.Exists(Recording)) File.Delete(Recording);
            SessionState.SetString(PreviousStartSceneKey, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(ActiveKey, true);
            // Unity preserves and restores the current SceneSetup when a playModeStartScene is used.
            EditorSceneManager.playModeStartScene = scene;
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SupplySnapshot()
        {
            LevelRunner.PlaytestDefinition = null;
            LevelRunner.SaveReferenceRequested -= SaveRecording;
            if (!SessionState.GetBool(ActiveKey, false)) return;
            LevelRunner.PlaytestDefinition = LevelJson.Read(File.ReadAllText(Snapshot));
            LevelRunner.SaveReferenceRequested += SaveRecording;
        }
        private static void SaveRecording(LevelDefinition level, GameSession session) =>
            LevelJson.AtomicWrite(Recording, JsonUtility.ToJson(SolutionRecord.Capture(level, session), true));

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveKey, false)) return;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousStartSceneKey, ""));
            SessionState.SetBool(ActiveKey, false);
            LevelRunner.PlaytestDefinition = null;
            LevelRunner.SaveReferenceRequested -= SaveRecording;
        }
        public static void CollectRecording(LevelDocument document)
        {
            if (!File.Exists(Recording) || document == null) return;
            var recorded = JsonUtility.FromJson<SolutionRecord>(File.ReadAllText(Recording));
            if (recorded.levelId != document.level.id || recorded.contentHash != LevelJson.Hash(document.level)) return;
            recorded.Verify(document.level, true);
            document.Change("保存试玩参考解法", () => document.solution = recorded);
            File.Delete(Recording);
        }
    }
}
