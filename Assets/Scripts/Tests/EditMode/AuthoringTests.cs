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
            // A small authoring fixture keeps device/resize coverage independent of shipped levels.
            document.level = LevelDocument.NewLevel(11, 7);
            document.level.schemaVersion = 1;
            document.level.playerSpawn = new PlayerSpawn { x = 2, z = 2, facing = "E" };
            document.level.crates = new[]
            {
                new CrateDefinition { id = "crate_01", x = 3, z = 2 },
                new CrateDefinition { id = "crate_02", x = 3, z = 4 }
            };
            document.level.sockets = new[]
            {
                new SocketDefinition { id = "socket_s", x = 4, z = 2, isGoal = false },
                new SocketDefinition { id = "socket_a", x = 9, z = 4, isGoal = true },
                new SocketDefinition { id = "socket_b", x = 9, z = 2, isGoal = true }
            };
            document.level.gates = new[] { new GateDefinition { id = "gate_01", x = 6, z = 4, facing = "E",
                powerMode = "Any", sourceSocketIds = new[] { "socket_s", "socket_a" } } };
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
        [Test] public void CrateReplacementKeepsIdentityAndSocketAndSupportsUndoRedo()
        {
            var cell = new Cell(4, 2); document.Move("crate_01", cell);
            string before = LevelJson.Hash(document.level); string id = null;
            document.Change("覆盖箱型", () => id = document.Place(LevelBrush.CargoCrate, cell, "N"));
            Assert.That(id, Is.EqualTo("crate_01")); Assert.That(document.level.crates.Length, Is.EqualTo(2));
            Assert.That(((CrateDefinition)document.Find(id)).kind, Is.EqualTo(CrateDefinition.Cargo));
            Assert.That(document.level.sockets.Any(s => s.id == "socket_s" && s.Cell == cell), Is.True);
            Assert.That(document.level.schemaVersion, Is.EqualTo(2));
            Undo.PerformUndo(); Assert.That(LevelJson.Hash(document.level), Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(((CrateDefinition)document.Find(id)).kind, Is.EqualTo(CrateDefinition.Cargo));
        }
        [Test] public void PlayerAndCrateReplacementOnlyChangesActorLayer()
        {
            var cell = new Cell(4, 2); document.Place(LevelBrush.Player, cell, "E");
            string crate = document.Place(LevelBrush.Crate, cell, "N");
            Assert.That(document.level.playerSpawn, Is.Null);
            Assert.That(document.Find(crate).Cell, Is.EqualTo(cell));
            Assert.That(document.Find("socket_s"), Is.Not.Null);
            document.Place(LevelBrush.Player, cell, "S");
            Assert.That(document.Find(crate), Is.Null);
            Assert.That(document.level.playerSpawn.Cell, Is.EqualTo(cell));
            Assert.That(document.level.playerSpawn.facing, Is.EqualTo("S"));
            Assert.That(document.Find("socket_s"), Is.Not.Null);
        }
        [Test] public void SocketRoleReplacementPreservesIdAndGateConnections()
        {
            var cell = new Cell(4, 2); string[] sources = document.level.gates[0].sourceSocketIds.ToArray();
            string goal = document.Place(LevelBrush.GoalSocket, cell, "N");
            Assert.That(goal, Is.EqualTo("socket_s")); Assert.That(((SocketDefinition)document.Find(goal)).isGoal, Is.True);
            string utility = document.Place(LevelBrush.UtilitySocket, cell, "N");
            Assert.That(utility, Is.EqualTo(goal)); Assert.That(((SocketDefinition)document.Find(utility)).isGoal, Is.False);
            Assert.That(document.level.gates[0].sourceSocketIds, Is.EqualTo(sources));
            Assert.That(document.level.sockets.Count(s => s.Cell == cell), Is.EqualTo(1));
        }
        [Test] public void DifferentDeviceReplacementRemovesSocketReferencesAndUndoRestoresThem()
        {
            var cell = new Cell(4, 2); document.Move("crate_01", cell);
            string before = LevelJson.Hash(document.level); string id = null;
            document.Change("覆盖机关", () => id = document.Place(LevelBrush.Redirector, cell, "E"));
            Assert.That(document.Find("socket_s"), Is.Null);
            Assert.That(document.Find("crate_01").Cell, Is.EqualTo(cell));
            Assert.That(document.level.gates[0].sourceSocketIds, Is.EqualTo(new[] { "socket_a" }));
            Assert.That(((RedirectorDefinition)document.Find(id)).facing, Is.EqualTo("E"));
            Undo.PerformUndo(); Assert.That(LevelJson.Hash(document.level), Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(document.Find("socket_s"), Is.Null);
        }
        [Test] public void RepaintingGateAndRedirectorPreservesTheirIdentityAndSettings()
        {
            var gate = document.level.gates[0]; gate.powerMode = "All"; string[] sources = gate.sourceSocketIds.ToArray();
            Assert.That(document.Place(LevelBrush.Gate, gate.Cell, "S"), Is.EqualTo(gate.id));
            Assert.That(gate.facing, Is.EqualTo("S")); Assert.That(gate.powerMode, Is.EqualTo("All"));
            Assert.That(gate.sourceSocketIds, Is.EqualTo(sources));
            var cell = new Cell(1, 1); string id = document.Place(LevelBrush.Redirector, cell, "N");
            Assert.That(document.Place(LevelBrush.Redirector, cell, "W"), Is.EqualTo(id));
            Assert.That(document.level.redirectors.Length, Is.EqualTo(1));
            Assert.That(((RedirectorDefinition)document.Find(id)).facing, Is.EqualTo("W"));
        }
        [Test] public void RejectedReplacementLeavesExistingObjectsAndReferencesUntouched()
        {
            var cell = new Cell(4, 2); document.Place(LevelBrush.Player, cell, "E");
            string before = LevelJson.Hash(document.level);
            Assert.Throws<InvalidOperationException>(() => document.Place(LevelBrush.Gate, cell, "N"));
            Assert.That(LevelJson.Hash(document.level), Is.EqualTo(before));
            Assert.Throws<InvalidOperationException>(() => document.Place(LevelBrush.Redirector, cell, "invalid"));
            Assert.That(LevelJson.Hash(document.level), Is.EqualTo(before));
        }
        [Test] public void RecipeValidationRejectsOverlapsBeforeReplacementOperationsRun()
        {
            string before = LevelJson.Hash(document.level); var invalid = document.level.Copy();
            invalid.crates[0].x = invalid.crates[1].x; invalid.crates[0].z = invalid.crates[1].z;
            Assert.Throws<FormatException>(() => RecipeImporter.ImportInto(document, "{\"definition\":" + LevelJson.Write(invalid) + "}"));
            Assert.That(LevelJson.Hash(document.level), Is.EqualTo(before));
        }
        [Test] public void DraftSurvivesSerializationAndIsIsolated()
        {
            document.level.playerSpawn.facing = "W"; document.Backup();
            var recovered = ScriptableObject.CreateInstance<LevelDocument>();
            recovered.draftPath = document.draftPath;
            try { recovered.RestoreDraft(); Assert.That(LevelJson.Hash(recovered.level), Is.EqualTo(LevelJson.Hash(document.level))); recovered.level.playerSpawn.facing = "E"; Assert.That(document.level.playerSpawn.facing, Is.EqualTo("W")); }
            finally { UnityEngine.Object.DestroyImmediate(recovered); }
        }
        [Test] public void ProofExpiresOnEditAndCanBeReplayedAndRebound()
        {
            document.level = LevelJson.Read(Resources.Load<TextAsset>("configs/test_levels/LAB01_LowFriction").text);
            var proof = SolutionRecord.Capture(document.level, SolutionRecord.ReplayCommands(document.level, "E"));
            document.level.playerSpawn.facing = document.level.playerSpawn.facing == "N" ? "S" : "N";
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
                string facing = document.level.playerSpawn.facing == "N" ? "S" : "N";
                document.Change("更改朝向", () => document.level.playerSpawn.facing = facing);
                document.Save(path, false); Assert.That(document.IsDirty, Is.False);
                Undo.PerformUndo(); Assert.That(document.IsDirty, Is.True);
                Assert.That(LevelJson.Read(File.ReadAllText(path)).playerSpawn.facing, Is.EqualTo(facing));
            }
            finally { File.Delete(path); }
        }
        [Test] public void RecipeImportClearsOldFilePathInRestorableDraft()
        {
            document.filePath = "OriginalMustNotBeOverwritten.json";
            RecipeImporter.ImportInto(document, File.ReadAllText("Docs/LevelRecipes/L04.json"));
            Assert.That(document.filePath, Is.Empty);
            var recovered = ScriptableObject.CreateInstance<LevelDocument>();
            recovered.draftPath = document.draftPath;
            try
            {
                recovered.RestoreDraft(); Assert.That(recovered.filePath, Is.Empty);
                Assert.That(recovered.IsDirty, Is.True); Assert.That(recovered.level.id, Is.EqualTo("L04"));
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
