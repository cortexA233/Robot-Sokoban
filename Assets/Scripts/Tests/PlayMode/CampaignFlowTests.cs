using System.Collections;
using System;
using NUnit.Framework;
using Sokoban.Domain;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sokoban.Tests
{
    public sealed class CampaignFlowTests
    {
        private LevelRunner runner;
        [UnitySetUp] public IEnumerator SetUp()
        {
            LevelRunner.PlaytestDefinition = null;
            runner = new GameObject("Campaign flow test").AddComponent<LevelRunner>();
            yield return null;
            runner.enabled = false;
            Assert.That(runner.Session, Is.Null, "正式启动应先显示主菜单。");
            Assert.That(runner.SelectLevel(0), Is.True);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            LevelRunner.PlaytestDefinition = null;
            if (runner) UnityEngine.Object.Destroy(runner.gameObject);
            yield return null;
        }

        private IEnumerator SolveCurrentLevel()
        {
            var proof = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/" + runner.Definition.id + ".solution").text);
            foreach (char command in proof.commands)
            {
                Assert.That(runner.TryMove((Direction)Enum.Parse(typeof(Direction), command.ToString())), Is.True);
                if (runner.Session.State.Completed)
                {
                    Assert.That(runner.Completed, Is.False, "结算必须等动画完成。");
                    Assert.That(runner.NextLevel(), Is.False, "动画未结束不能跳关。");
                }
                float deadline = Time.realtimeSinceStartup + 3;
                while (runner.Presenter.Busy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(runner.Presenter.Busy, Is.False, "动作播放未结束。");
            }
            Assert.That(runner.Completed, Is.True);
        }

        [UnityTest, Timeout(120000)] public IEnumerator CompletingFirstLevelCanContinueToSecondWithFreshSession()
        {
            Assert.That(runner.Definition.id, Is.EqualTo("L01"));
            yield return SolveCurrentLevel();
            Assert.That(runner.CanGoNext, Is.True);
            Assert.That(runner.NextLevel(), Is.True);
            yield return null;
            Assert.That(runner.Definition.id, Is.EqualTo("L02"));
            Assert.That(runner.Session.State.Moves, Is.Zero);
            Assert.That(runner.Session.UndoCount, Is.Zero);
            Assert.That(runner.Session.Commands, Is.Empty);
            Assert.That(runner.Completed, Is.False);
            Assert.That(runner.Presenter.Busy, Is.False);
            Assert.That(UnityEngine.Object.FindObjectsOfType<BoardView>().Length, Is.EqualTo(1));
            yield return SolveCurrentLevel();
            Assert.That(runner.NextLevel(), Is.True);
            yield return null;
            Assert.That(runner.Definition.id, Is.EqualTo("L03"));
            Assert.That(runner.Session.State.Moves, Is.Zero); Assert.That(runner.Session.UndoCount, Is.Zero);
        }
        [UnityTest, Timeout(120000)] public IEnumerator LastLevelFinishesCampaignAndReturnsToSelection()
        {
            Assert.That(runner.SelectLevel(runner.CampaignLevelCount - 1), Is.True);
            yield return SolveCurrentLevel();
            Assert.That(runner.IsFinalCampaignLevel, Is.True);
            Assert.That(runner.CompletionHeading, Is.EqualTo("空间站已重启！"));
            Assert.That(runner.CanGoNext, Is.False); Assert.That(runner.NextLevel(), Is.False);
            Assert.That(runner.OpenLevelSelect(), Is.True);
            Assert.That(runner.LevelSelectionOpen, Is.True);
            Assert.That(runner.SelectLevel(0), Is.True);
            yield return null;
            Assert.That(runner.Definition.id, Is.EqualTo("L01"));
            Assert.That(runner.LevelSelectionOpen, Is.False); Assert.That(runner.Completed, Is.False);
            Assert.That(runner.Session.State.Moves, Is.Zero);
        }
        [UnityTest] public IEnumerator SelectionPausesThenResumesOrCancelsActiveCommandOnSwitch()
        {
            Assert.That(runner.NextLevel(), Is.False);
            Assert.That(runner.TryMove(Direction.S), Is.True);
            yield return new WaitForSeconds(.12f);
            Assert.That(runner.OpenLevelSelect(), Is.True);
            Vector3 before = runner.Board.Robot.transform.position;
            yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(before, runner.Board.Robot.transform.position), Is.LessThan(.001f));
            Assert.That(runner.TryMove(Direction.E), Is.False);
            runner.CloseLevelSelect();
            yield return new WaitForSeconds(.4f);
            Assert.That(runner.Paused, Is.False); Assert.That(runner.Presenter.Busy, Is.False);
            Assert.That(runner.Session.State.Moves, Is.EqualTo(1));
            runner.TryMove(Direction.W); runner.OpenLevelSelect();
            Assert.That(runner.SelectLevel(1), Is.True);
            yield return new WaitForSeconds(1);
            Assert.That(runner.Definition.id, Is.EqualTo("L02")); Assert.That(runner.Session.State.Moves, Is.Zero);
            Assert.That(runner.Completed, Is.False); Assert.That(runner.Presenter.Busy, Is.False);
            Assert.That(UnityEngine.Object.FindObjectsOfType<BoardView>().Length, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator ReturningFromSelectionPreservesPauseAndRejectsInvalidIndices()
        {
            var state = runner.Session.State;
            Assert.That(runner.SelectLevel(-1), Is.False); Assert.That(runner.SelectLevel(runner.CampaignLevelCount), Is.False);
            Assert.That(runner.Session.State, Is.SameAs(state));
            runner.SetPaused(true); runner.OpenLevelSelect(); runner.CloseLevelSelect();
            yield return null;
            Assert.That(runner.Paused, Is.True); Assert.That(runner.Session.State, Is.SameAs(state));
        }
        [UnityTest, Timeout(180000)] public IEnumerator OriginalThirdLevelContinuesThroughAllCargoLevels()
        {
            Assert.That(runner.CampaignLevelCount, Is.EqualTo(6));
            Assert.That(runner.SelectLevel(2), Is.True);
            Assert.That(runner.IsFinalCampaignLevel, Is.False);
            yield return SolveCurrentLevel();
            for (int index = 3; index < 6; index++)
            {
                Assert.That(runner.NextLevel(), Is.True);
                yield return null;
                Assert.That(runner.Definition.id, Is.EqualTo("L0" + (index + 1)));
                Assert.That(runner.Session.State.Moves, Is.Zero);
                Assert.That(runner.Session.UndoCount, Is.Zero);
                Assert.That(runner.Session.Commands, Is.Empty);
                foreach (var crate in runner.Definition.crates)
                    Assert.That(runner.Board.Crates[crate.id].Find(crate.IsEnergy ? "EnergyCrateRoot" : "CargoCrateRoot"), Is.Not.Null);
                runner.ToggleCamera();
                yield return SolveCurrentLevel();
                var proof = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/" + runner.Definition.id + ".solution").text);
                Assert.That(runner.Session.State.Moves, Is.EqualTo(proof.expectedMoves));
                Assert.That(runner.Session.State.Pushes, Is.EqualTo(proof.expectedPushes));
                Assert.That(runner.PoweredGoalCount, Is.EqualTo(runner.GoalCount));
            }
            Assert.That(runner.IsFinalCampaignLevel, Is.True); Assert.That(runner.NextLevel(), Is.False);
        }

        [UnityTest] public IEnumerator EditorPlaytestNeverNavigatesIntoCampaign()
        {
            UnityEngine.Object.Destroy(runner.gameObject); yield return null;
            LevelRunner.PlaytestDefinition = LevelJson.Read(Resources.Load<TextAsset>("configs/test_levels/LAB01_LowFriction").text);
            string hash = LevelJson.Hash(LevelRunner.PlaytestDefinition);
            runner = new GameObject("Preview flow test").AddComponent<LevelRunner>(); yield return null;
            runner.enabled = false; runner.TryMove(Direction.E); yield return new WaitForSeconds(1.1f);
            Assert.That(runner.Completed, Is.True); Assert.That(runner.IsPlaytest, Is.True);
            Assert.That(runner.NextLevel(), Is.False); Assert.That(runner.OpenLevelSelect(), Is.False);
            Assert.That(runner.SelectLevel(0), Is.False); Assert.That(runner.CampaignLevelCount, Is.Zero);
            Assert.That(LevelJson.Hash(LevelRunner.PlaytestDefinition), Is.EqualTo(hash));
        }
    }
}
