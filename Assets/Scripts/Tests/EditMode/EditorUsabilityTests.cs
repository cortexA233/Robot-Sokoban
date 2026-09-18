using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Domain;
using Sokoban.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Terrain = Sokoban.Domain.Terrain;

namespace Sokoban.Tests
{
    public sealed class EditorUsabilityTests
    {
        private LevelEditorWindow window;
        private LevelDocument document;
        private string draft;

        [UnitySetUp] public IEnumerator SetUp()
        {
            window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            document = window.Document;
            draft = "Library/SokobanDrafts/Usability_" + Guid.NewGuid().ToString("N") + ".json";
            document.draftPath = draft; document.filePath = ""; document.solution = null; document.savedSolution = "";
            document.level = LevelDocument.NewLevel(7, 7);
            document.level.playerSpawn = new PlayerSpawn { x = 1, z = 2, facing = "E" };
            document.level.crates = new[] { new CrateDefinition { id = "crate_a", x = 2, z = 2 }, new CrateDefinition { id = "crate_b", x = 4, z = 2 } };
            document.level.sockets = new[] { new SocketDefinition { id = "socket_a", x = 2, z = 2, isGoal = false }, new SocketDefinition { id = "goal", x = 5, z = 2, isGoal = true } };
            document.Border(); document.savedHash = LevelJson.Hash(document.level);
            window.Show(); window.position = new Rect(150, 130, 1060, 720); window.CreateGUI();
            yield return null;
        }

        [TearDown] public void TearDown()
        {
            if (window) { document.savedHash = LevelJson.Hash(document.level); window.RefreshLive(); window.Close(); }
            if (document) { Undo.ClearUndo(document); UnityEngine.Object.DestroyImmediate(document); }
            if (File.Exists(draft)) File.Delete(draft);
        }

        private void Click(int x, int z, bool release = true, int button = 0)
        {
            var tile = window.rootVisualElement.Q<Label>($"cell-{x}-{z}");
            using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = button, mousePosition = tile.worldBound.center })) tile.SendEvent(evt);
            if (release) using (var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = button, mousePosition = tile.worldBound.center })) tile.SendEvent(evt);
        }
        private IEnumerator Submit(string name)
        {
            var button = window.rootVisualElement.Q<Button>(name); Assert.That(button, Is.Not.Null, name);
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) button.SendEvent(evt);
            yield return null;
        }

        [UnityTest] public IEnumerator RepeatedCrossCellSelectionClearsOldObjectWithoutEditingOrUndo()
        {
            string before = LevelJson.Hash(document.level); int group = Undo.GetCurrentGroup();
            for (int i = 0; i < 3; i++)
            {
                Click(2, 2); Assert.That(window.SelectedObjectId, Is.EqualTo("crate_a"));
                Click(4, 2); Assert.That(window.SelectedCell, Is.EqualTo(new Cell(4, 2)));
                Assert.That(window.SelectedObjectId, Is.EqualTo("crate_b"));
                Click(3, 3); Assert.That(window.SelectedCell, Is.EqualTo(new Cell(3, 3)));
                Assert.That(window.SelectedObjectId, Is.Null);
                Assert.That(window.rootVisualElement.Q<Label>("selected-object"), Is.Null);
                yield return null;
            }
            Assert.That(LevelJson.Hash(document.level), Is.EqualTo(before)); Assert.That(document.IsDirty, Is.False);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group), "选择不得创建编辑撤销组。");
            Assert.That(File.Exists(draft), Is.False, "纯选择无需改写草稿。");
        }

        [UnityTest] public IEnumerator SidebarSelectionExitsPlacementAndSameCellPreservesSocket()
        {
            Click(2, 2); yield return Submit("brush-Crate"); yield return Submit("select-socket_a");
            Assert.That(window.CurrentBrush, Is.EqualTo(LevelBrush.Select));
            Click(2, 2); Assert.That(window.SelectedObjectId, Is.EqualTo("socket_a"));
            Assert.That(window.rootVisualElement.Q<Button>("select-socket_a").text, Does.StartWith("✓"));
            Assert.That(window.rootVisualElement.Q<Label>("cell-2-2").text, Does.Contain("s"));
            Click(4, 2); Assert.That(window.SelectedObjectId, Is.EqualTo("crate_b"));
            Click(1, 2); Assert.That(window.SelectedObjectId, Is.EqualTo("player"));
            Assert.That(window.rootVisualElement.Q<Button>("select-player"), Is.Not.Null);
            Assert.That(document.level.crates.Length, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest] public IEnumerator IllegalPlacementDoesNotLeaveSelectionOnPreviousCell()
        {
            Click(2, 2); yield return Submit("brush-Gate"); Click(1, 2);
            Assert.That(window.SelectedCell, Is.EqualTo(new Cell(1, 2)));
            Assert.That(window.SelectedObjectId, Is.EqualTo("player"));
            Assert.That(window.rootVisualElement.Q<Label>("selected-cell").text, Does.Contain(new Cell(1, 2).ToString()));
            Assert.That(document.level.gates, Is.Empty);
            Assert.That(document.level.crates.Length, Is.EqualTo(2));
            yield return Submit("select-player"); Click(2, 2); Click(4, 2);
            Assert.That(window.SelectedObjectId, Is.EqualTo("crate_b"));
            yield return null;
        }

        [UnityTest] public IEnumerator RemovedToolsStayAbsentAndRightClickDoesNotEdit()
        {
            Assert.That(window.rootVisualElement.Q<Button>("replay"), Is.Null);
            Assert.That(window.rootVisualElement.Q<Button>("brush-Erase"), Is.Null);
            Assert.That(window.rootVisualElement.Q("erase-layer"), Is.Null);
            Assert.That(window.rootVisualElement.Q<Button>("border").text, Is.EqualTo("一键生成边界墙"));
            Assert.That(window.rootVisualElement.Q<Button>("frame-board").text, Is.EqualTo("场景内相机聚焦"));
            window.SetBrush((LevelBrush)10); Assert.That(window.CurrentBrush, Is.EqualTo(LevelBrush.Select));
            yield return Submit("brush-Wall");
            string before = LevelJson.Hash(document.level); int group = Undo.GetCurrentGroup();
            Click(2, 2, button: 1); yield return null;
            Assert.That(LevelJson.Hash(document.level), Is.EqualTo(before));
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group));
            Assert.That(document.IsDirty, Is.False);
        }

        [UnityTest] public IEnumerator PaintingOverBoxKeepsSocketAndUndoRedoRestoresKinds()
        {
            yield return Submit("brush-CargoCrate"); Click(2, 2); yield return null;
            Assert.That(window.SelectedObjectId, Is.EqualTo("crate_a"));
            Assert.That(((CrateDefinition)document.Find("crate_a")).kind, Is.EqualTo(CrateDefinition.Cargo));
            Assert.That(document.Find("socket_a"), Is.Not.Null);
            Assert.That(document.level.crates.Length, Is.EqualTo(2));
            yield return Submit("undo"); Assert.That(((CrateDefinition)document.Find("crate_a")).kind, Is.EqualTo(CrateDefinition.Energy));
            yield return Submit("redo"); Assert.That(((CrateDefinition)document.Find("crate_a")).kind, Is.EqualTo(CrateDefinition.Cargo));
            yield return Submit("select-crate_a"); yield return Submit("delete-entity");
            Assert.That(document.Find("crate_a"), Is.Null); Assert.That(document.Find("socket_a"), Is.Not.Null);
        }

        [UnityTest] public IEnumerator ObjectButtonReleasesPointerBeforePropertiesAreRebuilt()
        {
            Click(2, 2);
            var button = window.rootVisualElement.Q<Button>("select-socket_a");
            using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = button.worldBound.center })) button.SendEvent(evt);
            using (var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = button.worldBound.center })) button.SendEvent(evt);
            yield return null;
            Assert.That(PointerCaptureHelper.GetCapturingElement(window.rootVisualElement.panel, PointerId.mousePointerId), Is.Null);
            Assert.That(window.SelectedObjectId, Is.EqualTo("socket_a"));
            Click(4, 2); yield return null;
            Assert.That(window.SelectedCell, Is.EqualTo(new Cell(4, 2)));
            Assert.That(window.SelectedObjectId, Is.EqualTo("crate_b"));
        }

        [UnityTest] public IEnumerator NewMouseDownEndsLostStrokeAndUndoOnlyRevertsLastStroke()
        {
            yield return Submit("brush-Wall"); Click(1, 1, false); Click(2, 1);
            Assert.That(document.level.TerrainAt(new Cell(1, 1)), Is.EqualTo(Terrain.Wall));
            Assert.That(document.level.TerrainAt(new Cell(2, 1)), Is.EqualTo(Terrain.Wall));
            Undo.PerformUndo(); yield return null;
            Assert.That(document.level.TerrainAt(new Cell(1, 1)), Is.EqualTo(Terrain.Wall));
            Assert.That(document.level.TerrainAt(new Cell(2, 1)), Is.EqualTo(Terrain.Floor));
        }

        [UnityTest] public IEnumerator DirectPlayDisplaysFreshErrorsAndDoesNotEnterPlayOrWriteSnapshot()
        {
            const string snapshot = "Library/SokobanDrafts/Playtest.json";
            string before = File.Exists(snapshot) ? File.ReadAllText(snapshot) : null;
            document.level.playerSpawn = null; yield return Submit("playtest");
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            Assert.That(window.LastValidation.IsValid, Is.False);
            Assert.That(window.LastValidation.Issues.Any(i => i.Message.Contains("玩家")), Is.True);
            Assert.That(window.rootVisualElement.Q<ScrollView>("validation-issues").childCount, Is.GreaterThan(1));
            Assert.That(window.rootVisualElement.Q("editor-notice").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            document.level.playerSpawn = new PlayerSpawn { x = 1, z = 2, facing = "E" }; document.level.crates = Array.Empty<CrateDefinition>();
            yield return Submit("playtest");
            Assert.That(window.LastValidation.Issues.Any(i => i.Message.Contains("必须放置一个玩家")), Is.False);
            Assert.That(window.LastValidation.Issues.Any(i => i.Message.Contains("能源箱")), Is.True);
            Assert.That(File.Exists(snapshot) ? File.ReadAllText(snapshot) : null, Is.EqualTo(before));
            double until = EditorApplication.timeSinceStartup + 6.3;
            while (EditorApplication.timeSinceStartup < until) yield return null;
            Assert.That(window.rootVisualElement.Q("editor-notice").style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(window.rootVisualElement.Q<ScrollView>("validation-issues").childCount, Is.GreaterThan(1));
        }

        [UnityTest] public IEnumerator TerrainBrushUndoRedoRestoresEditedContentInWindow()
        {
            yield return Submit("brush-LowFriction"); Click(3, 3); yield return null;
            string painted = LevelJson.Hash(document.level);
            Assert.That(document.level.TerrainAt(new Cell(3, 3)), Is.EqualTo(Terrain.LowFriction));
            yield return Submit("undo");
            Assert.That(document.level.TerrainAt(new Cell(3, 3)), Is.EqualTo(Terrain.Floor));
            yield return Submit("redo");
            Assert.That(document.level.TerrainAt(new Cell(3, 3)), Is.EqualTo(Terrain.LowFriction));
            Assert.That(LevelJson.Hash(document.level), Is.EqualTo(painted));
            string saved = draft + ".level.json";
            try
            {
                document.Save(saved, false); document.Open(saved); window.RefreshLive();
                Assert.That(LevelJson.Hash(document.level), Is.EqualTo(painted));
                Assert.That(window.rootVisualElement.Q<Label>("cell-3-3").text, Is.EqualTo("≋"));
            }
            finally { if (File.Exists(saved)) File.Delete(saved); }
        }

        [UnityTest] public IEnumerator WarningsAreAllowedAndLocatedErrorsSelectCorrectObject()
        {
            yield return Submit("validate-level"); Assert.That(window.LastValidation.IsValid, Is.True);
            Assert.That(window.LastValidation.Issues.Any(i => !i.IsError), Is.True);
            document.level.gates = new[] { new GateDefinition { id = "gate", x = 3, z = 3, facing = "N", powerMode = "Any", sourceSocketIds = Array.Empty<string>() } };
            yield return Submit("validate-level");
            var error = window.rootVisualElement.Q<ScrollView>("validation-issues").Query<Button>().ToList().First(b => b.text.Contains("供电插槽"));
            error.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) error.SendEvent(evt);
            yield return null;
            Assert.That(window.SelectedCell, Is.EqualTo(new Cell(3, 3))); Assert.That(window.SelectedObjectId, Is.EqualTo("gate"));
            yield return null;
        }
    }
}
