using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using Sokoban.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Terrain = Sokoban.Domain.Terrain;

namespace Sokoban.Tests
{
    public sealed class LiveEditorTests
    {
        private LevelRunner runner;
        private LevelDocument author;
        private readonly List<LiveEditWorkspace> workspaces = new List<LiveEditWorkspace>();
        private string recoveryBefore, output;
        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            recoveryBefore = File.Exists(LiveEditWorkspace.RecoveryPath) ? File.ReadAllText(LiveEditWorkspace.RecoveryPath) : null;
            output = "Library/SokobanDrafts/Live/Test_" + Guid.NewGuid().ToString("N") + ".json";
            LevelRunner.PlaytestDefinition = null;
            runner = Object.FindObjectOfType<LevelRunner>();
            if (!runner) { runner = new GameObject("Live editing test").AddComponent<LevelRunner>(); runner.Progress = new PlayerProgress(() => null, _ => { }); yield return null; }
            runner.Progress = new PlayerProgress(() => null, _ => { });
            runner.enabled = false; runner.SelectLevel(1);
            author = ScriptableObject.CreateInstance<LevelDocument>(); author.draftPath = output + ".author";
            author.level = runner.Definition.Copy(); author.savedHash = LevelJson.Hash(author.level);
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var workspace in workspaces)
            {
                if (!workspace) continue;
                workspace.ReleaseInput(); Undo.ClearUndo(workspace.Draft);
                string path = workspace.BackupPath;
                Object.DestroyImmediate(workspace.Draft); Object.DestroyImmediate(workspace);
                if (File.Exists(path)) File.Delete(path);
            }
            workspaces.Clear();
            if (author) { Undo.ClearUndo(author); Object.DestroyImmediate(author); }
            if (File.Exists(output)) File.Delete(output);
            if (File.Exists(output + ".author")) File.Delete(output + ".author");
            if (recoveryBefore != null) File.WriteAllText(LiveEditWorkspace.RecoveryPath, recoveryBefore);
            else if (File.Exists(LiveEditWorkspace.RecoveryPath)) File.Delete(LiveEditWorkspace.RecoveryPath);
            if (Application.isPlaying && runner) Object.Destroy(runner.gameObject); yield return null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        private LiveEditWorkspace Capture()
        { var workspace = LiveEditWorkspace.Capture(runner, author); workspaces.Add(workspace); return workspace; }

        [UnityTest] public IEnumerator CaptureUpgradeUndoAndRecoverKeepSourceAndKinds()
        {
            runner.LoadLevel(LevelJson.Read(Resources.Load<TextAsset>("configs/test_levels/LAB01_LowFriction").text));
            author.level = runner.Definition.Copy(); author.savedHash = LevelJson.Hash(author.level);
            author.Change("未保存作者修改", () => author.level.playerSpawn.facing = "W");
            string original = LevelJson.Write(author.level); string sourceHash = LevelJson.Hash(runner.Definition);
            var workspace = Capture(); var draft = workspace.Draft;
            Assert.That(workspace.SourceHash, Is.EqualTo(sourceHash)); Assert.That(draft.level.schemaVersion, Is.EqualTo(1));
            Assert.That(LevelJson.Hash(draft.level), Is.EqualTo(sourceHash));
            string id = draft.level.crates[0].id;
            draft.Change("改普通箱", () => draft.SetCrateKind(id, CrateDefinition.Cargo));
            Assert.That(draft.level.schemaVersion, Is.EqualTo(2));
            Undo.PerformUndo(); Assert.That(draft.level.schemaVersion, Is.EqualTo(1));
            Assert.That(LevelJson.Hash(draft.level), Is.EqualTo(sourceHash));
            Undo.PerformRedo(); workspace.Backup();
            var recovered = LiveEditWorkspace.Recover(author); workspaces.Add(recovered);
            Assert.That(recovered.IsAttached, Is.False);
            Assert.That(recovered.Draft.level.crates.Single(c => c.id == id).kind, Is.EqualTo(CrateDefinition.Cargo));
            Assert.That(LevelJson.Write(author.level), Is.EqualTo(original));
            Assert.That(author.IsDirty, Is.True); yield return null;
        }

        [UnityTest] public IEnumerator RepeatedTypeAndTerrainApplyReturnsExactOriginalSession()
        {
            var before = runner.Session.State; string original = LevelJson.Hash(author.level);
            var workspace = Capture(); string id = workspace.Draft.level.crates[0].id;
            var floor = FreeCell(workspace.Draft.level);
            for (int i = 0; i < 10; i++)
            {
                if (i > 0) workspace.Resume();
                string kind = i % 2 == 0 ? CrateDefinition.Cargo : CrateDefinition.Energy;
                workspace.Draft.Change("改箱型", () => workspace.Draft.SetCrateKind(id, kind));
                workspace.Draft.Change("改地形", () => workspace.Draft.Paint(floor, i % 2 == 0 ? '~' : '.'));
                Assert.That(workspace.Apply(out string error), Is.True, error);
                Assert.That(runner.Definition.crates.Single(c => c.id == id).kind, Is.EqualTo(kind));
                Assert.That(runner.Definition.TerrainAt(floor), Is.EqualTo(i % 2 == 0 ? Terrain.LowFriction : Terrain.Floor));
                Assert.That(runner.Session.UndoCount, Is.Zero); Assert.That(runner.Session.ReferenceReplayValid, Is.False);
                yield return null;
                Assert.That(runner.GetComponentsInChildren<BoardView>().Length, Is.EqualTo(1));
                Assert.That(runner.GetComponentsInChildren<CameraRig>().Length, Is.EqualTo(1));
            }
            Assert.That(workspace.End(out _), Is.True);
            Assert.That(runner.Session.State, Is.SameAs(before)); Assert.That(runner.CampaignIndex, Is.EqualTo(1));
            Assert.That(LevelJson.Hash(author.level), Is.EqualTo(original));
            Assert.That(runner.Session.ReferenceReplayValid, Is.True);
        }

        [UnityTest] public IEnumerator InvalidDraftAndChangedAuthorCannotOverwriteWorkingData()
        {
            var workspace = Capture(); var board = runner.Board; var before = runner.Session.State;
            workspace.Draft.Change("无效玩家", () => workspace.Draft.level.playerSpawn.x = -1);
            Assert.That(workspace.Apply(out _), Is.False); Assert.That(runner.Board, Is.SameAs(board));
            Assert.That(runner.Session.State, Is.SameAs(before)); Assert.Throws<InvalidOperationException>(() => workspace.SaveCopy(output));
            Assert.That(File.Exists(output), Is.False);
            Undo.PerformUndo(); workspace.SaveCopy(output);
            var copy = LevelJson.Read(File.ReadAllText(output)); Assert.That(copy.id, Is.Not.EqualTo(author.level.id));
            author.Change("独立作者修改", () => author.level.playerSpawn.facing = "S"); string changed = LevelJson.Write(author.level);
            Assert.Throws<InvalidOperationException>(() => workspace.BringBack());
            Assert.That(LevelJson.Write(author.level), Is.EqualTo(changed));
            Assert.That(runner.TryApplyLiveDraft(workspace.Draft.level, BoardSnapshot.FromDefinition(workspace.Draft.level), "stale", runner.Revision, out _), Is.False);
            yield return null;
        }

        [UnityTest] public IEnumerator WindowPaintRotateUndoAndLiveRestorePreserveRedirectors()
        {
            var window = ScriptableObject.CreateInstance<LevelEditorWindow>(); window.Show(); yield return null;
            try
            {
                var serialized = new SerializedObject(window); serialized.FindProperty("document").objectReferenceValue = author; serialized.ApplyModifiedPropertiesWithoutUndo();
                window.RefreshLive(); var original = runner.Session.State; string originalHash = LevelJson.Hash(runner.Definition);
                yield return Submit(window, "capture-live"); workspaces.Add(window.LiveWorkspace);
                var floor = FreeCell(window.Document.level);
                yield return Submit(window, "brush-Redirector");
                window.rootVisualElement.Q<PopupField<string>>("brush-facing").value = "E";
                Paint(window, floor); yield return null;
                Assert.That(window.Document.level.schemaVersion, Is.EqualTo(3));
                string id = window.Document.level.redirectors.Single().id;
                var facing = window.rootVisualElement.Q<PopupField<string>>("entity-facing");
                Assert.That(facing, Is.Not.Null); facing.value = "S"; yield return null;
                Assert.That(window.Document.level.redirectors.Single().facing, Is.EqualTo("S"));
                yield return Submit(window, "undo"); Assert.That(window.Document.level.redirectors.Single().facing, Is.EqualTo("E"));
                yield return Submit(window, "redo"); Assert.That(window.Document.level.redirectors.Single().facing, Is.EqualTo("S"));
                yield return Submit(window, "validate-level");
                yield return Submit(window, "apply-live");
                Assert.That(runner.Definition.redirectors.Single().facing, Is.EqualTo("S"));
                Assert.That(runner.Board.Redirectors[id].Facing, Is.EqualTo(Direction.S));
                window.LiveWorkspace.SaveCopy(output);
                var saved = LevelJson.Read(File.ReadAllText(output));
                Assert.That(saved.redirectors.Single().facing, Is.EqualTo("S"));
                var recovered = LiveEditWorkspace.Recover(author); workspaces.Add(recovered);
                Assert.That(recovered.Draft.level.redirectors.Single().facing, Is.EqualTo("S"));
                yield return Submit(window, "end-live");
                Assert.That(runner.Session.State, Is.SameAs(original));
                Assert.That(LevelJson.Hash(runner.Definition), Is.EqualTo(originalHash));
                Assert.That(runner.Board.Redirectors, Is.Empty);
            }
            finally { window.Close(); }
        }

        private static Cell FreeCell(LevelDefinition level) => Enumerable.Range(0, level.height)
            .SelectMany(z => Enumerable.Range(0, level.width).Select(x => new Cell(x, z)))
            .First(c => level.TerrainAt(c) == Terrain.Floor && level.playerSpawn.Cell != c &&
                !level.crates.Any(a => a.Cell == c) && !level.sockets.Any(a => a.Cell == c) && !level.gates.Any(a => a.Cell == c) && !level.redirectors.Any(a => a.Cell == c));
        private static IEnumerator Submit(LevelEditorWindow window, string name)
        {
            yield return null;
            var button = window.rootVisualElement.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name); Assert.That(button.enabledInHierarchy, Is.True, name);
            button.Focus(); using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt);
            yield return null;
        }
        private static void Paint(LevelEditorWindow window, Cell cell)
        {
            var tile = window.rootVisualElement.Q<Label>($"cell-{cell.x}-{cell.z}"); Assert.That(tile, Is.Not.Null);
            using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = tile.worldBound.center })) tile.SendEvent(evt);
            using (var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = tile.worldBound.center })) tile.SendEvent(evt);
        }
        [UnityTest] public IEnumerator WindowInputCapturesPaintsChangesKindAppliesAndReturns()
        {
            var window = ScriptableObject.CreateInstance<LevelEditorWindow>(); window.Show(); yield return null;
            try
            {
                var serialized = new SerializedObject(window); serialized.FindProperty("document").objectReferenceValue = author; serialized.ApplyModifiedPropertiesWithoutUndo();
                window.RefreshLive(); var original = runner.Session.State;
                yield return Submit(window, "capture-live"); workspaces.Add(window.LiveWorkspace);
                Assert.That(window.Document.isLiveDraft, Is.True); Assert.That(runner.LiveEditing, Is.True);
                var floor = FreeCell(window.Document.level);
                yield return Submit(window, "brush-LowFriction"); Paint(window, floor); yield return null;
                Assert.That(window.Document.level.TerrainAt(floor), Is.EqualTo(Terrain.LowFriction));
                yield return Submit(window, "apply-live"); Assert.That(runner.Definition.TerrainAt(floor), Is.EqualTo(Terrain.LowFriction));
                yield return Submit(window, "resume-live");
                var empty = FreeCell(window.Document.level);
                yield return Submit(window, "brush-CargoCrate"); Paint(window, empty); yield return null;
                string id = window.Document.level.crates.Single(c => c.Cell == empty).id;
                Assert.That(window.Document.level.crates.Single(c => c.id == id).kind, Is.EqualTo(CrateDefinition.Cargo));
                var kind = window.rootVisualElement.Q<PopupField<string>>("crate-kind"); Assert.That(kind, Is.Not.Null);
                kind.value = "能源箱"; yield return null;
                Assert.That(window.Document.level.crates.Single(c => c.id == id).kind, Is.EqualTo(CrateDefinition.Energy));
                yield return Submit(window, "undo"); Assert.That(window.Document.level.crates.Single(c => c.id == id).kind, Is.EqualTo(CrateDefinition.Cargo));
                yield return Submit(window, "apply-live"); Assert.That(runner.Definition.crates.Single(c => c.id == id).kind, Is.EqualTo(CrateDefinition.Cargo));
                window.LiveWorkspace.SaveCopy(output); Assert.That(File.Exists(output), Is.True);
                yield return Submit(window, "end-live"); Assert.That(runner.Session.State, Is.SameAs(original));
                Assert.That(window.Document, Is.SameAs(author));
                yield return Submit(window, "capture-live"); workspaces.Add(window.LiveWorkspace);
                Assert.That(runner.EndLiveSandbox(out _), Is.True);
                float deadline = Time.realtimeSinceStartup + 2;
                while (window.Document.isLiveDraft && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(window.Document, Is.SameAs(author), "Ending through the GM runtime API must also restore the author window.");
            }
            finally { window.Close(); }
        }
    }
}
