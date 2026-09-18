using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using Sokoban.Editor;
using UnityEditor;
using UnityEngine;

namespace Sokoban.Tests
{
    public sealed class CargoAuthoringTests
    {
        private LevelDocument document;
        [SetUp] public void SetUp()
        {
            document = ScriptableObject.CreateInstance<LevelDocument>();
            document.draftPath = "Library/SokobanDrafts/Cargo_" + Guid.NewGuid().ToString("N") + ".json";
            document.level = LevelJson.Read(Resources.Load<TextAsset>("configs/test_levels/LAB01_LowFriction").text);
        }
        [TearDown] public void TearDown()
        {
            if (File.Exists(document.draftPath)) File.Delete(document.draftPath);
            Undo.ClearUndo(document); UnityEngine.Object.DestroyImmediate(document);
        }

        [Test] public void LegacyLevelKeepsEnergyMeaningAndOriginalProofHash()
        {
            var proof = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/LAB01_LowFriction.solution").text);
            Assert.That(document.level.schemaVersion, Is.EqualTo(1));
            Assert.That(document.level.crates.All(c => c.IsEnergy), Is.True);
            Assert.That(LevelJson.Write(document.level), Does.Not.Contain("\"kind\""));
            Assert.That(LevelJson.Hash(document.level), Is.EqualTo(proof.contentHash));
            Assert.DoesNotThrow(() => proof.Verify(document.level, true));
        }

        [Test] public void CargoBrushUpgradesLegacyDataAndUndoRestoresOriginalVersion()
        {
            string before = LevelJson.Hash(document.level); string id = null;
            Assert.Throws<InvalidOperationException>(() => document.Place(LevelBrush.CargoCrate, new Cell(-1, 0), "N"));
            document.Change("放普通箱", () => id = document.Place(LevelBrush.CargoCrate, new Cell(1, 1), "N"));
            Assert.That(document.level.schemaVersion, Is.EqualTo(2));
            Assert.That(((CrateDefinition)document.Find(id)).kind, Is.EqualTo(CrateDefinition.Cargo));
            Undo.PerformUndo(); Assert.That(document.level.schemaVersion, Is.EqualTo(1));
            Assert.That(LevelJson.Hash(document.level), Is.EqualTo(before));
            Undo.PerformRedo(); Assert.That(((CrateDefinition)document.Find(id)).kind, Is.EqualTo(CrateDefinition.Cargo));
        }

        [Test] public void TypeEditInvalidatesProofAndSurvivesDraftAndCopy()
        {
            document.solution = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/LAB01_LowFriction.solution").text);
            document.Change("改普通箱", () => document.SetCrateKind("crate_01", CrateDefinition.Cargo));
            Assert.That(LevelJson.Hash(document.level), Is.Not.EqualTo(document.solution.contentHash));
            document.RestoreDraft();
            Assert.That(document.level.crates[0].kind, Is.EqualTo(CrateDefinition.Cargo));
            var copy = document.level.Copy(); copy.crates[0].kind = CrateDefinition.Energy;
            Assert.That(document.level.crates[0].kind, Is.EqualTo(CrateDefinition.Cargo));
            Assert.That(LevelJson.Read(LevelJson.Write(document.level)).crates[0].kind, Is.EqualTo(CrateDefinition.Cargo));
            Undo.PerformUndo(); Assert.DoesNotThrow(() => document.solution.Verify(document.level, true));
        }

        [TestCase("L04", 1)] [TestCase("L05", 2)] [TestCase("L06", 2)]
        public void TypedRecipeUsesAuthorOperationsAndPreservesReplay(string id, int cargoCount)
        {
            RecipeImporter.ImportInto(document, File.ReadAllText("Docs/LevelRecipes/" + id + ".json"));
            Assert.That(document.level.schemaVersion, Is.EqualTo(2));
            Assert.That(document.level.crates.Count(c => c.kind == CrateDefinition.Cargo), Is.EqualTo(cargoCount));
            Assert.That(document.filePath, Is.Empty);
            document.RestoreDraft(); Assert.That(document.level.id, Is.EqualTo(id));
            Assert.DoesNotThrow(() => document.solution.Verify(document.level, true));
        }

        [TestCase("missingKind", "\"Cargo\"")] [TestCase("kind", "null")] [TestCase("kind", "1")]
        public void TypedJsonCannotSilentlyDefaultMissingOrInvalidType(string field, string value)
        {
            document.SetCrateKind("crate_01", CrateDefinition.Cargo);
            string json = LevelJson.Write(document.level).Replace("\"kind\": \"Cargo\"", "\"" + field + "\": " + value);
            Assert.Throws<FormatException>(() => LevelJson.Read(json));
        }

        [Test] public void LegacyJsonRejectsUnexpectedKindAndRecipeFailurePreservesDraft()
        {
            string original = LevelJson.Write(document.level);
            string bad = original.Replace("\"id\": \"crate_01\"", "\"id\": \"crate_01\", \"kind\": \"Cargo\"");
            Assert.Throws<FormatException>(() => LevelJson.Read(bad));
            Assert.Throws<FormatException>(() => RecipeImporter.ImportInto(document, "{\"definition\":" + bad + ",\"commands\":\"E\"}"));
            Assert.That(LevelJson.Write(document.level), Is.EqualTo(original));
        }
    }
}
