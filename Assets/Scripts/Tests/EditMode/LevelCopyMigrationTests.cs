using System;
using System.IO;
using NUnit.Framework;
using Sokoban.Editor;
using UnityEngine;

namespace Sokoban.Tests
{
    public sealed class LevelCopyMigrationTests
    {
        [Serializable] private sealed class Draft { public string levelJson, solutionJson, filePath, savedHash, savedSolution; }
        [Test] public void LegacyCopyCanBeReadImportedAndRecoveredWithoutChangingThePuzzle()
        {
            var level = LevelJson.Read(Resources.Load<TextAsset>("configs/test_levels/LAB01_LowFriction").text);
            string legacy = LevelJson.Write(level).Insert(1, "\"title\":\"旧标题\",\"briefing\":\"旧提示\",\"completionText\":\"旧完成文案\",");
            var migrated = LevelJson.Read(legacy);
            Assert.That(LevelJson.Hash(migrated), Is.EqualTo(LevelJson.Hash(level)));
            Assert.That(SolutionRecord.ReplayCommands(migrated, "E").State.Completed, Is.True);
            foreach (string field in new[] { "title", "briefing", "completionText" })
                Assert.That(LevelJson.Write(migrated), Does.Not.Contain("\"" + field + "\""));
            var document = ScriptableObject.CreateInstance<LevelDocument>();
            document.draftPath = "Library/SokobanDrafts/Migration_" + Guid.NewGuid().ToString("N") + ".json";
            try
            {
                RecipeImporter.ImportInto(document, "{\"definition\":" + legacy + ",\"commands\":\"E\"}");
                Assert.That(document.solution.Verify(document.level, true), Does.Contain("通过"));
                File.WriteAllText(document.draftPath, JsonUtility.ToJson(new Draft { levelJson = legacy }));
                document.RestoreDraft(); Assert.That(LevelJson.Hash(document.level), Is.EqualTo(LevelJson.Hash(level)));
                document.Backup(); Assert.That(File.ReadAllText(document.draftPath), Does.Not.Contain("旧标题"));
            }
            finally { if (File.Exists(document.draftPath)) File.Delete(document.draftPath); UnityEngine.Object.DestroyImmediate(document); }
            Assert.Throws<FormatException>(() => LevelJson.Read(legacy.Replace("\"旧标题\"", "false")));
            Assert.Throws<FormatException>(() => LevelJson.Read(legacy.Replace("\"title\"", "\"unknownGameplayField\"")));
        }
    }
}
