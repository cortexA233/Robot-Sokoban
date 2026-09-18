using System;
using System.IO;
using NUnit.Framework;
using Sokoban.Domain;
using UnityEngine;

namespace Sokoban.Tests
{
    public sealed class PlayerProgressTests
    {
        private LevelDefinition level;
        private string saved;
        private PlayerProgress progress;
        [SetUp] public void SetUp()
        {
            level = LevelJson.Read(Resources.Load<TextAsset>("configs/test_levels/LAB01_LowFriction").text);
            saved = null; progress = new PlayerProgress(() => saved, json => saved = json);
        }
        private GameSession Solved() => SolutionRecord.ReplayCommands(level, "E");

        [Test] public void FirstLaunchVisitCompletionAndReloadKeepStableIdentity()
        {
            Assert.That(progress.RecentIndex(new[] { level }), Is.EqualTo(-1));
            Assert.That(progress.Best(level), Is.Null);
            progress.Visit(level); progress.Complete(level, Solved());
            var reloaded = new PlayerProgress(() => saved, _ => { });
            Assert.That(reloaded.RecentIndex(new[] { level }), Is.Zero);
            Assert.That(reloaded.Best(level).moves, Is.EqualTo(1));
            Assert.That(reloaded.Best(level).pushes, Is.EqualTo(1));
            var copy = reloaded.Best(level); copy.moves = 999;
            Assert.That(reloaded.Best(level).moves, Is.EqualTo(1), "Queries must not expose mutable persisted state.");
        }
        [Test] public void BestScoreRemainsOneRunRatherThanIndependentMinima()
        {
            progress.Complete(level, Solved());
            saved = saved.Replace("\"moves\": 1", "\"moves\": 3").Replace("\"pushes\": 1", "\"pushes\": 0");
            progress = new PlayerProgress(() => saved, json => saved = json);
            progress.Complete(level, Solved());
            Assert.That(progress.Best(level).moves, Is.EqualTo(1));
            Assert.That(progress.Best(level).pushes, Is.EqualTo(1));
            saved = saved.Replace("\"pushes\": 1", "\"pushes\": 0");
            progress = new PlayerProgress(() => saved, json => saved = json);
            progress.Complete(level, Solved());
            Assert.That(progress.Best(level).pushes, Is.Zero, "More pushes at equal moves must not replace a best run.");
        }
        [Test] public void ReorderRemovalAndRevisionDoNotMisattributeScores()
        {
            progress.Complete(level, Solved());
            var other = level.Copy(); other.id = "other";
            Assert.That(progress.RecentIndex(new[] { other, level }), Is.EqualTo(1));
            Assert.That(progress.RecentIndex(new[] { other }), Is.EqualTo(-1));
            level.playerSpawn.facing = level.playerSpawn.facing == "N" ? "S" : "N";
            Assert.That(progress.Best(level), Is.Null);
            Assert.That(progress.HasOlderScore(level), Is.True);
            Assert.That(progress.RecentIndex(new[] { level }), Is.Zero, "Continue starts the updated level from its start.");
        }
        [TestCase("{")]
        [TestCase("{}")]
        [TestCase("{\"schemaVersion\":2,\"recentLevelId\":\"x\",\"scores\":[]}")]
        [TestCase("{\"schemaVersion\":1,\"recentLevelId\":\"x\",\"scores\":[{}]}")]
        public void CorruptOrUnsupportedDataDoesNotBlockPlaying(string json)
        {
            progress = new PlayerProgress(() => json, text => saved = text);
            Assert.That(progress.Status, Is.Not.Empty);
            Assert.That(progress.Best(level), Is.Null);
            progress.Complete(level, Solved());
            Assert.That(new PlayerProgress(() => saved, _ => { }).Best(level).moves, Is.EqualTo(1));
        }
        [Test] public void FailedSaveRetainsInMemoryResultAndCanRetry()
        {
            bool fail = true;
            progress = new PlayerProgress(() => null, text => { if (fail) throw new IOException("Read only"); saved = text; });
            progress.Complete(level, Solved());
            Assert.That(progress.PendingSave, Is.True);
            Assert.That(progress.Best(level).moves, Is.EqualTo(1));
            fail = false; Assert.That(progress.TrySave(), Is.True);
            Assert.That(progress.PendingSave, Is.False);
            Assert.That(new PlayerProgress(() => saved, _ => { }).Best(level).pushes, Is.EqualTo(1));
        }
        [Test] public void FileRecoveryKeepsTheUnreadableOriginalAndLastGoodBackup()
        {
            string folder = Path.Combine("Library/SokobanProgressTests", Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "progress.json");
            try
            {
                progress = new PlayerProgress(path); progress.Complete(level, Solved());
                var other = level.Copy(); other.id = "other"; progress.Visit(other);
                File.WriteAllText(path, "unreadable original");
                var recovered = new PlayerProgress(path);
                Assert.That(recovered.Best(level).moves, Is.EqualTo(1));
                Assert.That(recovered.Status, Does.Contain("备份"));
                recovered.Visit(level);
                Assert.That(File.ReadAllText(Directory.GetFiles(folder, "*.unreadable-*")[0]), Is.EqualTo("unreadable original"));
                Assert.That(new PlayerProgress(path).Best(level).moves, Is.EqualTo(1));
                string original = File.ReadAllText(path);
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    recovered.Visit(other);
                Assert.That(recovered.PendingSave, Is.True);
                Assert.That(File.ReadAllText(path), Is.EqualTo(original));
                Assert.That(recovered.TrySave(), Is.True);
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
        [Test] public void IncompleteAndDebugSessionsCannotCreateScores()
        {
            progress.Complete(level, new GameSession(level)); Assert.That(saved, Is.Null);
            var session = Solved();
            string error;
            Assert.That(session.TryApplyDebugEdit(DebugBoardEdit.Place(DebugBoardEdit.PlayerId, session.State.Player, Direction.N), out error), Is.True);
            progress.Complete(level, session);
            Assert.That(saved, Is.Null);
        }
    }
}
