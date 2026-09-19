using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Sokoban.Tests
{
    public sealed class RedirectorGameplayTests
    {
        private LevelRunner runner;
        [UnitySetUp] public IEnumerator SetUp()
        {
            LevelRunner.PlaytestDefinition = null;
            runner = new GameObject("Redirector gameplay test").AddComponent<LevelRunner>(); runner.Progress = new PlayerProgress(() => null, _ => { }); yield return null;
            runner.enabled = false; runner.LoadLevel(Load("L07")); yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        { if (runner) Object.Destroy(runner.gameObject); yield return null; }
        private static LevelDefinition Load(string id) => LevelJson.Read(Resources.Load<TextAsset>("configs/levels/" + id).text);
        private IEnumerator Walk(string commands)
        {
            foreach (char c in commands)
            {
                Assert.That(runner.TryMove((Direction)Enum.Parse(typeof(Direction), c.ToString())), Is.True);
                float deadline = Time.realtimeSinceStartup + 3;
                while (runner.Presenter.Busy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(runner.Presenter.Busy, Is.False);
            }
        }
        private static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        { var delta = b - a; return Vector3.Distance(p, a + delta * Mathf.Clamp01(Vector3.Dot(p - a, delta) / delta.sqrMagnitude)); }

        [UnityTest] public IEnumerator VisibleBoxFollowsEveryCornerAndRobotStopsAfterFirstStep()
        {
            yield return Walk("SEE");
            var resolution = runner.Session.Rules.Resolve(runner.Session.State, Direction.N);
            Assert.That(runner.TryMove(Direction.N), Is.True);
            int samples = 0;
            while (runner.Presenter.Busy)
            {
                Vector3 actual = runner.Board.Crates["crate_A"].position;
                float error = resolution.Microsteps.Min(s => SegmentDistance(actual, BoardView.Position(s.CrateFrom), BoardView.Position(s.CrateTo)));
                Assert.That(error, Is.LessThan(.002f), "Box cut across a corner instead of following the track.");
                samples++; yield return null;
            }
            Assert.That(samples, Is.GreaterThan(5));
            Assert.That(runner.Board.Crates["crate_A"].position, Is.EqualTo(new Vector3(5, 0, 4)));
            Assert.That(runner.Board.Robot.transform.position, Is.EqualTo(new Vector3(3, 0, 2)));
            Assert.That(runner.Session.State.Pushes, Is.EqualTo(1));
            var view = runner.Board.Redirectors.Values.Single(); Assert.That(view.Facing, Is.EqualTo(Direction.E));
            var mesh = view.transform.Find("Direction and edge arrows").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertices.Count(v => Mathf.Abs(v.x) > .4f), Is.GreaterThan(0), "Edge arrows must remain outside the crate.");
        }

        [UnityTest] public IEnumerator UndoDuringTheTurnRestoresWholeCommand() => CancelDuringTurn(false);
        [UnityTest] public IEnumerator RestartDuringTheTurnRestoresInitialBoard() => CancelDuringTurn(true);
        private IEnumerator CancelDuringTurn(bool restart)
        {
            yield return Walk("SEE"); var before = runner.Session.State;
            Assert.That(runner.TryMove(Direction.N), Is.True); yield return new WaitForSeconds(.75f);
            if (restart) runner.Restart(); else runner.Undo();
            yield return new WaitForSeconds(.8f);
            Assert.That(runner.Board.Crates["crate_A"].position, Is.EqualTo(new Vector3(3, 0, 2)));
            Assert.That(runner.Session.State.Pushes, Is.Zero); Assert.That(runner.Presenter.Busy, Is.False);
            Assert.That(runner.Session.Commands, Is.EqualTo(restart ? "" : "SEE"));
            if (!restart) Assert.That(runner.Session.State, Is.SameAs(before));
        }

        [UnityTest] public IEnumerator PauseAndCameraSwitchDuringTurnResumeToTheSameGoal()
        {
            runner.ToggleCamera();
            yield return Walk("SEE"); runner.TryMove(Direction.N); yield return new WaitForSeconds(.75f);
            runner.ToggleCamera(); // Camera input belongs to gameplay, before the pause menu takes input.
            runner.SetPaused(true); var position = runner.Board.Crates["crate_A"].position;
            yield return new WaitForSeconds(.3f);
            Assert.That(runner.Board.Crates["crate_A"].position, Is.EqualTo(position));
            runner.SetPaused(false);
            while (runner.Presenter.Busy) yield return null;
            yield return Walk("SEENNN");
            Assert.That(runner.Completed, Is.True, "Resume must finish the reference route.");
            Assert.That(runner.Cameras.Output.orthographic, Is.True, "The in-flight camera switch must survive pause/resume.");
            Assert.That(runner.Session.State.Pushes, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator LargestBoardFitsTopViewAndRepeatedRebuildReleasesMeshes()
        {
            for (int i = 0; i < 5; i++) { runner.LoadLevel(Load("L12")); yield return null; }
            Assert.That(runner.Cameras.TopDown, Is.True); yield return new WaitForSeconds(.4f);
            foreach (var corner in new[] { new Vector3(-.5f, 0, -.5f), new Vector3(8.5f, 0, -.5f), new Vector3(-.5f, 0, 8.5f), new Vector3(8.5f, 0, 8.5f) })
            {
                var viewport = runner.Cameras.Output.WorldToViewportPoint(corner);
                Assert.That(viewport.x, Is.InRange(0f, 1f)); Assert.That(viewport.y, Is.InRange(0f, 1f)); Assert.That(viewport.z, Is.GreaterThan(0));
            }
            Assert.That(Object.FindObjectsOfType<RedirectorView>().Length, Is.EqualTo(2));
            Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name == "Fixed redirector arrows"), Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator CircuitTextIsAbsentBeforeAndAfterPowerHandoff()
        {
            foreach (int prefix in new[] { 0, 35 })
            {
                runner.LoadLevel(Load("L12"));
                var proof = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/L12.solution").text);
                foreach (char command in proof.commands.Take(prefix))
                    Assert.That(runner.Session.Move((Direction)Enum.Parse(typeof(Direction), command.ToString())).Accepted, Is.True);
                runner.Board.Restore(runner.Session); runner.Cameras.Snap();
                yield return new WaitForSeconds(.4f);
                Assert.That(runner.Board.Circuits.GetComponentsInChildren<UnityEngine.UI.Text>(true), Is.Empty);
                Assert.That(runner.Board.Circuits.GetComponentsInChildren<Canvas>(true), Is.Empty);
                Assert.That(runner.Board.Redirectors.Count, Is.EqualTo(2));
            }
        }
    }
}
