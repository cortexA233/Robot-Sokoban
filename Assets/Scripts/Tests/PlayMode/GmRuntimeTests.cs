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
            runner = new GameObject("GM test runner").AddComponent<LevelRunner>(); yield return null;
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
            Control<InputField>("X").text = target.x.ToString(); Control<InputField>("Z").text = target.z.ToString();
            yield return Click("Place"); Assert.That(runner.Session.State.Player, Is.EqualTo(target));
            Assert.That(runner.Session.ReferenceReplayValid, Is.False);
            Assert.That(runner.Board.Robot.transform.position, Is.EqualTo(BoardView.Position(target)));
            yield return Click("Undo"); Assert.That(runner.Session.State, Is.SameAs(before));
            Assert.That(runner.Session.ReferenceReplayValid, Is.True);
            yield return Click("Close"); Assert.That(runner.DebugInputCaptured, Is.False);
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
            var board = runner.Board; runner.SetDebugAnimationPaused(true); runner.BeginLiveEditing();
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
            Assert.That(runner.DebugAnimationPaused, Is.True);
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
