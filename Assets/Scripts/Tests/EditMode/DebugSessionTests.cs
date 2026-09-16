using System;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using UnityEngine;
using Terrain = Sokoban.Domain.Terrain;

namespace Sokoban.Tests
{
    public sealed class DebugSessionTests
    {
        private static LevelDefinition Level() => LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L04").text);
        private static Cell Empty(LevelDefinition l, GameSession s) => Enumerable.Range(0, l.height)
            .SelectMany(z => Enumerable.Range(0, l.width).Select(x => new Cell(x, z)))
            .First(c => l.TerrainAt(c) == Terrain.Floor && c != s.State.Player && !s.State.Crates.Values.Contains(c));

        [Test] public void MixedHistoryRestoresCommandsValidityCountsAndIdentity()
        {
            var l = Level(); var s = new GameSession(l); var initial = s.State;
            string solution = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/L04.solution").text).commands;
            Assert.That(s.Move((Direction)Enum.Parse(typeof(Direction), solution[0].ToString())).Accepted, Is.True);
            var before = s.State; string commands = s.Commands;
            var cargo = l.crates.First(c => !c.IsEnergy);
            Assert.That(s.TryApplyDebugEdit(DebugBoardEdit.Place(cargo.id, Empty(l, s)), out _), Is.True);
            Assert.That(s.ReferenceReplayValid, Is.False); Assert.That(s.Commands, Is.EqualTo(commands));
            Assert.That(s.State.Moves, Is.EqualTo(before.Moves));
            var copy = s.Copy();
            Assert.That(s.Undo(), Is.True); Assert.That(s.State, Is.SameAs(before));
            Assert.That(s.ReferenceReplayValid, Is.True); Assert.That(s.Commands, Is.EqualTo(commands));
            s.Undo(); Assert.That(s.State, Is.SameAs(initial)); Assert.That(s.Commands, Is.Empty);
            Assert.That(copy.ReferenceReplayValid, Is.False); Assert.That(copy.UndoCount, Is.EqualTo(2));
        }

        [Test] public void InvalidAndNoOpEditsHaveNoSideEffects()
        {
            var l = Level(); var s = new GameSession(l); var before = s.State;
            Assert.That(s.TryApplyDebugEdit(DebugBoardEdit.Place(DebugBoardEdit.PlayerId, new Cell(-1, 0)), out _), Is.False);
            Assert.That(s.TryApplyDebugEdit(DebugBoardEdit.Place(DebugBoardEdit.PlayerId, before.Player), out _), Is.False);
            Assert.That(s.TryApplyDebugEdit(DebugBoardEdit.Place("missing", Empty(l, s)), out _), Is.False);
            Assert.That(s.State, Is.SameAs(before)); Assert.That(s.UndoCount, Is.Zero); Assert.That(s.ReferenceReplayValid, Is.True);
        }

        [Test] public void SwappingKindsUsesIdentityAndUndoRestoresPower()
        {
            var l = Level(); var s = new GameSession(l);
            var energy = l.crates.First(c => c.IsEnergy); var cargo = l.crates.First(c => !c.IsEnergy);
            var goal = l.sockets.First(g => g.isGoal).Cell;
            Assert.That(s.TryApplyDebugEdit(DebugBoardEdit.Place(energy.id, goal), out _), Is.True);
            Assert.That(s.State.Completed, Is.True);
            var edit = new DebugBoardEdit(); edit.Positions[energy.id] = s.State.Crates[cargo.id]; edit.Positions[cargo.id] = goal;
            Assert.That(s.TryApplyDebugEdit(edit, out _), Is.True);
            Assert.That(s.State.Completed, Is.False); Assert.That(s.Rules.Power(s.State).Sockets.Values.All(p => !p), Is.True);
            s.Undo(); Assert.That(s.State.Completed, Is.True);
        }

        [Test] public void CheckpointAllowsGateOccupancyAndEnergyShortageButFormalLevelDoesNot()
        {
            var l = Level(); var goal = l.sockets.First();
            l.gates = new[] { new GateDefinition { id = "gate", x = l.playerSpawn.x, z = l.playerSpawn.z,
                facing = "N", powerMode = "All", sourceSocketIds = new[] { goal.id } } };
            foreach (var c in l.crates) c.kind = CrateDefinition.Cargo;
            Assert.That(LevelValidator.Validate(l).IsValid, Is.False);
            var s = GameSession.FromCheckpoint(l, BoardSnapshot.FromDefinition(l));
            Assert.That(s.Rules.Power(s.State).OpenGates["gate"], Is.True);
            Assert.That(s.ReferenceReplayValid, Is.False); s.Restart(); Assert.That(s.ReferenceReplayValid, Is.False);
            Assert.Throws<InvalidOperationException>(() => SolutionRecord.Capture(l, s));
            var capture = BoardSnapshot.Capture(l, s.State); capture.crates[0].kind = CrateDefinition.Energy;
            Assert.Throws<ArgumentException>(() => GameSession.FromCheckpoint(l, capture));
        }
    }
}
