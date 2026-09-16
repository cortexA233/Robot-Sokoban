using System;
using System.Collections.Generic;
using System.Linq;

namespace Sokoban.Domain
{
    // Portable checkpoint. Kinds belong to the definition and are checked by ID,
    // never inferred from position or dictionary enumeration order.
    [Serializable]
    public sealed class BoardSnapshot
    {
        public PlayerSpawn player;
        public CrateDefinition[] crates;

        public static BoardSnapshot Capture(LevelDefinition definition, GameState state) => new BoardSnapshot
        {
            player = new PlayerSpawn { x = state.Player.x, z = state.Player.z, facing = state.Facing.ToString() },
            crates = definition.crates.Select(crate => new CrateDefinition
            { id = crate.id, kind = crate.kind, x = state.Crates[crate.id].x, z = state.Crates[crate.id].z }).ToArray()
        };

        public static BoardSnapshot FromDefinition(LevelDefinition definition) => new BoardSnapshot
        { player = definition.playerSpawn?.Copy(), crates = definition.crates?.Select(c => c?.Copy()).ToArray() };

        public LevelDefinition ApplyTo(LevelDefinition definition)
        {
            if (definition == null || crates == null || definition.crates == null || player == null)
                throw new ArgumentException("现场快照缺少关卡、玩家或箱子数据。");
            var expected = definition.crates.ToDictionary(c => c.id, c => c.kind);
            var ids = new HashSet<string>();
            foreach (var crate in crates)
                if (crate == null || string.IsNullOrEmpty(crate.id) || !ids.Add(crate.id) ||
                    !expected.TryGetValue(crate.id, out string kind) || kind != crate.kind)
                    throw new ArgumentException("快照箱子 ID/类型与关卡定义不一致。");
            if (ids.Count != expected.Count) throw new ArgumentException("快照缺少箱子。");
            var result = definition.Copy();
            result.playerSpawn = player.Copy(); result.crates = crates.Select(c => c.Copy()).ToArray();
            return result;
        }
    }

    public sealed class DebugBoardEdit
    {
        public const string PlayerId = "@player";
        public readonly Dictionary<string, Cell> Positions = new Dictionary<string, Cell>();
        public Direction? Facing;
        public string Label = "GM 移位";

        public static DebugBoardEdit Place(string id, Cell cell, Direction? facing = null)
        {
            var edit = new DebugBoardEdit { Facing = facing };
            edit.Positions.Add(id, cell); return edit;
        }
    }
}
