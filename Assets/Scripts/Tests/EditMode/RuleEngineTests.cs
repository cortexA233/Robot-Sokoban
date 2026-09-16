using System;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using Sokoban.Editor;
using UnityEngine;

namespace Sokoban.Tests
{
    public sealed class RuleEngineTests
    {
        private static LevelDefinition Board()
        {
            var level = LevelDocument.NewLevel(9, 7);
            level.playerSpawn = new PlayerSpawn { x = 1, z = 3, facing = "E" };
            level.crates = new[] { new CrateDefinition { id = "c1", x = 2, z = 3 } };
            level.sockets = new[] { new SocketDefinition { id = "goal", x = 6, z = 3, isGoal = true } };
            return level;
        }
        private static void Terrain(LevelDefinition level, int x, int z, char value)
        {
            var row = level.terrainRows[level.height - 1 - z].ToCharArray(); row[x] = value;
            level.terrainRows[level.height - 1 - z] = new string(row);
        }
        private static LevelDefinition Ice()
        { var level = Board(); for (int x = 3; x <= 5; x++) Terrain(level, x, 3, '~'); return level; }

        [TestCase(Direction.N, 1, 4)] [TestCase(Direction.S, 1, 2)] [TestCase(Direction.W, 0, 3)]
        public void OrdinaryMoveIsOneGridCell(Direction direction, int x, int z)
        {
            var rules = new RuleEngine(Board()); var initial = rules.CreateInitialState(); var move = rules.Resolve(initial, direction);
            Assert.That(move.Accepted, Is.True); Assert.That(move.NextState.Player, Is.EqualTo(new Cell(x, z)));
            Assert.That(move.NextState.Moves, Is.EqualTo(1)); Assert.That(move.NextState.Pushes, Is.Zero);
            Assert.That(initial.Player, Is.EqualTo(new Cell(1, 3))); Assert.That(initial.Moves, Is.Zero);
        }
        [TestCase('#')] [TestCase('_')]
        public void WallAndVoidRejectWithoutChangingState(char terrain)
        {
            var level = Board(); Terrain(level, 1, 4, terrain); var rules = new RuleEngine(level); var state = rules.CreateInitialState();
            var result = rules.Resolve(state, Direction.N); Assert.That(result.Accepted, Is.False); Assert.That(result.NextState, Is.SameAs(state));
        }
        [Test] public void MapEdgeCannotBeCrossed()
        {
            var level = Board(); level.playerSpawn.x = 0; var session = new GameSession(level);
            Assert.That(session.Move(Direction.W).Accepted, Is.False); Assert.That(session.State.Moves, Is.Zero);
        }
        [Test] public void PushMovesPlayerIntoOldCrateCellAndCountsOnce()
        {
            var session = new GameSession(Board()); var result = session.Move(Direction.E);
            Assert.That(result.Accepted, Is.True); Assert.That(session.State.Player, Is.EqualTo(new Cell(2, 3)));
            Assert.That(session.State.Crates["c1"], Is.EqualTo(new Cell(3, 3))); Assert.That(session.State.Pushes, Is.EqualTo(1));
        }
        [TestCase(true)] [TestCase(false)] public void BlockedFirstPushIsAtomic(bool anotherCrate)
        {
            var level = Board();
            if (anotherCrate) level.crates = level.crates.Concat(new[] { new CrateDefinition { id = "c2", x = 3, z = 3 } }).ToArray();
            else Terrain(level, 3, 3, '#');
            var session = new GameSession(level); var before = session.State;
            Assert.That(session.Move(Direction.E).Accepted, Is.False); Assert.That(session.State, Is.SameAs(before)); Assert.That(session.Commands, Is.Empty);
        }
        [TestCase("Any", false, false, false)] [TestCase("Any", false, true, true)]
        [TestCase("Any", true, false, true)] [TestCase("Any", true, true, true)]
        [TestCase("All", false, false, false)] [TestCase("All", false, true, false)]
        [TestCase("All", true, false, false)] [TestCase("All", true, true, true)]
        public void GatePowerTruthTable(string mode, bool first, bool second, bool expected)
        {
            var level = Board();
            level.crates = new[] { new CrateDefinition { id = "c1", x = first ? 4 : 2, z = first ? 1 : 4 }, new CrateDefinition { id = "c2", x = second ? 5 : 3, z = second ? 1 : 4 } };
            level.sockets = level.sockets.Concat(new[] { new SocketDefinition { id = "s1", x = 4, z = 1 }, new SocketDefinition { id = "s2", x = 5, z = 1 } }).ToArray();
            level.gates = new[] { new GateDefinition { id = "g", x = 6, z = 2, facing = "E", powerMode = mode, sourceSocketIds = new[] { "s1", "s2" } } };
            var session = new GameSession(level); Assert.That(session.Rules.Power(session.State).OpenGates["g"], Is.EqualTo(expected));
        }
        [Test] public void OccupiedUnpoweredGateAllowsPushAndPlayerHandoff()
        {
            var level = Board(); level.gates = new[] { new GateDefinition { id = "g", x = 2, z = 3, facing = "E", powerMode = "Any", sourceSocketIds = new[] { "goal" } } };
            var session = new GameSession(level);
            Assert.That(session.Rules.Power(session.State).OpenGates["g"], Is.True);
            Assert.That(session.Move(Direction.E).Accepted, Is.True);
            Assert.That(session.Rules.Power(session.State).OpenGates["g"], Is.True);
            Assert.That(session.Rules.Power(session.State).PoweredGates["g"], Is.False);
            session.Move(Direction.N); Assert.That(session.Rules.Power(session.State).OpenGates["g"], Is.False);
            session.Undo(); Assert.That(session.Rules.Power(session.State).OpenGates["g"], Is.True);
        }
        [Test] public void IceSlidesToGoalWithoutMovingPlayerAgain()
        {
            var session = new GameSession(Ice()); var result = session.Move(Direction.E);
            Assert.That(session.State.Player, Is.EqualTo(new Cell(2, 3))); Assert.That(session.State.Crates["c1"], Is.EqualTo(new Cell(6, 3)));
            Assert.That(session.State.Moves, Is.EqualTo(1)); Assert.That(session.State.Pushes, Is.EqualTo(1));
            Assert.That(result.Microsteps.Count, Is.EqualTo(4)); Assert.That(session.State.Completed, Is.True);
            Assert.That(session.Rules.Power(session.State).Sockets["goal"], Is.True);
        }
        [TestCase(true)] [TestCase(false)] public void IceStopsBeforeWallOrCrate(bool wall)
        {
            var level = Ice(); level.sockets[0].z = 4;
            if (wall) Terrain(level, 6, 3, '#');
            else level.crates = level.crates.Concat(new[] { new CrateDefinition { id = "c2", x = 6, z = 3 } }).ToArray();
            var session = new GameSession(level); session.Move(Direction.E);
            Assert.That(session.State.Crates["c1"], Is.EqualTo(new Cell(5, 3)));
            if (!wall) Assert.That(session.State.Crates["c2"], Is.EqualTo(new Cell(6, 3)));
        }
        [Test] public void PushFromIceStopsOnFirstOrdinaryFloor()
        {
            var level = Ice(); Terrain(level, 3, 3, '.'); level.playerSpawn.x = 5; level.crates[0].x = 4;
            var session = new GameSession(level); session.Move(Direction.W);
            Assert.That(session.State.Crates["c1"], Is.EqualTo(new Cell(3, 3))); Assert.That(session.State.Player, Is.EqualTo(new Cell(4, 3)));
        }
        [TestCase(false, 5)] [TestCase(true, 6)] public void IceRecomputesPowerBeforeEachMicrostep(bool alternativePower, int expectedX)
        {
            var level = Ice(); level.sockets[0].z = 4;
            level.sockets = level.sockets.Concat(new[] { new SocketDefinition { id = "s", x = 2, z = 3 }, new SocketDefinition { id = "other", x = 7, z = 1 } }).ToArray();
            level.gates = new[] { new GateDefinition { id = "g", x = 6, z = 3, facing = "E", powerMode = "Any", sourceSocketIds = new[] { alternativePower ? "other" : "s" } } };
            if (alternativePower) level.crates = level.crates.Concat(new[] { new CrateDefinition { id = "c2", x = 7, z = 1 } }).ToArray();
            var session = new GameSession(level); var initial = session.State; var result = session.Move(Direction.E);
            Assert.That(result.Microsteps[0].Power.Sockets["s"], Is.False);
            Assert.That(session.State.Crates["c1"], Is.EqualTo(new Cell(expectedX, 3)));
            session.Undo(); Assert.That(session.State, Is.SameAs(initial)); Assert.That(session.Rules.Power(session.State).Sockets["s"], Is.True);
        }
        [Test] public void FirstStepBlockedWhileCrateOnIceStillRejects()
        {
            var level = Ice(); level.playerSpawn.x = 2; level.crates[0].x = 3; Terrain(level, 4, 3, '#');
            var session = new GameSession(level); Assert.That(session.Move(Direction.E).Accepted, Is.False); Assert.That(session.State.Moves, Is.Zero);
        }
        [Test] public void PlayerOnSocketNeverPowersAndUtilityDoesNotComplete()
        {
            var level = Board(); level.sockets = level.sockets.Concat(new[] { new SocketDefinition { id = "s", x = 3, z = 3 }, new SocketDefinition { id = "p", x = 1, z = 3 } }).ToArray();
            var session = new GameSession(level); Assert.That(session.Rules.Power(session.State).Sockets["p"], Is.False);
            session.Move(Direction.E); Assert.That(session.Rules.Power(session.State).Sockets["s"], Is.True); Assert.That(session.State.Completed, Is.False);
        }
        [Test] public void SuccessfulCommandRecordingTracksUndoAndRestart()
        {
            var session = new GameSession(Ice()); var before = session.State;
            session.Move(Direction.E); session.Move(Direction.E); Assert.That(session.Commands, Is.EqualTo("E"));
            session.Undo(); Assert.That(session.State, Is.SameAs(before)); Assert.That(session.Commands, Is.Empty);
            session.Move(Direction.N); session.Restart(); Assert.That(session.UndoCount, Is.Zero); Assert.That(session.Commands, Is.Empty); Assert.That(session.State.Moves, Is.Zero);
        }
        [Test] public void SessionOwnsDefinitionAndDoesNotMutateAuthorData()
        {
            var level = Board(); var before = LevelJson.Write(level); var session = new GameSession(level); session.Move(Direction.E);
            Assert.That(LevelJson.Write(level), Is.EqualTo(before)); Terrain(level, 3, 3, '#'); session.Restart();
            Assert.That(session.Move(Direction.E).Accepted, Is.True);
        }
        [TestCase("L01", 8, 3, 3, 4)] [TestCase("L02", 32, 16, 8, 2)] [TestCase("L03", 68, 28, 15, 3)]
        [TestCase("L04", 22, 8, 3, 4)] [TestCase("L05", 42, 13, 3, 4)] [TestCase("L06", 41, 15, 3, 5)]
        [TestCase("LAB01_LowFriction", 1, 1, 2, 3)]
        public void PublishedRecipeMatchesGddReference(string id, int moves, int pushes, int x, int z)
        {
            var asset = Resources.Load<TextAsset>("configs/" + (id.StartsWith("LAB") ? "test_levels/" : "levels/") + id);
            var level = LevelJson.Read(asset.text); var proof = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/" + id + ".solution").text);
            Assert.That(proof.expectedMoves, Is.EqualTo(moves)); Assert.That(proof.expectedPushes, Is.EqualTo(pushes)); Assert.That(proof.expectedPlayer, Is.EqualTo(new Cell(x, z)));
            Assert.DoesNotThrow(() => proof.Verify(level, true));
        }
    }
}
