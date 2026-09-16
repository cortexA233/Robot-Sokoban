using System;
using System.Linq;

namespace Sokoban.Domain
{
    public enum Direction { N, E, S, W }
    public enum Terrain { Void, Floor, Wall, LowFriction }

    [Serializable]
    public struct Cell : IEquatable<Cell>, IComparable<Cell>
    {
        public int x;
        public int z;
        public Cell(int x, int z) { this.x = x; this.z = z; }
        public Cell Step(Direction direction)
        {
            switch (direction)
            {
                case Direction.N: return new Cell(x, z + 1);
                case Direction.E: return new Cell(x + 1, z);
                case Direction.S: return new Cell(x, z - 1);
                case Direction.W: return new Cell(x - 1, z);
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
        public bool Equals(Cell other) => x == other.x && z == other.z;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => unchecked(x * 397 ^ z);
        public int CompareTo(Cell other) => z == other.z ? x.CompareTo(other.x) : z.CompareTo(other.z);
        public override string ToString() => $"({x},{z})";
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
    }

    [Serializable]
    public class PlacedEntity
    {
        public string id;
        public int x;
        public int z;
        public Cell Cell => new Cell(x, z);
    }

    [Serializable]
    public sealed class PlayerSpawn
    {
        public int x;
        public int z;
        public string facing;
        public Cell Cell => new Cell(x, z);
        public PlayerSpawn Copy() => (PlayerSpawn)MemberwiseClone();
    }

    [Serializable] public sealed class CrateDefinition : PlacedEntity
    {
        public const string Energy = "Energy";
        public const string Cargo = "Cargo";
        public string kind = Energy;
        public bool IsEnergy => kind == Energy;
        public CrateDefinition Copy() => (CrateDefinition)MemberwiseClone();
    }
    [Serializable] public sealed class SocketDefinition : PlacedEntity
    {
        public bool isGoal;
        public SocketDefinition Copy() => (SocketDefinition)MemberwiseClone();
    }
    [Serializable] public sealed class GateDefinition : PlacedEntity
    {
        public string facing;
        public string powerMode;
        public string[] sourceSocketIds;
        public GateDefinition Copy()
        {
            var copy = (GateDefinition)MemberwiseClone();
            copy.sourceSocketIds = sourceSocketIds?.ToArray();
            return copy;
        }
    }
    [Serializable] public sealed class DecorationDefinition : PlacedEntity
    {
        public string typeId;
        public string facing;
        public DecorationDefinition Copy() => (DecorationDefinition)MemberwiseClone();
    }

    // Author data only. Runtime state never writes back into these DTOs.
    [Serializable]
    public sealed class LevelDefinition
    {
        public int schemaVersion;
        public string id;
        public string title;
        public string briefing;
        public string completionText;
        public int width;
        public int height;
        public float gridSize;
        public string[] terrainRows;
        public PlayerSpawn playerSpawn;
        public CrateDefinition[] crates;
        public SocketDefinition[] sockets;
        public GateDefinition[] gates;
        public DecorationDefinition[] decorations;

        public bool Contains(Cell cell) => cell.x >= 0 && cell.z >= 0 && cell.x < width && cell.z < height;
        public Terrain TerrainAt(Cell cell)
        {
            if (!Contains(cell)) return Terrain.Void;
            int row = height - 1 - cell.z;
            if (terrainRows == null || row >= terrainRows.Length || terrainRows[row] == null || cell.x >= terrainRows[row].Length) return Terrain.Void;
            switch (terrainRows[row][cell.x])
            {
                case '.': return Terrain.Floor;
                case '#': return Terrain.Wall;
                case '~': return Terrain.LowFriction;
                default: return Terrain.Void;
            }
        }
        public LevelDefinition Copy()
        {
            var copy = (LevelDefinition)MemberwiseClone();
            copy.terrainRows = terrainRows?.ToArray();
            copy.playerSpawn = playerSpawn?.Copy();
            copy.crates = crates?.Select(c => c?.Copy()).ToArray();
            copy.sockets = sockets?.Select(s => s?.Copy()).ToArray();
            copy.gates = gates?.Select(g => g?.Copy()).ToArray();
            copy.decorations = decorations?.Select(d => d?.Copy()).ToArray();
            return copy;
        }
    }
}
