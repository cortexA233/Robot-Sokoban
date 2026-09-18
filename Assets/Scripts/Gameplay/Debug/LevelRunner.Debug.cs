using System;
using System.Collections.Generic;
using System.Linq;
using KToolkit;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sokoban
{
    public sealed partial class LevelRunner
    {
        public string SessionId { get; private set; } = Guid.NewGuid().ToString("N");
        public int Revision { get; private set; }
        public bool LiveEditing { get; private set; }
        public bool IsLiveSandbox => liveOrigin != null;
        public bool DebugVisible { get; private set; }
        public bool DebugAnimationPaused { get; private set; }
        public bool DebugInputCaptured => DebugVisible || LiveEditing;
        public static event Action<LevelRunner> LiveEditRequested;
        public static event Action<LevelRunner, string, Cell, bool> LiveCrateRequested;
        public static bool HasLiveEditor => LiveEditRequested != null;
        public IReadOnlyCollection<string> DebugLog => debugLog;
        private readonly Queue<string> debugLog = new Queue<string>();
        private GameObject presentationRoot;
        private LiveOrigin liveOrigin;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Sokoban.UI.GmPage Gm { get; private set; }
#endif
        public static bool DebugEnabled
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }
        private sealed class LiveOrigin
        {
            public LevelDefinition Definition;
            public GameSession Session;
            public CameraRig.ViewSettings Camera;
            public bool IsPlaytest, Paused, DebugPaused;
            public int CampaignIndex;
            public string Message;
        }

        // All potentially failing construction happens while the previous board is intact.
        private void InstallSession(LevelDefinition definition, GameSession session, CameraRig.ViewSettings view = null)
        {
            var candidate = new GameObject("Level presentation"); candidate.SetActive(false);
            candidate.transform.SetParent(transform, false);
            BoardView board = null; CameraRig cameras = null;
            LevelDefinition copy = definition.Copy();
            try
            {
                board = new GameObject("Board").AddComponent<BoardView>(); board.transform.SetParent(candidate.transform, false);
                board.Build(copy); board.Restore(session);
                cameras = new GameObject("Camera rig").AddComponent<CameraRig>(); cameras.transform.SetParent(candidate.transform, false);
                cameras.Initialize(copy, board.Robot.transform, board); UI?.Preferences.Apply(cameras);
                board.Circuits.BindCamera(cameras);
                cameras.Restore(view); board.SetTopDown(cameras.TopDown);
            }
            catch { Destroy(candidate); throw; }
            // No callbacks or yield points in this commit section.
            ClearPresentation();
            Definition = copy; Session = session; Board = board; Cameras = cameras; presentationRoot = candidate;
            Presenter = GetComponent<CommandPresenter>() ?? gameObject.AddComponent<CommandPresenter>();
            Presenter.Initialize(Board, Audio); candidate.SetActive(true); input.Clear(); idleTime = 0;
            Completed = Session.State.Completed; Error = null; ApplyPresentationPause();
        }
        private void ApplyPresentationPause() => Presenter?.SetPaused(Paused || NavigationLocked || LiveEditing || DebugAnimationPaused);
        private void InitializeDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Gm = KUIManager.instance.CreateUI<Sokoban.UI.GmPage>(this);
#endif
        }
        private void DisposeDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Gm != null && !Gm.isDestroyed) Gm.DestroySelf();
#endif
            LiveEditing = DebugVisible = false;
        }
        private void ReadDebugInput(ref bool consumed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame && !NavigationLocked)
            { SetDebugVisible(!DebugVisible); consumed = true; }
            if (DebugVisible && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (Gm != null && Gm.Picking) Gm.CancelPicking(); else SetDebugVisible(false);
                consumed = true;
            }
            Gm?.ReadPointer();
#endif
            consumed |= DebugInputCaptured;
        }
        public void SetDebugVisible(bool visible)
        {
            if (!DebugEnabled || NavigationLocked) return;
            DebugVisible = visible; input.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Gm?.Present(visible);
#endif
            NotifyChanged();
        }
        public void SetDebugAnimationPaused(bool paused)
        { DebugAnimationPaused = DebugEnabled && paused; ApplyPresentationPause(); }
        public void RecordDebug(string message)
        {
            if (!DebugEnabled) return;
            if (debugLog.Count == 60) debugLog.Dequeue();
            debugLog.Enqueue(message);
        }
        public bool TryApplyDebugEdit(DebugBoardEdit edit, out string error)
        {
            error = null;
            if (!DebugEnabled || Session == null || NavigationLocked || LiveEditing || LevelSelectionOpen)
            { error = "当前会话不允许 GM 移位。"; return false; }
            if (!Session.TryApplyDebugEdit(edit, out error)) { RecordDebug(error); return false; }
            Presenter.Cancel(); input.Clear(); Board.Restore(Session); Cameras.Snap(); Revision++;
            Completed = Session.State.Completed; idleTime = 0;
            Message = "GM 调试局面 · " + (edit.Label ?? "移位"); RecordDebug(Message); NotifyChanged(); return true;
        }
        public void RequestLiveEdit()
        {
            if (HasLiveEditor && Session != null && !NavigationLocked && !LevelSelectionOpen && UI?.SettingsOpen != true)
            { SetDebugVisible(false); LiveEditRequested?.Invoke(this); }
        }
        public void RequestLiveCrate(string kindOrId, Cell cell, bool delete)
        {
            if (HasLiveEditor && Session != null && !NavigationLocked && !LiveEditing && !LevelSelectionOpen)
            { SetDebugVisible(false); LiveCrateRequested?.Invoke(this, kindOrId, cell, delete); }
        }
        public void BeginLiveEditing()
        {
            if (!DebugEnabled || Session == null || NavigationLocked || LevelSelectionOpen || UI?.SettingsOpen == true)
                throw new InvalidOperationException("当前会话无法捕获现场。");
            if (liveOrigin == null) liveOrigin = new LiveOrigin { Definition = Definition.Copy(), Session = Session.Copy(),
                Camera = Cameras.Capture(), IsPlaytest = IsPlaytest, Paused = Paused, DebugPaused = DebugAnimationPaused, CampaignIndex = CampaignIndex, Message = Message };
            Presenter.Cancel(); Board.Restore(Session); Cameras.Snap(); input.Clear(); Completed = Session.State.Completed;
            LiveEditing = true; DebugVisible = false; ApplyPresentationPause(); NotifyChanged();
        }
        public void SuspendLiveEditing()
        { LiveEditing = false; input.Clear(); ApplyPresentationPause(); NotifyChanged(); }
        public bool TryApplyLiveDraft(LevelDefinition definition, BoardSnapshot snapshot, string expectedSession, int expectedRevision, out string error)
        {
            error = null;
            if (!DebugEnabled || !LiveEditing || liveOrigin == null || NavigationLocked || expectedSession != SessionId || expectedRevision != Revision)
            { error = "现场来源已变化，请重新捕获；原草稿保留。"; return false; }
            try
            {
                var next = GameSession.FromCheckpoint(definition, snapshot);
                var visibleDefinition = snapshot.ApplyTo(definition);
                InstallSession(visibleDefinition, next, Cameras.Capture());
                IsPlaytest = true; CampaignIndex = -1; LevelSelectionOpen = false;
                LiveEditing = false; Paused = false; Revision++; DebugAnimationPaused = false;
                Message = "现场试玩 · 新起点 · Z 不跨应用边界"; RecordDebug(Message);
                ApplyPresentationPause(); NotifyChanged(); return true;
            }
            catch (Exception exception) { error = exception.Message; RecordDebug("应用失败：" + error); return false; }
        }
        public bool EndLiveSandbox(out string error)
        {
            error = null;
            if (liveOrigin == null) { SuspendLiveEditing(); return true; }
            if (NavigationLocked) { error = "转场中不能结束现场试玩。"; return false; }
            try
            {
                var origin = liveOrigin;
                InstallSession(origin.Definition, origin.Session.Copy(), origin.Camera);
                IsPlaytest = origin.IsPlaytest; CampaignIndex = origin.CampaignIndex; Paused = origin.Paused;
                Message = origin.Message; liveOrigin = null; LiveEditing = false; DebugAnimationPaused = origin.DebugPaused; Revision++;
                ApplyPresentationPause(); NotifyChanged(); return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
        public string DiagnosticJson()
        {
            if (Session == null) return "{\"message\":\"当前没有关卡\"}";
            return JsonUtility.ToJson(new Diagnostic { sessionId = SessionId, revision = Revision,
                mode = IsLiveSandbox ? "LiveSandbox" : IsPlaytest ? "AuthorPlaytest" : "Campaign",
                levelJson = LevelJson.Write(Definition), contentHash = LevelJson.Hash(Definition),
                state = BoardSnapshot.Capture(Definition, Session.State), commands = Session.Commands,
                moves = Session.State.Moves, pushes = Session.State.Pushes, referenceReplayValid = Session.ReferenceReplayValid,
                presenterBusy = Presenter.Busy, log = debugLog.ToArray() }, true);
        }
        [Serializable] private sealed class Diagnostic
        {
            public string sessionId, mode, levelJson, contentHash, commands;
            public int revision, moves, pushes;
            public BoardSnapshot state;
            public bool referenceReplayValid, presenterBusy;
            public string[] log;
        }
    }
}
