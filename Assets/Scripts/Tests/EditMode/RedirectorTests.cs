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
    public sealed class RedirectorTests
    {
        private static LevelDefinition Board()
        {
            var level = LevelDocument.NewLevel(9, 9);
            level.playerSpawn = new PlayerSpawn { x = 1, z = 3, facing = "E" };
            level.crates = new[] { new CrateDefinition { id = "energy", x = 2, z = 3 } };
            level.sockets = new[] { new SocketDefinition { id = "goal", x = 7, z = 7, isGoal = true } };
            level.redirectors = new[] { Plate(3, 3, "N") };
            return level;
        }
        private static RedirectorDefinition Plate(int x, int z, string facing) =>
            new RedirectorDefinition { id = "r_" + x + "_" + z, x = x, z = z, facing = facing };
        private static void Tile(LevelDefinition level, int x, int z, char value)
        {
            var row = level.terrainRows[level.height - z - 1].ToCharArray(); row[x] = value;
            level.terrainRows[level.height - z - 1] = new string(row);
        }

        [TestCase("N", 3, 4)] [TestCase("E", 4, 3)] [TestCase("S", 3, 2)] [TestCase("W", 3, 3)]
        public void ArrowSetsOutgoingDirectionAndPlayerRemainsAtOldCrate(string facing, int x, int z)
        {
            var level = Board(); level.redirectors[0].facing = facing;
            var session = new GameSession(level); var before = session.State;
            var result = session.Move(Direction.E);
            Assert.That(result.Accepted, Is.True);
            Assert.That(session.State.Crates["energy"], Is.EqualTo(new Cell(x, z)));
            Assert.That(session.State.Player, Is.EqualTo(new Cell(2, 3)));
            Assert.That(session.State.Facing, Is.EqualTo(Direction.E));
            Assert.That(session.State.Moves, Is.EqualTo(1)); Assert.That(session.State.Pushes, Is.EqualTo(1));
            Assert.That(session.Undo(), Is.True); Assert.That(session.State, Is.SameAs(before));
        }

        [TestCase(Direction.N)] [TestCase(Direction.E)] [TestCase(Direction.S)] [TestCase(Direction.W)]
        public void CanEnterFromEachSideWithoutReorientingTheRobot(Direction incoming)
        {
            var level = Board(); var opposite = (Direction)(((int)incoming + 2) % 4);
            Cell crate = new Cell(3, 3).Step(opposite), player = crate.Step(opposite);
            level.crates[0].x = crate.x; level.crates[0].z = crate.z;
            level.playerSpawn.x = player.x; level.playerSpawn.z = player.z;
            var result = new GameSession(level).Move(incoming);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.NextState.Crates["energy"], Is.EqualTo(incoming == Direction.S ? new Cell(3, 3) : new Cell(3, 4)));
            Assert.That(result.NextState.Player, Is.EqualTo(crate));
        }

        [TestCase(CrateDefinition.Energy, true)] [TestCase(CrateDefinition.Cargo, false)]
        public void BothCrateKindsTurnButOnlyEnergyPowersTheLandingSocket(string kind, bool complete)
        {
            var level = Board(); level.crates[0].kind = kind; level.sockets[0].x = 3; level.sockets[0].z = 4;
            if (kind == CrateDefinition.Cargo) level.crates = level.crates.Concat(new[] { new CrateDefinition { id = "spare", x = 6, z = 6 } }).ToArray();
            var session = new GameSession(level); session.Move(Direction.E);
            Assert.That(session.State.Completed, Is.EqualTo(complete));
            Assert.That(session.Rules.Power(session.State).Sockets["goal"], Is.EqualTo(complete));
        }

        [Test] public void MultipleCornersAndTracksUseOneCommandEvenBeyondMapSpan()
        {
            var level = Board(); level.redirectors = new[] { Plate(3, 3, "N"), Plate(3, 7, "E"), Plate(7, 7, "S") };
            for (int z = 4; z < 7; z++) Tile(level, 3, z, '~');
            for (int x = 4; x < 7; x++) Tile(level, x, 7, '~');
            for (int z = 2; z < 7; z++) Tile(level, 7, z, '~');
            level.sockets[0].z = 1;
            var session = new GameSession(level); var result = session.Move(Direction.E);
            Assert.That(result.Microsteps.Count, Is.GreaterThan(9)); Assert.That(session.State.Completed, Is.True);
            Assert.That(result.Microsteps.All(s => Math.Abs(s.CrateFrom.x - s.CrateTo.x) + Math.Abs(s.CrateFrom.z - s.CrateTo.z) == 1), Is.True);
            Assert.That(session.UndoCount, Is.EqualTo(1)); session.Undo(); Assert.That(session.Commands, Is.Empty);
        }

        [TestCase('#')] [TestCase('_')]
        public void BlockedOutletStopsOnThePlate(char obstruction)
        {
            var level = Board(); Tile(level, 3, 4, obstruction);
            var result = new GameSession(level).Move(Direction.E);
            Assert.That(result.Accepted, Is.True); Assert.That(result.NextState.Crates["energy"], Is.EqualTo(new Cell(3, 3)));
            Assert.That(result.Microsteps.Count, Is.EqualTo(1));
        }

        [Test] public void ClearingAnOutletDoesNotRestartAndNextPushMayLeaveInAnotherDirection()
        {
            var level = Board(); level.sockets[0].x = 3; level.sockets[0].z = 2;
            level.crates = level.crates.Concat(new[] { new CrateDefinition { id = "brake", kind = CrateDefinition.Cargo, x = 3, z = 4 } }).ToArray();
            var session = new GameSession(level); session.Move(Direction.E); session.Move(Direction.N); session.Move(Direction.E);
            Assert.That(session.State.Crates["energy"], Is.EqualTo(new Cell(3, 3)));
            Assert.That(session.State.Crates["brake"], Is.EqualTo(new Cell(4, 4)));
            Assert.That(session.Move(Direction.S).Accepted, Is.True); Assert.That(session.State.Completed, Is.True);
        }

        [TestCase(false)] [TestCase(true)] public void GateEntryUsesCurrentPowerAndAnOpenGateIsAStop(bool powered)
        {
            var level = Board(); level.gates = new[] { new GateDefinition { id = "gate", x = 3, z = 4, facing = "N", powerMode = "Any", sourceSocketIds = new[] { "goal" } } };
            if (powered)
            {
                level.crates = level.crates.Concat(new[] { new CrateDefinition { id = "supply", x = 7, z = 7 } }).ToArray();
                level.sockets[0].isGoal = false;
                level.sockets = level.sockets.Concat(new[] { new SocketDefinition { id = "target", x = 6, z = 6, isGoal = true } }).ToArray();
            }
            var session = new GameSession(level); var result = session.Move(Direction.E);
            Assert.That(session.State.Crates["energy"], Is.EqualTo(new Cell(3, powered ? 4 : 3)));
            Assert.That(result.Microsteps.Count, Is.EqualTo(powered ? 2 : 1));
        }

        [Test] public void LeavingSupplyBeforeTurningCannotCoastThroughTheClosedGate()
        {
            var level = Board(); level.sockets = level.sockets.Concat(new[] { new SocketDefinition { id = "supply", x = 2, z = 3 } }).ToArray();
            level.gates = new[] { new GateDefinition { id = "gate", x = 3, z = 4, facing = "N", powerMode = "Any", sourceSocketIds = new[] { "supply" } } };
            var session = new GameSession(level); session.Move(Direction.E);
            Assert.That(session.State.Crates["energy"], Is.EqualTo(new Cell(3, 3)));
            Assert.That(session.Rules.Power(session.State).OpenGates["gate"], Is.False);
            session.Undo(); Assert.That(session.Rules.Power(session.State).OpenGates["gate"], Is.True);
        }

        [Test] public void CycleRejectsTheEntireMoveIncludingPowerHistoryAndCounters()
        {
            var level = Board(); level.redirectors = new[] { Plate(3, 3, "N"), Plate(3, 4, "E"), Plate(4, 4, "S"), Plate(4, 3, "W") };
            level.sockets = level.sockets.Concat(new[] { new SocketDefinition { id = "supply", x = 2, z = 3 } }).ToArray();
            var session = new GameSession(level); var before = session.State;
            var result = session.Move(Direction.E);
            Assert.That(result.RejectReason, Is.EqualTo(RejectReason.TransportCycle)); Assert.That(result.Microsteps, Is.Empty);
            Assert.That(session.State, Is.SameAs(before)); Assert.That(session.UndoCount, Is.Zero); Assert.That(session.Commands, Is.Empty);
            Assert.That(session.Rules.Power(session.State).Sockets["supply"], Is.True);
        }

        [Test] public void StandingWalkingSpawningAndGmPlacementNeverTriggerTransport()
        {
            var level = Board(); level.crates[0].x = 3;
            var session = new GameSession(level);
            Assert.That(session.State.Crates["energy"], Is.EqualTo(new Cell(3, 3)));
            Assert.That(session.TryApplyDebugEdit(DebugBoardEdit.Place("energy", new Cell(4, 3)), out _), Is.True);
            Assert.That(session.TryApplyDebugEdit(DebugBoardEdit.Place("energy", new Cell(3, 3)), out _), Is.True);
            Assert.That(session.State.Crates["energy"], Is.EqualTo(new Cell(3, 3)));
            session.TryApplyDebugEdit(DebugBoardEdit.Place("energy", new Cell(5, 5)), out _);
            session.Move(Direction.E); session.Move(Direction.E);
            Assert.That(session.State.Player, Is.EqualTo(new Cell(3, 3)));
            var copy = BoardSnapshot.Capture(level, session.State).ApplyTo(level);
            copy.redirectors[0].facing = "W"; Assert.That(level.redirectors[0].facing, Is.EqualTo("N"));
        }

        [TestCase("L04")] [TestCase("L05")] [TestCase("L06")] [TestCase("LAB01_LowFriction")]
        public void ExistingWireFormatHashAndProofStayUnchanged(string id)
        {
            string group = id.StartsWith("LAB") ? "test_levels" : "levels";
            var level = LevelJson.Read(Resources.Load<TextAsset>("configs/" + group + "/" + id).text);
            var proof = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/" + id + ".solution").text);
            Assert.That(level.redirectors, Is.Empty); Assert.That(LevelJson.Write(level), Does.Not.Contain("redirectors"));
            Assert.That(LevelJson.Hash(level), Is.EqualTo(proof.contentHash)); Assert.DoesNotThrow(() => proof.Verify(level, true));
        }

        [Test] public void V3IsStrictAndCannotBeSilentlySavedAsLegacy()
        {
            var level = Board(); string json = LevelJson.Write(level);
            Assert.That(LevelJson.Write(LevelJson.Read(json)), Is.EqualTo(json));
            Assert.Throws<FormatException>(() => LevelJson.Read(json.Replace("\"redirectors\"", "\"wrongField\"")));
            Assert.Throws<FormatException>(() => LevelJson.Read(json.Replace("\"schemaVersion\": 3", "\"schemaVersion\": 2")));
            level.schemaVersion = 2; Assert.Throws<FormatException>(() => LevelJson.Write(level));
            level.schemaVersion = 3; level.redirectors[0].facing = "NE";
            Assert.That(LevelValidator.Validate(level).Issues.Any(i => i.IsError && i.Cell == new Cell(3, 3)), Is.True);
            level.redirectors[0].facing = "N"; level.redirectors[0].x = 7; level.redirectors[0].z = 7;
            Assert.That(LevelValidator.Validate(level).IsValid, Is.False);
            level.redirectors[0].x = 3; level.redirectors[0].z = 3; Tile(level, 3, 3, '~');
            Assert.That(LevelValidator.Validate(level).IsValid, Is.False);
        }

        [Test] public void AuthoringUpgradeUndoMoveCropAndDraftRecoveryKeepDirections()
        {
            var document = ScriptableObject.CreateInstance<LevelDocument>();
            document.draftPath = "Library/SokobanDrafts/Redirector_" + Guid.NewGuid().ToString("N") + ".json";
            try
            {
                document.level = LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L04").text);
                string original = LevelJson.Hash(document.level), id = null;
                document.Change("转向板", () => id = document.Place(LevelBrush.Redirector, new Cell(4, 2), "E"));
                Assert.That(document.level.schemaVersion, Is.EqualTo(3));
                Undo.PerformUndo(); Assert.That(LevelJson.Hash(document.level), Is.EqualTo(original));
                Undo.PerformRedo(); Assert.That(((RedirectorDefinition)document.Find(id)).facing, Is.EqualTo("E"));
                document.SetCrateKind(document.level.crates[0].id, CrateDefinition.Cargo);
                Assert.That(document.level.schemaVersion, Is.EqualTo(3));
                Assert.Throws<InvalidOperationException>(() => document.Paint(new Cell(4, 2), '~'));
                document.Change("用目标插槽覆盖转向板", () => document.Place(LevelBrush.GoalSocket, new Cell(4, 2), "N"));
                Assert.That(document.Find(id), Is.Null);
                Assert.That(document.level.sockets.Any(s => s.Cell == new Cell(4, 2) && s.isGoal), Is.True);
                Undo.PerformUndo(); Assert.That(((RedirectorDefinition)document.Find(id)).facing, Is.EqualTo("E"));
                Assert.Throws<InvalidOperationException>(() => document.Move(id, document.level.sockets[0].Cell));
                document.Change("移动转向板", () => { document.Resize(9, 9); document.Paint(new Cell(7, 7), '.'); document.Move(id, new Cell(7, 7)); });
                Assert.That(document.CroppedCount(7, 7), Is.EqualTo(1));
                document.Change("缩图", () => document.Resize(7, 7)); Assert.That(document.level.redirectors, Is.Empty);
                Undo.PerformUndo(); document.Backup(); document.RestoreDraft();
                Assert.That(document.level.redirectors.Single().Cell, Is.EqualTo(new Cell(7, 7)));
                Assert.That(document.level.redirectors.Single().facing, Is.EqualTo("E"));
                document.Change("刷墙", () => document.Paint(new Cell(7, 7), '#')); Assert.That(document.level.redirectors, Is.Empty);
                Undo.PerformUndo(); Assert.That(document.level.redirectors.Length, Is.EqualTo(1));
            }
            finally { if (File.Exists(document.draftPath)) File.Delete(document.draftPath); Undo.ClearUndo(document); UnityEngine.Object.DestroyImmediate(document); }
        }

        [TestCase("L07")] [TestCase("L08")] [TestCase("L09")] [TestCase("L10")] [TestCase("L11")] [TestCase("L12")]
        public void ShippedRecipeRoundTripsAndUsesEveryAuthoredMechanic(string id)
        {
            var document = ScriptableObject.CreateInstance<LevelDocument>();
            document.draftPath = "Library/SokobanDrafts/Recipe_" + Guid.NewGuid().ToString("N") + ".json";
            try
            {
                RecipeImporter.ImportInto(document, File.ReadAllText("Docs/LevelRecipes/" + id + ".json"));
                var shipped = LevelJson.Read(Resources.Load<TextAsset>("configs/levels/" + id).text);
                Assert.That(LevelJson.Hash(document.level), Is.EqualTo(LevelJson.Hash(shipped)));
                Assert.That(shipped.width, Is.LessThanOrEqualTo(9)); Assert.That(shipped.height, Is.LessThanOrEqualTo(9));
                Assert.That(shipped.schemaVersion, Is.EqualTo(3)); Assert.That(LevelValidator.Validate(shipped).IsValid, Is.True);
                document.RestoreDraft(); Assert.DoesNotThrow(() => document.solution.Verify(document.level, true));
                var session = new GameSession(shipped);
                var crateCells = new System.Collections.Generic.HashSet<Cell>(); var allCells = new System.Collections.Generic.HashSet<Cell>();
                var pushedIds = new System.Collections.Generic.HashSet<string>();
                foreach (char input in document.solution.commands)
                {
                    var result = session.Move((Direction)Enum.Parse(typeof(Direction), input.ToString())); Assert.That(result.Accepted, Is.True);
                    foreach (var step in result.Microsteps)
                    {
                        allCells.Add(step.PlayerTo);
                        if (step.CrateId == null) continue;
                        pushedIds.Add(step.CrateId); crateCells.Add(step.CrateTo); allCells.Add(step.CrateTo);
                    }
                }
                Assert.That(session.State.Completed, Is.True);
                Assert.That(shipped.redirectors.All(r => crateCells.Contains(r.Cell)), Is.True, "Every turn plate must participate.");
                Assert.That(crateCells.Any(c => shipped.TerrainAt(c) == Sokoban.Domain.Terrain.LowFriction), Is.True);
                Assert.That(shipped.gates.All(g => allCells.Contains(g.Cell)), Is.True, "Every gate must participate.");
                Assert.That(shipped.crates.All(c => pushedIds.Contains(c.id)), Is.True, "Every crate needs a purpose.");
                var proof = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/" + id + ".solution").text);
                Assert.DoesNotThrow(() => proof.Verify(shipped, true));
            }
            finally { if (File.Exists(document.draftPath)) File.Delete(document.draftPath); UnityEngine.Object.DestroyImmediate(document); }
        }
    }
}
