using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sokoban.Editor
{
    public static class GameplayAssetSetup
    {
        [MenuItem("Tools/Sokoban/Prepare Gameplay Assets")]
        public static void Prepare()
        {
            const string actorPath = "Assets/Resources/prefabs/gameplay/player/PlayerActor.prefab";
            const string scenePath = "Assets/Scenes/Bootstrap.unity";
            Scene original = SceneManager.GetActiveScene();
            Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(temporary);
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(actorPath))
                {
                    var root = new GameObject("PlayerActor");
                    try
                    {
                        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/prefabs/gameplay/player/Robot.prefab");
                        if (!source) throw new InvalidOperationException("现有机器人 Prefab 缺失。");
                        var visual = (GameObject)PrefabUtility.InstantiatePrefab(source); visual.transform.SetParent(root.transform, false);
                        var presenter = root.AddComponent<RobotPresenter>(); presenter.animator = visual.GetComponent<Animator>();
                        var clips = presenter.animator.runtimeAnimatorController.animationClips;
                        presenter.idle = clips.Single(c => c.name == "Idle"); presenter.move = clips.Single(c => c.name == "Move"); presenter.push = clips.Single(c => c.name == "Push");
                        PrefabUtility.SaveAsPrefabAsset(root, actorPath);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                if (!File.Exists(scenePath))
                {
                    new GameObject("LevelRunner").AddComponent<LevelRunner>();
                    var light = new GameObject("Station light").AddComponent<Light>(); light.type = LightType.Directional;
                    light.intensity = 1.2f; light.shadows = LightShadows.Soft; light.transform.rotation = Quaternion.Euler(45, -35, 0);
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .6f, .65f);
                    EditorSceneManager.SaveScene(temporary, scenePath);
                }
            }
            finally { if (original.IsValid()) SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(temporary, true); }
            AssetDatabase.SaveAssets();
            Debug.Log("PlayerActor 和 Bootstrap 已就绪；原机器人资产与现有场景保留。");
        }

        [MenuItem("Tools/Sokoban/Create Campaign Catalog")]
        public static void PrepareCampaignCatalog()
        {
            const string path = "Assets/Resources/configs/CampaignCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<CampaignCatalog>(path)) return;
            var levels = Enumerable.Range(4, 9).Select(i => AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/configs/levels/L" + i.ToString("00") + ".json")).ToArray();
            if (levels.Any(level => !level)) throw new InvalidOperationException("请先导入 L04–L12 关卡。");
            var catalog = ScriptableObject.CreateInstance<CampaignCatalog>();
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("levels"); entries.arraySize = levels.Length;
            for (int i = 0; i < levels.Length; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            catalog.ReadLevels();
            AssetDatabase.CreateAsset(catalog, path);
            AssetDatabase.SaveAssets();
        }
    }
}
