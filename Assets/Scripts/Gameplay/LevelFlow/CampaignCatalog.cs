using System;
using System.Collections.Generic;
using System.Linq;
using Sokoban.Domain;
using UnityEngine;

namespace Sokoban
{
    [CreateAssetMenu(fileName = "CampaignCatalog", menuName = "Robot Sokoban/Campaign Catalog")]
    public sealed class CampaignCatalog : ScriptableObject
    {
        [SerializeField, Tooltip("按游玩顺序引用关卡 JSON；默认不包含开发实验关。")]
        private TextAsset[] levels = Array.Empty<TextAsset>();

        public LevelDefinition[] ReadLevels()
        {
            if (levels == null || levels.Length == 0) throw new InvalidOperationException("正式关卡目录为空。");
            var definitions = new List<LevelDefinition>(levels.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < levels.Length; i++)
            {
                if (!levels[i]) throw new InvalidOperationException($"关卡目录第 {i + 1} 项缺少 JSON 引用。");
                var level = LevelJson.Read(levels[i].text);
                var report = LevelValidator.Validate(level);
                if (!report.IsValid) throw new InvalidOperationException(level.id + "：" + string.Join("\n", report.Issues.Where(issue => issue.IsError)));
                if (!ids.Add(level.id)) throw new InvalidOperationException("关卡目录 ID 重复：" + level.id);
                definitions.Add(level);
            }
            return definitions.ToArray();
        }
    }
}
