using System;
using System.Collections;
using System.IO;
using KToolkit;
using NUnit.Framework;
using Sokoban.Domain;
using Sokoban.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Sokoban.Tests
{
    public sealed class PlayerProgressFlowTests
    {
        private LevelRunner runner;
        private string folder, path;
        private float timeScale;
        [UnitySetUp] public IEnumerator SetUp()
        {
            timeScale = Time.timeScale; Time.timeScale = 4;
            folder = Path.GetFullPath("Library/SokobanProgressTests/Flow_" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(folder, "progress.json");
            LevelRunner.PlaytestDefinition = null;
            yield return Boot();
        }
        private IEnumerator Boot()
        {
            runner = new GameObject("Progress flow test").AddComponent<LevelRunner>();
            runner.Progress = new PlayerProgress(path);
            yield return null; runner.enabled = false;
            Assert.That(runner.Error, Is.Null);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (runner) Object.Destroy(runner.gameObject);
            yield return null;
            LevelRunner.PlaytestDefinition = null; Time.timeScale = timeScale;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        private IEnumerator Solve()
        {
            var proof = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/" + runner.Definition.id + ".solution").text);
            foreach (char command in proof.commands)
            {
                Assert.That(runner.TryMove((Direction)Enum.Parse(typeof(Direction), command.ToString())), Is.True);
                float deadline = Time.realtimeSinceStartup + 3;
                while (runner.Presenter.Busy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(runner.Presenter.Busy, Is.False);
            }
            Assert.That(runner.Completed, Is.True);
        }
        [UnityTest, Timeout(30000)] public IEnumerator CampaignCompletionSurvivesRestartAndContinueStartsFresh()
        {
            runner.SelectLevel(3); yield return Solve();
            var level = runner.Definition.Copy();
            Assert.That(runner.Progress.Best(level).moves, Is.EqualTo(runner.Session.State.Moves));
            Assert.That(runner.CompletedLevelCount, Is.EqualTo(1));
            Assert.That(((CompletionPage)KUIManager.instance.GetFirstUIWithType<CompletionPage>()).transform.Find("Content/Best").GetComponent<Text>().text, Does.Contain("最佳"));
            Object.Destroy(runner.gameObject); yield return null; yield return Boot();
            Assert.That(runner.ContinueIndex, Is.EqualTo(3));
            Assert.That(((MainMenuPage)KUIManager.instance.GetFirstUIWithType<MainMenuPage>()).transform.Find("Content/Actions/Start/Label").GetComponent<Text>().text, Is.EqualTo("继续游戏"));
            Assert.That(runner.UI.EnterLevel(runner.ContinueIndex), Is.True);
            float deadline = Time.realtimeSinceStartup + 4;
            while (runner.UI.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(runner.UI.IsTransitioning, Is.False);
            Assert.That(runner.Definition.id, Is.EqualTo(level.id));
            Assert.That(runner.Session.State.Moves, Is.Zero);
            Assert.That(runner.Completed, Is.False);
            Assert.That(runner.GetCampaignStatus(3), Does.StartWith("已完成"));
        }
        [UnityTest] public IEnumerator SelectionStatusesAreEmptyOrValidBestMovesAfterDiskReload()
        {
            var levels = Resources.Load<CampaignCatalog>("configs/CampaignCatalog").ReadLevels();
            Assert.That(runner.GetCampaignStatus(0), Is.Empty);
            runner.SelectLevel(0); Assert.That(runner.GetCampaignStatus(0), Is.Empty, "A visit alone is not completion.");
            string hash = LevelJson.Hash(levels[0]);
            LevelJson.AtomicWrite(path, "{\"schemaVersion\":1,\"recentLevelId\":\"" + levels[0].id + "\",\"scores\":[{\"levelId\":\"" + levels[0].id +
                "\",\"contentHash\":\"" + hash + "\",\"moves\":10,\"pushes\":3},{\"levelId\":\"" + levels[1].id +
                "\",\"contentHash\":\"" + new string('0', 64) + "\",\"moves\":8,\"pushes\":2}]}");
            Object.Destroy(runner.gameObject); yield return null; yield return Boot();
            Assert.That(runner.GetCampaignStatus(0), Is.EqualTo("已完成 · 10 步"));
            Assert.That(runner.GetCampaignStatus(1), Is.Empty); Assert.That(runner.Progress.HasOlderScore(levels[1]), Is.True);
            Assert.That(runner.Progress.Best(levels[0]).pushes, Is.EqualTo(3)); Assert.That(runner.CompletedLevelCount, Is.EqualTo(1));
            runner.OpenLevelSelect(); yield return null;
            var page = (LevelSelectPage)KUIManager.instance.GetFirstUIWithType<LevelSelectPage>();
            Assert.That(page.transform.Find("Content/Summary").GetComponent<Text>().text, Is.EqualTo("1 / " + levels.Length + " 已完成"));
            for (int i = 0; i < levels.Length; i++)
            {
                var row = page.transform.Find("Content/List/Viewport/Rows/Level" + (i + 1).ToString("00"));
                Assert.That(row.GetComponent<Button>().interactable, Is.True);
                Assert.That(row.Find("Status").GetComponent<Text>().text, Is.EqualTo(i == 0 ? "已完成 · 10 步" : ""));
            }
        }

        [UnityTest, Timeout(30000)] public IEnumerator DebugEditsAndLiveSandboxCannotPublishScores()
        {
            runner.SelectLevel(3); string error;
            var facing = runner.Session.State.Facing == Direction.N ? Direction.S : Direction.N;
            Assert.That(runner.TryApplyDebugEdit(DebugBoardEdit.Place(DebugBoardEdit.PlayerId, runner.Session.State.Player, facing), out error), Is.True);
            yield return Solve(); Assert.That(runner.CompletedLevelCount, Is.Zero);
            runner.SelectLevel(3); runner.BeginLiveEditing(); runner.SuspendLiveEditing();
            Assert.That(runner.IsLiveSandbox, Is.True);
            yield return Solve(); Assert.That(runner.CompletedLevelCount, Is.Zero);
            Assert.That(runner.EndLiveSandbox(out error), Is.True);
            Assert.That(new PlayerProgress(path).Best(runner.Definition), Is.Null);
        }
        [UnityTest, Timeout(30000)] public IEnumerator AuthorAndDirectLoadsDoNotWriteFormalProgress()
        {
            runner.LoadLevel(LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L07").text));
            yield return Solve(); Assert.That(File.Exists(path), Is.False);
            Object.Destroy(runner.gameObject); yield return null;
            LevelRunner.PlaytestDefinition = LevelJson.Read(Resources.Load<TextAsset>("configs/test_levels/LAB01_LowFriction").text);
            yield return Boot(); Assert.That(runner.IsPlaytest, Is.True);
            yield return Solve(); Assert.That(File.Exists(path), Is.False);
        }
    }
}
