using System;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using UnityEngine;
using Terrain = Sokoban.Domain.Terrain;

namespace Sokoban.Tests
{
    public sealed class StationCircuitLayoutTests
    {
        private static LevelDefinition Level() => LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L06").text);

        [Test] public void CircuitIdentitiesAndRoutesSurviveAuthorArrayReordering()
        {
            var level = Level();
            var second = level.gates[0].Copy(); second.id = "other_gate"; second.x = 1; second.z = 1;
            level.gates = level.gates.Concat(new[] { second }).ToArray();
            var before = new StationCircuitLayout(level);
            level.sockets = level.sockets.Reverse().ToArray(); level.gates = level.gates.Reverse().ToArray();
            foreach (var gate in level.gates) gate.sourceSocketIds = gate.sourceSocketIds.Reverse().ToArray();
            var after = new StationCircuitLayout(level);
            CollectionAssert.AreEquivalent(before.GateLabels, after.GateLabels);
            foreach (var socket in level.sockets) Assert.That(after.PrimaryGateFor(socket.id), Is.EqualTo(before.PrimaryGateFor(socket.id)));
            Assert.That(after.GatesFor("socket_a").Length, Is.EqualTo(2));
            Assert.That(after.GatesFor("socket_b"), Is.Empty, "A goal alone must not become a gate source.");
            for (int i = 0; i < before.Connections.Count; i++)
            {
                Assert.That(after.Connections[i].SocketId, Is.EqualTo(before.Connections[i].SocketId));
                Assert.That(after.Connections[i].GateId, Is.EqualTo(before.Connections[i].GateId));
                CollectionAssert.AreEqual(before.Connections[i].Points, after.Connections[i].Points);
            }
        }

        [Test] public void NarrowCorridorWiresAvoidWallsAndTheUnrelatedGoalGlyph()
        {
            var level = Level(); var layout = new StationCircuitLayout(level);
            Assert.That(layout.Connections.Count, Is.EqualTo(2));
            var b = level.sockets.Single(s => s.id == "socket_b").Cell;
            foreach (var connection in layout.Connections)
            {
                Assert.That(connection.Points.Length, Is.GreaterThanOrEqualTo(2));
                foreach (var cell in connection.Cells) Assert.That(level.TerrainAt(cell), Is.EqualTo(Terrain.Floor));
                for (int i = 1; i < connection.Points.Length; i++)
                {
                    var first = connection.Points[i-1]; var next = connection.Points[i];
                    Assert.That(Mathf.Abs(first.x-next.x) < .001f || Mathf.Abs(first.z-next.z) < .001f, Is.True);
                    for (int sample = 0; sample <= 100; sample++)
                    {
                        var p = Vector3.Lerp(first, next, sample/100f);
                        Assert.That(level.TerrainAt(new Cell(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z))), Is.EqualTo(Terrain.Floor));
                        Assert.That(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(b.x,b.z)), Is.GreaterThan(.30f), "No false junction over B's goal symbol.");
                    }
                }
            }
        }

        [Test] public void DisconnectedElectricalSourcesRetainIdentityWithoutInventingThroughWallWires()
        {
            var level = Level(); level.terrainRows = Enumerable.Repeat("#######", 9).ToArray();
            level.gates[0].x = 1;
            foreach (var socket in level.sockets.Cast<PlacedEntity>().Concat(level.gates))
            {
                var row = level.terrainRows[level.height-1-socket.z].ToCharArray(); row[socket.x]='.';
                level.terrainRows[level.height-1-socket.z]=new string(row);
            }
            var layout = new StationCircuitLayout(level);
            Assert.That(layout.GatesFor("socket_s"), Is.EqualTo(new[] { "G1" }));
            Assert.That(layout.Connections.All(c=>c.Points.Length==0), Is.True);
        }

        [Test] public void NinthLevelHasOneOrangeSocketIdentityAndAllOriginalConnections()
        {
            var level = LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L12").text);
            string hash = LevelJson.Hash(level);
            var layout = new StationCircuitLayout(level);
            Assert.That(layout.PrimaryGateFor("socket_a"), Is.EqualTo("gate_F"));
            Assert.That(layout.GateStyles["gate_F"], Is.EqualTo(1));
            Assert.That(layout.PrimaryGateFor("socket_s"), Is.EqualTo("gate_D"));
            Assert.That(layout.PrimaryGateFor("socket_b"), Is.Null);
            CollectionAssert.AreEquivalent(new[] { "socket_a/gate_F", "socket_a/gate_D", "socket_s/gate_D" },
                layout.Connections.Select(c => c.SocketId + "/" + c.GateId));
            Assert.That(LevelJson.Hash(level), Is.EqualTo(hash));
        }

        [TestCase(false)] [TestCase(true)]
        public void PrimaryIdentityUsesStableIdToBreakTies(bool exclusive)
        {
            var level = Level();
            var a = level.gates[0].Copy(); a.id = "a_gate";
            var z = a.Copy(); z.id = "z_gate"; z.x = 1;
            a.sourceSocketIds = z.sourceSocketIds = exclusive ? new[] { "socket_a" } : new[] { "socket_a", "socket_s" };
            level.gates = new[] { z, a };
            Assert.That(new StationCircuitLayout(level).PrimaryGateFor("socket_a"), Is.EqualTo("a_gate"));
            level.gates = level.gates.Reverse().ToArray();
            foreach (var gate in level.gates) gate.sourceSocketIds = gate.sourceSocketIds.Reverse().ToArray();
            Assert.That(new StationCircuitLayout(level).PrimaryGateFor("socket_a"), Is.EqualTo("a_gate"));
        }
    }
}
