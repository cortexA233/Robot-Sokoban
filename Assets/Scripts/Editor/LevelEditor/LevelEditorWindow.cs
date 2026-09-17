using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sokoban.Domain;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Terrain = Sokoban.Domain.Terrain;

namespace Sokoban.Editor
{
    public sealed class LevelEditorWindow : EditorWindow
    {
        [SerializeField] private LevelDocument document;
        [SerializeField] private LevelDocument authorDocument;
        [SerializeField] private LiveEditWorkspace liveWorkspace;
        [SerializeField] private LevelBrush brush = LevelBrush.Select;
        [SerializeField] private AuthorLayer layer = AuthorLayer.Actors;
        [SerializeField] private string selectedId;
        [SerializeField] private string facing = "N";
        private Cell? selectedCell;
        private VisualElement grid;
        private ScrollView properties, issues;
        private Label status, hint;
        private bool brushing;
        private int undoGroup;
        private readonly HashSet<Cell> stroke = new HashSet<Cell>();
        public LevelDocument Document => document;
        public LiveEditWorkspace LiveWorkspace => liveWorkspace;
        private LevelDocument AuthorDocument => authorDocument ? authorDocument : document;
        private bool LiveMode => document && document.isLiveDraft;
        private bool CanEditDocument => !EditorApplication.isPlayingOrWillChangePlaymode || (LiveMode && liveWorkspace && liveWorkspace.Editing);

        [MenuItem("Sokoban_Tools/Level Editor")]
        public static LevelEditorWindow OpenWindow() => GetWindow<LevelEditorWindow>("Sokoban Level Editor");

        private void OnEnable()
        {
            if (liveWorkspace) liveWorkspace.RestorePaths();
            if (authorDocument && File.Exists(LevelDocument.DraftPath))
            {
                authorDocument.draftPath = LevelDocument.DraftPath;
                try { authorDocument.RestoreDraft(); }
                catch (Exception exception) { Debug.LogWarning("原作者草稿恢复失败，备份保留：" + exception.Message); }
            }
            if (!document)
            {
                document = CreateInstance<LevelDocument>(); document.hideFlags = HideFlags.HideAndDontSave;
            }
            if (!LiveMode && File.Exists(LevelDocument.DraftPath))
            {
                try { document.RestoreDraft(); }
                catch (Exception e) { Debug.LogWarning("草稿读取失败，原文件已保留：" + e.Message); }
            }
            if (document.level == null) document.level = LevelDocument.NewLevel();
            if (!EditorApplication.isPlayingOrWillChangePlaymode) PlaytestBridge.CollectRecording(AuthorDocument);
            minSize = new Vector2(860, 620);
            saveChangesMessage = "此关卡尚未保存。保存后关闭，或放弃本次修改？";
            SceneView.duringSceneGui += DrawScene;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }
        private void OnDisable()
        {
            EndStroke();
            if (document && document.level != null) document.Backup();
            if (liveWorkspace) { liveWorkspace.Backup(); liveWorkspace.ReleaseInput(); }
            SceneView.duringSceneGui -= DrawScene;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayMode;
        }
        private void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) { PlaytestBridge.CollectRecording(AuthorDocument); Refresh(); }
            RefreshMode();
        }
        private void OnUndoRedo() { if (document) { document.Backup(); if (LiveMode && liveWorkspace) liveWorkspace.Backup(); Refresh(); } }
        public override void SaveChanges() { if (Save(false)) base.SaveChanges(); }
        public override void DiscardChanges()
        {
            if (LiveMode) { liveWorkspace?.Backup(); base.DiscardChanges(); return; }
            if (!string.IsNullOrEmpty(document.filePath) && File.Exists(document.filePath)) document.Open(document.filePath);
            else { document.level = LevelDocument.NewLevel(); document.savedHash = LevelJson.Hash(document.level); document.solution = null; document.Backup(); }
            base.DiscardChanges();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, paddingBottom = 8 } };
            rootVisualElement.Add(toolbar);
            Button(toolbar, "新建", New, "new-level"); Button(toolbar, "打开", Open, "open-level");
            Button(toolbar, "保存", () => Save(false), "save-level"); Button(toolbar, "另存为副本", () => Save(true), "copy-level");
            Button(toolbar, "校验", Validate, "validate-level"); Button(toolbar, "试玩并录制", () => Run(() => PlaytestBridge.Play(document)), "playtest");
            Button(toolbar, "回放参考解法", Replay, "replay"); Button(toolbar, "导入配方", ImportRecipe, "import-recipe");
            Button(toolbar, "帮助", () => HelpWindow.OpenHelp(), "help");
            var liveBar = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } }; rootVisualElement.Add(liveBar);
            Button(liveBar, "捕获当前游戏局面", () => CaptureLive(UnityEngine.Object.FindObjectOfType<LevelRunner>()), "capture-live");
            Button(liveBar, "编辑已有现场草稿", () => Run(() => { ShowLiveDraft(); liveWorkspace.Resume(); RefreshMode(); }), "resume-live");
            Button(liveBar, "应用并继续试玩", ApplyLive, "apply-live");
            Button(liveBar, "结束现场试玩", EndLive, "end-live");
            Button(liveBar, "带回作者文档", () => Run(() => { liveWorkspace.BringBack(); hint.text = "已带回作者文档；尚未写入原文件。"; }), "bring-live");
            Button(liveBar, "恢复/查看现场草稿", () => Run(() => { ShowLiveDraft(); Refresh(); }), "recover-live");
            var body = new VisualElement { name = "EditorBody", style = { flexDirection = FlexDirection.Row, flexGrow = 1, minHeight = 280 } };
            rootVisualElement.Add(body);
            var palette = new ScrollView { style = { width = 150, flexShrink = 0, paddingRight = 6 } }; body.Add(palette);
            palette.Add(new Label("画笔 / 当前图层"));
            foreach (LevelBrush value in Enum.GetValues(typeof(LevelBrush)))
            {
                var captured = value;
                Button(palette, BrushName(value), () => { brush = captured; hint.text = "当前画笔：" + BrushName(brush) + "。右键擦除所选图层；Wall/Void 会删除该格实体。"; }, "brush-" + value);
            }
            var layerField = new EnumField("擦除层", layer); palette.Add(layerField);
            layerField.RegisterValueChangedCallback(e => layer = (AuthorLayer)e.newValue);
            var facingField = new PopupField<string>("朝向", new List<string> { "N", "E", "S", "W" }, facing); palette.Add(facingField);
            facingField.RegisterValueChangedCallback(e => facing = e.newValue);
            Button(palette, "撤销 Ctrl+Z", Undo.PerformUndo, "undo"); Button(palette, "重做 Ctrl+Y", Undo.PerformRedo, "redo");
            Button(palette, "外围墙", () => Edit("建立外围墙", document.Border), "border");
            Button(palette, "SceneView 聚焦", FrameBoard, "frame-board");
            palette.Add(new Label("示例关卡"));
            foreach (string name in new[] { "L04", "L05", "L06", "LAB01_LowFriction" })
            {
                string path = "Assets/Resources/configs/" + (name.StartsWith("LAB") ? "test_levels/" : "levels/") + name + ".json";
                Button(palette, name.StartsWith("LAB") ? "LAB01（开发测试）" : name, () => { if (ConfirmDiscard()) Run(() => { document.Open(path); Refresh(); FrameBoard(); }); }, "example-" + name);
            }
            var canvas = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { style = { flexGrow = 1, backgroundColor = new Color(.12f, .15f, .19f), paddingTop = 12, paddingLeft = 12 } };
            body.Add(canvas); grid = new VisualElement(); canvas.Add(grid);
            properties = new ScrollView { style = { width = 280, flexShrink = 0, paddingLeft = 8 } }; body.Add(properties);
            hint = new Label("左键放置/选取，地形可拖刷；右键擦除当前图层。SceneView 的 Alt/中键导航保留。") { style = { whiteSpace = WhiteSpace.Normal } };
            rootVisualElement.Add(hint);
            issues = new ScrollView { style = { height = 130, borderTopWidth = 1, borderTopColor = Color.gray } }; rootVisualElement.Add(issues);
            status = new Label { style = { whiteSpace = WhiteSpace.Normal } }; rootVisualElement.Add(status);
            rootVisualElement.RegisterCallback<PointerUpEvent>(_ => EndStroke(), TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKey);
            RefreshMode();
            Refresh();
        }
        private static string BrushName(LevelBrush value) => new[] { "选择", "地板 Floor", "墙 Wall", "虚空 Void", "低摩擦轨道", "玩家", "能源箱", "辅助插槽", "目标插槽", "受电门", "擦除当前层", "普通箱（不供电）" }[(int)value];
        private static void Button(VisualElement parent, string text, Action action, string name = null)
        { var button = new Button(action) { text = text, name = name }; button.style.minHeight = 25; parent.Add(button); }
        private void OnInspectorUpdate()
        {
            // The GM page can end the same runtime session while this window is open.
            if (LiveMode && liveWorkspace && liveWorkspace.IsAttached && !liveWorkspace.Runner.IsLiveSandbox) EndLive();
            RefreshMode();
        }
        private void RefreshMode()
        {
            if (rootVisualElement == null) return;
            rootVisualElement.SetEnabled(true);
            rootVisualElement.Q<VisualElement>("EditorBody")?.SetEnabled(CanEditDocument);
            foreach (string name in new[] { "new-level", "open-level", "playtest", "replay", "import-recipe" })
                rootVisualElement.Q<Button>(name)?.SetEnabled(!LiveMode && !EditorApplication.isPlayingOrWillChangePlaymode);
            foreach (string name in new[] { "save-level", "copy-level", "validate-level" })
                rootVisualElement.Q<Button>(name)?.SetEnabled(LiveMode || !EditorApplication.isPlayingOrWillChangePlaymode);
            foreach (var button in rootVisualElement.Query<Button>().ToList())
                if (button.name != null && button.name.StartsWith("example-")) button.SetEnabled(!LiveMode);
            rootVisualElement.Q<Button>("capture-live")?.SetEnabled(EditorApplication.isPlaying);
            rootVisualElement.Q<Button>("resume-live")?.SetEnabled(liveWorkspace && liveWorkspace.IsAttached && !liveWorkspace.Editing);
            rootVisualElement.Q<Button>("apply-live")?.SetEnabled(LiveMode && liveWorkspace && liveWorkspace.Editing);
            rootVisualElement.Q<Button>("end-live")?.SetEnabled(liveWorkspace && liveWorkspace.IsAttached);
            rootVisualElement.Q<Button>("bring-live")?.SetEnabled(liveWorkspace);
            rootVisualElement.Q<Button>("recover-live")?.SetEnabled(liveWorkspace || File.Exists(LiveEditWorkspace.RecoveryPath));
        }
        public bool CaptureLive(LevelRunner runner)
        {
            try
            {
                if (!runner || runner.Session == null) throw new InvalidOperationException("当前游戏没有关卡。");
                if (liveWorkspace && liveWorkspace.HasUnappliedChanges)
                {
                    int choice = EditorUtility.DisplayDialogComplex("现场草稿尚未应用", "可以继续编辑已有草稿，或重新捕获当前局面。旧草稿备份保留。", "继续已有草稿", "取消", "重新捕获并替换");
                    if (choice == 1) return false;
                    if (choice == 0) { ShowLiveDraft(); liveWorkspace.Resume(); Refresh(); return true; }
                }
                var sourceAuthor = AuthorDocument;
                var old = liveWorkspace;
                if (old) { old.Backup(); old.ReleaseInput(); }
                var next = LiveEditWorkspace.Capture(runner, sourceAuthor);
                if (!authorDocument) authorDocument = sourceAuthor;
                liveWorkspace = next; document = next.Draft; selectedId = null; selectedCell = null;
                if (old) { Undo.ClearUndo(old.Draft); DestroyImmediate(old.Draft); DestroyImmediate(old); }
                RefreshLive(); Focus(); return true;
            }
            catch (Exception exception) { if (hint != null) hint.text = exception.Message; else Debug.LogWarning(exception.Message); return false; }
        }
        private void ShowLiveDraft()
        {
            if (!authorDocument && !LiveMode) authorDocument = document;
            if (!liveWorkspace) liveWorkspace = LiveEditWorkspace.Recover(AuthorDocument);
            document = liveWorkspace.Draft; selectedId = null; selectedCell = null; RefreshLive();
        }
        public void RefreshLive() { if (grid == null) CreateGUI(); else Refresh(); }
        public void ApplyLive()
        {
            EndStroke();
            if (!liveWorkspace) return;
            if (!liveWorkspace.Apply(out string error)) { Validate(); hint.text = error; return; }
            Refresh(); hint.text = "已应用新的现场起点，可在 Game View 继续试玩。Z 不跨应用边界。";
        }
        public void EndLive()
        {
            string error = null;
            if (!liveWorkspace || !liveWorkspace.End(out error)) { hint.text = error ?? "没有现场会话。"; return; }
            if (authorDocument) { document = authorDocument; authorDocument = null; }
            selectedId = null; selectedCell = null; Refresh(); hint.text = "已返回原运行局面。现场草稿仍可查看或另存。";
        }
        private void Run(Action action)
        { try { action(); } catch (Exception e) { if (hint != null) hint.text = e.Message; } }
        private void Edit(string label, Action change)
        { if (CanEditDocument) Run(() => { document.Change(label, change); if (LiveMode) liveWorkspace.Backup(); Refresh(); }); }
        private void Refresh()
        {
            if (grid == null || !document || document.level == null) return;
            grid.Clear();
            var level = document.level;
            for (int z = level.height - 1; z >= 0; z--)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } }; grid.Add(row);
                for (int x = 0; x < level.width; x++)
                {
                    var cell = new Cell(x, z);
                    var tile = new Label(Symbol(cell)) { name = $"cell-{x}-{z}", tooltip = cell + " / " + level.TerrainAt(cell),
                        style = { width = 30, height = 30, marginRight = 2, marginBottom = 2, unityTextAlign = TextAnchor.MiddleCenter,
                            backgroundColor = TileColor(cell), color = Color.white, fontSize = 17 } };
                    row.Add(tile);
                    tile.RegisterCallback<PointerDownEvent>(e =>
                    {
                        if (e.altKey || (e.button != 0 && e.button != 1)) return;
                        BeginStroke(); Apply(cell, e.button == 1); e.StopPropagation();
                    });
                    tile.RegisterCallback<PointerEnterEvent>(e =>
                    { hint.text = "格 " + cell + " · " + BrushName(brush) + (brush == LevelBrush.Wall || brush == LevelBrush.Void ? "（删除此格实体）" : ""); if (brushing && e.pressedButtons != 0 && IsTerrainBrush()) Apply(cell, e.pressedButtons == 2); });
                    tile.RegisterCallback<PointerUpEvent>(_ => { EndStroke(); Refresh(); });
                }
            }
            DrawProperties();
            hasUnsavedChanges = !LiveMode && document.IsDirty;
            status.text = $"{level.width} × {level.height} · {(document.IsDirty ? "● 未保存" : "已保存")} · {(string.IsNullOrEmpty(document.filePath) ? "新草稿" : document.filePath)}\n" +
                (document.solution == null ? "尚无参考解法" : document.solution.contentHash == LevelJson.Hash(level) ? "参考解法：当前版本" : "参考解法：已过期，需重新回放");
            if (LiveMode) status.text = "临时现场草稿 · " + (liveWorkspace && liveWorkspace.IsAttached ? liveWorkspace.Editing ? "编辑中，游戏输入已占用" : "游戏运行中，点击编辑或重新捕获" : "来源已结束，可另存副本") + "\n" + status.text;
            RefreshMode();
            SceneView.RepaintAll();
        }
        private string Symbol(Cell cell)
        {
            var l = document.level;
            if (l.playerSpawn != null && l.playerSpawn.Cell == cell) return "P";
            var crate = l.crates.FirstOrDefault(c => c.Cell == cell);
            if (crate != null) return crate.IsEnergy ? "C" : "X";
            if (l.gates.Any(g => g.Cell == cell)) return "D";
            var s = l.sockets.FirstOrDefault(v => v.Cell == cell); if (s != null) return s.isGoal ? "◎" : "s";
            return l.TerrainAt(cell) == Terrain.LowFriction ? "≋" : l.TerrainAt(cell) == Terrain.Wall ? "■" : "";
        }
        private Color TileColor(Cell cell)
        {
            if (selectedCell == cell) return new Color(.7f, .4f, .12f);
            switch (document.level.TerrainAt(cell))
            {
                case Terrain.Floor: return new Color(.28f, .34f, .4f);
                case Terrain.Wall: return new Color(.49f, .52f, .56f);
                case Terrain.LowFriction: return new Color(.15f, .43f, .58f);
                default: return new Color(.07f, .08f, .10f);
            }
        }
        private bool IsTerrainBrush() => brush >= LevelBrush.Floor && brush <= LevelBrush.LowFriction;
        private void BeginStroke()
        {
            if (!CanEditDocument || brushing) return;
            brushing = true; stroke.Clear(); Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup();
            document.RecordUndo("棋盘画笔");
        }
        private void EndStroke()
        {
            if (!brushing) return;
            brushing = false; Undo.CollapseUndoOperations(undoGroup); document.Backup();
            if (LiveMode && liveWorkspace) liveWorkspace.Backup();
            hasUnsavedChanges = !LiveMode && document.IsDirty;
        }
        private void Apply(Cell cell, bool erase)
        {
            if (!CanEditDocument || !document.level.Contains(cell) || !stroke.Add(cell)) return;
            Run(() =>
            {
                selectedCell = cell;
                if (erase || brush == LevelBrush.Erase) document.Erase(cell, layer);
                else if (IsTerrainBrush()) document.Paint(cell, new[] { '.', '#', '_', '~' }[(int)brush - (int)LevelBrush.Floor]);
                else if (brush != LevelBrush.Select) selectedId = document.Place(brush, cell, facing);
                else selectedId = document.level.playerSpawn != null && document.level.playerSpawn.Cell == cell ? "player" :
                    document.level.crates.Cast<PlacedEntity>().Concat(document.level.sockets).Concat(document.level.gates).FirstOrDefault(e => e.Cell == cell)?.id;
                EditorUtility.SetDirty(document);
                // Rebuild after pointer dispatch so drag capture/enter continues to work.
                rootVisualElement.schedule.Execute(Refresh);
            });
        }

        private void DrawProperties()
        {
            properties.Clear(); var level = document.level;
            properties.Add(new Label("关卡属性")); properties.Add(new Label("ID: " + level.id));
            TextField(properties, "标题", level.title, text => Edit("修改标题", () => level.title = text));
            TextField(properties, "目标提示", level.briefing, text => Edit("修改提示", () => level.briefing = text));
            TextField(properties, "完成文案", level.completionText, text => Edit("修改完成文案", () => level.completionText = text));
            var width = new IntegerField("宽 5–32") { value = level.width, isDelayed = true };
            var height = new IntegerField("高 5–32") { value = level.height, isDelayed = true }; properties.Add(width); properties.Add(height);
            Button(properties, "应用尺寸", () =>
            {
                int count = document.CroppedCount(width.value, height.value);
                if (count > 0 && !EditorUtility.DisplayDialog("缩小地图", $"将裁掉 {count} 个元素，并删除相关供电引用。此操作可以撤销。", "调整", "取消")) return;
                Edit("调整地图尺寸", () => document.Resize(width.value, height.value));
            }, "resize");
            properties.Add(new Label("选中格 " + selectedCell));
            if (selectedCell.HasValue)
            {
                var at = selectedCell.Value;
                foreach (var entityAtCell in level.crates.Cast<PlacedEntity>().Concat(level.sockets).Concat(level.gates).Where(e => e.Cell == at))
                {
                    string id = entityAtCell.id; Button(properties, "选择 " + id, () => { selectedId = id; DrawProperties(); });
                }
            }
            bool player = selectedId == "player" && level.playerSpawn != null;
            var entity = document.Find(selectedId);
            if (!player && entity == null) return;
            properties.Add(new Label(player ? "玩家" : entity.id));
            var position = player ? level.playerSpawn.Cell : entity.Cell;
            var x = new IntegerField("X") { value = position.x, isDelayed = true };
            var z = new IntegerField("Z") { value = position.z, isDelayed = true }; properties.Add(x); properties.Add(z);
            Button(properties, "移动到坐标", () => Edit("移动元素", () => { document.Move(selectedId, new Cell(x.value, z.value)); selectedCell = new Cell(x.value, z.value); }), "move-entity");
            if (player || entity is GateDefinition)
            {
                string direction = player ? level.playerSpawn.facing : ((GateDefinition)entity).facing;
                var dropdown = new PopupField<string>("朝向", new List<string> { "N", "E", "S", "W" }, LevelValidator.IsFacing(direction) ? direction : "N");
                properties.Add(dropdown); dropdown.RegisterValueChangedCallback(e => Edit("旋转元素", () => { if (player) level.playerSpawn.facing = e.newValue; else ((GateDefinition)entity).facing = e.newValue; }));
            }
            if (entity is CrateDefinition crate)
            {
                var kind = new PopupField<string>("箱子类型", new List<string> { "能源箱", "普通箱" }, crate.IsEnergy ? "能源箱" : "普通箱") { name = "crate-kind" };
                kind.tooltip = "普通箱可以推动，但不会给插槽供电，也不需要归位。";
                properties.Add(kind);
                kind.RegisterValueChangedCallback(e => Edit("修改箱子类型", () => document.SetCrateKind(crate.id,
                    e.newValue == "能源箱" ? CrateDefinition.Energy : CrateDefinition.Cargo)));
            }
            if (entity is SocketDefinition socket)
            {
                var toggle = new Toggle("参与通关目标") { value = socket.isGoal }; properties.Add(toggle);
                toggle.RegisterValueChangedCallback(e => Edit("修改插槽", () => socket.isGoal = e.newValue));
            }
            if (entity is GateDefinition gate)
            {
                var mode = new PopupField<string>("供电模式", new List<string> { "Any", "All" }, gate.powerMode == "All" ? "All" : "Any");
                mode.tooltip = "Any：任一来源供电；All：全部来源供电。"; properties.Add(mode);
                mode.RegisterValueChangedCallback(e => Edit("修改门模式", () => gate.powerMode = e.newValue));
                properties.Add(new Label("勾选供电插槽"));
                foreach (var source in level.sockets)
                {
                    string id = source.id;
                    var sourceToggle = new Toggle($"{id} {source.Cell} {(source.isGoal ? "目标" : "辅助")}") { value = gate.sourceSocketIds.Contains(id) };
                    properties.Add(sourceToggle);
                    sourceToggle.RegisterValueChangedCallback(e => Edit("修改供电来源", () => document.SetGateSources(gate.id,
                        e.newValue ? gate.sourceSocketIds.Concat(new[] { id }).ToArray() : gate.sourceSocketIds.Where(s => s != id).ToArray())));
                }
            }
            Button(properties, "删除选中元素", () => Edit("删除元素", () => document.Erase(position, player || entity is CrateDefinition ? AuthorLayer.Actors : AuthorLayer.Devices)), "delete-entity");
        }
        private static void TextField(VisualElement parent, string title, string value, Action<string> change)
        {
            var field = new UnityEngine.UIElements.TextField(title) { value = value ?? "", isDelayed = true }; parent.Add(field);
            field.RegisterValueChangedCallback(e => change(e.newValue));
        }
        private bool ConfirmDiscard()
        {
            if (!document.IsDirty) return true;
            int result = EditorUtility.DisplayDialogComplex("未保存的关卡", "当前工作副本尚未保存。", "保存", "取消", "放弃");
            return result == 0 ? Save(false) : result == 2;
        }
        private void New()
        {
            if (!ConfirmDiscard()) return;
            Undo.ClearUndo(document); document.level = LevelDocument.NewLevel(); document.filePath = ""; document.savedHash = ""; document.solution = null;
            document.savedSolution = "";
            selectedId = null; selectedCell = null; document.Backup(); Refresh(); FrameBoard();
        }
        private void Open()
        {
            if (!ConfirmDiscard()) return;
            string path = EditorUtility.OpenFilePanel("打开关卡", "Assets/Resources/configs/levels", "json");
            if (path.Length > 0) Run(() => { document.Open(path); Refresh(); FrameBoard(); });
        }
        private bool Save(bool copy)
        {
            if (LiveMode)
            {
                try
                {
                    if (copy)
                    {
                        string target = EditorUtility.SaveFilePanel("另存现场关卡副本", "Assets/Resources/configs/test_levels", document.level.id + "_live", "json");
                        if (string.IsNullOrEmpty(target)) return false;
                        liveWorkspace.SaveCopy(target); hint.text = "现场关卡副本已保存（新 ID）。";
                    }
                    else { liveWorkspace.SaveDraft(); hint.text = "现场草稿已备份，原关卡文件未改动。"; }
                    Refresh(); return true;
                }
                catch (Exception exception) { hint.text = exception.Message; return false; }
            }
            string path = document.filePath;
            if (copy || string.IsNullOrEmpty(path)) path = EditorUtility.SaveFilePanel("保存关卡", "Assets/Resources/configs/levels", document.level.id + (copy ? "_copy" : ""), "json");
            if (string.IsNullOrEmpty(path)) return false;
            if (copy && !string.IsNullOrEmpty(document.filePath) && string.Equals(Path.GetFullPath(path), Path.GetFullPath(document.filePath), StringComparison.OrdinalIgnoreCase))
            { hint.text = "副本必须选择其他文件，不能覆盖原关卡。"; return false; }
            try { document.Save(path, copy); Refresh(); hint.text = "关卡已保存。"; return true; }
            catch (Exception e) { hint.text = "保存失败，工作副本保留：" + e.Message; return false; }
        }
        public void Validate()
        {
            issues.Clear(); var report = LiveMode ? LevelValidator.ValidateLive(document.level) : LevelValidator.Validate(document.level);
            issues.Add(new Label(report.IsValid ? "结构校验通过；这不等于有解证明。" : "校验失败：请修复以下错误。"));
            foreach (var issue in report.Issues)
            {
                var captured = issue;
                Button(issues, issue.ToString() + (issue.Cell.HasValue ? " " + issue.Cell : ""), () =>
                { if (!captured.Cell.HasValue) return; selectedCell = captured.Cell; brush = LevelBrush.Select; selectedId = document.level.gates.FirstOrDefault(g => g.Cell == captured.Cell.Value)?.id; Refresh(); FrameCell(captured.Cell.Value); });
            }
        }
        private void Replay()
        {
            Run(() =>
            {
                if (document.solution == null) throw new InvalidOperationException("请先试玩通关并保存参考解法。");
                string result = document.solution.Verify(document.level, false);
                document.Change("验证参考解法", () => document.solution.contentHash = LevelJson.Hash(document.level));
                Refresh(); hint.text = result;
            });
        }
        private void ImportRecipe()
        {
            if (!ConfirmDiscard()) return;
            string path = EditorUtility.OpenFilePanel("导入策划配方（JSON）", "Docs/LevelRecipes", "json");
            if (string.IsNullOrEmpty(path)) return;
            Run(() => { RecipeImporter.ImportInto(document, File.ReadAllText(path)); document.filePath = ""; document.savedHash = ""; Refresh(); FrameBoard(); });
        }
        private void OnKey(KeyDownEvent e)
        {
            if (!CanEditDocument) return;
            if (e.target is UnityEngine.UIElements.TextField || e.target is TextElement) return;
            if (e.ctrlKey && e.keyCode == KeyCode.S) { Save(false); e.StopPropagation(); }
            else if (e.ctrlKey && e.keyCode == KeyCode.Z) { Undo.PerformUndo(); e.StopPropagation(); }
            else if (e.ctrlKey && e.keyCode == KeyCode.Y) { Undo.PerformRedo(); e.StopPropagation(); }
            else if (e.keyCode == KeyCode.Q || e.keyCode == KeyCode.E) Rotate(e.keyCode == KeyCode.E ? 1 : -1);
        }
        private void Rotate(int delta)
        {
            Edit("旋转元素", () =>
            {
                string current = selectedId == "player" && document.level.playerSpawn != null ? document.level.playerSpawn.facing : (document.Find(selectedId) as GateDefinition)?.facing ?? facing;
                string rotated = ((Direction)(((int)Enum.Parse(typeof(Direction), current) + delta + 4) % 4)).ToString();
                if (selectedId == "player" && document.level.playerSpawn != null) document.level.playerSpawn.facing = rotated;
                else if (document.Find(selectedId) is GateDefinition gate) gate.facing = rotated;
                facing = rotated;
            });
        }
        private void FrameBoard()
        {
            var scene = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
            scene.LookAt(new Vector3((document.level.width - 1) / 2f, 0, (document.level.height - 1) / 2f), Quaternion.Euler(75, 0, 0), Mathf.Max(document.level.width, document.level.height) * .6f);
        }
        private static void FrameCell(Cell cell) => SceneView.lastActiveSceneView?.LookAt(BoardView.Position(cell), Quaternion.Euler(75, 0, 0), 4);
        private void DrawScene(SceneView view)
        {
            if (!document || document.level == null || !CanEditDocument) return;
            var l = document.level;
            for (int z = 0; z < l.height; z++)
                for (int x = 0; x < l.width; x++)
                {
                    var cell = new Cell(x, z); Vector3 p = BoardView.Position(cell);
                    Handles.DrawSolidRectangleWithOutline(new[] { p + new Vector3(-.48f, 0, -.48f), p + new Vector3(-.48f, 0, .48f), p + new Vector3(.48f, 0, .48f), p + new Vector3(.48f, 0, -.48f) }, TileColor(cell), Color.gray);
                    Handles.Label(p + Vector3.up * .03f, Symbol(cell) + " " + x + "," + z);
                }
            foreach (var gate in l.gates)
                foreach (string id in gate.sourceSocketIds)
                {
                    var source = l.sockets.FirstOrDefault(s => s.id == id); if (source == null) continue;
                    Handles.color = Color.cyan; Handles.DrawDottedLine(BoardView.Position(source.Cell) + Vector3.up * .12f, BoardView.Position(gate.Cell) + Vector3.up * .12f, 4);
                }
            var evt = Event.current; if (evt.alt || evt.button == 2) return;
            if (evt.type == EventType.KeyDown)
            {
                if (evt.control && evt.keyCode == KeyCode.S) { Save(false); evt.Use(); }
                else if (evt.keyCode == KeyCode.Q || evt.keyCode == KeyCode.E) { Rotate(evt.keyCode == KeyCode.E ? 1 : -1); evt.Use(); }
            }
            var plane = new Plane(Vector3.up, Vector3.zero);
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 point = ray.GetPoint(distance); var hover = new Cell(Mathf.FloorToInt(point.x + .5f), Mathf.FloorToInt(point.z + .5f));
            if (evt.type == EventType.MouseUp && brushing) { EndStroke(); Refresh(); evt.Use(); }
            if (!l.Contains(hover)) return;
            if (evt.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Handles.color = Color.yellow; Handles.DrawWireCube(BoardView.Position(hover), new Vector3(.95f, .03f, .95f));
            if (evt.type == EventType.MouseDown && (evt.button == 0 || evt.button == 1)) { BeginStroke(); Apply(hover, evt.button == 1); evt.Use(); }
            if (evt.type == EventType.MouseDrag && brushing && IsTerrainBrush()) { Apply(hover, evt.button == 1); evt.Use(); }
            if (evt.type == EventType.MouseMove) view.Repaint();
        }
    }

    public sealed class HelpWindow : EditorWindow
    {
        public static void OpenHelp() => GetWindow<HelpWindow>("关卡编辑器帮助");
        public void CreateGUI()
        {
            rootVisualElement.Add(new Label("1. 新建 → 调整宽高 → 外围墙。\n2. 放玩家 P、能源箱 C、目标插槽 ◎；普通箱 X 可推、不供电、无需归位。\n3. 放门 D 后，在属性中勾选来源插槽。\n4. 校验；点击错误可定位。\n5. 试玩并录制；↑→↓← 为世界方向，V 切视角，Z 撤销，R 重开。\n6. 通关后保存参考解法，再退出 Play Mode。\n7. 保存关卡；回放通过后再保存，绑定当前版本。\n\n地图坐标从左下角 (0,0) 开始，北方在上。\n左键选择/放置，地形可拖刷；右键按图层擦除。\nCtrl+S 保存，Ctrl+Z / Ctrl+Y 撤销重做，Q/E 旋转。\nSceneView Alt/中键保留导航。\n\n当前为首轮实现；正式目录管理、菜单/进度及完整验收仍待后续迭代。") { style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 16, paddingTop = 16 } });
        }
    }
}
