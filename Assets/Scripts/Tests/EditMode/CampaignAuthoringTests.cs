using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using Sokoban.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Sokoban.Tests
{
    public sealed class CampaignAuthoringTests
    {
        private CampaignCatalog catalog;
        private string path;
        private TextAsset First => Resources.Load<TextAsset>("configs/levels/L04");
        private SolutionRecord Proof => JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/L04.solution").text);
        [SetUp] public void SetUp()
        {
            path = "Assets/Resources/configs/V040CatalogTest_" + Guid.NewGuid().ToString("N") + ".asset";
            catalog = ScriptableObject.CreateInstance<CampaignCatalog>(); AssetDatabase.CreateAsset(catalog, path);
        }
        [TearDown] public void TearDown()
        { if (catalog) Undo.ClearUndo(catalog); AssetDatabase.DeleteAsset(path); }

        [Test] public void AllShippedEntriesHaveCurrentReplayProofsAndRequiredResources()
        {
            var report = CampaignBuildGuard.Inspect(CampaignAuthoring.Catalog);
            Assert.That(report.IsValid, Is.True, report.Summary); Assert.That(report.Entries.Count, Is.EqualTo(CampaignAuthoring.Catalog.ReadLevels().Length));
        }
        [Test] public void MissingMalformedAndDuplicateEntriesAreAllReported()
        {
            var malformed = new TextAsset("{broken");
            try
            {
                var report = CampaignAuthoring.ValidateEntries(new[] { null, malformed, First, First }, _ => Proof);
                Assert.That(report.IsValid, Is.False); Assert.That(report.Entries.Count, Is.EqualTo(4));
                Assert.That(report.Problems.Any(p => p.Index == 0 && p.Message.Contains("缺少")), Is.True);
                Assert.That(report.Problems.Any(p => p.Index == 1 && p.Message.Contains("无法读取")), Is.True);
                Assert.That(report.Problems.Any(p => p.Index == 3 && p.Message.Contains("ID 重复")), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(malformed); }
        }
        [TestCase("missing")] [TestCase("stale")] [TestCase("commands")] [TestCase("counts")]
        public void MissingStaleOrFalseProofCannotPassPublishingValidation(string fault)
        {
            var proof = Proof;
            if (fault == "missing") proof = null;
            else if (fault == "stale") proof.contentHash = "old";
            else if (fault == "commands") proof.commands = "N";
            else proof.expectedMoves++;
            var report = CampaignAuthoring.ValidateEntries(new[] { First }, _ => proof);
            Assert.That(report.IsValid, Is.False); Assert.That(report.Entries[0].Structure, Is.EqualTo("通过"));
            Assert.That(report.Problems.Any(p => p.IsError && p.Index == 0), Is.True);
        }
        [Test] public void StructuralErrorsKeepCoordinatesAndBlockBuildGuard()
        {
            var level = LevelJson.Read(First.text); level.crates[0].x = -1;
            var asset = new TextAsset(LevelJson.Write(level));
            try
            {
                var report = CampaignAuthoring.ValidateEntries(new[] { asset }, _ => Proof);
                Assert.That(report.IsValid, Is.False); Assert.That(report.Problems.Any(p => p.Cell.HasValue && p.Cell.Value.x == -1), Is.True);
                Assert.DoesNotThrow(() => new CampaignBuildGuard().OnPreprocessBuild(null), "默认正式目录应该通过构建回调。");
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
        [Test] public void EmptyAndMissingCatalogFailBuildGate()
        {
            Assert.Throws<BuildFailedException>(() => CampaignBuildGuard.RequireValid(catalog));
            Assert.Throws<BuildFailedException>(() => CampaignBuildGuard.RequireValid(null));
        }
    }
}
