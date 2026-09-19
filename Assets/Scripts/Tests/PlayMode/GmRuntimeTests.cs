using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Terrain = Sokoban.Domain.Terrain;

namespace Sokoban.Tests
{
    public sealed class GmRuntimeTests
    {
        private LevelRunner runner;
        [UnitySetUp] public IEnumerator SetUp()
        {
            LevelRunner.PlaytestDefinition = null;
            runner = new GameObject("GM test runner").AddComponent<LevelRunner>(); runner.Progress = new PlayerProgress(() => null, _ => { }); yield return null;
            runner.enabled = false; runner.SelectLevel(0); yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        { if (runner) Object.Destroy(runner.gameObject); yield return null; LevelRunner.PlaytestDefinition = null; }
        private T Control<T>(string name) where T : Component => runner.Gm.gameObject.GetComponentsInChildren<T>(true).First(c => c.name == name);
        private Cell Empty() => Enumerable.Range(0, runner.Definition.height)
            .SelectMany(z => Enumerable.Range(0, runner.Definition.width).Select(x => new Cell(x, z)))
            .First(c => runner.Definition.TerrainAt(c) == Terrain.Floor && c != runner.Session.State.Player && !runner.Session.State.Crates.Values.Contains(c));
        private IEnumerator Click(string name)
        {
            yield return null; Canvas.ForceUpdateCanvases(); var button = Control<Button>(name);
            Assert.That(button.IsInteractable(), Is.True, name);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), name + " is obscured");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest] public IEnumerator RealGmControlsMoveUndoAndBlockUnderlyingGame()
        {
            var before = runner.Session.State; var target = Empty(); runner.SetDebugVisible(true);
            Assert.That(runner.TryMove(Direction.N), Is.False);
            runner.Gm.SelectCell(before.Player);
            Control<InputField>("X").text = target.x.ToString(); Control<InputField>("Z").text = target.z.ToString();
            yield return Click("Place"); Assert.That(runner.Session.State.Player, Is.EqualTo(target));
            Assert.That(runner.Session.ReferenceReplayValid, Is.False);
            Assert.That(runner.Board.Robot.transform.position, Is.EqualTo(BoardView.Position(target)));
            yield return Click("Undo"); Assert.That(runner.Session.State, Is.SameAs(before));
            Assert.That(runner.Session.ReferenceReplayValid, Is.True);
            yield return Click("Close"); Assert.That(runner.DebugInputCaptured, Is.False);
        }

        [UnityTest] public IEnumerator SelectionMovesEachKindAndClearsOnEmptyAndSessionChanges()
        {
            runner.SetDebugVisible(true);
            Assert.That(Control<Button>("Place").interactable, Is.False);
            foreach (string id in new[] { DebugBoardEdit.PlayerId }.Concat(runner.Definition.crates.Select(c => c.id)))
            {
                var before = runner.Session.State;
                Cell source = id == DebugBoardEdit.PlayerId ? before.Player : before.Crates[id];
                Cell target = Empty();
                Control<InputField>("X").text = target.x.ToString(); Control<InputField>("Z").text = target.z.ToString();
                int history = runner.Session.UndoCount;
                runner.Gm.SelectCell(source);
                Assert.That(runner.Gm.SelectedId, Is.EqualTo(id));
                Assert.That(Control<InputField>("X").text, Is.EqualTo(target.x.ToString()), "Source selection must not overwrite destination.");
                Assert.That(runner.Session.UndoCount, Is.EqualTo(history));
                yield return Click("Place");
                Assert.That(id == DebugBoardEdit.PlayerId ? runner.Session.State.Player : runner.Session.State.Crates[id], Is.EqualTo(target));
                Assert.That(runner.Session.State.Moves, Is.EqualTo(before.Moves));
                Assert.That(runner.Session.State.Pushes, Is.EqualTo(before.Pushes));
                yield return Click("Undo");
                Assert.That(Control<Text>("Selection").text, Does.Contain(source.ToString()));
                Assert.That(runner.Session.State, Is.SameAs(before));
                runner.Gm.SelectCell(Empty());
                Assert.That(runner.Gm.SelectedId, Is.Null); Assert.That(Control<Button>("Place").interactable, Is.False);
            }
            foreach (var cell in runner.Definition.sockets.Select(c => c.Cell).Concat(new[] { new Cell(0, 0), new Cell(-1, -1) }))
            {
                runner.Gm.SelectCell(runner.Session.State.Player); runner.Gm.SelectCell(cell);
                Assert.That(runner.Gm.SelectedId, Is.Null); Assert.That(Control<Button>("Place").interactable, Is.False);
            }
            runner.Gm.SelectCell(runner.Session.State.Player); runner.Restart();
            Assert.That(runner.Gm.SelectedId, Is.Null);
            runner.Gm.SelectCell(runner.Session.State.Player); runner.SelectLevel(1);
            Assert.That(runner.Gm.SelectedId, Is.Null);
            runner.Gm.SelectCell(runner.Session.State.Player); runner.BeginLiveEditing();
            var draft = BoardSnapshot.Capture(runner.Definition, runner.Session.State).ApplyTo(runner.Definition);
            Assert.That(runner.TryApplyLiveDraft(draft, BoardSnapshot.FromDefinition(draft), runner.SessionId, runner.Revision, out _), Is.True);
            Assert.That(runner.Gm.SelectedId, Is.Null);
            runner.SetDebugVisible(true); runner.Gm.SelectCell(runner.Session.State.Player);
            Assert.That(runner.EndLiveSandbox(out _), Is.True); Assert.That(runner.Gm.SelectedId, Is.Null);
        }

        [UnityTest] public IEnumerator CrateOnSocketOrRedirectorIsSelectedAndInvalidMovesPreserveHistory()
        {
            var level = runner.Definition.Copy(); var cargo = level.crates.First(c => !c.IsEnergy);
            var source = cargo.Cell; level.schemaVersion = 3;
            level.redirectors = new[] { new RedirectorDefinition { id = "under_crate", x = source.x, z = source.z, facing = "N" } };
            runner.LoadLevel(level); runner.SetDebugVisible(true); runner.Gm.SelectCell(source);
            Assert.That(runner.Gm.SelectedId, Is.EqualTo(cargo.id));
            var before = runner.Session.State;
            foreach (Cell target in new[] { new Cell(-1, 0), new Cell(0, 0), before.Player, source })
            {
                Control<InputField>("X").text = target.x.ToString(); Control<InputField>("Z").text = target.z.ToString();
                yield return Click("Place"); Assert.That(runner.Session.State, Is.SameAs(before)); Assert.That(runner.Session.UndoCount, Is.Zero);
            }
            var goal = level.sockets.First(s => s.isGoal).Cell;
            Assert.That(runner.TryApplyDebugEdit(DebugBoardEdit.Place(cargo.id, goal), out _), Is.True);
            runner.Gm.SelectCell(goal); Assert.That(runner.Gm.SelectedId, Is.EqualTo(cargo.id));
            Assert.That(runner.Session.State.Completed, Is.False);
            runner.Gm.SelectCell(source); Assert.That(runner.Gm.SelectedId, Is.Null, "Bare redirector is not movable.");
        }

        [UnityTest] public IEnumerator MousePicksSourceButPanelAndCoordinateInputDoNotPickThrough()
        {
            var mouse = InputSystem.AddDevice<Mouse>(); runner.SetDebugVisible(true); runner.enabled = true;
            try
            {
                foreach (var pair in runner.Session.State.Crates.Concat(new[] { new KeyValuePair<string, Cell>(DebugBoardEdit.PlayerId, runner.Session.State.Player) }))
                {
                    runner.Cameras.Snap(); yield return null;
                    Vector2 point = runner.Cameras.Output.WorldToScreenPoint(BoardView.Position(pair.Value));
                    var panel = Control<RectTransform>("Panel"); var bounds = ((RectTransform)runner.Gm.transform).rect;
                    panel.anchoredPosition = new Vector2(point.x < Screen.width / 2f ? bounds.width - panel.rect.width : 0, 0);
                    yield return null; Canvas.ForceUpdateCanvases();
                    var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                    Assert.That(hits, Is.Empty, "Board source must be visible beside the movable panel.");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
                    Assert.That(runner.Gm.SelectedId, Is.EqualTo(pair.Key));
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
                }
                string selected = runner.Gm.SelectedId; var state = runner.Session.State;
                var field = Control<InputField>("X"); Vector2 input = RectTransformUtility.WorldToScreenPoint(null, field.transform.TransformPoint(((RectTransform)field.transform).rect.center));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = input }.WithButton(MouseButton.Left)); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = input }); yield return null;
                Assert.That(runner.Gm.SelectedId, Is.EqualTo(selected)); Assert.That(runner.Session.State, Is.SameAs(state));
                Assert.That(field.isFocused, Is.True);
            }
            finally { InputSystem.RemoveDevice(mouse); runner.enabled = false; }
        }

        [UnityTest] public IEnumerator PrefabOnlyExposesRetainedTabsAndSearchCannotRevealRemovedOperations()
        {
            runner.SetDebugVisible(true);
            var names = runner.Gm.gameObject.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            foreach (string removed in new[] { "Monitor", "MonitorText", "PowerContent", "LogContent", "Entity", "Pick", "FacingRow", "SwapRow", "AnimationPause", "Additions", "AdditionHint", "Copy" })
                Assert.That(names, Does.Not.Contain(removed));
            Assert.That(Control<Button>("Live").GetComponentInChildren<Text>().text, Is.EqualTo("现场编辑（打开关卡编辑器）"));
            Assert.That(Control<Text>("LiveHint").text, Is.EqualTo("编辑关卡后请在关卡编辑器内保存变更或保存为新副本"));
            Control<InputField>("Search").text = "诊断"; yield return null;
            Assert.That(Control<RectTransform>("LevelContent").gameObject.activeSelf, Is.False);
            Assert.That(Control<RectTransform>("BoardContent").gameObject.activeSelf, Is.False);
            Control<InputField>("Search").text = "移动"; yield return null;
            Assert.That(Control<RectTransform>("BoardContent").gameObject.activeSelf, Is.True);
        }

        [UnityTest] public IEnumerator RetainedLevelControlsFitAfterTabWidthAndFontChanges()
        {
            runner.SetDebugVisible(true);
            yield return Click("TabLevelContent");
            for (int i = 0; i < 3; i++) yield return Click("FontLarge");
            Control<Slider>("Width").value = 640; yield return null; Canvas.ForceUpdateCanvases();
            var viewport = Control<ScrollRect>("Scroll").viewport;
            foreach (string name in new[] { "Load", "Live", "LiveHint" })
            {
                var rect = Control<RectTransform>(name);
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, rect);
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 1), name + " panel=" + Control<RectTransform>("Panel").rect + " root=" + ((RectTransform)runner.Gm.transform).rect + " content=" + Control<RectTransform>("Content").rect + " pref=" + LayoutUtility.GetPreferredHeight(Control<RectTransform>("Content")));
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(viewport.rect.yMax + 1), name);
            }
            var hint = Control<Text>("LiveHint");
            Assert.That(hint.rectTransform.rect.height, Is.GreaterThanOrEqualTo(hint.preferredHeight - 1));
            foreach (var field in runner.Gm.gameObject.GetComponentsInChildren<InputField>(true))
                Assert.That(field.textComponent.rectTransform.rect.height, Is.GreaterThanOrEqualTo(field.textComponent.preferredHeight - 1), field.name + " input text must remain readable");
            yield return Click("TabBoardContent");
            Assert.That(Control<Button>("Place").gameObject.activeInHierarchy, Is.True);
            Assert.That(Control<RectTransform>("Panel").rect.height, Is.LessThan(780), "Removed rows must not leave the old fixed body height.");
        }

        [UnityTest] public IEnumerator InvalidGmDuringAnimationLeavesTweenAndHistoryUntouched()
        {
            var proof = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/L04.solution").text);
            Assert.That(runner.TryMove((Direction)System.Enum.Parse(typeof(Direction), proof.commands[0].ToString())), Is.True);
            Assert.That(runner.Presenter.Busy, Is.True); var before = runner.Session.State;
            Assert.That(runner.TryApplyDebugEdit(DebugBoardEdit.Place(DebugBoardEdit.PlayerId, new Cell(-1, -1)), out _), Is.False);
            Assert.That(runner.Presenter.Busy, Is.True); Assert.That(runner.Session.State, Is.SameAs(before));
            Assert.That(runner.TryApplyDebugEdit(DebugBoardEdit.Place(DebugBoardEdit.PlayerId, Empty()), out _), Is.True);
            Assert.That(runner.Presenter.Busy, Is.False);
            yield return new WaitForSeconds(.9f); runner.Undo(); Assert.That(runner.Session.State, Is.SameAs(before));
            Assert.That(runner.Board.Robot.transform.position, Is.EqualTo(BoardView.Position(before.Player)));
        }

        [UnityTest] public IEnumerator LiveApplyRejectsInvalidDraftThenRebuildsTypesAndRestoresOrigin()
        {
            var origin = runner.Session.State; string hash = LevelJson.Hash(runner.Definition);
            var board = runner.Board; runner.SetPaused(true); runner.BeginLiveEditing();
            var draft = BoardSnapshot.Capture(runner.Definition, runner.Session.State).ApplyTo(runner.Definition);
            var bad = draft.Copy(); bad.playerSpawn.x = -1;
            Assert.That(runner.TryApplyLiveDraft(bad, BoardSnapshot.FromDefinition(bad), runner.SessionId, runner.Revision, out _), Is.False);
            Assert.That(runner.Board, Is.SameAs(board)); Assert.That(runner.Session.State, Is.SameAs(origin));
            foreach (var c in draft.crates) c.kind = CrateDefinition.Cargo;
            Assert.That(runner.TryApplyLiveDraft(draft, BoardSnapshot.FromDefinition(draft), runner.SessionId, runner.Revision, out _), Is.True);
            Assert.That(runner.Session.UndoCount, Is.Zero); Assert.That(runner.Session.ReferenceReplayValid, Is.False);
            Assert.That(runner.CampaignIndex, Is.EqualTo(-1)); Assert.That(runner.IsPlaytest, Is.True);
            Assert.That(runner.Session.Rules.Power(runner.Session.State).Sockets.Values.All(v => !v), Is.True);
            runner.Restart(); Assert.That(runner.Definition.crates.All(c => !c.IsEnergy), Is.True);
            Assert.That(runner.EndLiveSandbox(out _), Is.True); yield return null;
            Assert.That(LevelJson.Hash(runner.Definition), Is.EqualTo(hash)); Assert.That(runner.Session.State, Is.SameAs(origin));
            Assert.That(runner.CampaignIndex, Is.EqualTo(0)); Assert.That(runner.IsPlaytest, Is.False);
            Assert.That(runner.Paused, Is.True);
            Assert.That(runner.GetComponentsInChildren<BoardView>().Length, Is.EqualTo(1));
            Assert.That(runner.GetComponentsInChildren<CameraRig>().Length, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator F1AndEscapeAreConsumedWithoutPausingGame()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>(); runner.enabled = true;
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1)); yield return null;
                Assert.That(runner.DebugVisible, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
                Assert.That(runner.DebugVisible, Is.False); Assert.That(runner.Paused, Is.False);
            }
            finally { InputSystem.RemoveDevice(keyboard); runner.enabled = false; }
        }
    }
}
