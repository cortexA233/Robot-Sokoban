using System.Collections;
using NUnit.Framework;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sokoban.Tests
{
    public sealed class GameplayTests
    {
        private LevelRunner runner;
        private LevelDefinition lab;
        [UnitySetUp] public IEnumerator SetUp()
        {
            LevelRunner.PlaytestDefinition = null;
            lab = LevelJson.Read(Resources.Load<TextAsset>("configs/test_levels/LAB01_LowFriction").text);
            runner = new GameObject("Test runner").AddComponent<LevelRunner>();
            yield return null;
            runner.enabled = false; // Commands below exercise the public game API deterministically.
            runner.LoadLevel(lab);
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (runner) Object.Destroy(runner.gameObject);
            yield return null;
        }
        [UnityTest] public IEnumerator RealRobotUsesOnePushClockAndMatchesContactPlane()
        {
            Assert.That(runner.Board.Robot.animator.applyRootMotion, Is.False);
            Assert.That(runner.Board.Robot.Contact, Is.Not.Null);
            runner.TryMove(Direction.E);
            yield return new WaitForSeconds(.32f);
            float backFace = runner.Board.Crates["crate_01"].position.x - .4f;
            Assert.That(Mathf.Abs(runner.Board.Robot.Contact.position.x - backFace), Is.LessThanOrEqualTo(.01f));
            yield return new WaitForSeconds(.8f);
            Assert.That(runner.Completed, Is.True);
            Assert.That(Vector3.Distance(runner.Board.Robot.transform.position, new Vector3(2, 0, 3)), Is.LessThan(.001f));
            Assert.That(runner.Board.Crates["crate_01"].position.x, Is.EqualTo(6).Within(.001f));
        }
        [UnityTest] public IEnumerator UndoDuringExtension() => CancelAt(.05f, false);
        [UnityTest] public IEnumerator UndoDuringContact() => CancelAt(.3f, false);
        [UnityTest] public IEnumerator UndoDuringSlide() => CancelAt(.65f, false);
        [UnityTest] public IEnumerator RestartDuringExtension() => CancelAt(.05f, true);
        [UnityTest] public IEnumerator RestartDuringContact() => CancelAt(.3f, true);
        [UnityTest] public IEnumerator RestartDuringSlide() => CancelAt(.65f, true);
        private IEnumerator CancelAt(float seconds, bool restart)
        {
            runner.TryMove(Direction.E); yield return new WaitForSeconds(seconds);
            if (restart) runner.Restart(); else runner.Undo();
            yield return new WaitForSeconds(1.1f);
            Assert.That(runner.Session.State.Player, Is.EqualTo(new Cell(1, 3)));
            Assert.That(runner.Session.State.Crates["crate_01"], Is.EqualTo(new Cell(2, 3)));
            Assert.That(runner.Session.State.Moves, Is.Zero); Assert.That(runner.Session.Commands, Is.Empty);
            Assert.That(runner.Board.Robot.transform.position.x, Is.EqualTo(1).Within(.001f));
            Assert.That(runner.Board.Crates["crate_01"].position.x, Is.EqualTo(2).Within(.001f));
            Assert.That(runner.Board.Robot.animator.transform.InverseTransformPoint(runner.Board.Robot.Contact.position).z, Is.EqualTo(.36f).Within(.01f));
            Assert.That(runner.Presenter.Busy, Is.False); Assert.That(runner.Completed, Is.False);
        }
        [UnityTest] public IEnumerator PauseFreezesOwnedActionThenResumes()
        {
            runner.TryMove(Direction.E); yield return new WaitForSeconds(.3f); runner.SetPaused(true);
            Vector3 player = runner.Board.Robot.transform.position, crate = runner.Board.Crates["crate_01"].position;
            yield return new WaitForSeconds(.4f);
            Assert.That(Vector3.Distance(player, runner.Board.Robot.transform.position), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(crate, runner.Board.Crates["crate_01"].position), Is.LessThan(.0001f));
            runner.SetPaused(false); yield return new WaitForSeconds(1);
            Assert.That(runner.Completed, Is.True);
        }
        [UnityTest] public IEnumerator CameraSwitchDuringSlidePreservesCommandAndNorthUpProjection()
        {
            runner.TryMove(Direction.E); yield return new WaitForSeconds(.65f); runner.ToggleCamera();
            yield return new WaitForSeconds(.45f);
            Assert.That(runner.Completed, Is.True); Assert.That(runner.Session.State.Pushes, Is.EqualTo(1));
            Assert.That(runner.Cameras.Output.orthographic, Is.True);
            Assert.That(Vector3.Dot(runner.Cameras.Output.transform.up, Vector3.forward), Is.GreaterThan(.999f));
            runner.Restart(); Assert.That(runner.Cameras.TopDown, Is.True);
        }
        [UnityTest] public IEnumerator RepeatedLoadAndCancelLeavesOneBoardAndOriginalAuthorData()
        {
            string hash = LevelJson.Hash(lab);
            for (int i = 0; i < 10; i++)
            {
                runner.LoadLevel(lab); runner.TryMove(Direction.E); yield return null;
            }
            runner.Restart(); yield return new WaitForSeconds(1);
            Assert.That(Object.FindObjectsOfType<BoardView>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<RobotPresenter>().Length, Is.EqualTo(1));
            Assert.That(runner.Presenter.Busy, Is.False); Assert.That(runner.Completed, Is.False);
            Assert.That(LevelJson.Hash(lab), Is.EqualTo(hash));
        }
    }
}
