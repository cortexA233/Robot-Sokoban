using System;
using System.IO;
using System.Linq;
using Sokoban.Domain;
using UnityEditor;
using UnityEngine;
using Terrain = Sokoban.Domain.Terrain;

namespace Sokoban.Editor
{
    public enum LevelBrush { Select, Floor, Wall, Void, LowFriction, Player, Crate, UtilitySocket, GoalSocket, Gate, Erase, CargoCrate }
    public enum AuthorLayer { Terrain, Devices, Actors }

    // Both the window and recipe importer call these author operations.
    public sealed class LevelDocument : ScriptableObject
    {
        [SerializeField] public bool isLiveDraft;
        // Unity inline serialization fabricates objects for null class fields. Keep Undo
        // snapshots as JSON strings so an absent player/proof stays absent after restore.
        [SerializeField] private string serializedLevel = "";
        [SerializeField] private string serializedSolution = "";
        [NonSerialized] private LevelDefinition levelCache;
        [NonSerialized] private SolutionRecord solutionCache;
        [NonSerialized] private string observedLevel = "";
        [NonSerialized] private string observedSolution = "";
        public LevelDefinition level
        {
            get
            {
                if (observedLevel != serializedLevel) { levelCache = string.IsNullOrEmpty(serializedLevel) ? null : LevelJson.Read(serializedLevel); observedLevel = serializedLevel; }
                return levelCache;
            }
            set { levelCache = value; serializedLevel = value == null ? "" : LevelJson.Write(value); observedLevel = serializedLevel; }
        }
        public SolutionRecord solution
        {
            get
            {
                if (observedSolution != serializedSolution) { solutionCache = string.IsNullOrEmpty(serializedSolution) ? null : JsonUtility.FromJson<SolutionRecord>(serializedSolution); observedSolution = serializedSolution; }
                return solutionCache;
            }
            set { solutionCache = value; serializedSolution = value == null ? "" : JsonUtility.ToJson(value); observedSolution = serializedSolution; }
        }
        private void CaptureAuthorState()
        {
            LevelDefinition currentLevel = level;
            SolutionRecord currentSolution = solution;
            level = currentLevel; solution = currentSolution;
        }
        public void RecordUndo(string label) { CaptureAuthorState(); Undo.RegisterCompleteObjectUndo(this, label); }
        // Storage baselines must not roll back with an authoring Undo after a save.
        [NonSerialized] public string filePath = "";
        [NonSerialized] public string savedHash = "";
        [NonSerialized] public string savedSolution = "";
        public bool IsDirty => level != null && (LevelJson.Hash(level) != savedHash || (solution == null ? "" : JsonUtility.ToJson(solution)) != savedSolution);
        public const string DraftPath = "Library/SokobanDrafts/Current.json";
        [NonSerialized] public string draftPath = DraftPath;

        public static LevelDefinition NewLevel(int width = 9, int height = 9)
        {
            if (width < 5 || width > 32 || height < 5 || height > 32) throw new ArgumentOutOfRangeException("尺寸必须是 5–32。");
            return new LevelDefinition
            {
                schemaVersion = 2, id = "level_" + Guid.NewGuid().ToString("N").Substring(0, 8), title = "新关卡",
                briefing = "把能源箱送入目标插槽。", completionText = "区域已恢复供电", width = width, height = height,
                gridSize = 1, terrainRows = Enumerable.Repeat(new string('.', width), height).ToArray(),
                crates = Array.Empty<CrateDefinition>(), sockets = Array.Empty<SocketDefinition>(),
                gates = Array.Empty<GateDefinition>(), decorations = Array.Empty<DecorationDefinition>()
            };
        }

        public void Change(string label, Action action)
        {
            Undo.IncrementCurrentGroup();
            RecordUndo(label);
            action();
            CaptureAuthorState();
            EditorUtility.SetDirty(this);
            Backup();
        }

        public void Paint(Cell cell, char terrain)
        {
            if (!level.Contains(cell) || ".#~_".IndexOf(terrain) < 0) throw new InvalidOperationException("非法地形或越界格。");
            if (terrain == '#' || terrain == '_') { Erase(cell, AuthorLayer.Actors); Erase(cell, AuthorLayer.Devices); }
            if (terrain == '~' && (level.sockets.Any(s => s.Cell == cell) || level.gates.Any(g => g.Cell == cell)))
                throw new InvalidOperationException("请先移走插槽/门，再刷低摩擦轨道。");
            char[] row = level.terrainRows[level.height - 1 - cell.z].ToCharArray();
            row[cell.x] = terrain;
            level.terrainRows[level.height - 1 - cell.z] = new string(row);
        }

        public void Border()
        {
            for (int z = 0; z < level.height; z++)
                for (int x = 0; x < level.width; x++)
                    if (x == 0 || z == 0 || x == level.width - 1 || z == level.height - 1) Paint(new Cell(x, z), '#');
        }

        public string Place(LevelBrush brush, Cell cell, string facing)
        {
            var ground = level.TerrainAt(cell);
            if (ground == Terrain.Void || ground == Terrain.Wall) throw new InvalidOperationException("请先放置地板。");
            if (brush == LevelBrush.Player || brush == LevelBrush.Crate || brush == LevelBrush.CargoCrate)
            {
                if (level.crates.Any(c => c.Cell == cell) || (brush != LevelBrush.Player && level.playerSpawn != null && level.playerSpawn.Cell == cell))
                    throw new InvalidOperationException("玩家与箱子不能重叠。");
                if (brush == LevelBrush.Player)
                {
                    if (!isLiveDraft && level.gates.Any(g => g.Cell == cell)) throw new InvalidOperationException("玩家不能出生在门上。");
                    level.playerSpawn = new PlayerSpawn { x = cell.x, z = cell.z, facing = facing }; return "player";
                }
                string id = NewId("crate");
                if (brush == LevelBrush.CargoCrate) level.schemaVersion = 2;
                level.crates = level.crates.Concat(new[] { new CrateDefinition { id = id, x = cell.x, z = cell.z,
                    kind = brush == LevelBrush.CargoCrate ? CrateDefinition.Cargo : CrateDefinition.Energy } }).ToArray();
                return id;
            }
            if (ground != Terrain.Floor || level.sockets.Any(s => s.Cell == cell) || level.gates.Any(g => g.Cell == cell))
                throw new InvalidOperationException("插槽/门必须放在没有其他机关的普通地板上。");
            if (brush == LevelBrush.Gate)
            {
                if (!isLiveDraft && level.playerSpawn != null && level.playerSpawn.Cell == cell) throw new InvalidOperationException("门不能覆盖玩家出生点。");
                string id = NewId("gate");
                level.gates = level.gates.Concat(new[] { new GateDefinition { id = id, x = cell.x, z = cell.z, facing = facing, powerMode = "Any", sourceSocketIds = Array.Empty<string>() } }).ToArray();
                return id;
            }
            if (brush != LevelBrush.GoalSocket && brush != LevelBrush.UtilitySocket) throw new InvalidOperationException("请选择实体画笔。");
            string socketId = NewId("socket");
            level.sockets = level.sockets.Concat(new[] { new SocketDefinition { id = socketId, x = cell.x, z = cell.z, isGoal = brush == LevelBrush.GoalSocket } }).ToArray();
            return socketId;
        }

        private static string NewId(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        public void Erase(Cell cell, AuthorLayer layer)
        {
            if (layer == AuthorLayer.Terrain) { Paint(cell, '.'); return; }
            if (layer == AuthorLayer.Actors)
            {
                if (level.playerSpawn != null && level.playerSpawn.Cell == cell) level.playerSpawn = null;
                level.crates = level.crates.Where(c => c.Cell != cell).ToArray();
            }
            else
            {
                var removed = level.sockets.Where(s => s.Cell == cell).Select(s => s.id).ToArray();
                level.sockets = level.sockets.Where(s => s.Cell != cell).ToArray();
                level.gates = level.gates.Where(g => g.Cell != cell).ToArray();
                foreach (var gate in level.gates) gate.sourceSocketIds = gate.sourceSocketIds.Except(removed).ToArray();
            }
        }

        public PlacedEntity Find(string id) => level.crates.Cast<PlacedEntity>().Concat(level.sockets).Concat(level.gates).FirstOrDefault(e => e.id == id);
        public void Move(string id, Cell to)
        {
            if (level.TerrainAt(to) == Terrain.Void || level.TerrainAt(to) == Terrain.Wall) throw new InvalidOperationException("目标格不可放置。");
            if (id == "player" || Find(id) is CrateDefinition)
            {
                if (level.crates.Any(c => c.id != id && c.Cell == to) || (id != "player" && level.playerSpawn != null && level.playerSpawn.Cell == to))
                    throw new InvalidOperationException("目标格已有玩家/箱子。");
                if (id == "player")
                {
                    if (!isLiveDraft && level.gates.Any(g => g.Cell == to)) throw new InvalidOperationException("玩家不能出生在门上。");
                    level.playerSpawn.x = to.x; level.playerSpawn.z = to.z; return;
                }
            }
            else if (level.TerrainAt(to) != Terrain.Floor || level.sockets.Any(s => s.id != id && s.Cell == to) || level.gates.Any(g => g.id != id && g.Cell == to) ||
                (!isLiveDraft && Find(id) is GateDefinition && level.playerSpawn != null && level.playerSpawn.Cell == to))
                throw new InvalidOperationException("目标格无法容纳这个机关。");
            var entity = Find(id) ?? throw new InvalidOperationException("选中元素已不存在。");
            entity.x = to.x; entity.z = to.z;
        }
        public void SetGateSources(string id, string[] sources) => level.gates.Single(g => g.id == id).sourceSocketIds = sources.Distinct().ToArray();
        public void SetCrateKind(string id, string kind)
        {
            if (kind != CrateDefinition.Energy && kind != CrateDefinition.Cargo) throw new ArgumentException("箱子类型必须是 Energy 或 Cargo。", nameof(kind));
            var crate = level.crates.Single(c => c.id == id);
            if (kind == CrateDefinition.Cargo) level.schemaVersion = 2;
            crate.kind = kind;
        }
        public int CroppedCount(int width, int height) => level.crates.Cast<PlacedEntity>().Concat(level.sockets).Concat(level.gates).Concat(level.decorations)
            .Count(e => e.x >= width || e.z >= height) + (level.playerSpawn != null && (level.playerSpawn.x >= width || level.playerSpawn.z >= height) ? 1 : 0);
        public void Resize(int width, int height)
        {
            if (width < 5 || width > 32 || height < 5 || height > 32) throw new InvalidOperationException("尺寸必须为 5–32。");
            var old = level.Copy();
            for (int z = 0; z < old.height; z++)
                for (int x = 0; x < old.width; x++)
                    if (x >= width || z >= height) { Erase(new Cell(x, z), AuthorLayer.Actors); Erase(new Cell(x, z), AuthorLayer.Devices); }
            level.decorations = level.decorations.Where(e => e.x < width && e.z < height).ToArray();
            level.width = width; level.height = height;
            level.terrainRows = Enumerable.Range(0, height).Select(r => new string(Enumerable.Range(0, width)
                .Select(x => old.Contains(new Cell(x, height - r - 1)) ? old.terrainRows[old.height - (height - r - 1) - 1][x] : '_').ToArray())).ToArray();
        }

        public void Save(string path, bool copy)
        {
            var output = level.Copy();
            if (copy) output.id = "level_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var outputSolution = solution?.Copy();
            if (copy && outputSolution != null) { outputSolution.levelId = output.id; outputSolution.contentHash = ""; }
            LevelJson.AtomicWrite(path, LevelJson.Write(output));
            if (outputSolution != null) LevelJson.AtomicWrite(SolutionPath(path), JsonUtility.ToJson(outputSolution, true) + "\n");
            level = output; filePath = path; savedHash = LevelJson.Hash(level);
            solution = outputSolution; savedSolution = solution == null ? "" : JsonUtility.ToJson(solution);
            Backup(); AssetDatabase.Refresh();
        }
        public static string SolutionPath(string path) => "Assets/Resources/configs/solutions/" + Path.GetFileNameWithoutExtension(path) + ".solution.json";
        public void Open(string path)
        {
            var loaded = LevelJson.Read(File.ReadAllText(path));
            if (loaded.width < 5 || loaded.width > 32 || loaded.height < 5 || loaded.height > 32 ||
                loaded.terrainRows.Length != loaded.height || loaded.terrainRows.Any(r => r.Length != loaded.width))
                throw new FormatException("地图尺寸或行长不合法，无法显示棋盘；原工作副本保留。");
            // Invalid but well-formed drafts are deliberately editable.
            string solutionPath = SolutionPath(path);
            var loadedSolution = File.Exists(solutionPath) ? JsonUtility.FromJson<SolutionRecord>(File.ReadAllText(solutionPath)) : null;
            level = loaded; filePath = path; savedHash = LevelJson.Hash(level);
            solution = loadedSolution;
            savedSolution = solution == null ? "" : JsonUtility.ToJson(solution);
            Undo.ClearUndo(this); Backup();
        }
        [Serializable] private sealed class Draft
        {
            public string levelJson, solutionJson;
            public string filePath, savedHash, savedSolution;
        }
        [Serializable] private sealed class LegacyDraft
        {
            public LevelDefinition level;
            public SolutionRecord solution;
        }
        public void Backup()
        {
            CaptureAuthorState();
            LevelJson.AtomicWrite(draftPath, JsonUtility.ToJson(new Draft
            { levelJson = serializedLevel, solutionJson = serializedSolution, filePath = filePath, savedHash = savedHash, savedSolution = savedSolution }, true));
        }
        public void RestoreDraft()
        {
            string contents = File.ReadAllText(draftPath);
            var draft = JsonUtility.FromJson<Draft>(contents);
            if (string.IsNullOrEmpty(draft.levelJson))
            {
                var legacy = JsonUtility.FromJson<LegacyDraft>(contents);
                if (legacy.level == null || legacy.level.width < 5) throw new FormatException("草稿地图缺失，保留原工作副本。");
                level = legacy.level;
                solution = string.IsNullOrEmpty(legacy.solution?.levelId) ? null : legacy.solution;
            }
            else
            {
                level = LevelJson.Read(draft.levelJson);
                solution = string.IsNullOrEmpty(draft.solutionJson) ? null : JsonUtility.FromJson<SolutionRecord>(draft.solutionJson);
            }
            filePath = draft.filePath ?? "";
            savedHash = draft.savedHash ?? ""; savedSolution = draft.savedSolution ?? "";
        }
    }
}
