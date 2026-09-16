using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sokoban.Domain
{
    public sealed class GameState
    {
        public Cell Player { get; }
        public Direction Facing { get; }
        public IReadOnlyDictionary<string, Cell> Crates { get; }
        public int Moves { get; }
        public int Pushes { get; }
        public bool Completed { get; }
        internal GameState(Cell player, Direction facing, IDictionary<string, Cell> crates, int moves, int pushes, bool completed)
        {
            Player = player; Facing = facing; Moves = moves; Pushes = pushes; Completed = completed;
            Crates = new ReadOnlyDictionary<string, Cell>(new Dictionary<string, Cell>(crates));
        }
    }

    public sealed class PowerState
    {
        public IReadOnlyDictionary<string, bool> Sockets { get; }
        public IReadOnlyDictionary<string, bool> PoweredGates { get; }
        public IReadOnlyDictionary<string, bool> OpenGates { get; }
        internal PowerState(Dictionary<string, bool> sockets, Dictionary<string, bool> powered, Dictionary<string, bool> open)
        {
            Sockets = new ReadOnlyDictionary<string, bool>(sockets);
            PoweredGates = new ReadOnlyDictionary<string, bool>(powered);
            OpenGates = new ReadOnlyDictionary<string, bool>(open);
        }
    }

    public sealed class Microstep
    {
        public Cell PlayerFrom { get; }
        public Cell PlayerTo { get; }
        public string CrateId { get; }
        public Cell CrateFrom { get; }
        public Cell CrateTo { get; }
        public PowerState Power { get; }
        internal Microstep(Cell from, Cell to, string crateId, Cell crateFrom, Cell crateTo, PowerState power)
        { PlayerFrom = from; PlayerTo = to; CrateId = crateId; CrateFrom = crateFrom; CrateTo = crateTo; Power = power; }
    }

    public enum RejectReason { None, PlayerBlocked, CrateBlocked, ChainPush, Completed }

    public sealed class MoveResolution
    {
        public bool Accepted => RejectReason == RejectReason.None;
        public RejectReason RejectReason { get; }
        public GameState NextState { get; }
        public IReadOnlyList<Microstep> Microsteps { get; }
        internal MoveResolution(RejectReason reason, GameState state, IList<Microstep> steps = null)
        { RejectReason = reason; NextState = state; Microsteps = new ReadOnlyCollection<Microstep>(steps ?? new List<Microstep>()); }
    }

    // Captures its own definition, so later author edits cannot change an active session.
    public sealed class RuleEngine
    {
        private readonly LevelDefinition level;
        private readonly Dictionary<Cell, GateDefinition> gates;
        private readonly HashSet<string> energyCrateIds;
        public RuleEngine(LevelDefinition definition) : this(definition, null) { }
        internal RuleEngine(LevelDefinition definition, BoardSnapshot checkpoint)
        {
            if (checkpoint != null) definition = checkpoint.ApplyTo(definition);
            var report = checkpoint == null ? LevelValidator.Validate(definition) : LevelValidator.ValidateLive(definition);
            if (!report.IsValid) throw new ArgumentException(string.Join("\n", report.Issues.Where(i => i.IsError)), nameof(definition));
            level = definition.Copy();
            gates = level.gates.ToDictionary(g => g.Cell);
            energyCrateIds = new HashSet<string>(level.crates.Where(c => c.IsEnergy).Select(c => c.id));
        }

        public GameState WithPositions(GameState previous, Cell player, Direction facing, IDictionary<string, Cell> crates)
        {
            if (!Enum.IsDefined(typeof(Direction), facing)) throw new ArgumentException("玩家朝向无效。");
            if (crates == null || crates.Count != level.crates.Length || level.crates.Any(c => !crates.ContainsKey(c.id)))
                throw new ArgumentException("GM 移位不能改变箱子 ID 集合。");
            var occupied = new HashSet<Cell>();
            foreach (var cell in new[] { player }.Concat(crates.Values))
            {
                if (!level.Contains(cell) || level.TerrainAt(cell) == Terrain.Wall || level.TerrainAt(cell) == Terrain.Void)
                    throw new ArgumentException("目标格不可放置：" + cell);
                if (!occupied.Add(cell)) throw new ArgumentException("玩家或箱子占格重叠：" + cell);
            }
            return State(player, facing, new Dictionary<string, Cell>(crates), previous.Moves, previous.Pushes);
        }

        public GameState CreateInitialState()
        {
            var crates = level.crates.ToDictionary(c => c.id, c => c.Cell);
            return State(level.playerSpawn.Cell, (Direction)Enum.Parse(typeof(Direction), level.playerSpawn.facing), crates, 0, 0);
        }
        private GameState State(Cell player, Direction facing, Dictionary<string, Cell> crates, int moves, int pushes)
        {
            var energized = PoweredCells(crates);
            return new GameState(player, facing, crates, moves, pushes, level.sockets.Where(s => s.isGoal).All(s => energized.Contains(s.Cell)));
        }

        private HashSet<Cell> PoweredCells(IEnumerable<KeyValuePair<string, Cell>> crates) =>
            new HashSet<Cell>(crates.Where(c => energyCrateIds.Contains(c.Key)).Select(c => c.Value));

        public PowerState Power(GameState state) => Power(state.Player, state.Crates);
        private PowerState Power(Cell player, IEnumerable<KeyValuePair<string, Cell>> crates)
        {
            var occupied = new HashSet<Cell>(crates.Select(c => c.Value));
            var energized = PoweredCells(crates);
            var sockets = level.sockets.ToDictionary(s => s.id, s => energized.Contains(s.Cell));
            var powered = level.gates.ToDictionary(g => g.id, g => g.powerMode == "All"
                ? g.sourceSocketIds.All(id => sockets[id]) : g.sourceSocketIds.Any(id => sockets[id]));
            var open = level.gates.ToDictionary(g => g.id, g => powered[g.id] || g.Cell == player || occupied.Contains(g.Cell));
            return new PowerState(sockets, powered, open);
        }

        private bool Passable(Cell cell, PowerState power) => level.Contains(cell) &&
            (level.TerrainAt(cell) == Terrain.Floor || level.TerrainAt(cell) == Terrain.LowFriction) &&
            (!gates.TryGetValue(cell, out var gate) || power.OpenGates[gate.id]);

        public MoveResolution Resolve(GameState state, Direction direction)
        {
            if (!Enum.IsDefined(typeof(Direction), direction)) throw new ArgumentOutOfRangeException(nameof(direction));
            if (state.Completed) return new MoveResolution(RejectReason.Completed, state);
            var power = Power(state);
            Cell next = state.Player.Step(direction);
            if (!Passable(next, power)) return new MoveResolution(RejectReason.PlayerBlocked, state);
            string pushed = state.Crates.FirstOrDefault(c => c.Value == next).Key;
            Cell crateTarget = next.Step(direction);
            if (pushed != null)
            {
                if (state.Crates.Values.Contains(crateTarget)) return new MoveResolution(RejectReason.ChainPush, state);
                if (!Passable(crateTarget, power) || crateTarget == state.Player) return new MoveResolution(RejectReason.CrateBlocked, state);
            }
            var crates = state.Crates.ToDictionary(c => c.Key, c => c.Value);
            if (pushed != null) crates[pushed] = crateTarget;
            power = Power(next, crates);
            var steps = new List<Microstep> { new Microstep(state.Player, next, pushed, next, crateTarget, power) };
            if (pushed != null)
            {
                int guard = 0;
                while (level.TerrainAt(crates[pushed]) == Terrain.LowFriction)
                {
                    Cell from = crates[pushed];
                    Cell to = from.Step(direction);
                    if (!Passable(to, power) || to == next || crates.ContainsValue(to)) break;
                    if (++guard > Math.Max(level.width, level.height)) throw new InvalidOperationException("滑行超出地图跨度。");
                    crates[pushed] = to;
                    power = Power(next, crates);
                    steps.Add(new Microstep(next, next, pushed, from, to, power));
                }
            }
            return new MoveResolution(RejectReason.None, State(next, direction, crates, state.Moves + 1, state.Pushes + (pushed == null ? 0 : 1)), steps);
        }
    }
}
