using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sokoban.Editor
{
    public sealed class CampaignBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) => RequireValid(CampaignAuthoring.Catalog);

        public static void RequireValid(CampaignCatalog catalog)
        {
            var result = Inspect(catalog);
            if (!result.IsValid) throw new BuildFailedException("正式目录不能构建。请打开 Sokoban_Tools > Campaign Catalog 修复：\n" + result.Summary);
            Debug.Log(result.Summary);
        }

        public static CatalogValidation Inspect(CampaignCatalog catalog)
        {
            var result = CampaignAuthoring.Validate(catalog);
            RequireAsset<SceneAsset>(result, "Assets/Scenes/Bootstrap.unity");
            foreach (string screen in new[] { "MainMenu", "Hud", "Pause", "LevelSelect", "Completion", "Settings", "GeneralFade" })
                RequireAsset<GameObject>(result, "Assets/Resources/UI_prefabs/screens/" + screen + ".prefab");
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
                RequireAsset<AudioClip>(result, "Assets/Resources/audio/sfx/" + cue + ".wav");
            var actor = RequireAsset<GameObject>(result, "Assets/Resources/prefabs/gameplay/player/PlayerActor.prefab");
            if (actor && !actor.GetComponent<RobotPresenter>()) result.Problems.Add(new CatalogProblem(-1, AssetDatabase.GetAssetPath(actor), "缺少 RobotPresenter。"));
            var theme = RequireAsset<StationKitTheme>(result, "Assets/Resources/configs/StationKitTheme.asset");
            if (theme)
            {
                foreach (string id in new[] { "FloorPlain", "FloorService", "FloorGrate", "WallStraight", "WallCorner", "WallEnd", "EnergyCrate", "CargoCrate", "GoalSocket", "UtilitySocket", "PowerGate" })
                    if (theme.assets == null || !theme.assets.Any(a => a != null && a.id == id && a.prefab))
                        result.Problems.Add(new CatalogProblem(-1, AssetDatabase.GetAssetPath(theme), "缺少表现 Prefab：" + id));
                if (theme.styles == null || theme.styles.Length == 0 || theme.styles.Any(s => s == null || !s.material) || !theme.powerOff || !theme.powerOn || !theme.trackSurface || !theme.trackMark)
                    result.Problems.Add(new CatalogProblem(-1, AssetDatabase.GetAssetPath(theme), "供电/轨道材质配置缺失。"));
            }
            return result;
        }

        private static T RequireAsset<T>(CatalogValidation result, string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) result.Problems.Add(new CatalogProblem(-1, path, "必需资源缺失。"));
            return asset;
        }
    }
}
