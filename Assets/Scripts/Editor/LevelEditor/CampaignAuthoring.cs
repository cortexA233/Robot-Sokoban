using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sokoban.Domain;
using UnityEditor;
using UnityEngine;

namespace Sokoban.Editor
{
    public sealed class CatalogProblem
    {
        public int Index { get; }
        public string AssetPath { get; }
        public string Message { get; }
        public Cell? Cell { get; }
        public bool IsError { get; }
        public CatalogProblem(int index, string path, string message, bool error = true, Cell? cell = null)
        { Index = index; AssetPath = path; Message = message; IsError = error; Cell = cell; }
        public override string ToString() => (Index >= 0 ? $"第 {Index + 1} 项 · " : "") + AssetPath + "：" + Message;
    }

    public sealed class CatalogEntryStatus
    {
        public TextAsset Asset;
        public LevelDefinition Level;
        public string AssetPath;
        public string Structure = "未通过";
        public string Solution = "未检查";
        public readonly List<CatalogProblem> Problems = new List<CatalogProblem>();
        public bool IsValid => Problems.All(p => !p.IsError);
    }

    public sealed class CatalogValidation
    {
        public readonly List<CatalogEntryStatus> Entries = new List<CatalogEntryStatus>();
        public readonly List<CatalogProblem> Problems = new List<CatalogProblem>();
        public bool IsValid => Problems.All(p => !p.IsError);
        public string Summary => IsValid ? $"目录通过：{Entries.Count} 关，结构与当前参考解法均有效。" :
            $"目录未通过：{Problems.Count(p => p.IsError)} 个错误。\n" + string.Join("\n", Problems.Where(p => p.IsError));
    }

    // Validates the existing catalog and replay proofs for the build guard.
    public static class CampaignAuthoring
    {
        public const string CatalogPath = "Assets/Resources/configs/CampaignCatalog.asset";
        public static CampaignCatalog Catalog => AssetDatabase.LoadAssetAtPath<CampaignCatalog>(CatalogPath);

        public static TextAsset[] ReadEntries(CampaignCatalog catalog)
        {
            if (!catalog) throw new InvalidOperationException("正式关卡目录资源缺失：" + CatalogPath);
            var property = new SerializedObject(catalog).FindProperty("levels");
            var result = new TextAsset[property.arraySize];
            for (int i = 0; i < result.Length; i++) result[i] = property.GetArrayElementAtIndex(i).objectReferenceValue as TextAsset;
            return result;
        }

        public static CatalogEntryStatus Inspect(TextAsset asset, int index = 0, Func<TextAsset, SolutionRecord> readSolution = null)
        {
            var entry = new CatalogEntryStatus { Asset = asset, AssetPath = asset ? AssetDatabase.GetAssetPath(asset) : CatalogPath };
            if (!asset) { entry.Problems.Add(new CatalogProblem(index, entry.AssetPath, "缺少关卡 JSON 引用。")); return entry; }
            try
            {
                entry.Level = LevelJson.Read(asset.text);
                var report = LevelValidator.Validate(entry.Level);
                foreach (var issue in report.Issues)
                    entry.Problems.Add(new CatalogProblem(index, entry.AssetPath, issue.Message, issue.IsError, issue.Cell));
                if (!report.IsValid) return entry;
                entry.Structure = "通过";
                try
                {
                    var proof = (readSolution ?? ReadSolution)(asset);
                    if (proof == null) throw new InvalidOperationException("缺少参考解法，请试玩通关、保存解法并保存关卡。");
                    proof.Verify(entry.Level, true);
                    entry.Solution = $"当前有效 · {proof.expectedMoves} 步 / {proof.expectedPushes} 推";
                }
                catch (Exception exception)
                {
                    entry.Solution = exception.Message;
                    entry.Problems.Add(new CatalogProblem(index, entry.AssetPath, exception.Message));
                }
            }
            catch (Exception exception) { entry.Problems.Add(new CatalogProblem(index, entry.AssetPath, "关卡无法读取：" + exception.Message)); }
            return entry;
        }

        private static SolutionRecord ReadSolution(TextAsset asset)
        {
            string path = LevelDocument.SolutionPath(AssetDatabase.GetAssetPath(asset));
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<SolutionRecord>(File.ReadAllText(path));
        }

        public static CatalogValidation ValidateEntries(IEnumerable<TextAsset> assets, Func<TextAsset, SolutionRecord> readSolution = null)
        {
            var result = new CatalogValidation();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in assets ?? Array.Empty<TextAsset>())
            {
                int index = result.Entries.Count;
                var entry = Inspect(asset, index, readSolution); result.Entries.Add(entry);
                if (entry.Level != null && !ids.Add(entry.Level.id))
                    entry.Problems.Add(new CatalogProblem(index, entry.AssetPath, "关卡 ID 重复：" + entry.Level.id));
                result.Problems.AddRange(entry.Problems);
            }
            if (result.Entries.Count == 0) result.Problems.Add(new CatalogProblem(-1, CatalogPath, "正式关卡目录为空。"));
            return result;
        }

        public static CatalogValidation Validate(CampaignCatalog catalog)
        {
            if (catalog) return ValidateEntries(ReadEntries(catalog));
            var result = new CatalogValidation();
            result.Problems.Add(new CatalogProblem(-1, CatalogPath, "正式关卡目录资源缺失。")); return result;
        }

    }
}
