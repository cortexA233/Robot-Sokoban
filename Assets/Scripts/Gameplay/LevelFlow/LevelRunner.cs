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
    public sealed class LevelRunner : MonoBehaviour
    {
        public static LevelDefinition PlaytestDefinition;
        public static event Action<LevelDefinition, GameSession> SaveReferenceRequested;
        public event Action Changed;
        public string initialLevel = "configs/levels/L01";
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
        public bool CanSelectLevel => !IsPlaytest && campaign.Length > 0 && !NavigationLocked;
        public bool CanGoNext => !NavigationLocked && !LevelSelectionOpen && !IsPlaytest && Completed && Presenter != null && !Presenter.Busy && CampaignIndex >= 0 && CampaignIndex + 1 < campaign.Length;
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
                if (IsPlaytest) LoadLevel(PlaytestDefinition);
                else NotifyChanged();
            }
            catch (Exception exception) { ReportError(exception); }
        }

        public string GetCampaignTitle(int index) => campaign[index].title;

        public void LoadLevel(LevelDefinition level)
        {
            if (NavigationLocked) return;
            ApplyLevel(level);
        }
        private void ApplyLevel(LevelDefinition level)
        {
            // Validate before tearing down a working level; never mutate author data.
            var session = new GameSession(level);
            ClearPresentation();
            Definition = level.Copy(); Session = session;
            CampaignIndex = IsPlaytest ? -1 : Array.FindIndex(campaign, entry => entry.id == level.id && LevelJson.Hash(entry) == LevelJson.Hash(level));
            Board = new GameObject("Board").AddComponent<BoardView>(); Board.transform.SetParent(transform);
            Board.Build(Definition); Board.Restore(Session);
            Presenter = GetComponent<CommandPresenter>() ?? gameObject.AddComponent<CommandPresenter>(); Presenter.Initialize(Board);
            Cameras = new GameObject("Camera rig").AddComponent<CameraRig>(); Cameras.transform.SetParent(transform);
            Cameras.Initialize(Definition, Board.Robot.transform);
            UI?.Preferences.Apply(Cameras);
            Completed = Session.State.Completed; Paused = false; LevelSelectionOpen = false;
            idleTime = 0; input.Clear(); Message = level.briefing; Error = null;
            NotifyChanged();
        }

        // Immediate author/test API. Player navigation uses cover/swap/reveal in StationUIController.
        public bool SelectLevel(int index)
        {
            if (NavigationLocked || IsPlaytest || index < 0 || index >= campaign.Length) return false;
            ApplyLevel(campaign[index]); return true;
        }
        internal void SelectCoveredLevel(int index) => ApplyLevel(campaign[index]);
        public bool NextLevel() => CanGoNext && SelectLevel(CampaignIndex + 1);
        internal void ReturnToMenu()
        {
            ClearPresentation();
            Session = null; Definition = null; CampaignIndex = -1;
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
            if (Paused || Completed) return;
            if (!Presenter.Busy) { idleTime += Time.deltaTime; Board.Robot.Sample(0, idleTime % Board.Robot.idle.length); }
            var direction = input.Poll(Presenter.Busy, Cameras.Forward);
            if (direction.HasValue) TryMove(direction.Value);
        }
        public bool TryMove(Direction direction)
        {
            if (Session == null || NavigationLocked || LevelSelectionOpen || Paused || Presenter.Busy || Completed) return false;
            var result = Session.Move(direction);
            if (!result.Accepted)
            {
                Board.Robot.transform.rotation = Quaternion.Euler(0, (int)direction * 90, 0);
                if (Time.unscaledTime - rejectedAt >= .5f)
                { rejectedAt = Time.unscaledTime; Message = "前方受阻。只能推动一个箱子；按 Z 撤销。"; NotifyChanged(); }
                return false;
            }
            Presenter.Present(result, () =>
            {
                Board.Restore(Session); idleTime = 0; Completed = Session.State.Completed;
                if (Completed) Message = Definition.completionText;
                NotifyChanged();
            });
            NotifyChanged(); return true;
        }
        public void Undo()
        {
            if (Session == null || NavigationLocked || LevelSelectionOpen) return;
            Presenter.Cancel(); input.Clear(); Session.Undo(); Board.Restore(Session);
            Completed = Session.State.Completed; Message = Definition.briefing; idleTime = 0; NotifyChanged();
        }
        public void Restart()
        {
            if (Session == null || NavigationLocked || LevelSelectionOpen) return;
            Presenter.Cancel(); input.Clear(); Session.Restart(); Board.Restore(Session);
            Completed = Session.State.Completed; Message = Definition.briefing; idleTime = 0; NotifyChanged();
        }
        public void ToggleCamera()
        {
            if (Session == null || NavigationLocked || Paused || LevelSelectionOpen) return;
            Cameras.Toggle(); Board.SetTopDown(Cameras.TopDown); input.Clear(); NotifyChanged();
        }
        public void SetPaused(bool value)
        { if (NavigationLocked) return; Paused = value; Presenter?.SetPaused(value); input.Clear(); NotifyChanged(); }
        internal void SetNavigationLocked(bool value)
        { NavigationLocked = value; Presenter?.SetPaused(value || Paused); input.Clear(); NotifyChanged(); }
        public void SaveReference()
        {
            if (!IsPlaytest || !Completed || NavigationLocked || Presenter.Busy) return;
            try { SaveReferenceRequested?.Invoke(Definition.Copy(), Session); Message = "参考解法已记录；退出 Play Mode 后返回编辑器。"; }
            catch (Exception exception) { Message = exception.Message; }
            NotifyChanged();
        }
        internal void ReportError(Exception exception)
        { Error = exception.Message; Debug.LogException(exception); NotifyChanged(); }
        private void NotifyChanged()
        {
            bool free = Session == null || NavigationLocked || Paused || Completed || LevelSelectionOpen || (Cameras && Cameras.TopDown);
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = free;
            Changed?.Invoke();
        }
        private void ClearPresentation()
        {
            Presenter?.Cancel();
            if (Board) { Board.gameObject.SetActive(false); Destroy(Board.gameObject); }
            if (Cameras) { Cameras.gameObject.SetActive(false); Destroy(Cameras.gameObject); }
            Board = null; Cameras = null;
        }
        private void OnDestroy()
        { UI?.Dispose(); Presenter?.Cancel(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
