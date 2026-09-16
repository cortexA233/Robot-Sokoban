using System;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using Sokoban.Editor;

namespace Sokoban.Tests
{
    public sealed class CargoCrateTests
    {
        private static LevelDefinition Board()
        {
            var level = LevelDocument.NewLevel(9, 7);
            level.playerSpawn = new PlayerSpawn { x = 1, z = 3, facing = "E" };
            level.crates = new[]
            {
                new CrateDefinition { id = "cargo", x = 2, z = 3, kind = CrateDefinition.Cargo },
                new CrateDefinition { id = "energy", x = 7, z = 5 }
            };
            level.sockets = new[] { new SocketDefinition { id = "goal", x = 6, z = 3, isGoal = true } };
            return level;
        }

        [TestCase(true)] [TestCase(false)]
        public void CargoOnGoalOrUtilityDoesNotSupplyPower(bool goal)
        {
            var level = Board();
            level.sockets = goal
                ? new[] { new SocketDefinition { id = "socket", x = 3, z = 3, isGoal = true } }
                : level.sockets.Concat(new[] { new SocketDefinition { id = "socket", x = 3, z = 3 } }).ToArray();
            level.gates = new[] { new GateDefinition { id = "gate", x = 5, z = 3, facing = "E", powerMode = "Any", sourceSocketIds = new[] { "socket" } } };
            var session = new GameSession(level);
            Assert.That(session.Move(Direction.E).Accepted, Is.True);
            Assert.That(session.State.Crates["cargo"], Is.EqualTo(new Cell(3, 3)));
            Assert.That(session.State.Player, Is.EqualTo(new Cell(2, 3)));
            Assert.That(session.Rules.Power(session.State).Sockets["socket"], Is.False);
            Assert.That(session.Rules.Power(session.State).OpenGates["gate"], Is.False);
            Assert.That(session.State.Completed, Is.False);
            Assert.That(session.State.Moves, Is.EqualTo(1)); Assert.That(session.State.Pushes, Is.EqualTo(1));
        }

        [Test] public void CargoDoesNotCountTowardEnergyRequirementOrInitialCompletion()
        {
            var level = Board(); level.sockets[0].x = 2;
            Assert.That(LevelValidator.Validate(level).Issues.Any(i => i.Message.Contains("开局已经完成")), Is.False);
            level.crates[1].kind = CrateDefinition.Cargo;
            Assert.That(LevelValidator.Validate(level).Issues.Any(i => i.IsError && i.Message.Contains("能源箱数")), Is.True);
        }

        [Test] public void CargoMayStayInCornerWhenEnergyCompletesGoal()
        {
            var level = Board(); level.crates[0].x = 0; level.crates[0].z = 0;
            level.crates[1].x = 6; level.crates[1].z = 3;
            Assert.That(LevelValidator.Validate(level).IsValid, Is.True);
            Assert.That(new GameSession(level).State.Completed, Is.True);
        }

        [TestCase("Any", true)] [TestCase("All", false)]
        public void MixedCratesRespectGateModes(string mode, bool powered)
        {
            var level = Board();
            level.sockets = level.sockets.Concat(new[]
            {
                new SocketDefinition { id = "cargo_socket", x = 2, z = 3 },
                new SocketDefinition { id = "energy_socket", x = 7, z = 5 }
            }).ToArray();
            level.gates = new[] { new GateDefinition { id = "gate", x = 5, z = 3, facing = "E", powerMode = mode, sourceSocketIds = new[] { "cargo_socket", "energy_socket" } } };
            var session = new GameSession(level);
            Assert.That(session.Rules.Power(session.State).PoweredGates["gate"], Is.EqualTo(powered));
            Assert.That(session.Rules.Power(session.State).Sockets["cargo_socket"], Is.False);
        }

        [Test] public void CargoHoldsUnpoweredGateUntilPlayerLeavesAndUndoRestoresIt()
        {
            var level = Board();
            level.gates = new[] { new GateDefinition { id = "gate", x = 2, z = 3, facing = "E", powerMode = "Any", sourceSocketIds = new[] { "goal" } } };
            var session = new GameSession(level);
            Assert.That(session.Rules.Power(session.State).OpenGates["gate"], Is.True);
            Assert.That(session.Move(Direction.E).Accepted, Is.True);
            Assert.That(session.Rules.Power(session.State).OpenGates["gate"], Is.True);
            Assert.That(session.Rules.Power(session.State).PoweredGates["gate"], Is.False);
            session.Move(Direction.N);
            Assert.That(session.Rules.Power(session.State).OpenGates["gate"], Is.False);
            session.Undo(); Assert.That(session.Rules.Power(session.State).OpenGates["gate"], Is.True);
            session.Restart(); Assert.That(session.State.Crates["cargo"], Is.EqualTo(new Cell(2, 3)));
            Assert.That(session.State.Moves, Is.Zero);
        }

        [Test] public void CargoSlidesAsOneCommandWithoutPoweringGoalAndCanUndo()
        {
            var level = Board(); level.terrainRows[3] = "...~~~...";
            var session = new GameSession(level); var before = session.State;
            var result = session.Move(Direction.E);
            Assert.That(result.Microsteps.Count, Is.EqualTo(4));
            Assert.That(session.State.Crates["cargo"], Is.EqualTo(new Cell(6, 3)));
            Assert.That(session.State.Player, Is.EqualTo(new Cell(2, 3)));
            Assert.That(session.State.Pushes, Is.EqualTo(1)); Assert.That(session.State.Completed, Is.False);
            Assert.That(session.Rules.Power(session.State).Sockets["goal"], Is.False);
            session.Undo(); Assert.That(session.State, Is.SameAs(before));
        }

        [TestCase("Energy", "Cargo")] [TestCase("Cargo", "Energy")] [TestCase("Cargo", "Cargo")]
        public void MixedCratesNeverChainPush(string first, string second)
        {
            var level = Board(); level.crates[0].kind = first;
            level.crates = level.crates.Concat(new[] { new CrateDefinition { id = "blocker", x = 3, z = 3, kind = second } }).ToArray();
            var session = new GameSession(level); var before = session.State;
            Assert.That(session.Move(Direction.E).RejectReason, Is.EqualTo(RejectReason.ChainPush));
            Assert.That(session.State, Is.SameAs(before));
        }

        [TestCase(null)] [TestCase("")] [TestCase("Heavy")]
        public void UnknownKindIsLocatedValidationError(string kind)
        {
            var level = Board(); level.crates[0].kind = kind;
            Assert.That(LevelValidator.Validate(level).Issues.Any(i => i.IsError && i.Cell == new Cell(2, 3) && i.Message.Contains("类型")), Is.True);
        }

        [Test] public void SessionCapturesCrateKindsAndRejectsCargoInLegacySchema()
        {
            var level = Board();
            level.sockets = level.sockets.Concat(new[] { new SocketDefinition { id = "cargo_socket", x = 2, z = 3 } }).ToArray();
            var session = new GameSession(level);
            level.crates[0].kind = CrateDefinition.Energy;
            Assert.That(session.Rules.Power(session.State).Sockets["cargo_socket"], Is.False);
            level.crates[0].kind = CrateDefinition.Cargo; level.schemaVersion = 1;
            Assert.That(LevelValidator.Validate(level).IsValid, Is.False);
            Assert.Throws<FormatException>(() => LevelJson.Write(level));
        }
    }
}
