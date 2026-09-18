using System.Collections;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Sokoban.Tests
{
    public sealed class CameraReadabilityTests
    {
        private LevelRunner runner;
        private Keyboard keyboard;

        [UnitySetUp] public IEnumerator SetUp()
        {
            LevelRunner.PlaytestDefinition = null;
            runner = new GameObject("Camera readability test").AddComponent<LevelRunner>(); runner.Progress = new PlayerProgress(() => null, _ => { });
            yield return null; runner.enabled = false;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (runner) Object.Destroy(runner.gameObject);
            LevelRunner.PlaytestDefinition = null; yield return null;
        }

        [UnityTest] public IEnumerator EveryCampaignEntryDefaultsToNorthUpAndFitsTheHudClearArea()
        {
            for (int i = 0; i < runner.CampaignLevelCount; i++)
            {
                runner.SelectLevel(i); yield return new WaitForSeconds(.3f);
                Assert.That(runner.Cameras.TopDown, Is.True);
                Assert.That(runner.Cameras.Output.orthographic, Is.True);
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(Cursor.visible, Is.True);
                CheckFraming();
                runner.ToggleCamera(); // A prior choice must not replace the next level's default.
            }
            runner.SelectLevel(8);
            foreach (float aspect in new[] { .75f, 16f/9, 2.4f })
            {
                runner.Cameras.Output.aspect = aspect; runner.Cameras.Snap();
                yield return new WaitForSeconds(.3f); CheckFraming();
            }
        }

        private void CheckFraming()
        {
            var camera = runner.Cameras.Output;
            Assert.That(Vector3.Dot(camera.transform.up, Vector3.forward), Is.GreaterThan(.999f));
            foreach (float x in new[] { -.5f, runner.Definition.width-.5f })
                foreach (float z in new[] { -.5f, runner.Definition.height-.5f })
                {
                    var p = camera.WorldToViewportPoint(new Vector3(x,0,z));
                    Assert.That(p.x, Is.InRange(.03f,.97f)); Assert.That(p.y, Is.InRange(.17f,.83f));
                }
        }

        [UnityTest] public IEnumerator ProjectionCommitsAtTheEndAndRapidReversalKeepsTheSameSession()
        {
            runner.SelectLevel(2); yield return new WaitForSeconds(.3f);
            var session = runner.Session;
            runner.ToggleCamera(); yield return new WaitForSeconds(.15f);
            Assert.That(runner.Cameras.Output.orthographic, Is.True, "Keep the source projection during travel.");
            yield return new WaitForSeconds(.2f);
            Assert.That(runner.Cameras.Output.orthographic, Is.False);
            runner.ToggleCamera(); yield return new WaitForSeconds(.15f);
            Assert.That(runner.Cameras.Output.orthographic, Is.False);
            runner.ToggleCamera(); yield return new WaitForSeconds(.3f);
            Assert.That(runner.Cameras.TopDown || runner.Cameras.Output.orthographic, Is.False);
            Assert.That(runner.Session, Is.SameAs(session)); Assert.That(session.State.Moves, Is.Zero);
            runner.ToggleCamera(); yield return new WaitForSeconds(.3f); CheckFraming();
        }

        [UnityTest] public IEnumerator AuthorPlaytestDefaultsToTopAndRestartPreservesAnExplicitViewChoice()
        {
            Object.Destroy(runner.gameObject); yield return null;
            LevelRunner.PlaytestDefinition = StationCircuitTests.ComparisonFixture();
            runner = new GameObject("Author camera test").AddComponent<LevelRunner>(); runner.Progress = new PlayerProgress(() => null, _ => { });
            yield return new WaitForSeconds(.3f); runner.enabled = false;
            Assert.That(runner.IsPlaytest && runner.Cameras.TopDown, Is.True);
            runner.ToggleCamera(); yield return new WaitForSeconds(.3f);
            runner.Restart(); Assert.That(runner.Cameras.TopDown, Is.False);
            runner.SetPaused(true); Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            runner.SetPaused(false); Assert.That(runner.Cameras.TopDown, Is.False);
            runner.ToggleCamera(); yield return new WaitForSeconds(.3f);
            CheckFraming();
        }

        [UnityTest] public IEnumerator FullOrbitKeepsWallsAndGateStructureVisibleWithoutChangingRules()
        {
            runner.SelectLevel(2); yield return null;
            string hash = LevelJson.Hash(runner.Definition);
            var state = runner.Session.State;
            var walls = runner.Board.GetComponentsInChildren<Transform>().Where(t=>t.name=="Upper")
                .SelectMany(t=>t.GetComponentsInChildren<Renderer>()).ToArray();
            var gateStructure = runner.Board.Gates.Values.SelectMany(g=>g.transform.Find("PowerGateRoot/UpperStructure")
                .GetComponentsInChildren<Renderer>().Concat(g.transform.Find("PowerGateRoot/MovingParts").GetComponentsInChildren<Renderer>())).ToArray();
            var wallColliders = runner.Board.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Wall ")).ToArray();
            Assert.That(walls, Is.Not.Empty); Assert.That(gateStructure, Is.Not.Empty);
            Assert.That(walls.Concat(gateStructure).All(r=>!r.enabled), Is.True);
            var view = runner.Cameras.Capture(); view.topDown = false;
            foreach (float pitch in new[] { 30f, 48f, 65f })
                for (int yaw = 0; yaw < 360; yaw += 30)
                {
                    view.pitch = pitch; view.yaw = yaw; view.distance = pitch == 30 ? 3 : 6;
                    runner.Cameras.Restore(view); yield return new WaitForSeconds(.3f);
                    Assert.That(walls.Concat(gateStructure).All(r=>r.enabled), Is.True,
                        "Third person must never cull upper geometry, including at yaw="+yaw+", pitch="+pitch);
                    Assert.That(wallColliders.All(c=>c.enabled && c.size.y>1), Is.True,
                        "Complete walls must retain their full camera obstacles.");
                    Assert.That(Physics.CheckSphere(runner.Cameras.Output.transform.position,.025f), Is.False,
                        "Cinemachine should avoid the restored obstacles.");
                    Assert.That(runner.Session.Rules.Resolve(state,Direction.W).Accepted, Is.False);
                }
            Assert.That(runner.Session.State, Is.SameAs(state)); Assert.That(LevelJson.Hash(runner.Definition), Is.EqualTo(hash));
            runner.ToggleCamera();
            Assert.That(walls.Concat(gateStructure).All(r=>!r.enabled), Is.True);
            runner.ToggleCamera();
            Assert.That(walls.Concat(gateStructure).All(r=>r.enabled), Is.True,
                "Returning to third person must restore every upper renderer immediately, without an occlusion delay.");
        }

        [UnityTest] public IEnumerator ActualVKeyClearsBufferedMovementAndTogglesWithoutResettingTheCommand()
        {
            runner.LoadLevel(StationCircuitTests.ComparisonFixture()); yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>("Camera test keyboard");
            runner.enabled = true;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
            var session = runner.Session;
            Assert.That(runner.TryMove(Direction.E), Is.True);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.UpArrow)); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.UpArrow,Key.V)); yield return null;
            Assert.That(runner.Cameras.TopDown, Is.False);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.UpArrow)); yield return new WaitForSeconds(.6f);
            Assert.That(runner.Session, Is.SameAs(session));
            Assert.That(session.State.Moves, Is.EqualTo(1)); Assert.That(session.State.Pushes, Is.EqualTo(1));
            Assert.That(session.Commands, Is.EqualTo("E")); Assert.That(session.UndoCount, Is.EqualTo(1));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.UpArrow,Key.V)); yield return null;
            Assert.That(runner.Cameras.TopDown, Is.True);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return new WaitForSeconds(.3f);
            Assert.That(runner.Cameras.Output.orthographic, Is.True); Assert.That(Cursor.visible, Is.True);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.UpArrow)); yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return new WaitForSeconds(.4f);
            Assert.That(session.State.Moves, Is.EqualTo(2));
            runner.Undo(); Assert.That(session.Commands, Is.EqualTo("E"));
            Assert.That(session.State.Player, Is.EqualTo(new Cell(3,2)));
        }
    }
}
