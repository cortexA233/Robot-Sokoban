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
        private string category = "局面";
        private GameSession boundSession;
        public string SelectedId { get; private set; }
        private readonly Dictionary<string, string> categories = new Dictionary<string, string>
        { { "LevelContent", "关卡" }, { "BoardContent", "局面" } };
        private Text status;
        private InputField x, z;
        private GmOverlay overlay;
        private float refreshAt;
        private int fontSize = 22;
        private T Find<T>(string name) where T : Component => gameObject.GetComponentsInChildren<T>(true).First(c => c.name == name);
        private void Bind(string name, UnityEngine.Events.UnityAction action) => Find<Button>(name).onClick.AddListener(action);
        public override void InitParams(params object[] args) => runner = (LevelRunner)args[0];
        public override void OnStart()
        {
            x = Find<InputField>("X"); z = Find<InputField>("Z"); status = Find<Text>("Status");
            Bind("Close", () => runner.SetDebugVisible(false));
            Bind("FontSmall", () => SetFont(-2)); Bind("FontLarge", () => SetFont(2));
            Find<Slider>("Width").onValueChanged.AddListener(width => { Find<RectTransform>("Panel").SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width); ClampPanel(); });
            foreach (string key in categories.Keys)
            { string captured = categories[key]; Bind("Tab" + key, () => { category = captured; ApplySearch(); }); }
            Find<InputField>("Search").onValueChanged.AddListener(_ => ApplySearch());
            Bind("Place", Place);
            Bind("Undo", runner.Undo); Bind("Restart", runner.Restart);
            Bind("View", () => { runner.Cameras?.Toggle(); });
            Bind("Load", () =>
            {
                if (!int.TryParse(Find<InputField>("LevelIndex").text, out int index)) { status.text = "请输入关卡序号。"; return; }
                if (!runner.CanSelectLevel || index < 1 || index > runner.CampaignLevelCount) { status.text = "当前不能进入该关卡。"; return; }
                runner.SetDebugVisible(false); runner.UI.EnterLevel(index - 1);
            });
            Bind("Live", runner.RequestLiveEdit);
            Bind("EndLive", () => { if (!runner.EndLiveSandbox(out string error)) status.text = error; });
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
        private bool ReadCell(out Cell cell)
        {
            cell = default;
            if (!int.TryParse(x.text, out int cx) || !int.TryParse(z.text, out int cz)) { status.text = "坐标必须是整数。"; return false; }
            cell = new Cell(cx, cz); return true;
        }
        private bool CanSelect => runner.DebugVisible && runner.Session != null && !runner.NavigationLocked && !runner.LiveEditing && !runner.LevelSelectionOpen;
        private bool TrySelectedPosition(out Cell cell)
        {
            cell = default;
            if (runner.Session == null || boundSession != runner.Session || string.IsNullOrEmpty(SelectedId)) return false;
            if (SelectedId == DebugBoardEdit.PlayerId) { cell = runner.Session.State.Player; return true; }
            return runner.Session.State.Crates.TryGetValue(SelectedId, out cell);
        }
        public void ClearSelection()
        {
            SelectedId = null;
            if (overlay) overlay.SelectedId = null;
        }
        // Board clicks select a source only. They never change the destination or history.
        public void SelectCell(Cell cell)
        {
            if (!CanSelect) return;
            boundSession = runner.Session;
            SelectedId = runner.Session.State.Crates.FirstOrDefault(c => c.Value == cell).Key;
            if (SelectedId == null && runner.Session.State.Player == cell) SelectedId = DebugBoardEdit.PlayerId;
            status.text = SelectedId == null ? "此格没有可移动对象。" : "已选中对象；填写目标 X/Z 后移动。";
            Refresh();
        }
        private void Place()
        {
            if (!CanSelect || !TrySelectedPosition(out _))
            { ClearSelection(); Refresh(); status.text = "请先点击棋盘上的玩家或箱子。"; return; }
            if (ReadCell(out Cell cell))
                status.text = runner.TryApplyDebugEdit(DebugBoardEdit.Place(SelectedId, cell), out string error) ? "已应用，可撤销。" : error;
        }
        public void ReadPointer()
        {
            if (!CanSelect || !Find<RectTransform>("BoardContent").gameObject.activeInHierarchy || runner.Cameras == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Vector2 screen = Mouse.current.position.ReadValue();
            var hits = new List<RaycastResult>(); EventSystem.current?.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
            if (hits.Count > 0) return;
            Ray ray = runner.Cameras.Output.ScreenPointToRay(screen);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            Vector3 point = ray.GetPoint(distance);
            SelectCell(new Cell(Mathf.FloorToInt(point.x + .5f), Mathf.FloorToInt(point.z + .5f)));
        }
        public void Present(bool visible)
        {
            Find<RectTransform>("Panel").gameObject.SetActive(visible);
            if (visible) Refresh();
            transform.SetAsLastSibling();
        }
        private void ClampPanel()
        {
            var panel = Find<RectTransform>("Panel"); var bounds = ((RectTransform)transform).rect;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(panel.rect.width, bounds.width - 24));
            Canvas.ForceUpdateCanvases();
            // Size the body to the remaining visible controls; overflow still scrolls.
            var stack = Find<RectTransform>("Stack");
            // Propagate the new width before measuring wrapped text height.
            LayoutRebuilder.ForceRebuildLayoutImmediate(stack);
            Find<LayoutElement>("Scroll").preferredHeight = Mathf.Max(100, LayoutUtility.GetPreferredHeight(Find<RectTransform>("Content")));
            stack.GetComponent<VerticalLayoutGroup>().CalculateLayoutInputVertical();
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Min(bounds.height - 24, LayoutUtility.GetPreferredHeight(stack) + 40));
            LayoutRebuilder.ForceRebuildLayoutImmediate(stack);
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
            ClampPanel();
        }
        private void SetFont(int delta)
        {
            fontSize = Mathf.Clamp(fontSize + delta, 18, 28);
            foreach (var label in gameObject.GetComponentsInChildren<Text>(true)) label.fontSize = fontSize;
            ClampPanel();
        }
        public void Refresh()
        {
            if (isDestroyed || status == null) return;
            bool game = runner.Session != null;
            if (boundSession != runner.Session) { boundSession = runner.Session; ClearSelection(); }
            bool selected = TrySelectedPosition(out Cell position);
            if (!selected) ClearSelection();
            if (overlay) overlay.SelectedId = SelectedId;
            Find<Text>("Selection").text = !selected ? "点击棋盘上的玩家或箱子以选择。" :
                (SelectedId == DebugBoardEdit.PlayerId ? "玩家" : SelectedId + (runner.Definition.crates.First(c => c.id == SelectedId).IsEnergy ? " · 能源箱" : " · 普通箱")) + "  " + position;
            Find<Button>("Place").interactable = selected && CanSelect;
            foreach (string name in new[] { "Restart", "View" }) Find<Button>(name).interactable = game && !runner.NavigationLocked && !runner.LiveEditing;
            Find<Button>("Undo").interactable = game && runner.Session.UndoCount > 0 && !runner.NavigationLocked && !runner.LiveEditing;
            Find<Button>("Load").interactable = runner.CanSelectLevel;
            Find<Button>("Live").gameObject.SetActive(LevelRunner.HasLiveEditor);
            Find<Text>("LiveHint").gameObject.SetActive(LevelRunner.HasLiveEditor);
            Find<Button>("Live").interactable = game && !runner.NavigationLocked && !runner.LevelSelectionOpen;
            Find<Button>("EndLive").gameObject.SetActive(runner.IsLiveSandbox);
            Find<Text>("LevelInfo").text = game ? runner.LevelLabel + "\nID: " + runner.Definition.id + "\n" +
                (runner.IsLiveSandbox ? "现场试玩" : runner.IsPlaytest ? "作者试玩" : "正式关卡") + " · " + LevelJson.Hash(runner.Definition).Substring(0, 12) +
                "\n能源箱 " + runner.Definition.crates.Count(c => c.IsEnergy) + " · 普通箱 " + runner.Definition.crates.Count(c => !c.IsEnergy) : "当前没有关卡，可输入序号进入。";
            if (runner.DebugVisible) { ClampPanel(); transform.SetAsLastSibling(); }
        }
        public override void Update()
        {
            base.Update();
            if (runner.DebugVisible && Time.unscaledTime >= refreshAt)
            { refreshAt = Time.unscaledTime + .2f; Refresh(); }
        }
        public override void OnDestroy()
        {
            runner.Changed -= Refresh;
            if (overlay) UnityEngine.Object.Destroy(overlay);
        }
    }
}
#endif
