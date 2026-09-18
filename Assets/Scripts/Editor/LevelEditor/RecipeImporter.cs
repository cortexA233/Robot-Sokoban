using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sokoban.Domain;
using UnityEditor;
using UnityEngine;

namespace Sokoban.Editor
{
    [Serializable]
    public sealed class LevelRecipe
    {
        public LevelDefinition definition;
        public string commands;
    }

    public static class RecipeImporter
    {
        public static void ImportInto(LevelDocument document, string json)
        {
            var recipe = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (recipe["definition"] == null) throw new FormatException("配方缺少 definition。");
            var source = LevelJson.Read(recipe["definition"].ToString());
            if (recipe["commands"] != null && recipe["commands"].Type != JTokenType.String) throw new FormatException("配方 commands 必须是方向字符串。");
            string commands = (string)recipe["commands"];
            var report = LevelValidator.Validate(source);
            if (!report.IsValid) throw new FormatException(string.Join("\n", report.Issues.Where(i => i.IsError)));
            // Build in isolation. A failed import never replaces the author's current work.
            var temporary = ScriptableObject.CreateInstance<LevelDocument>();
            try
            {
                temporary.level = LevelDocument.NewLevel(source.width, source.height);
                temporary.level.schemaVersion = source.schemaVersion;
                temporary.draftPath = document.draftPath;
                temporary.level.id = source.id;
                for (int z = 0; z < source.height; z++)
                    for (int x = 0; x < source.width; x++) temporary.Paint(new Cell(x, z), source.terrainRows[source.height - z - 1][x]);
                temporary.Place(LevelBrush.Player, source.playerSpawn.Cell, source.playerSpawn.facing);
                foreach (var crate in source.crates)
                    temporary.Find(temporary.Place(crate.IsEnergy ? LevelBrush.Crate : LevelBrush.CargoCrate, crate.Cell, "N")).id = crate.id;
                foreach (var socket in source.sockets)
                    temporary.Find(temporary.Place(socket.isGoal ? LevelBrush.GoalSocket : LevelBrush.UtilitySocket, socket.Cell, "N")).id = socket.id;
                foreach (var gate in source.gates)
                {
                    var added = (GateDefinition)temporary.Find(temporary.Place(LevelBrush.Gate, gate.Cell, gate.facing));
                    added.id = gate.id; added.powerMode = gate.powerMode; temporary.SetGateSources(gate.id, gate.sourceSocketIds);
                }
                foreach (var redirector in source.redirectors)
                    temporary.Find(temporary.Place(LevelBrush.Redirector, redirector.Cell, redirector.facing)).id = redirector.id;
                temporary.level.decorations = source.decorations.Select(d => d.Copy()).ToArray();
                SolutionRecord solution = null;
                if (!string.IsNullOrEmpty(commands)) solution = SolutionRecord.Capture(temporary.level, SolutionRecord.ReplayCommands(temporary.level, commands));
                document.Change("导入策划配方", () =>
                {
                    document.level = temporary.level.Copy(); document.solution = solution;
                    document.filePath = ""; document.savedHash = ""; document.savedSolution = "";
                });
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        // [MenuItem("Tools/Sokoban/Import Design Recipes")]
        public static void ImportDesignRecipes()
        {
            foreach (string recipePath in Directory.GetFiles("Docs/LevelRecipes", "*.json").OrderBy(p => p))
            {
                var document = ScriptableObject.CreateInstance<LevelDocument>();
                try
                {
                    ImportInto(document, File.ReadAllText(recipePath));
                    string category = document.level.id.StartsWith("LAB", StringComparison.Ordinal) ? "test_levels" : "levels";
                    string path = $"Assets/Resources/configs/{category}/{document.level.id}.json";
                    if (File.Exists(path)) throw new IOException("关卡已存在，批量导入不会覆盖：" + path);
                    document.Save(path, false);
                    Debug.Log(document.level.id + ": " + document.solution.Verify(document.level, true));
                }
                finally { UnityEngine.Object.DestroyImmediate(document); }
            }
        }
    }
}
