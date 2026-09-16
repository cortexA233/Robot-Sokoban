using System;
using System.Linq;
using Sokoban.Domain;
using UnityEngine;

namespace Sokoban
{
    [Serializable]
    public sealed class SolutionRecord
    {
        public int schemaVersion = 1;
        public string levelId;
        public string contentHash;
        public string commands;
        public int expectedMoves;
        public int expectedPushes;
        public Cell expectedPlayer;
        public Cell[] expectedCrateCells;

        public static SolutionRecord Capture(LevelDefinition level, GameSession session)
        {
            if (!session.State.Completed) throw new InvalidOperationException("尚未通关，不能作为参考解法。");
            var replay = ReplayCommands(level, session.Commands);
            if (!replay.State.Completed) throw new InvalidOperationException("录制未到达完成状态。");
            return new SolutionRecord
            {
                levelId = level.id, contentHash = LevelJson.Hash(level), commands = session.Commands,
                expectedMoves = replay.State.Moves, expectedPushes = replay.State.Pushes,
                expectedPlayer = replay.State.Player, expectedCrateCells = replay.State.Crates.Values.OrderBy(c => c).ToArray()
            };
        }

        public static GameSession ReplayCommands(LevelDefinition level, string commands)
        {
            if (commands == null) throw new FormatException("解法命令缺失。");
            var session = new GameSession(level);
            for (int i = 0; i < commands.Length; i++)
            {
                if (!LevelValidator.IsFacing(commands[i].ToString())) throw new FormatException($"第 {i + 1} 条不是 N/E/S/W。");
                var result = session.Move((Direction)Enum.Parse(typeof(Direction), commands[i].ToString()));
                if (!result.Accepted) throw new InvalidOperationException($"第 {i + 1} 条 {commands[i]} 被拒绝：{result.RejectReason}；玩家 {session.State.Player}。");
            }
            return session;
        }

        public string Verify(LevelDefinition level, bool requireCurrentHash)
        {
            if (schemaVersion != 1 || levelId != level.id) throw new InvalidOperationException("解法版本或关卡 ID 不匹配。");
            if (requireCurrentHash && contentHash != LevelJson.Hash(level)) throw new InvalidOperationException("参考解法已过期，请重新回放。");
            var replay = ReplayCommands(level, commands);
            if (!replay.State.Completed) throw new InvalidOperationException("全部命令结束后仍未通关。");
            if (replay.State.Moves != expectedMoves || replay.State.Pushes != expectedPushes || replay.State.Player != expectedPlayer ||
                expectedCrateCells == null || !replay.State.Crates.Values.OrderBy(c => c).SequenceEqual(expectedCrateCells.OrderBy(c => c)))
                throw new InvalidOperationException("解法终点或移动/推动计数不匹配。");
            return $"回放通过：{replay.State.Moves} 步 / {replay.State.Pushes} 次推动。";
        }
        public SolutionRecord Copy() => JsonUtility.FromJson<SolutionRecord>(JsonUtility.ToJson(this));
    }
}
