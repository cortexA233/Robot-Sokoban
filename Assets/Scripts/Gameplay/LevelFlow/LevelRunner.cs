using System;
using System.Linq;
using DG.Tweening;
using KToolkit;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sokoban
{
    // First playable authoring milestone. Campaign menus/progress are separate follow-up work.
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
        private readonly GridInput input = new GridInput();
        private string message = "";
        private string error;
        private float idleTime;
        private float rejectedAt = -1;
        private bool isPlaytest;

        private void Start()
        {
            try
            {
                KFrameworkManager.instance.InitKFramework();
                DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
                isPlaytest = PlaytestDefinition != null;
                var asset = Resources.Load<TextAsset>(initialLevel);
                if (!isPlaytest && !asset) throw new InvalidOperationException("初始关卡缺失，请先导入设计配方。");
                LoadLevel(isPlaytest ? PlaytestDefinition : LevelJson.Read(asset.text));
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
            Board = new GameObject("Board").AddComponent<BoardView>(); Board.transform.SetParent(transform);
            Board.Build(Definition); Board.Restore(Session);
            Presenter = GetComponent<CommandPresenter>() ?? gameObject.AddComponent<CommandPresenter>(); Presenter.Initialize(Board);
            Cameras = new GameObject("Camera rig").AddComponent<CameraRig>(); Cameras.transform.SetParent(transform);
            Cameras.Initialize(Definition, Board.Robot.transform);
            Completed = Session.State.Completed; Paused = false; idleTime = 0; input.Clear();
            message = level.briefing; UpdateCursor();
        }
        private void Update()
        {
            if (Session == null || error != null) return;
            var keyboard = Keyboard.current;
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
            if (Paused || Presenter.Busy || Completed) return false;
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
            if (!Completed || Presenter.Busy) return;
            try { SaveReferenceRequested?.Invoke(Definition.Copy(), Session); message = "参考解法已记录；退出 Play Mode 后返回编辑器。"; }
            catch (Exception exception) { message = exception.Message; }
        }
        private void OnGUI()
        {
            if (error != null) { GUI.Box(new Rect(20, 20, Screen.width - 40, 100), error); return; }
            if (Session == null) return;
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
                GUILayout.BeginArea(new Rect(Screen.width / 2f - 180, Screen.height / 2f - 110, 360, 220), GUI.skin.box);
                GUILayout.Label(Completed ? Definition.completionText : "已暂停");
                if (Completed && isPlaytest && GUILayout.Button("保存为参考解法")) SaveReference();
                if (Completed && GUILayout.Button("撤销最后一步")) Undo();
                if (GUILayout.Button("重开本关")) { SetPaused(false); Restart(); }
                if (Paused && GUILayout.Button("继续")) SetPaused(false);
                if (isPlaytest) GUILayout.Label("点击 Unity 顶部 Play 按钮退出，返回原设计。");
                GUILayout.EndArea();
            }
        }
        private void OnDestroy() { Presenter?.Cancel(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
