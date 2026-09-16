using System;
using System.Linq;
using DG.Tweening;
using KToolkit;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sokoban
{
    // Owns one active session; catalog navigation never changes editor playtest data.
    public sealed class LevelRunner : MonoBehaviour
    {
        public static LevelDefinition PlaytestDefinition;
        public static event Action<LevelDefinition, GameSession> SaveReferenceRequested;
        public string initialLevel = "configs/levels/L01";
        public GameSession Session { get; private set; }
        public LevelDefinition Definition { get; private set; }
        public BoardView Board { get; private set; }
        public CommandPresenter Presenter { get; private set; }
        public CameraRig Cameras { get; private set; }
        public bool Paused { get; private set; }
        public bool Completed { get; private set; }
        public bool LevelSelectionOpen { get; private set; }
        public bool IsPlaytest => isPlaytest;
        public int CampaignLevelCount => campaign.Length;
        public int CampaignIndex { get; private set; } = -1;
        public bool CanSelectLevel => !isPlaytest && campaign.Length > 0;
        public bool CanGoNext => !LevelSelectionOpen && !isPlaytest && Completed && Presenter != null && !Presenter.Busy && CampaignIndex >= 0 && CampaignIndex + 1 < campaign.Length;
        public bool IsFinalCampaignLevel => !isPlaytest && CampaignIndex >= 0 && CampaignIndex == campaign.Length - 1;
        public string CompletionHeading => IsFinalCampaignLevel ? "空间站已重启！" : Definition.completionText;
        private readonly GridInput input = new GridInput();
        private string message = "";
        private string error;
        private float idleTime;
        private float rejectedAt = -1;
        private bool isPlaytest;
        private LevelDefinition[] campaign = Array.Empty<LevelDefinition>();
        private bool pausedBeforeSelection;
        private Vector2 selectionScroll;

        private void Start()
        {
            try
            {
                KFrameworkManager.instance.InitKFramework();
                DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
                isPlaytest = PlaytestDefinition != null;
                if (isPlaytest) LoadLevel(PlaytestDefinition);
                else
                {
                    var catalog = Resources.Load<CampaignCatalog>("configs/CampaignCatalog");
                    if (!catalog) throw new InvalidOperationException("CampaignCatalog 缺失，请创建正式关卡目录。");
                    campaign = catalog.ReadLevels();
                    var initial = Resources.Load<TextAsset>(initialLevel);
                    string initialId = initial ? LevelJson.Read(initial.text).id : null;
                    int index = Array.FindIndex(campaign, level => level.id == initialId);
                    SelectLevel(index >= 0 ? index : 0);
                }
            }
            catch (Exception exception) { error = exception.Message; Debug.LogException(exception); }
        }

        public void LoadLevel(LevelDefinition level)
        {
            // Validate before tearing down a working level.
            var session = new GameSession(level);
            Presenter?.Cancel();
            if (Board) { Board.gameObject.SetActive(false); Destroy(Board.gameObject); }
            if (Cameras) { Cameras.gameObject.SetActive(false); Destroy(Cameras.gameObject); }
            Definition = level.Copy(); Session = session;
            CampaignIndex = isPlaytest ? -1 : Array.FindIndex(campaign, entry => entry.id == level.id && LevelJson.Hash(entry) == LevelJson.Hash(level));
            Board = new GameObject("Board").AddComponent<BoardView>(); Board.transform.SetParent(transform);
            Board.Build(Definition); Board.Restore(Session);
            Presenter = GetComponent<CommandPresenter>() ?? gameObject.AddComponent<CommandPresenter>(); Presenter.Initialize(Board);
            Cameras = new GameObject("Camera rig").AddComponent<CameraRig>(); Cameras.transform.SetParent(transform);
            Cameras.Initialize(Definition, Board.Robot.transform);
            Completed = Session.State.Completed; Paused = false; LevelSelectionOpen = false; idleTime = 0; input.Clear();
            message = level.briefing; UpdateCursor();
        }
        public bool SelectLevel(int index)
        {
            if (isPlaytest || index < 0 || index >= campaign.Length) return false;
            LoadLevel(campaign[index]);
            return true;
        }
        public bool NextLevel() => CanGoNext && SelectLevel(CampaignIndex + 1);
        public bool OpenLevelSelect()
        {
            if (!CanSelectLevel || LevelSelectionOpen) return false;
            pausedBeforeSelection = Paused;
            LevelSelectionOpen = true; selectionScroll = Vector2.zero;
            SetPaused(true);
            return true;
        }
        public void CloseLevelSelect()
        {
            if (!LevelSelectionOpen) return;
            LevelSelectionOpen = false;
            SetPaused(pausedBeforeSelection);
        }
        private void Update()
        {
            if (Session == null || error != null) return;
            var keyboard = Keyboard.current;
            if (LevelSelectionOpen)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CloseLevelSelect();
                return;
            }
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) SetPaused(!Paused);
                if (keyboard.vKey.wasPressedThisFrame) ToggleCamera();
                if (keyboard.zKey.wasPressedThisFrame && !Paused) Undo();
                if (keyboard.rKey.wasPressedThisFrame && !Paused) Restart();
            }
            Cameras.ReadMouse(!Paused && !Completed);
            if (Paused || Completed) return;
            if (!Presenter.Busy) { idleTime += Time.deltaTime; Board.Robot.Sample(0, idleTime % Board.Robot.idle.length); }
            var direction = input.Poll(Presenter.Busy, Cameras.Forward);
            if (direction.HasValue) TryMove(direction.Value);
        }
        public bool TryMove(Direction direction)
        {
            if (LevelSelectionOpen || Paused || Presenter.Busy || Completed) return false;
            var result = Session.Move(direction);
            if (!result.Accepted)
            {
                Board.Robot.transform.rotation = Quaternion.Euler(0, (int)direction * 90, 0);
                if (Time.unscaledTime - rejectedAt >= .5f) { rejectedAt = Time.unscaledTime; message = "前方受阻。只能推动一个箱子；按 Z 撤销。"; }
                return false;
            }
            Presenter.Present(result, () =>
            {
                Board.Restore(Session); idleTime = 0;
                Completed = Session.State.Completed;
                if (Completed) message = Definition.completionText;
                UpdateCursor();
            });
            return true;
        }
        public void Undo()
        {
            Presenter.Cancel(); input.Clear(); Session.Undo(); Board.Restore(Session);
            Completed = Session.State.Completed; message = Definition.briefing; idleTime = 0; UpdateCursor();
        }
        public void Restart()
        {
            Presenter.Cancel(); input.Clear(); Session.Restart(); Board.Restore(Session);
            Completed = Session.State.Completed; message = Definition.briefing; idleTime = 0; UpdateCursor();
        }
        public void ToggleCamera() { Cameras.Toggle(); Board.SetTopDown(Cameras.TopDown); input.Clear(); }
        public void SetPaused(bool value)
        { Paused = value; Presenter.SetPaused(value); input.Clear(); UpdateCursor(); }
        private void UpdateCursor()
        { Cursor.lockState = Paused || Completed ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = Paused || Completed; }
        public void SaveReference()
        {
            if (!isPlaytest || !Completed || Presenter.Busy) return;
            try { SaveReferenceRequested?.Invoke(Definition.Copy(), Session); message = "参考解法已记录；退出 Play Mode 后返回编辑器。"; }
            catch (Exception exception) { message = exception.Message; }
        }
        private void OnGUI()
        {
            if (error != null) { GUI.Box(new Rect(20, 20, Screen.width - 40, 100), error); return; }
            if (Session == null) return;
            if (LevelSelectionOpen) { DrawLevelSelection(); return; }
            GUILayout.BeginArea(new Rect(18, 18, 480, 170), GUI.skin.box);
            GUILayout.Label((isPlaytest ? "关卡编辑器 · 试玩" : "空间站重启 · 开发版") + "   /   " + Definition.title);
            var power = Session.Rules.Power(Session.State);
            int total = Definition.sockets.Count(s => s.isGoal);
            int lit = Definition.sockets.Count(s => s.isGoal && power.Sockets[s.id]);
            GUILayout.Label($"目标 {lit}/{total}     移动 {Session.State.Moves}     推动 {Session.State.Pushes}");
            GUILayout.Label(message);
            GUILayout.Label("WASD 镜头方向 / ↑→↓← 世界方向 · V 双视角 · Z 撤销 · R 重开 · Esc 暂停");
            GUILayout.Label("N ↑（俯视固定北方）   " + (Cameras.TopDown ? "俯视" : "跟随 / 鼠标环绕 / 滚轮缩放"));
            GUILayout.EndArea();
            if (Paused || Completed)
            {
                GUILayout.BeginArea(CenteredPanel(400, 340), GUI.skin.box);
                GUILayout.Space(12);
                GUILayout.Label(Completed ? CompletionHeading : "已暂停");
                if (Completed)
                {
                    GUILayout.Label($"{Definition.title} · {Session.State.Moves} 步 / {Session.State.Pushes} 次推动");
                    if (IsFinalCampaignLevel) GUILayout.Label("主核心已恢复供电，可以返回选关重新挑战。");
                    GUILayout.Space(10);
                    if (CanGoNext && GUILayout.Button("下一关  →  " + campaign[CampaignIndex + 1].title, GUILayout.Height(34))) { NextLevel(); GUIUtility.ExitGUI(); }
                    if (isPlaytest && GUILayout.Button("保存为参考解法", GUILayout.Height(30))) SaveReference();
                }
                if (CanSelectLevel && GUILayout.Button(Completed ? "返回选关" : "选择关卡", GUILayout.Height(30))) { OpenLevelSelect(); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("重开本关", GUILayout.Height(30))) { SetPaused(false); Restart(); GUIUtility.ExitGUI(); }
                if (Completed && GUILayout.Button("撤销最后一步", GUILayout.Height(30))) { Undo(); GUIUtility.ExitGUI(); }
                if (Paused && !Completed && GUILayout.Button("继续游戏", GUILayout.Height(30))) { SetPaused(false); GUIUtility.ExitGUI(); }
                if (isPlaytest) GUILayout.Label("点击 Unity 顶部 Play 按钮退出，返回原设计。");
                GUILayout.EndArea();
            }
        }
        private void DrawLevelSelection()
        {
            GUILayout.BeginArea(CenteredPanel(460, 420), GUI.skin.box);
            GUILayout.Space(12); GUILayout.Label("选择关卡");
            GUILayout.Label("选择后从该关初始局面开始。实验关只在编辑器中提供。");
            GUILayout.Space(12);
            selectionScroll = GUILayout.BeginScrollView(selectionScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < campaign.Length; i++)
                if (GUILayout.Button($"{i + 1:00}  {campaign[i].title}" + (i == CampaignIndex ? "  · 当前关卡" : ""), GUILayout.Height(42)))
                { SelectLevel(i); GUIUtility.ExitGUI(); }
            GUILayout.EndScrollView();
            if (GUILayout.Button(Completed ? "返回结算" : "返回游戏", GUILayout.Height(32))) { CloseLevelSelect(); GUIUtility.ExitGUI(); }
            GUILayout.Space(10); GUILayout.EndArea();
        }
        private static Rect CenteredPanel(float desiredWidth, float desiredHeight)
        {
            float width = Mathf.Min(desiredWidth, Screen.width - 24), height = Mathf.Min(desiredHeight, Screen.height - 24);
            return new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
        }
        private void OnDestroy() { Presenter?.Cancel(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
