using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using Sokoban.Editor;
using UnityEditor;
using UnityEngine;

namespace Sokoban.Tests
{
    public sealed class AuthoringTests
    {
        private LevelDocument document;
        [SetUp] public void SetUp()
        {
            document = ScriptableObject.CreateInstance<LevelDocument>();
            document.draftPath = "Library/SokobanDrafts/Test_" + Guid.NewGuid().ToString("N") + ".json";
            document.level = LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L02").text);
            Undo.IncrementCurrentGroup();
        }
        [TearDown] public void TearDown()
        {
            if (File.Exists(document.draftPath)) File.Delete(document.draftPath);
            Undo.ClearUndo(document); UnityEngine.Object.DestroyImmediate(document);
        }
        [Test] public void JsonRoundTripKeepsEveryFieldAndStableHash()
        {
            string json = LevelJson.Write(document.level); var reloaded = LevelJson.Read(json);
            Assert.That(LevelJson.Write(reloaded), Is.EqualTo(json)); Assert.That(LevelJson.Hash(reloaded), Is.EqualTo(LevelJson.Hash(document.level)));
        }
        [TestCase("\"isGoal\": false,", "")] [TestCase("\"gridSize\": 1.0,", "")] [TestCase("\"x\": 2,", "\"x\": 2.2,")]
        public void MissingAndInvalidJsonFieldsAreRejected(string from, string to)
        {
            string json = LevelJson.Write(document.level);
            // Field formatting is deterministic; remove isGoal even when it is last in its object.
            if (from.StartsWith("\"isGoal")) json = json.Replace("\"isGoal\": false", "\"notIsGoal\": false");
            else { Assert.That(json.Contains(from), Is.True); json = json.Replace(from, to); }
            Assert.Throws<FormatException>(() => LevelJson.Read(json));
        }
        [Test] public void DuplicateJsonFieldsAreRejected()
        {
            string json = LevelJson.Write(document.level).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1");
            Assert.Catch(() => LevelJson.Read(json));
        }
        [Test] public void InvalidIdsReferencesAndOverlapsProduceLocatedErrors()
        {
            document.level.gates[0].sourceSocketIds = new[] { "missing" };
            var issue = LevelValidator.Validate(document.level).Issues.First(i => i.IsError);
            Assert.That(issue.Cell, Is.EqualTo(document.level.gates[0].Cell));
            document.level.gates[0].sourceSocketIds = new[] { "socket_s" };
            document.level.crates[0].id = document.level.sockets[0].id;
            Assert.That(LevelValidator.Validate(document.level).IsValid, Is.False);
        }
        [Test] public void DeviceRulesRejectOverlapIceAndPlayerOnGate()
        {
            document.level.gates[0].x = document.level.sockets[0].x; document.level.gates[0].z = document.level.sockets[0].z;
            Assert.That(LevelValidator.Validate(document.level).Issues.Any(i => i.IsError && i.Message.Contains("重叠")), Is.True);
            Assert.Throws<InvalidOperationException>(() => document.Paint(document.level.sockets[0].Cell, '~'));
            Assert.Throws<InvalidOperationException>(() => document.Place(LevelBrush.Gate, document.level.playerSpawn.Cell, "N"));
        }
        [Test] public void DeletingSocketRemovesReferencesAndUndoRestoresEverything()
        {
            string before = LevelJson.Hash(document.level);
            document.Change("删除辅助槽", () => document.Erase(new Cell(4, 2), AuthorLayer.Devices));
            Assert.That(document.level.gates[0].sourceSocketIds, Does.Not.Contain("socket_s"));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.That(LevelJson.Hash(document.level), Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(document.level.sockets.Any(s => s.id == "socket_s"), Is.False);
        }
        [Test] public void WallPaintAndEntityRemovalAreOneUndo()
        {
            string before = LevelJson.Hash(document.level);
            document.Change("单次拖刷", () => { document.Paint(new Cell(3, 2), '#'); document.Paint(new Cell(4, 2), '#'); });
            Assert.That(document.level.crates.Length, Is.EqualTo(1)); Assert.That(document.level.gates[0].sourceSocketIds, Does.Not.Contain("socket_s"));
            Undo.PerformUndo(); Assert.That(LevelJson.Hash(document.level), Is.EqualTo(before));
        }
        [Test] public void ResizeCropsEntitiesAndReferencesAndCanUndo()
        {
            string before = LevelJson.Hash(document.level);
            Assert.That(document.CroppedCount(8, 7), Is.EqualTo(2));
            document.Change("缩图", () => document.Resize(8, 7));
            Assert.That(document.level.sockets.Any(s => s.id == "socket_a"), Is.False);
            Assert.That(document.level.gates[0].sourceSocketIds, Is.EqualTo(new[] { "socket_s" }));
            Undo.PerformUndo(); Assert.That(LevelJson.Hash(document.level), Is.EqualTo(before));
            document.Resize(15, 10); Assert.That(document.level.TerrainAt(new Cell(14, 9)), Is.EqualTo(Sokoban.Domain.Terrain.Void));
        }
        [Test] public void ActorErasePreservesSocketUnderBox()
        {
            document.Move("crate_01", new Cell(4, 2)); document.Erase(new Cell(4, 2), AuthorLayer.Actors);
            Assert.That(document.level.sockets.Any(s => s.id == "socket_s"), Is.True);
        }
        [Test] public void DraftSurvivesSerializationAndIsIsolated()
        {
            document.level.title = "尚未保存的中文标题"; document.Backup();
            var recovered = ScriptableObject.CreateInstance<LevelDocument>();
            recovered.draftPath = document.draftPath;
            try { recovered.RestoreDraft(); Assert.That(LevelJson.Hash(recovered.level), Is.EqualTo(LevelJson.Hash(document.level))); recovered.level.title = "other"; Assert.That(document.level.title, Is.EqualTo("尚未保存的中文标题")); }
            finally { UnityEngine.Object.DestroyImmediate(recovered); }
        }
        [Test] public void ProofExpiresOnEditAndCanBeReplayedAndRebound()
        {
            var proof = SolutionRecord.Capture(document.level, SolutionRecord.ReplayCommands(document.level, "ENWNEEEEEEWWWSSSWNNWNEEEENESSWSE"));
            document.level.title += " 改名";
            Assert.Throws<InvalidOperationException>(() => proof.Verify(document.level, true));
            Assert.DoesNotThrow(() => proof.Verify(document.level, false));
            proof.contentHash = LevelJson.Hash(document.level); Assert.DoesNotThrow(() => proof.Verify(document.level, true));
        }
        [Test] public void AtomicWriteFailurePreservesExistingFile()
        {
            string directory = Path.Combine("Library/SokobanDrafts", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "level.json"); File.WriteAllText(path, "original");
            try
            {
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    Assert.Catch<IOException>(() => LevelJson.AtomicWrite(path, "replacement"));
                Assert.That(File.ReadAllText(path), Is.EqualTo("original"));
            }
            finally { File.Delete(path); Directory.Delete(directory); }
        }
        [Test] public void CopyGetsNewIdAndDoesNotOverwriteOriginal()
        {
            string folder = "Library/SokobanDrafts/CopyTest"; Directory.CreateDirectory(folder);
            string original = Path.Combine(folder, "Original.json"), copy = Path.Combine(folder, "Copy.json");
            try
            {
                document.Save(original, false); string before = File.ReadAllText(original); string id = document.level.id;
                document.Save(copy, true); Assert.That(document.level.id, Is.Not.EqualTo(id)); Assert.That(File.ReadAllText(original), Is.EqualTo(before));
                Assert.That(LevelJson.Hash(LevelJson.Read(File.ReadAllText(copy))), Is.EqualTo(LevelJson.Hash(document.level)));
            }
            finally { File.Delete(original); File.Delete(copy); Directory.Delete(folder); }
        }
        [Test] public void UndoAfterSaveMarksWorkDirtyAgainstTheFileOnDisk()
        {
            string path = "Library/SokobanDrafts/UndoSaveTest.json";
            try
            {
                document.Save(path, false);
                document.Change("更改标题", () => document.level.title = "已保存的新标题");
                document.Save(path, false); Assert.That(document.IsDirty, Is.False);
                Undo.PerformUndo(); Assert.That(document.IsDirty, Is.True);
                Assert.That(LevelJson.Read(File.ReadAllText(path)).title, Is.EqualTo("已保存的新标题"));
            }
            finally { File.Delete(path); }
        }
        [Test] public void RecipeImportClearsOldFilePathInRestorableDraft()
        {
            document.filePath = "OriginalMustNotBeOverwritten.json";
            RecipeImporter.ImportInto(document, File.ReadAllText("Docs/LevelRecipes/L01.json"));
            Assert.That(document.filePath, Is.Empty);
            var recovered = ScriptableObject.CreateInstance<LevelDocument>();
            recovered.draftPath = document.draftPath;
            try
            {
                recovered.RestoreDraft(); Assert.That(recovered.filePath, Is.Empty);
                Assert.That(recovered.IsDirty, Is.True); Assert.That(recovered.level.id, Is.EqualTo("L01"));
            }
            finally { UnityEngine.Object.DestroyImmediate(recovered); }
        }
        [Test] public void IncompleteDraftRoundTripPreservesAbsentPlayerAndProof()
        {
            document.level = LevelDocument.NewLevel(); document.solution = null;
            Assert.That(LevelJson.Read(LevelJson.Write(document.level)).playerSpawn, Is.Null);
            document.Backup(); document.RestoreDraft();
            Assert.That(document.level.playerSpawn, Is.Null); Assert.That(document.solution, Is.Null);
        }
        [Test] public void UndoingFirstPlayerPlacementDoesNotFabricatePlayerOrSolution()
        {
            document.level = LevelDocument.NewLevel(); document.solution = null;
            document.Change("放第一个玩家", () => document.Place(LevelBrush.Player, new Cell(1, 3), "E"));
            Undo.PerformUndo();
            Assert.That(document.level.playerSpawn, Is.Null); Assert.That(document.solution, Is.Null);
            Undo.PerformRedo(); Assert.That(document.level.playerSpawn.Cell, Is.EqualTo(new Cell(1, 3)));
        }
    }
}
