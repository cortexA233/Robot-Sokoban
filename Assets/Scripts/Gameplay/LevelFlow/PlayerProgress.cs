using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sokoban.Domain;
using UnityEngine;

namespace Sokoban
{
    // Progress owns persistence and score comparison; puzzle sessions never write files.
    public sealed class PlayerProgress
    {
        [Serializable] public sealed class Score
        {
            public string levelId, contentHash;
            public int moves, pushes;
            public Score Copy() => (Score)MemberwiseClone();
        }
        [Serializable] private sealed class Document
        {
            public int schemaVersion = 1;
            public string recentLevelId = "";
            public List<Score> scores = new List<Score>();
        }

        private Document document = new Document();
        private readonly Action<string> write;
        public bool PendingSave { get; private set; }
        public string Status { get; private set; } = "";
        public string RecentLevelId => document.recentLevelId;
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "player-progress.json");

        public PlayerProgress(string path)
        {
            path = Path.GetFullPath(path);
            string lastGood = null;
            bool preserveUnreadable = false;
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    document = Parse(json); lastGood = json;
                }
                else if (File.Exists(path + ".bak"))
                {
                    string json = File.ReadAllText(path + ".bak");
                    document = Parse(json); lastGood = json; PendingSave = true;
                    Status = "已从备份恢复进度。";
                }
            }
            catch (Exception)
            {
                preserveUnreadable = File.Exists(path);
                try
                {
                    string json = File.ReadAllText(path + ".bak");
                    document = Parse(json); lastGood = json;
                    Status = "已从备份恢复进度。";
                }
                catch (Exception) { Status = "进度无法读取，已使用空记录；原文件会保留。"; }
                PendingSave = true;
            }
            write = json =>
            {
                // Keep unreadable/newer files for recovery before replacing anything.
                if (preserveUnreadable)
                {
                    File.Copy(path, path + ".unreadable-" + Guid.NewGuid().ToString("N"));
                    preserveUnreadable = false;
                }
                if (lastGood != null) LevelJson.AtomicWrite(path + ".bak", lastGood);
                LevelJson.AtomicWrite(path, json);
                lastGood = json;
            };
        }

        // A small persistence seam lets tests and previews avoid the player's real file.
        public PlayerProgress(Func<string> read, Action<string> write)
        {
            this.write = write ?? throw new ArgumentNullException(nameof(write));
            try
            {
                string json = read();
                if (!string.IsNullOrEmpty(json)) document = Parse(json);
            }
            catch (Exception) { Status = "进度无法读取，已使用空记录。"; PendingSave = true; }
        }

        public int RecentIndex(IReadOnlyList<LevelDefinition> levels)
        {
            for (int i = 0; i < levels.Count; i++) if (levels[i].id == RecentLevelId) return i;
            return -1;
        }
        public Score Best(LevelDefinition level)
        {
            string hash = LevelJson.Hash(level);
            return document.scores.FirstOrDefault(s => s.levelId == level.id && s.contentHash == hash)?.Copy();
        }
        public bool HasOlderScore(LevelDefinition level) => document.scores.Any(s => s.levelId == level.id) && Best(level) == null;
        public void Visit(LevelDefinition level)
        {
            if (document.recentLevelId != level.id) { document.recentLevelId = level.id; PendingSave = true; }
            TrySave();
        }
        public void Complete(LevelDefinition level, GameSession session)
        {
            if (!session.State.Completed || !session.ReferenceReplayValid) return;
            var best = Best(level);
            int moves = session.State.Moves, pushes = session.State.Pushes;
            if (best == null || moves < best.moves || (moves == best.moves && pushes < best.pushes))
            {
                document.scores.RemoveAll(s => s.levelId == level.id);
                document.scores.Add(new Score { levelId = level.id, contentHash = LevelJson.Hash(level), moves = moves, pushes = pushes });
                PendingSave = true;
            }
            Visit(level);
        }
        public bool TrySave()
        {
            if (!PendingSave) return true;
            try
            {
                write(JsonConvert.SerializeObject(document, Formatting.Indented) + "\n");
                PendingSave = false; Status = ""; return true;
            }
            catch (Exception)
            {
                Status = "进度暂未保存，可返回主菜单重试。";
                return false;
            }
        }

        private static Document Parse(string json)
        {
            var token = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (token.Count != 3 || token["schemaVersion"]?.Type != JTokenType.Integer || (int)token["schemaVersion"] != 1 ||
                token["recentLevelId"]?.Type != JTokenType.String || !(token["scores"] is JArray scores))
                throw new FormatException("Unknown or incomplete progress format.");
            var result = new Document { recentLevelId = (string)token["recentLevelId"] };
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in scores)
            {
                if (!(entry is JObject score) || score.Count != 4 || score["levelId"]?.Type != JTokenType.String ||
                    score["contentHash"]?.Type != JTokenType.String || score["moves"]?.Type != JTokenType.Integer || score["pushes"]?.Type != JTokenType.Integer)
                    throw new FormatException("Invalid score fields.");
                var value = score.ToObject<Score>();
                if (string.IsNullOrWhiteSpace(value.levelId) || !ids.Add(value.levelId) || value.contentHash.Length != 64 ||
                    !value.contentHash.All(Uri.IsHexDigit) || value.moves < 0 || value.pushes < 0 || value.pushes > value.moves)
                    throw new FormatException("Invalid score values.");
                result.scores.Add(value);
            }
            return result;
        }
    }
}
