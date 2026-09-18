using System;
using System.Collections.Generic;
using System.Linq;

namespace Sokoban.Domain
{
    public sealed class ValidationIssue
    {
        public bool IsError { get; }
        public string Message { get; }
        public Cell? Cell { get; }
        public ValidationIssue(bool isError, string message, Cell? cell = null)
        { IsError = isError; Message = message; Cell = cell; }
        public override string ToString() => (IsError ? "错误：" : "提示：") + Message;
    }

    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> issues = new List<ValidationIssue>();
        public IReadOnlyList<ValidationIssue> Issues => issues;
        public bool IsValid => issues.All(i => !i.IsError);
        public void Error(string message, Cell? cell = null) => issues.Add(new ValidationIssue(true, message, cell));
        public void Warn(string message, Cell? cell = null) => issues.Add(new ValidationIssue(false, message, cell));
    }

    public static class LevelValidator
    {
        public static bool IsFacing(string facing) => facing == "N" || facing == "E" || facing == "S" || facing == "W";

        public static ValidationReport Validate(LevelDefinition level) => Validate(level, false);
        // A live checkpoint may occupy a gate or deliberately have insufficient energy.
        // All shape, reference, type and occupancy constraints still apply.
        public static ValidationReport ValidateLive(LevelDefinition level) => Validate(level, true);
        private static ValidationReport Validate(LevelDefinition level, bool live)
        {
            var report = new ValidationReport();
            if (level == null) { report.Error("关卡数据为空。"); return report; }
            if (level.schemaVersion < 1 || level.schemaVersion > 3) report.Error("仅支持 schemaVersion=1、2 或 3。");
            if (string.IsNullOrWhiteSpace(level.id)) report.Error("关卡 ID 不能为空。");
            if (level.width < 5 || level.width > 32 || level.height < 5 || level.height > 32)
                report.Error("宽高必须为 5–32 格。");
            if (level.gridSize != 1f) report.Error("gridSize 必须为 1.0。");
            bool validTerrain = level.width > 0 && level.height > 0 && level.terrainRows != null &&
                level.terrainRows.Length == level.height && level.terrainRows.All(r => r != null && r.Length == level.width);
            if (!validTerrain) report.Error("地形行数或行长与宽高不符。");
            else
                for (int r = 0; r < level.height; r++)
                    for (int x = 0; x < level.width; x++)
                        if (".#~_".IndexOf(level.terrainRows[r][x]) < 0)
                            report.Error("未知地形符号。", new Cell(x, level.height - r - 1));
            if (level.crates == null || level.sockets == null || level.gates == null || level.decorations == null || level.redirectors == null)
            { report.Error("crates/sockets/gates/decorations/redirectors 数组不可缺失。"); return report; }
            if (level.schemaVersion < 3 && level.redirectors.Length > 0) report.Error("转向板需要 schemaVersion=3。");
            if (!validTerrain || level.width < 5 || level.width > 32 || level.height < 5 || level.height > 32) return report;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var dynamicCells = new HashSet<Cell>();
            var deviceCells = new HashSet<Cell>();
            var sockets = new Dictionary<string, SocketDefinition>(StringComparer.Ordinal);
            Action<Cell, string> checkGround = (cell, name) =>
            {
                if (!level.Contains(cell)) report.Error(name + " 越界。", cell);
                else if (level.TerrainAt(cell) == Terrain.Void || level.TerrainAt(cell) == Terrain.Wall)
                    report.Error(name + " 必须放在可行走地形上。", cell);
            };
            Action<PlacedEntity> checkEntity = entity =>
            {
                if (string.IsNullOrWhiteSpace(entity.id) || !ids.Add(entity.id)) report.Error("元素 ID 为空或重复：" + entity.id, entity.Cell);
                checkGround(entity.Cell, entity.id);
            };
            if (level.playerSpawn == null) report.Error("必须放置一个玩家。");
            else
            {
                checkGround(level.playerSpawn.Cell, "玩家");
                dynamicCells.Add(level.playerSpawn.Cell);
                if (!IsFacing(level.playerSpawn.facing)) report.Error("玩家朝向必须是 N/E/S/W。", level.playerSpawn.Cell);
            }
            foreach (var crate in level.crates)
            {
                if (crate == null) { report.Error("存在空箱子记录。"); continue; }
                checkEntity(crate);
                if (crate.kind != CrateDefinition.Energy && crate.kind != CrateDefinition.Cargo)
                    report.Error("箱子类型必须是 Energy 或 Cargo。", crate.Cell);
                else if (level.schemaVersion == 1 && !crate.IsEnergy)
                    report.Error("普通箱需要 schemaVersion=2。", crate.Cell);
                if (!dynamicCells.Add(crate.Cell)) report.Error("玩家或箱子占格重叠。", crate.Cell);
            }
            foreach (var socket in level.sockets)
            {
                if (socket == null) { report.Error("存在空插槽记录。"); continue; }
                checkEntity(socket);
                if (!deviceCells.Add(socket.Cell)) report.Error("插槽/门占格重叠。", socket.Cell);
                if (level.TerrainAt(socket.Cell) != Terrain.Floor) report.Error("插槽只能放在普通地板上。", socket.Cell);
                if (!string.IsNullOrEmpty(socket.id)) sockets[socket.id] = socket;
            }
            int goals = level.sockets.Count(s => s != null && s.isGoal);
            if (goals == 0) report.Error("至少需要一个目标插槽。");
            if (level.crates.Count(c => c != null && c.IsEnergy) < goals)
            {
                const string shortage = "能源箱数不能少于目标插槽数；普通箱不供电。";
                if (live) report.Warn(shortage); else report.Error(shortage);
            }
            var usedSockets = new HashSet<string>();
            foreach (var gate in level.gates)
            {
                if (gate == null) { report.Error("存在空门记录。"); continue; }
                checkEntity(gate);
                if (!deviceCells.Add(gate.Cell)) report.Error("插槽/门占格重叠。", gate.Cell);
                if (level.TerrainAt(gate.Cell) != Terrain.Floor) report.Error("门只能放在普通地板上。", gate.Cell);
                if (!live && level.playerSpawn != null && gate.Cell == level.playerSpawn.Cell) report.Error("玩家不能出生在门上。", gate.Cell);
                if (!IsFacing(gate.facing)) report.Error("门朝向必须是 N/E/S/W。", gate.Cell);
                if (gate.powerMode != "Any" && gate.powerMode != "All") report.Error("门模式必须是 Any 或 All。", gate.Cell);
                if (gate.sourceSocketIds == null || gate.sourceSocketIds.Length == 0) report.Error("请为门选择至少一个供电插槽。", gate.Cell);
                else
                {
                    var sources = new HashSet<string>();
                    foreach (string source in gate.sourceSocketIds)
                    {
                        if (source == null || !sockets.ContainsKey(source)) report.Error("门引用的插槽不存在：" + source, gate.Cell);
                        if (!sources.Add(source)) report.Error("门的供电来源重复：" + source, gate.Cell);
                        usedSockets.Add(source);
                    }
                }
            }
            foreach (var redirector in level.redirectors)
            {
                if (redirector == null) { report.Error("存在空转向板记录。"); continue; }
                checkEntity(redirector);
                if (!deviceCells.Add(redirector.Cell)) report.Error("转向板不能与插槽、门或其他转向板重叠。", redirector.Cell);
                if (level.TerrainAt(redirector.Cell) != Terrain.Floor) report.Error("转向板只能放在普通地板上。", redirector.Cell);
                if (!IsFacing(redirector.facing)) report.Error("转向板方向必须是 N/E/S/W。", redirector.Cell);
            }
            foreach (var decoration in level.decorations)
            {
                if (decoration == null) { report.Error("存在空装饰记录。"); continue; }
                checkEntity(decoration);
                if (string.IsNullOrWhiteSpace(decoration.typeId) || !IsFacing(decoration.facing))
                    report.Error("装饰类型或朝向无效。", decoration.Cell);
            }
            foreach (var socket in level.sockets.Where(s => s != null && !s.isGoal && !usedSockets.Contains(s.id)))
                report.Warn("辅助插槽未连接到门：" + socket.id, socket.Cell);
            if (!report.IsValid) return report;

            var reached = new HashSet<Cell> { level.playerSpawn.Cell };
            var queue = new Queue<Cell>();
            queue.Enqueue(level.playerSpawn.Cell);
            while (queue.Count > 0)
            {
                Cell current = queue.Dequeue();
                foreach (Direction direction in Enum.GetValues(typeof(Direction)))
                {
                    Cell cell = current.Step(direction);
                    if (!FixedObstacle(level, cell) && reached.Add(cell)) queue.Enqueue(cell);
                }
            }
            for (int z = 0; z < level.height; z++)
                for (int x = 0; x < level.width; x++)
                {
                    var cell = new Cell(x, z);
                    if (!FixedObstacle(level, cell) && !reached.Contains(cell)) report.Warn("即使所有门打开，此格仍与玩家不连通。", cell);
                }
            foreach (var crate in level.crates)
            {
                bool horizontal = FixedObstacle(level, crate.Cell.Step(Direction.E)) || FixedObstacle(level, crate.Cell.Step(Direction.W));
                bool vertical = FixedObstacle(level, crate.Cell.Step(Direction.N)) || FixedObstacle(level, crate.Cell.Step(Direction.S));
                if (horizontal && vertical && !level.sockets.Any(s => s.isGoal && s.Cell == crate.Cell))
                    report.Warn(crate.IsEnergy ? "能源箱位于非目标的固定直角死角。" : "普通箱位于固定死角；请确认这里是无需再次移动的停车位。", crate.Cell);
            }
            if (level.sockets.Where(s => s.isGoal).All(s => level.crates.Any(c => c.IsEnergy && c.Cell == s.Cell))) report.Warn("关卡开局已经完成。");
            return report;
        }

        private static bool FixedObstacle(LevelDefinition level, Cell cell) =>
            level.TerrainAt(cell) == Terrain.Void || level.TerrainAt(cell) == Terrain.Wall;
    }
}
