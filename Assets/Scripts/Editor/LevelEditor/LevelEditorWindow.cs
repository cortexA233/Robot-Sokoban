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
        private Label status, hint, toolStatus, noticeText;
        private VisualElement notice;
        private IVisualElementScheduledItem noticeTimer;
        private string validatedHash;
        private bool levelPropertiesExpanded;
        private bool brushing;
        private int undoGroup;
        private readonly HashSet<Cell> stroke = new HashSet<Cell>();
        public LevelDocument Document => document;
        public Cell? SelectedCell => selectedCell;
        public string SelectedObjectId => selectedId;
        public LevelBrush CurrentBrush => brush;
        public ValidationReport LastValidation { get; private set; }
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
        private void OnLostFocus() => EndStroke();
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
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, paddingBottom = 4 } };
            rootVisualElement.Add(toolbar);
            var files = ToolbarGroup(toolbar, "文件");
            Button(files, "新建", New, "new-level"); Button(files, "打开", Open, "open-level");
            Button(files, "保存", () => Save(false), "save-level"); Button(files, "另存副本", () => Save(true), "copy-level");
            Button(files, "导入配方", ImportRecipe, "import-recipe");
            var editing = ToolbarGroup(toolbar, "编辑");
            Button(editing, "撤销", Undo.PerformUndo, "undo"); Button(editing, "重做", Undo.PerformRedo, "redo");
            var testing = ToolbarGroup(toolbar, "校验与试玩");
            Button(testing, "校验", Validate, "validate-level"); Button(testing, "试玩并录制", Play, "playtest");
            Button(testing, "回放解法", Replay, "replay");
            var publishing = ToolbarGroup(toolbar, "目录与帮助");
            Button(publishing, "关卡目录", () => CampaignCatalogWindow.OpenWindow(), "campaign-catalog");
            Button(publishing, "帮助", () => HelpWindow.OpenHelp(), "help");
            var liveFoldout = new Foldout { text = "现场编辑", value = false, viewDataKey = "live-tools" }; rootVisualElement.Add(liveFoldout);
            var liveBar = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } }; liveFoldout.Add(liveBar);
            Button(liveBar, "捕获当前游戏局面", () => CaptureLive(UnityEngine.Object.FindObjectOfType<LevelRunner>()), "capture-live");
            Button(liveBar, "编辑已有现场草稿", () => Run(() => { ShowLiveDraft(); liveWorkspace.Resume(); RefreshMode(); }), "resume-live");
            Button(liveBar, "应用并继续试玩", ApplyLive, "apply-live");
            Button(liveBar, "结束现场试玩", EndLive, "end-live");
            Button(liveBar, "带回作者文档", () => Run(() => { liveWorkspace.BringBack(); hint.text = "已带回作者文档；尚未写入原文件。"; }), "bring-live");
            Button(liveBar, "恢复/查看现场草稿", () => Run(() => { ShowLiveDraft(); Refresh(); }), "recover-live");
            toolStatus = new Label { name = "tool-status", style = { whiteSpace = WhiteSpace.Normal, unityFontStyleAndWeight = FontStyle.Bold, paddingTop = 4, paddingBottom = 6 } };
            rootVisualElement.Add(toolStatus);
            var body = new VisualElement { name = "EditorBody", style = { flexDirection = FlexDirection.Row, flexGrow = 1, minHeight = 220 } };
            rootVisualElement.Add(body);
            var palette = new ScrollView { style = { width = 182, flexShrink = 0, paddingRight = 6 } }; body.Add(palette);
            AddBrushes(palette, "工具", LevelBrush.Select, LevelBrush.Erase);
            AddBrushes(palette, "地形", LevelBrush.Floor, LevelBrush.Wall, LevelBrush.Void, LevelBrush.LowFriction);
            AddBrushes(palette, "角色与箱子", LevelBrush.Player, LevelBrush.Crate, LevelBrush.CargoCrate);
            AddBrushes(palette, "机关", LevelBrush.GoalSocket, LevelBrush.UtilitySocket, LevelBrush.Gate, LevelBrush.Redirector);
            var layerNames = new List<string> { "地形", "机关", "角色与箱子" };
            var layerField = new PopupField<string>("右键擦除", layerNames, (int)layer) { name = "erase-layer" }; palette.Add(layerField);
            layerField.labelElement.style.minWidth = 64; layerField.labelElement.style.width = 64;
            layerField.RegisterValueChangedCallback(e => { layer = (AuthorLayer)layerNames.IndexOf(e.newValue); UpdateToolStatus(); });
            var facingField = new PopupField<string>("朝向", new List<string> { "N", "E", "S", "W" }, facing) { name = "brush-facing" }; palette.Add(facingField);
            facingField.labelElement.style.minWidth = 64; facingField.labelElement.style.width = 64;
            facingField.RegisterValueChangedCallback(e => { facing = e.newValue; UpdateToolStatus(); });
            Button(palette, "外围墙", () => Edit("建立外围墙", document.Border), "border");
            Button(palette, "SceneView 聚焦", FrameBoard, "frame-board");
            var examples = new Foldout { text = "示例关卡", value = false, viewDataKey = "examples" }; palette.Add(examples);
            foreach (string name in new[] { "L04", "L05", "L06", "L07", "L08", "L09", "L10", "L11", "L12", "LAB01_LowFriction" })
            {
                string path = "Assets/Resources/configs/" + (name.StartsWith("LAB") ? "test_levels/" : "levels/") + name + ".json";
                Button(examples, name.StartsWith("LAB") ? "LAB01（开发测试）" : name, () => OpenAsset(path), "example-" + name);
            }
            var canvas = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { style = { flexGrow = 1, backgroundColor = new Color(.12f, .15f, .19f), paddingTop = 12, paddingLeft = 12 } };
            body.Add(canvas); grid = new VisualElement { focusable = true, name = "board-grid" }; canvas.Add(grid);
            properties = new ScrollView { style = { width = 280, flexShrink = 0, paddingLeft = 8 } }; body.Add(properties);
            hint = new Label("左键放置/选取，地形可拖刷；右键擦除当前图层。SceneView 的 Alt/中键导航保留。") { style = { whiteSpace = WhiteSpace.Normal } };
            rootVisualElement.Add(hint);
            issues = new ScrollView { name = "validation-issues", style = { height = 130, flexShrink = 0, borderTopWidth = 1, borderTopColor = Color.gray } }; rootVisualElement.Add(issues);
            status = new Label { style = { whiteSpace = WhiteSpace.Normal } }; rootVisualElement.Add(status);
            notice = new VisualElement { name = "editor-notice", style = { position = Position.Absolute, right = 12, top = 112, width = 306,
                paddingLeft = 12, paddingRight = 12, paddingTop = 10, paddingBottom = 10, borderLeftWidth = 4, display = DisplayStyle.None } };
            noticeText = new Label { style = { whiteSpace = WhiteSpace.Normal, color = Color.white, fontSize = 13 } }; notice.Add(noticeText);
            Button(notice, "关闭提示", () => notice.style.display = DisplayStyle.None, "dismiss-notice"); rootVisualElement.Add(notice);
            rootVisualElement.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKey);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKey);
            RefreshMode();
            Refresh();
        }
        private void OnPointerUp(PointerUpEvent evt) => EndStroke();
        private static VisualElement ToolbarGroup(VisualElement parent, string title)
        {
            var group = new VisualElement { style = { marginRight = 12, marginBottom = 4 } }; parent.Add(group);
            group.Add(new Label(title) { style = { fontSize = 11, opacity = .7f } });
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } }; group.Add(row); return row;
        }
        private void AddBrushes(VisualElement parent, string title, params LevelBrush[] brushes)
        {
            var group = new Foldout { text = title, value = true, viewDataKey = "palette-" + title }; parent.Add(group);
            foreach (var value in brushes)
            {
                var captured = value;
                Button(group, BrushName(value), () => SetBrush(captured), "brush-" + value);
                group.Q<Button>("brush-" + value).tooltip = BrushDescription(value);
            }
        }
        private static string BrushDescription(LevelBrush value)
        {
            switch (value)
            {
                case LevelBrush.Select: return "点击格子选择；同格对象在右侧切换。Esc 回到选择。";
                case LevelBrush.Wall: case LevelBrush.Void: return "会删除该格所有对象及相关供电引用；可撤销。";
                case LevelBrush.Crate: return "能源箱：可推动，停在插槽上供电。";
                case LevelBrush.CargoCrate: return "普通箱：可推动，不供电，无需归位。";
                case LevelBrush.GoalSocket: return "目标插槽：需要能源箱供电才能通关，也可连接门。";
                case LevelBrush.UtilitySocket: return "辅助插槽：给门供电，不计入通关目标。";
                case LevelBrush.Gate: return "受电门：放置后在右侧选择供电插槽和开启条件。";
                case LevelBrush.Redirector: return "固定转向板：箱子进入后沿箭头继续移动。";
                case LevelBrush.LowFriction: return "两类箱子沿推动方向滑行，受阻停下。";
                case LevelBrush.Erase: return "只擦除当前擦除层；地形擦除会恢复地板。";
                default: return "左键连续放置；Esc 回到选择。";
            }
        }
        public void SetBrush(LevelBrush value)
        {
            EndStroke(); brush = value; UpdateToolStatus();
            if (hint != null) hint.text = BrushDescription(value);
        }
        private void UpdateToolStatus()
        {
            if (toolStatus == null) return;
            toolStatus.text = (brush == LevelBrush.Select ? "选择模式" : "画笔：" + BrushName(brush)) + "  ·  右键擦除：" +
                new[] { "地形", "机关", "角色与箱子" }[(int)layer] + "  ·  放置朝向：" + facing + "  ·  Esc 选择 / Q E 旋转";
            foreach (LevelBrush value in Enum.GetValues(typeof(LevelBrush)))
            {
                var button = rootVisualElement.Q<Button>("brush-" + value);
                if (button == null) continue;
                button.text = (brush == value ? "✓ " : "") + BrushName(value);
                button.style.unityFontStyleAndWeight = brush == value ? FontStyle.Bold : FontStyle.Normal;
            }
        }
        private static string BrushName(LevelBrush value) => new[] { "选择", "地板 Floor", "墙 Wall", "虚空 Void", "低摩擦轨道", "玩家", "能源箱", "辅助插槽", "目标插槽", "受电门", "擦除当前层", "普通箱（不供电）", "固定转向板" }[(int)value];
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
            foreach (string name in new[] { "undo", "redo" }) rootVisualElement.Q<Button>(name)?.SetEnabled(CanEditDocument);
            foreach (var button in rootVisualElement.Query<Button>().ToList())
                if (button.name != null && button.name.StartsWith("example-")) button.SetEnabled(!LiveMode && !EditorApplication.isPlayingOrWillChangePlaymode);
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
        { try { action(); } catch (Exception e) { Notify(e.Message, true); } }
        private void Notify(string message, bool error = false)
        {
            if (hint != null) hint.text = message;
            if (notice == null) return;
            noticeTimer?.Pause();
            noticeText.text = message;
            notice.style.backgroundColor = error ? new Color(.36f, .10f, .10f) : new Color(.10f, .24f, .30f);
            notice.style.borderLeftColor = error ? new Color(1f, .35f, .25f) : new Color(.3f, .8f, 1f);
            notice.style.display = DisplayStyle.Flex;
            noticeTimer = notice.schedule.Execute(() => notice.style.display = DisplayStyle.None).StartingIn(6000);
        }
        private void Edit(string label, Action change)
        { if (CanEditDocument) Run(() => { document.Change(label, change); if (LiveMode) liveWorkspace.Backup(); rootVisualElement.schedule.Execute(Refresh); }); }
        private void Refresh()
        {
            if (grid == null || !document || document.level == null) return;
            var level = document.level;
            NormalizeSelection();
            bool rebuild = grid.childCount != level.height || grid.childCount > 0 && grid[0].childCount != level.width;
            if (rebuild) grid.Clear();
            for (int z = level.height - 1; z >= 0; z--)
            {
                var row = rebuild ? new VisualElement { style = { flexDirection = FlexDirection.Row } } : grid[level.height - z - 1];
                if (rebuild) grid.Add(row);
                for (int x = 0; x < level.width; x++)
                {
                    var cell = new Cell(x, z);
                    if (!rebuild)
                    {
                        var existing = (Label)row[x]; existing.text = Symbol(cell); existing.style.backgroundColor = TileColor(cell);
                        existing.tooltip = CellDescription(cell); continue;
                    }
                    var tile = new Label(Symbol(cell)) { name = $"cell-{x}-{z}", tooltip = CellDescription(cell),
                        style = { width = 34, height = 34, marginRight = 2, marginBottom = 2, unityTextAlign = TextAnchor.MiddleCenter,
                            backgroundColor = TileColor(cell), color = Color.white, fontSize = 14 } };
                    row.Add(tile);
                    tile.RegisterCallback<PointerDownEvent>(e =>
                    {
                        if (e.altKey || (e.button != 0 && e.button != 1)) return;
                        grid.Focus(); EndStroke();
                        if (brush == LevelBrush.Select && e.button == 0) SelectCell(cell);
                        else { BeginStroke(); Apply(cell, e.button == 1); }
                        e.StopPropagation();
                    });
                    tile.RegisterCallback<PointerEnterEvent>(e =>
                    { hint.text = "格 " + cell + " · " + BrushName(brush) + (brush == LevelBrush.Wall || brush == LevelBrush.Void ? "（删除此格实体）" : ""); if (brushing && e.pressedButtons != 0 && IsTerrainBrush()) Apply(cell, e.pressedButtons == 2); });
                    tile.RegisterCallback<PointerUpEvent>(_ => { EndStroke(); Refresh(); });
                }
            }
            DrawProperties();
            UpdateToolStatus();
            if (validatedHash != null && validatedHash != LevelJson.Hash(level))
            {
                validatedHash = null; LastValidation = null; issues.Clear();
                issues.Add(new Label("内容已变更；点击校验或直接试玩以检查当前版本。"));
                if (notice != null) notice.style.display = DisplayStyle.None;
            }
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
            var redirector = l.redirectors.FirstOrDefault(r => r.Cell == cell);
            string arrow = redirector == null ? "" : Arrow(redirector.facing);
            var socket = l.sockets.FirstOrDefault(s => s.Cell == cell);
            string device = socket != null ? socket.isGoal ? "◎" : "s" : l.gates.Any(g => g.Cell == cell) ? "D" : arrow;
            if (l.playerSpawn != null && l.playerSpawn.Cell == cell) return "P" + device;
            var crate = l.crates.FirstOrDefault(c => c.Cell == cell);
            if (crate != null) return (crate.IsEnergy ? "C" : "X") + device;
            if (l.gates.Any(g => g.Cell == cell)) return "D";
            var s = l.sockets.FirstOrDefault(v => v.Cell == cell); if (s != null) return s.isGoal ? "◎" : "s";
            if (redirector != null) return arrow;
            return l.TerrainAt(cell) == Terrain.LowFriction ? "≋" : l.TerrainAt(cell) == Terrain.Wall ? "■" : "";
        }
        private static string Arrow(string direction) => direction == "N" ? "↑" : direction == "E" ? "→" : direction == "S" ? "↓" : "←";
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
        private IEnumerable<string> ObjectsAt(Cell cell)
        {
            if (document.level.playerSpawn != null && document.level.playerSpawn.Cell == cell) yield return "player";
            foreach (var entity in document.level.crates.Cast<PlacedEntity>().Concat(document.level.sockets).Concat(document.level.gates).Concat(document.level.redirectors))
                if (entity.Cell == cell) yield return entity.id;
        }
        private string ObjectName(string id)
        {
            if (id == "player") return "玩家";
            var entity = document.Find(id);
            if (entity is CrateDefinition crate) return crate.IsEnergy ? "能源箱" : "普通箱";
            if (entity is SocketDefinition socket) return socket.isGoal ? "目标插槽" : "辅助插槽";
            if (entity is GateDefinition) return "受电门";
            if (entity is RedirectorDefinition) return "固定转向板";
            return "无对象";
        }
        private static string TerrainName(Terrain terrain) => terrain == Terrain.Floor ? "地板" : terrain == Terrain.Wall ? "墙" : terrain == Terrain.LowFriction ? "低摩擦轨道" : "虚空";
        private string CellDescription(Cell cell) => cell + " · " + TerrainName(document.level.TerrainAt(cell)) + " · " + string.Join(" / ", ObjectsAt(cell).Select(ObjectName));
        private void NormalizeSelection()
        {
            if (!selectedCell.HasValue || !document.level.Contains(selectedCell.Value)) { selectedCell = null; selectedId = null; return; }
            if (!ObjectsAt(selectedCell.Value).Contains(selectedId)) selectedId = ObjectsAt(selectedCell.Value).FirstOrDefault();
        }
        public void SelectCell(Cell cell)
        {
            if (!CanEditDocument || !document.level.Contains(cell)) return;
            EndStroke(); brush = LevelBrush.Select;
            if (selectedCell != cell) selectedId = null;
            selectedCell = cell; NormalizeSelection(); Refresh();
        }
        public void SelectObject(string id)
        {
            if (!CanEditDocument) return;
            Cell? cell = id == "player" ? document.level.playerSpawn?.Cell : document.Find(id)?.Cell;
            if (!cell.HasValue) return;
            EndStroke(); brush = LevelBrush.Select; selectedCell = cell; selectedId = id;
            // A clicked object button must release its pointer before the property list replaces it.
            rootVisualElement.schedule.Execute(Refresh);
        }
        private void BeginStroke()
        {
            if (!CanEditDocument || brushing) return;
            brushing = true; stroke.Clear(); Undo.IncrementCurrentGroup(); undoGroup = Undo.GetCurrentGroup();
            document.RecordUndo("棋盘画笔");
        }
        private void EndStroke()
        {
            if (!brushing) return;
            brushing = false;
            // Serialize the completed stroke before finalizing its Undo group, so Redo has the edited state.
            document.Backup(); Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(undoGroup);
            if (LiveMode && liveWorkspace) liveWorkspace.Backup();
            hasUnsavedChanges = !LiveMode && document.IsDirty;
        }
        private void Apply(Cell cell, bool erase)
        {
            if (!CanEditDocument || !document.level.Contains(cell) || !stroke.Add(cell)) return;
            if (selectedCell != cell) selectedId = null;
            selectedCell = cell;
            try
            {
                if (erase || brush == LevelBrush.Erase) document.Erase(cell, layer);
                else if (IsTerrainBrush()) document.Paint(cell, new[] { '.', '#', '_', '~' }[(int)brush - (int)LevelBrush.Floor]);
                else if (brush != LevelBrush.Select) selectedId = document.Place(brush, cell, facing);
                else selectedId = document.level.playerSpawn != null && document.level.playerSpawn.Cell == cell ? "player" :
                    document.level.crates.Cast<PlacedEntity>().Concat(document.level.sockets).Concat(document.level.gates).Concat(document.level.redirectors).FirstOrDefault(e => e.Cell == cell)?.id;
                EditorUtility.SetDirty(document);
            }
            catch (Exception exception) { Notify(exception.Message, true); }
            finally { NormalizeSelection(); Refresh(); }
        }

        private void DrawProperties()
        {
            properties.Clear(); var level = document.level;
            var levelFields = new Foldout { text = "关卡属性与尺寸", value = levelPropertiesExpanded, name = "level-properties" }; properties.Add(levelFields);
            levelFields.RegisterValueChangedCallback(e => levelPropertiesExpanded = e.newValue);
            levelFields.Add(new Label("ID: " + level.id));
            TextField(levelFields, "标题", level.title, text => Edit("修改标题", () => level.title = text));
            TextField(levelFields, "目标提示", level.briefing, text => Edit("修改提示", () => level.briefing = text));
            TextField(levelFields, "完成文案", level.completionText, text => Edit("修改完成文案", () => level.completionText = text));
            var width = new IntegerField("宽 5–32") { value = level.width, isDelayed = true };
            var height = new IntegerField("高 5–32") { value = level.height, isDelayed = true }; levelFields.Add(width); levelFields.Add(height);
            Button(levelFields, "应用尺寸", () =>
            {
                int count = document.CroppedCount(width.value, height.value);
                if (count > 0 && !EditorUtility.DisplayDialog("缩小地图", $"将裁掉 {count} 个元素，并删除相关供电引用。此操作可以撤销。", "调整", "取消")) return;
                Edit("调整地图尺寸", () => document.Resize(width.value, height.value));
            }, "resize");
            properties.Add(new Label(selectedCell.HasValue ? "当前格 " + selectedCell : "点击棋盘选择一个格子") { name = "selected-cell", style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } });
            if (selectedCell.HasValue)
            {
                var at = selectedCell.Value;
                properties.Add(new Label("地形：" + TerrainName(level.TerrainAt(at))));
                foreach (string objectId in ObjectsAt(at))
                {
                    string id = objectId; bool active = selectedId == id;
                    Button(properties, (active ? "✓ " : "选择 ") + ObjectName(id), () => SelectObject(id), "select-" + id);
                    var choice = properties.Q<Button>("select-" + id); choice.tooltip = id;
                    choice.style.backgroundColor = active ? new Color(.18f, .37f, .46f) : new StyleColor(StyleKeyword.Null);
                }
                if (!ObjectsAt(at).Any()) properties.Add(new Label("此格没有对象。"));
            }
            bool player = selectedId == "player" && level.playerSpawn != null;
            var entity = document.Find(selectedId);
            if (!player && entity == null) return;
            properties.Add(new Label("正在编辑：" + ObjectName(selectedId)) { name = "selected-object", style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } });
            properties.Add(new Label("ID: " + selectedId) { style = { fontSize = 10, opacity = .6f } });
            var position = player ? level.playerSpawn.Cell : entity.Cell;
            var x = new IntegerField("X") { value = position.x, isDelayed = true };
            var z = new IntegerField("Z") { value = position.z, isDelayed = true }; properties.Add(x); properties.Add(z);
            Button(properties, "移动到坐标", () => Edit("移动元素", () => { document.Move(selectedId, new Cell(x.value, z.value)); selectedCell = new Cell(x.value, z.value); }), "move-entity");
            if (player || entity is GateDefinition || entity is RedirectorDefinition)
            {
                string direction = player ? level.playerSpawn.facing : entity is GateDefinition selectedGate ? selectedGate.facing : ((RedirectorDefinition)entity).facing;
                var dropdown = new PopupField<string>("朝向", new List<string> { "N", "E", "S", "W" }, LevelValidator.IsFacing(direction) ? direction : "N") { name = "entity-facing" };
                properties.Add(dropdown); dropdown.RegisterValueChangedCallback(e => Edit("旋转元素", () => { if (player) level.playerSpawn.facing = e.newValue; else if (entity is GateDefinition g) g.facing = e.newValue; else ((RedirectorDefinition)entity).facing = e.newValue; }));
                if (entity is RedirectorDefinition) properties.Add(new Label("箱子进入后沿箭头继续移动；受阻停稳后不会自行重启。") { style = { whiteSpace = WhiteSpace.Normal } });
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
                var mode = new PopupField<string>("开启条件", new List<string> { "任一来源供电", "全部来源供电" }, gate.powerMode == "All" ? 1 : 0) { name = "gate-power-mode" };
                mode.tooltip = "Any：任一来源供电；All：全部来源供电。"; properties.Add(mode);
                mode.RegisterValueChangedCallback(e => Edit("修改门模式", () => gate.powerMode = e.newValue == "全部来源供电" ? "All" : "Any"));
                properties.Add(new Label("勾选供电插槽"));
                foreach (var source in level.sockets)
                {
                    string id = source.id;
                    var sourceToggle = new Toggle($"{(source.isGoal ? "目标插槽" : "辅助插槽")} {source.Cell}") { value = gate.sourceSocketIds.Contains(id), tooltip = id, name = "source-" + id };
                    properties.Add(sourceToggle);
                    sourceToggle.RegisterValueChangedCallback(e => Edit("修改供电来源", () => document.SetGateSources(gate.id,
                        e.newValue ? gate.sourceSocketIds.Concat(new[] { id }).ToArray() : gate.sourceSocketIds.Where(s => s != id).ToArray())));
                }
            }
            Button(properties, "删除选中元素", () => Edit("删除元素", () => document.Erase(position, player || entity is CrateDefinition ? AuthorLayer.Actors : AuthorLayer.Devices)), "delete-entity");
        }
        private static void TextField(VisualElement parent, string title, string value, Action<string> change)
        {
            var field = new UnityEngine.UIElements.TextField(title) { value = value ?? "", isDelayed = true, name = "field-" + title }; parent.Add(field);
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
            ResetSelection(); levelPropertiesExpanded = true; document.Backup(); Refresh(); FrameBoard();
        }
        private void Open()
        {
            string path = EditorUtility.OpenFilePanel("打开关卡", "Assets/Resources/configs/levels", "json");
            if (path.Length > 0) OpenAsset(path);
        }
        private void ResetSelection()
        { EndStroke(); selectedId = null; selectedCell = null; brush = LevelBrush.Select; validatedHash = null; LastValidation = null; issues?.Clear(); if (notice != null) notice.style.display = DisplayStyle.None; }
        public bool OpenAsset(string path, Cell? focusCell = null)
        {
            if (LiveMode || EditorApplication.isPlayingOrWillChangePlaymode) { Notify("请先结束现场编辑并退出试玩，再打开作者关卡。", true); return false; }
            if (!ConfirmDiscard()) return false;
            try
            {
                EndStroke(); document.Open(path); ResetSelection(); Refresh(); FrameBoard(); Focus();
                if (focusCell.HasValue) { SelectCell(focusCell.Value); FrameCell(focusCell.Value); }
                return true;
            }
            catch (Exception exception) { Notify(exception.Message, true); return false; }
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
                catch (Exception exception) { Notify(exception.Message, true); return false; }
            }
            string path = document.filePath;
            if (copy || string.IsNullOrEmpty(path)) path = EditorUtility.SaveFilePanel("保存关卡", "Assets/Resources/configs/levels", document.level.id + (copy ? "_copy" : ""), "json");
            if (string.IsNullOrEmpty(path)) return false;
            if (copy && !string.IsNullOrEmpty(document.filePath) && string.Equals(Path.GetFullPath(path), Path.GetFullPath(document.filePath), StringComparison.OrdinalIgnoreCase))
            { hint.text = "副本必须选择其他文件，不能覆盖原关卡。"; return false; }
            try { EndStroke(); document.Save(path, copy); Refresh(); Notify("关卡已保存。"); return true; }
            catch (Exception e) { Notify("保存失败，工作副本保留：" + e.Message, true); return false; }
        }
        public void Validate()
        {
            EndStroke(); ShowValidation(LiveMode ? LevelValidator.ValidateLive(document.level) : LevelValidator.Validate(document.level));
        }
        public void Play()
        {
            EndStroke(); Run(() => PlaytestBridge.Play(document, ShowValidation));
        }
        private void ShowValidation(ValidationReport report)
        {
            LastValidation = report; validatedHash = LevelJson.Hash(document.level); issues.Clear();
            int errors = report.Issues.Count(i => i.IsError), warnings = report.Issues.Count(i => !i.IsError);
            string summary = report.IsValid ? $"结构通过 · {warnings} 条提示；结构合法不等于有解。" : $"校验未通过 · {errors} 个错误 / {warnings} 条提示";
            issues.Add(new HelpBox(summary, report.IsValid ? HelpBoxMessageType.Info : HelpBoxMessageType.Error));
            foreach (var issue in report.Issues)
            {
                var captured = issue;
                var row = new Button(() =>
                {
                    if (!captured.Cell.HasValue) return;
                    SelectCell(captured.Cell.Value);
                    string related = ObjectsAt(captured.Cell.Value).FirstOrDefault(id => captured.Message.Contains(id));
                    if (related != null) SelectObject(related);
                    FrameCell(captured.Cell.Value);
                }) { text = issue.ToString() + (issue.Cell.HasValue ? " " + issue.Cell : ""), style = { whiteSpace = WhiteSpace.Normal, minHeight = 25,
                    color = issue.IsError ? new Color(1f, .65f, .55f) : new Color(1f, .86f, .5f) } };
                issues.Add(row);
            }
            string reason = report.Issues.FirstOrDefault(i => i.IsError)?.Message;
            Notify(summary + (reason == null ? "" : "\n" + reason + "\n完整问题见底部列表，点击坐标可定位。"), !report.IsValid);
        }
        private void Replay()
        {
            Run(() =>
            {
                if (document.solution == null) throw new InvalidOperationException("请先试玩通关并保存参考解法。");
                string result = document.solution.Verify(document.level, false);
                document.Change("验证参考解法", () => document.solution.contentHash = LevelJson.Hash(document.level));
                Refresh(); Notify(result);
            });
        }
        private void ImportRecipe()
        {
            if (!ConfirmDiscard()) return;
            string path = EditorUtility.OpenFilePanel("导入策划配方（JSON）", "Docs/LevelRecipes", "json");
            if (string.IsNullOrEmpty(path)) return;
            Run(() => { EndStroke(); RecipeImporter.ImportInto(document, File.ReadAllText(path)); document.filePath = ""; document.savedHash = ""; ResetSelection(); Refresh(); FrameBoard(); });
        }
        private void OnKey(KeyDownEvent e)
        {
            if (!CanEditDocument) return;
            if (e.target is UnityEngine.UIElements.TextField || e.target is TextElement) return;
            if (e.keyCode == KeyCode.Escape) { SetBrush(LevelBrush.Select); e.StopPropagation(); }
            else if (e.ctrlKey && e.keyCode == KeyCode.S) { Save(false); e.StopPropagation(); }
            else if (e.ctrlKey && e.keyCode == KeyCode.Z) { Undo.PerformUndo(); e.StopPropagation(); }
            else if (e.ctrlKey && e.keyCode == KeyCode.Y) { Undo.PerformRedo(); e.StopPropagation(); }
            else if (e.keyCode == KeyCode.Q || e.keyCode == KeyCode.E) Rotate(e.keyCode == KeyCode.E ? 1 : -1);
        }
        private void Rotate(int delta)
        {
            if (selectedId != "player" && !(document.Find(selectedId) is GateDefinition) && !(document.Find(selectedId) is RedirectorDefinition))
            {
                facing = ((Direction)(((int)Enum.Parse(typeof(Direction), facing) + delta + 4) % 4)).ToString();
                rootVisualElement.Q<PopupField<string>>("brush-facing")?.SetValueWithoutNotify(facing); UpdateToolStatus(); return;
            }
            Edit("旋转元素", () =>
            {
                string current = selectedId == "player" && document.level.playerSpawn != null ? document.level.playerSpawn.facing : (document.Find(selectedId) as GateDefinition)?.facing ?? (document.Find(selectedId) as RedirectorDefinition)?.facing ?? facing;
                string rotated = ((Direction)(((int)Enum.Parse(typeof(Direction), current) + delta + 4) % 4)).ToString();
                if (selectedId == "player" && document.level.playerSpawn != null) document.level.playerSpawn.facing = rotated;
                else if (document.Find(selectedId) is GateDefinition gate) gate.facing = rotated;
                else if (document.Find(selectedId) is RedirectorDefinition redirector) redirector.facing = rotated;
                facing = rotated;
                rootVisualElement.Q<PopupField<string>>("brush-facing")?.SetValueWithoutNotify(rotated);
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
                    Handles.Label(p + Vector3.up * .03f, Symbol(cell) + " " + x + "," + z + (selectedCell == cell && selectedId != null ? " · " + ObjectName(selectedId) : ""));
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
                if (evt.keyCode == KeyCode.Escape) { SetBrush(LevelBrush.Select); evt.Use(); }
                else if (evt.control && evt.keyCode == KeyCode.S) { Save(false); evt.Use(); }
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
            if (brush == LevelBrush.Redirector) Handles.Label(BoardView.Position(hover) + Vector3.up * .06f, "转向 " + Arrow(facing));
            if (evt.type == EventType.MouseDown && (evt.button == 0 || evt.button == 1))
            {
                EndStroke();
                if (brush == LevelBrush.Select && evt.button == 0) SelectCell(hover);
                else { BeginStroke(); Apply(hover, evt.button == 1); }
                evt.Use();
            }
            if (evt.type == EventType.MouseDrag && brushing && IsTerrainBrush()) { Apply(hover, evt.button == 1); evt.Use(); }
            if (evt.type == EventType.MouseMove) view.Repaint();
        }
    }

    public sealed class HelpWindow : EditorWindow
    {
        public static void OpenHelp() => GetWindow<HelpWindow>("关卡编辑器帮助");
        public void CreateGUI()
        {
            rootVisualElement.Clear(); minSize = new Vector2(430, 430);
            var scroll = new ScrollView(); rootVisualElement.Add(scroll);
            scroll.Add(new Label("制作与试玩\n1. 新建 → 展开右侧关卡属性与尺寸 → 外围墙。\n2. 按地形、角色与箱子、机关分类选择画笔。P 玩家 / C 能源箱 / X 普通箱 / ◎ 目标插槽 / s 辅助插槽 / D 门。\n3. 门在右侧选择供电来源；转向板用朝向或 Q/E 旋转。\n4. 点击试玩会自动校验；有错误时侧边提示，完整列表保留在底部，带坐标的问题可以点击定位。\n5. 通关后保存参考解法，退出 Play Mode，保存关卡；修改后需回放解法并重新保存。\n\n选择与放置\n选择模式下点击其他格，会同步切换格子和对象；空格会清空旧对象。箱子和插槽同格时，右侧可切换正在编辑的对象。再次点击同格保留当前对象。\n点右侧对象按钮或按 Esc 回到选择；画笔模式可以连续放置。右键只擦除选定层，Wall/Void 会删除该格所有对象。\n\n正式关卡目录\n打开关卡目录，使用当前已保存作者关卡或拖入 JSON，加入前须通过结构与当前解法校验。上移/下移/移出支持撤销，最后保存目录；移出保留关卡和解法文件。开发测试关须显式加入。构建会重新检查正式目录和参考解法，错误会阻止构建。\n\n快捷键\nCtrl+S 保存，Ctrl+Z / Ctrl+Y 撤销重做，Esc 选择，Q/E 旋转。SceneView Alt/中键保留导航。试玩中方向键为世界方向，V 切视角，Z 撤销，R 重开。\n\n现场编辑\n展开顶栏现场编辑工具。捕获 → 编辑草稿 → 应用并继续 → 结束现场试玩；保存草稿不会覆盖原作者文件。\n\nL04–L12 为九张正式关卡，LAB01 为开发实验关。坐标从左下角 (0,0) 开始，北方在上。")
            { style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 16, paddingRight = 16, paddingTop = 16, paddingBottom = 16 } });
        }
    }
}
