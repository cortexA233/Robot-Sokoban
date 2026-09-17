using System;
using System.Linq;
using DG.Tweening;
using KToolkit;
using Sokoban.Domain;
using Sokoban.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sokoban
{
    // Owns the session. UGUI pages consume this API without owning puzzle rules.
    public sealed partial class LevelRunner : MonoBehaviour
    {
        public static LevelDefinition PlaytestDefinition;
        public static event Action<LevelDefinition, GameSession> SaveReferenceRequested;
        public event Action Changed;
        public string initialLevel = "configs/levels/L04";
        public GameSession Session { get; private set; }
        public LevelDefinition Definition { get; private set; }
        public BoardView Board { get; private set; }
        public CommandPresenter Presenter { get; private set; }
        public CameraRig Cameras { get; private set; }
        public StationUIController UI { get; private set; }
        public bool Paused { get; private set; }
        public bool Completed { get; private set; }
        public bool LevelSelectionOpen { get; private set; }
        public bool NavigationLocked { get; private set; }
        public bool IsPlaytest { get; private set; }
        public int CampaignLevelCount => campaign.Length;
        public int CampaignIndex { get; private set; } = -1;
        public int InitialCampaignIndex { get; private set; }
        public string Message { get; private set; } = "";
        public string Error { get; private set; }
        public bool CanSelectLevel => !IsPlaytest && !IsLiveSandbox && !LiveEditing && campaign.Length > 0 && !NavigationLocked;
        public bool CanGoNext => !NavigationLocked && !LevelSelectionOpen && !IsPlaytest && !IsLiveSandbox && Completed && Presenter != null && !Presenter.Busy && CampaignIndex >= 0 && CampaignIndex + 1 < campaign.Length;
        public bool IsFinalCampaignLevel => !IsPlaytest && CampaignIndex >= 0 && CampaignIndex == campaign.Length - 1;
        public string CompletionHeading => IsFinalCampaignLevel ? "空间站已重启！" : Definition?.completionText;
        public int GoalCount => Definition?.sockets.Count(s => s.isGoal) ?? 0;
        public int PoweredGoalCount
        {
            get
            {
                if (Session == null) return 0;
                var power = Session.Rules.Power(Session.State);
                return Definition.sockets.Count(s => s.isGoal && power.Sockets[s.id]);
            }
        }
        private readonly GridInput input = new GridInput();
        private LevelDefinition[] campaign = Array.Empty<LevelDefinition>();
        private float idleTime, rejectedAt = -1;
        private bool pausedBeforeSelection;

        private void Start()
        {
            try
            {
                KFrameworkManager.instance.InitKFramework();
                DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
                IsPlaytest = PlaytestDefinition != null;
                if (!IsPlaytest)
                {
                    var catalog = Resources.Load<CampaignCatalog>("configs/CampaignCatalog");
                    if (!catalog) throw new InvalidOperationException("CampaignCatalog 缺失，请创建正式关卡目录。");
                    campaign = catalog.ReadLevels();
                    var initial = Resources.Load<TextAsset>(initialLevel);
                    string initialId = initial ? LevelJson.Read(initial.text).id : null;
                    InitialCampaignIndex = Mathf.Max(0, Array.FindIndex(campaign, level => level.id == initialId));
                }
                UI = new StationUIController(this);
                InitializeDebug();
                if (IsPlaytest) LoadLevel(PlaytestDefinition);
                else NotifyChanged();
            }
            catch (Exception exception) { ReportError(exception); }
        }

        public string GetCampaignTitle(int index) => campaign[index].title;

        public void LoadLevel(LevelDefinition level)
        {
            if (NavigationLocked || IsLiveSandbox) return;
            ApplyLevel(level);
        }
        private void ApplyLevel(LevelDefinition level)
        {
            var session = new GameSession(level);
            InstallSession(level, session);
            SessionId = Guid.NewGuid().ToString("N"); Revision++;
            CampaignIndex = IsPlaytest ? -1 : Array.FindIndex(campaign, entry => entry.id == level.id && LevelJson.Hash(entry) == LevelJson.Hash(level));
            Completed = Session.State.Completed; Paused = false; LevelSelectionOpen = false;
            DebugAnimationPaused = false;
            idleTime = 0; input.Clear(); Message = level.briefing; Error = null;
            NotifyChanged();
        }

        // Immediate author/test API. Player navigation uses cover/swap/reveal in StationUIController.
        public bool SelectLevel(int index)
        {
            if (NavigationLocked || IsPlaytest || IsLiveSandbox || LiveEditing || index < 0 || index >= campaign.Length) return false;
            ApplyLevel(campaign[index]); return true;
        }
        internal void SelectCoveredLevel(int index) => ApplyLevel(campaign[index]);
        public bool NextLevel() => CanGoNext && SelectLevel(CampaignIndex + 1);
        internal void ReturnToMenu()
        {
            ClearPresentation();
            Session = null; Definition = null; CampaignIndex = -1;
            SessionId = Guid.NewGuid().ToString("N"); Revision++;
            Completed = Paused = LevelSelectionOpen = false; Message = ""; input.Clear();
            NotifyChanged();
        }
        public bool OpenLevelSelect()
        {
            if (!CanSelectLevel || LevelSelectionOpen) return false;
            pausedBeforeSelection = Paused; LevelSelectionOpen = true;
            SetPaused(true); return true;
        }
        public void CloseLevelSelect()
        {
            if (!LevelSelectionOpen || NavigationLocked) return;
            LevelSelectionOpen = false; SetPaused(pausedBeforeSelection);
        }

        private void Update()
        {
            bool consumed = false; ReadDebugInput(ref consumed);
            if (consumed || LiveEditing) return;
            UI?.ReadInput();
            if (Session == null || Error != null || NavigationLocked || LevelSelectionOpen || UI?.SettingsOpen == true) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && !Paused)
            {
                if (keyboard.vKey.wasPressedThisFrame) ToggleCamera();
                if (keyboard.zKey.wasPressedThisFrame) Undo();
                if (keyboard.rKey.wasPressedThisFrame) Restart();
            }
            Cameras.ReadMouse(!Paused && !Completed);
            if (Paused || Completed || DebugAnimationPaused) return;
            if (!Presenter.Busy) { idleTime += Time.deltaTime; Board.Robot.Sample(0, idleTime % Board.Robot.idle.length); }
            var direction = input.Poll(Presenter.Busy, Cameras.Forward);
            if (direction.HasValue) TryMove(direction.Value);
        }
        public bool TryMove(Direction direction)
        {
            if (Session == null || NavigationLocked || LevelSelectionOpen || DebugInputCaptured || DebugAnimationPaused || Paused || Presenter.Busy || Completed) return false;
            var result = Session.Move(direction);
            if (!result.Accepted)
            {
                RecordDebug("移动 " + direction + "：" + result.RejectReason);
                Board.Robot.transform.rotation = Quaternion.Euler(0, (int)direction * 90, 0);
                if (Time.unscaledTime - rejectedAt >= .5f)
                { rejectedAt = Time.unscaledTime; Message = result.RejectReason == RejectReason.TransportCycle
                    ? "运输路线形成循环，本次推动已取消。请调整挡停位置。" : "前方受阻。只能推动一个箱子；按 Z 撤销。"; NotifyChanged(); }
                return false;
            }
            Revision++; RecordDebug("移动 " + direction + "：接受");
            Presenter.Present(result, () =>
            {
                Board.Restore(Session, false); idleTime = 0; Completed = Session.State.Completed;
                if (Completed) Message = Definition.completionText;
                NotifyChanged();
            });
            NotifyChanged(); return true;
        }
        public void Undo()
        {
            if (Session == null || NavigationLocked || LevelSelectionOpen || LiveEditing || Session.UndoCount == 0) return;
            Presenter.Cancel(); input.Clear(); Session.Undo(); Board.Restore(Session); Cameras.Snap(); Revision++;
            RecordDebug("撤销");
            Completed = Session.State.Completed; Message = Definition.briefing; idleTime = 0; NotifyChanged();
        }
        public void Restart()
        {
            if (Session == null || NavigationLocked || LevelSelectionOpen || LiveEditing) return;
            Presenter.Cancel(); input.Clear(); Session.Restart(); Board.Restore(Session);
            Cameras.Snap(); Revision++; RecordDebug("重开");
            Completed = Session.State.Completed; Message = Definition.briefing; idleTime = 0; NotifyChanged();
        }
        public void ToggleCamera()
        {
            if (Session == null || NavigationLocked || Paused || LevelSelectionOpen) return;
            Cameras.Toggle(); Board.SetTopDown(Cameras.TopDown); input.Clear(); NotifyChanged();
        }
        public void SetPaused(bool value)
        { if (NavigationLocked) return; Paused = value; ApplyPresentationPause(); input.Clear(); NotifyChanged(); }
        internal void SetNavigationLocked(bool value)
        { NavigationLocked = value; ApplyPresentationPause(); input.Clear(); NotifyChanged(); }
        public void SaveReference()
        {
            if (!IsPlaytest || !Completed || NavigationLocked || Presenter.Busy || LiveEditing) return;
            if (!Session.ReferenceReplayValid) { Message = "当前为 GM 修改或现场起点，不能保存为原关卡参考解法。"; NotifyChanged(); return; }
            try { SaveReferenceRequested?.Invoke(Definition.Copy(), Session); Message = "参考解法已记录；退出 Play Mode 后返回编辑器。"; }
            catch (Exception exception) { Message = exception.Message; }
            NotifyChanged();
        }
        internal void ReportError(Exception exception)
        { Error = exception.Message; Debug.LogException(exception); NotifyChanged(); }
        private void NotifyChanged()
        {
            bool free = Session == null || NavigationLocked || DebugInputCaptured || Paused || Completed || LevelSelectionOpen || (Cameras && Cameras.TopDown);
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = free;
            Changed?.Invoke();
        }
        private void ClearPresentation()
        {
            Presenter?.Cancel();
            if (presentationRoot) { presentationRoot.SetActive(false); Destroy(presentationRoot); }
            presentationRoot = null;
            Board = null; Cameras = null;
        }
        private void OnDestroy()
        { DisposeDebug(); UI?.Dispose(); Presenter?.Cancel(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
