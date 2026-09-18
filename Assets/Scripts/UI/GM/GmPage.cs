#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using KToolkit;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sokoban.UI
{
    [KUI_Info("screens/GM/Gm", nameof(GmPage))]
    public sealed class GmPage : KUIPage
    {
        private LevelRunner runner;
        private string category = "局面", boundSession;
        private readonly List<string> ids = new List<string>();
        private readonly Dictionary<string, string> categories = new Dictionary<string, string>
        { { "LevelContent", "关卡" }, { "BoardContent", "局面" }, { "PowerContent", "机关" }, { "LogContent", "诊断" } };
        private Text status;
        private Dropdown entity, other;
        private InputField x, z;
        private GmOverlay overlay;
        private float refreshAt;
        private int fontSize = 22;
        public bool Picking { get; private set; }
        private bool monitoring;
        private T Find<T>(string name) where T : Component => gameObject.GetComponentsInChildren<T>(true).First(c => c.name == name);
        private void Bind(string name, UnityEngine.Events.UnityAction action) => Find<Button>(name).onClick.AddListener(action);
        public override void InitParams(params object[] args) => runner = (LevelRunner)args[0];
        public override void OnStart()
        {
            entity = Find<Dropdown>("Entity"); other = Find<Dropdown>("OtherEntity");
            x = Find<InputField>("X"); z = Find<InputField>("Z"); status = Find<Text>("Status");
            Bind("Close", () => runner.SetDebugVisible(false));
            Bind("Monitor", () => { monitoring = !monitoring; runner.SetDebugVisible(false); });
            Bind("FontSmall", () => SetFont(-2)); Bind("FontLarge", () => SetFont(2));
            Find<Slider>("Width").onValueChanged.AddListener(width => { Find<RectTransform>("Panel").SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width); ClampPanel(); });
            foreach (string key in categories.Keys)
            { string captured = categories[key]; Bind("Tab" + key, () => { category = captured; ApplySearch(); }); }
            Find<InputField>("Search").onValueChanged.AddListener(_ => ApplySearch());
            entity.onValueChanged.AddListener(_ => ReadSelectedPosition());
            Bind("Place", Place); Bind("Swap", Swap); Bind("Rotate", Rotate);
            Bind("Pick", () => { Picking = !Picking; status.text = Picking ? "点击面板外的棋盘格；Esc 取消。" : "点选已取消。"; });
            Bind("Undo", runner.Undo); Bind("Restart", runner.Restart);
            Bind("View", () => { runner.Cameras?.Toggle(); if (runner.Board) runner.Board.SetTopDown(runner.Cameras.TopDown); });
            Bind("Load", () =>
            {
                if (!int.TryParse(Find<InputField>("LevelIndex").text, out int index)) { status.text = "请输入关卡序号。"; return; }
                if (!runner.CanSelectLevel || index < 1 || index > runner.CampaignLevelCount) { status.text = "当前不能进入该关卡。"; return; }
                runner.SetDebugVisible(false); runner.UI.EnterLevel(index - 1);
            });
            Bind("Live", runner.RequestLiveEdit);
            Bind("EndLive", () => { if (!runner.EndLiveSandbox(out string error)) status.text = error; });
            Bind("AddEnergy", () => RequestCrate(CrateDefinition.Energy, false));
            Bind("AddCargo", () => RequestCrate(CrateDefinition.Cargo, false));
            Bind("RemoveCrate", () => RequestCrate(SelectedId, true));
            Bind("Copy", () => { GUIUtility.systemCopyBuffer = runner.DiagnosticJson(); status.text = "诊断快照已复制。"; });
            Find<Toggle>("AnimationPause").onValueChanged.AddListener(runner.SetDebugAnimationPaused);
            overlay = runner.gameObject.AddComponent<GmOverlay>();
            overlay.Initialize(runner, (RectTransform)transform, Find<LineRenderer>("OverlayStyle").sharedMaterial);
            Find<Toggle>("Grid").onValueChanged.AddListener(v => overlay.ShowGrid = v);
            Find<Toggle>("Ids").onValueChanged.AddListener(v => overlay.ShowIds = v);
            Find<Toggle>("Links").onValueChanged.AddListener(v => overlay.ShowLinks = v);
            var drag = Find<Text>("Title").gameObject.AddComponent<EventTrigger>();
            Find<Text>("Title").raycastTarget = true;
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            entry.callback.AddListener(data =>
            {
                var panel = Find<RectTransform>("Panel"); var pointer = (PointerEventData)data;
                panel.anchoredPosition += pointer.delta / gameObject.GetComponentInParent<Canvas>().scaleFactor;
                var canvas = (RectTransform)panel.parent;
                panel.anchoredPosition = new Vector2(Mathf.Clamp(panel.anchoredPosition.x, 0, Mathf.Max(0, canvas.rect.width - panel.rect.width)),
                    Mathf.Clamp(panel.anchoredPosition.y, -Mathf.Max(0, canvas.rect.height - panel.rect.height), 0));
            });
            drag.triggers.Add(entry);
            runner.Changed += Refresh; Refresh(); ApplySearch(); Present(false);
        }
        private string SelectedId => ids.Count == 0 ? DebugBoardEdit.PlayerId : ids[Mathf.Clamp(entity.value, 0, ids.Count - 1)];
        private bool ReadCell(out Cell cell)
        {
            cell = default;
            if (!int.TryParse(x.text, out int cx) || !int.TryParse(z.text, out int cz)) { status.text = "坐标必须是整数。"; return false; }
            cell = new Cell(cx, cz); return true;
        }
        private void Execute(DebugBoardEdit edit)
        { status.text = runner.TryApplyDebugEdit(edit, out string error) ? "已应用，可撤销。" : error; }
        private void Place() { if (ReadCell(out Cell cell)) Execute(DebugBoardEdit.Place(SelectedId, cell)); }
        private void Rotate() { if (runner.Session != null) Execute(new DebugBoardEdit { Facing = (Direction)Find<Dropdown>("Facing").value, Label = "修改朝向" }); }
        private void Swap()
        {
            if (runner.Session == null || ids.Count == 0) return;
            string a = SelectedId, b = ids[other.value];
            var edit = new DebugBoardEdit { Label = "交换位置" };
            edit.Positions[a] = Position(b); edit.Positions[b] = Position(a); Execute(edit);
        }
        private Cell Position(string id) => id == DebugBoardEdit.PlayerId ? runner.Session.State.Player : runner.Session.State.Crates[id];
        private void RequestCrate(string kindOrId, bool delete)
        {
            if (delete && kindOrId == DebugBoardEdit.PlayerId) { status.text = "请先选择箱子。"; return; }
            if (ReadCell(out Cell cell)) runner.RequestLiveCrate(kindOrId, cell, delete);
        }
        private void ReadSelectedPosition()
        {
            if (runner.Session == null || ids.Count == 0) return;
            var cell = Position(SelectedId); x.SetTextWithoutNotify(cell.x.ToString()); z.SetTextWithoutNotify(cell.z.ToString());
            overlay.SelectedId = SelectedId;
        }
        public void CancelPicking() { Picking = false; status.text = "点选已取消。"; }
        public void ReadPointer()
        {
            if (!Picking || !runner.DebugVisible || runner.Cameras == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Vector2 screen = Mouse.current.position.ReadValue();
            var hits = new List<RaycastResult>(); EventSystem.current?.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
            if (hits.Count > 0) return;
            Ray ray = runner.Cameras.Output.ScreenPointToRay(screen);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            Vector3 point = ray.GetPoint(distance);
            x.text = Mathf.FloorToInt(point.x + .5f).ToString(); z.text = Mathf.FloorToInt(point.z + .5f).ToString();
            Picking = false; status.text = "已选择格子，点击“移动到此格”应用。";
        }
        public void Present(bool visible)
        {
            Find<RectTransform>("Panel").gameObject.SetActive(visible);
            Find<Text>("MonitorText").gameObject.SetActive(monitoring && !visible);
            if (!visible) Picking = false;
            if (visible) { ClampPanel(); Refresh(); }
            transform.SetAsLastSibling();
        }
        private void ClampPanel()
        {
            var panel = Find<RectTransform>("Panel"); var bounds = ((RectTransform)transform).rect;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(944, bounds.height - 24));
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(panel.rect.width, bounds.width - 24));
            panel.anchoredPosition = new Vector2(Mathf.Clamp(panel.anchoredPosition.x, 0, Mathf.Max(0, bounds.width - panel.rect.width)),
                Mathf.Clamp(panel.anchoredPosition.y, -Mathf.Max(0, bounds.height - panel.rect.height), 0));
        }
        private void ApplySearch()
        {
            string search = Find<InputField>("Search").text.Trim();
            foreach (var pair in categories)
            {
                var content = Find<RectTransform>(pair.Key);
                bool match = pair.Value.Contains(search) || content.GetComponentsInChildren<Text>(true).Any(t => t.text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                content.gameObject.SetActive(search.Length > 0 ? match : pair.Value == category);
            }
        }
        private void SetFont(int delta)
        {
            fontSize = Mathf.Clamp(fontSize + delta, 18, 28);
            foreach (var label in gameObject.GetComponentsInChildren<Text>(true)) label.fontSize = fontSize;
        }
        public void Refresh()
        {
            if (isDestroyed || status == null) return;
            bool game = runner.Session != null;
            string signature = runner.SessionId + (game ? string.Join("|", runner.Definition.crates.Select(c => c.id + c.kind)) : "");
            if (boundSession != signature)
            {
                boundSession = signature; ids.Clear(); ids.Add(DebugBoardEdit.PlayerId);
                var labels = new List<string> { "玩家" };
                if (game) foreach (var c in runner.Definition.crates) { ids.Add(c.id); labels.Add(c.id + (c.IsEnergy ? " · 能源箱" : " · 普通箱")); }
                entity.ClearOptions(); other.ClearOptions(); entity.AddOptions(labels); other.AddOptions(labels);
                entity.SetValueWithoutNotify(0); other.SetValueWithoutNotify(Mathf.Min(1, ids.Count - 1));
                if (game) ReadSelectedPosition();
            }
            foreach (string name in new[] { "Place", "Swap", "Rotate", "Pick", "Restart", "View" }) Find<Button>(name).interactable = game && !runner.NavigationLocked && !runner.LiveEditing;
            Find<Button>("Undo").interactable = game && runner.Session.UndoCount > 0 && !runner.LiveEditing;
            Find<Button>("Load").interactable = runner.CanSelectLevel;
            Find<Button>("Live").interactable = game && LevelRunner.HasLiveEditor;
            Find<Button>("EndLive").gameObject.SetActive(runner.IsLiveSandbox);
            foreach (string name in new[] { "AddEnergy", "AddCargo", "RemoveCrate" }) Find<Button>(name).gameObject.SetActive(LevelRunner.HasLiveEditor && game);
            Find<Text>("AdditionHint").gameObject.SetActive(LevelRunner.HasLiveEditor && game);
            Find<Text>("LevelInfo").text = game ? runner.LevelLabel + "\nID: " + runner.Definition.id + "\n" +
                (runner.IsLiveSandbox ? "现场试玩" : runner.IsPlaytest ? "作者试玩" : "正式关卡") + " · " + LevelJson.Hash(runner.Definition).Substring(0, 12) +
                "\n能源箱 " + runner.Definition.crates.Count(c => c.IsEnergy) + " · 普通箱 " + runner.Definition.crates.Count(c => !c.IsEnergy) : "当前没有关卡，可输入序号进入。";
            Find<Text>("BoardInfo").text = !game ? "请先进入关卡。" : "逻辑玩家 " + runner.Session.State.Player +
                " · 朝向 " + runner.Session.State.Facing + "\n撤销 " + runner.Session.UndoCount + " · 动画 " + (runner.Presenter.Busy ? "播放中" : "稳定") +
                "\n参考解法 " + (runner.Session.ReferenceReplayValid ? "有效" : "调试起点/GM 修改，不可保存");
            if (game)
            {
                var power = runner.Session.Rules.Power(runner.Session.State);
                var lines = new List<string>();
                foreach (var socket in runner.Definition.sockets)
                {
                    string id = runner.Session.State.Crates.FirstOrDefault(c => c.Value == socket.Cell).Key;
                    var crate = runner.Definition.crates.FirstOrDefault(c => c.id == id);
                    string occupant = crate != null ? id + (crate.IsEnergy ? " 能源箱" : " 普通箱") : runner.Session.State.Player == socket.Cell ? "玩家" : "空";
                    lines.Add(socket.id + (socket.isGoal ? " [目标]" : " [辅助]") + "\n占据：" + occupant + " · " + (power.Sockets[socket.id] ? "已供电" : "未供电"));
                }
                foreach (var gate in runner.Definition.gates)
                    lines.Add(gate.id + " · " + gate.powerMode + "(" + string.Join(", ", gate.sourceSocketIds) + ")\n" +
                        (power.PoweredGates[gate.id] ? "通电打开" : power.OpenGates[gate.id] ? "未通电 · 占据保护打开" : "未通电 · 关闭"));
                Find<Text>("PowerInfo").text = string.Join("\n\n", lines);
            }
            else Find<Text>("PowerInfo").text = "当前没有机关。";
            Find<Text>("LogInfo").text = "输入：" + (runner.NavigationLocked ? "转场锁定" : runner.LiveEditing ? "现场编辑" : runner.DebugVisible ? "GM 占用" : runner.Paused ? "暂停" : runner.DebugAnimationPaused ? "动画暂停" : "可游玩") + "\n" + string.Join("\n", runner.DebugLog.Reverse());
            Find<Text>("MonitorText").text = game ? $"F1 GM · {runner.Definition.id} · 玩家 {runner.Session.State.Player} · 供电 {runner.PoweredGoalCount}/{runner.GoalCount} · {(runner.Presenter.Busy ? "动作中" : "稳定")}" : "F1 GM · 主菜单";
            Find<Toggle>("AnimationPause").SetIsOnWithoutNotify(runner.DebugAnimationPaused);
            if (runner.DebugVisible) transform.SetAsLastSibling();
        }
        public override void Update()
        {
            base.Update();
            if ((runner.DebugVisible || monitoring) && Time.unscaledTime >= refreshAt)
            { refreshAt = Time.unscaledTime + .2f; if (runner.DebugVisible) ClampPanel(); Refresh(); }
        }
        public override void OnDestroy()
        {
            runner.Changed -= Refresh;
            if (overlay) UnityEngine.Object.Destroy(overlay);
        }
    }
}
#endif
