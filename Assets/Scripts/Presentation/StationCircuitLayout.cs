using System;
using System.Collections.Generic;
using System.Linq;
using Sokoban.Domain;
using UnityEngine;
using Terrain = Sokoban.Domain.Terrain;

namespace Sokoban
{
    /// <summary>Derives display identities and static wiring from author data, never from power or crate occupancy.</summary>
    public sealed class StationCircuitLayout
    {
        public sealed class Connection
        {
            public string SocketId { get; internal set; }
            public string GateId { get; internal set; }
            public IReadOnlyList<Cell> Cells { get; internal set; }
            public Vector3[] Points { get; internal set; }
        }

        public IReadOnlyDictionary<string, string> GateLabels { get; }
        public IReadOnlyList<Connection> Connections { get; }

        public StationCircuitLayout(LevelDefinition level)
        {
            var labels = new Dictionary<string, string>();
            var connections = new List<Connection>();
            var sockets = level.sockets.ToDictionary(s => s.id);
            var occupied = new HashSet<Cell>(level.sockets.Select(s => s.Cell).Concat(level.gates.Select(g => g.Cell)));
            var gateCells = new HashSet<Cell>(level.gates.Select(g => g.Cell));
            var socketCells = new HashSet<Cell>(level.sockets.Select(s => s.Cell));
            foreach (var gate in level.gates.OrderBy(g => g.id, StringComparer.Ordinal))
            {
                labels.Add(gate.id, "G" + (labels.Count + 1));
                foreach (var id in gate.sourceSocketIds.OrderBy(id => sockets[id].isGoal).ThenBy(id => id, StringComparer.Ordinal))
                {
                    var cells = Route(level, sockets[id].Cell, gate.Cell, occupied);
                    if (cells.Length == 0) cells = Route(level, sockets[id].Cell, gate.Cell, gateCells);
                    bool passesSocket = cells.Skip(1).Take(Math.Max(0, cells.Length - 2)).Any(socketCells.Contains);
                    connections.Add(new Connection { SocketId = id, GateId = gate.id, Cells = cells,
                        Points = Corners(cells, passesSocket ? .34f : connections.Count % 2 == 0 ? .425f : -.425f) });
                }
            }
            GateLabels = labels;
            Connections = connections.AsReadOnly();
        }

        public string[] GatesFor(string socketId) => Connections.Where(c => c.SocketId == socketId).Select(c => GateLabels[c.GateId]).ToArray();

        private static Cell[] Route(LevelDefinition level, Cell start, Cell end, HashSet<Cell> occupied)
        {
            var pending = new Queue<Cell>(); var previous = new Dictionary<Cell, Cell>();
            pending.Enqueue(start); previous.Add(start, start);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (current == end)
                {
                    var cells = new List<Cell> { end };
                    while (cells[cells.Count - 1] != start) cells.Add(previous[cells[cells.Count - 1]]);
                    cells.Reverse(); return cells.ToArray();
                }
                // Prefer progress toward the destination; tie ordering is independent of input array order.
                var next = Enumerable.Range(0, 4).Select(i => current.Step((Direction)i))
                    .OrderBy(c => Math.Abs(c.x - end.x) + Math.Abs(c.z - end.z));
                foreach (var cell in next)
                {
                    var terrain = level.TerrainAt(cell);
                    if (terrain == Terrain.Wall || terrain == Terrain.Void || previous.ContainsKey(cell) ||
                        (cell != end && occupied.Contains(cell))) continue;
                    previous.Add(cell, current); pending.Enqueue(cell);
                }
            }
            // Some valid author maps connect electrically across disconnected rooms. Keep the identity,
            // but do not invent a traversable floor route or draw a cable straight through a wall.
            return Array.Empty<Cell>();
        }

        private static Vector3[] Corners(Cell[] cells, float lane)
        {
            if (cells.Length < 2) return Array.Empty<Vector3>();
            var points = new List<Vector3> { BoardView.Position(cells[0]) };
            for (int i = 1; i < cells.Length - 1; i++)
            {
                var before = BoardView.Position(cells[i]) - BoardView.Position(cells[i - 1]);
                var after = BoardView.Position(cells[i + 1]) - BoardView.Position(cells[i]);
                if (before != after) points.Add(BoardView.Position(cells[i]));
            }
            points.Add(BoardView.Position(cells[cells.Length - 1]));
            var shifted = points.ToArray();
            for (int i = 0; i < points.Count; i++)
            {
                var incoming = i > 0 ? (points[i] - points[i - 1]).normalized : (points[1] - points[0]).normalized;
                var outgoing = i + 1 < points.Count ? (points[i + 1] - points[i]).normalized : incoming;
                var normal = Vector3.Cross(Vector3.up, incoming);
                if (incoming != outgoing) normal += Vector3.Cross(Vector3.up, outgoing);
                shifted[i] += normal * lane;
                shifted[i].y = .0014f;
            }
            shifted[0] += (points[1] - points[0]).normalized * .47f;
            shifted[shifted.Length - 1] -= (points[points.Count - 1] - points[points.Count - 2]).normalized * .46f;
            return shifted;
        }
    }
}
