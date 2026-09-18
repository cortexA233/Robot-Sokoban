using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sokoban.Editor
{
    public sealed class CampaignCatalogWindow : EditorWindow
    {
        private ScrollView list, problems;
        private Label state;
        private HelpBox feedback;
        private ObjectField candidate;

        [MenuItem("Sokoban_Tools/Campaign Catalog")]
        public static CampaignCatalogWindow OpenWindow() => GetWindow<CampaignCatalogWindow>("正式关卡目录");
        private void OnEnable() { minSize = new Vector2(700, 480); Undo.undoRedoPerformed += Refresh; EditorApplication.projectChanged += Refresh; EditorApplication.playModeStateChanged += OnPlayMode; }
        private void OnDisable() { Undo.undoRedoPerformed -= Refresh; EditorApplication.projectChanged -= Refresh; EditorApplication.playModeStateChanged -= OnPlayMode; }
        private void OnPlayMode(PlayModeStateChange change) => Refresh();
        public void CreateGUI()
        {
            rootVisualElement.Clear(); rootVisualElement.style.paddingLeft = 10; rootVisualElement.style.paddingRight = 10;
            rootVisualElement.Add(new Label("正式关卡目录") { style = { fontSize = 18, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } });
            rootVisualElement.Add(new Label("此顺序用于游戏选关与下一关。移出只修改目录引用，关卡文件与解法仍保留；Ctrl+Z 可撤销。") { style = { whiteSpace = WhiteSpace.Normal } });
            var toolbar = Row(rootVisualElement);
            AddButton(toolbar, "保存目录", () => { CampaignAuthoring.Save(CampaignAuthoring.Catalog); Refresh(); Show("目录已保存。"); }, "save-catalog");
            AddButton(toolbar, "校验目录与构建资源", ValidateCatalog, "validate-catalog");
            AddButton(toolbar, "撤销", Undo.PerformUndo, "catalog-undo"); AddButton(toolbar, "重做", Undo.PerformRedo, "catalog-redo");
            state = new Label { name = "catalog-status" }; rootVisualElement.Add(state);
            var addRow = Row(rootVisualElement);
            candidate = new ObjectField("加入关卡 JSON") { objectType = typeof(TextAsset), allowSceneObjects = false, name = "catalog-candidate", style = { flexGrow = 1 } }; addRow.Add(candidate);
            AddButton(addRow, "使用当前作者关卡", UseCurrent, "use-author-level");
            AddButton(addRow, "加入目录", () => { CampaignAuthoring.Add(CampaignAuthoring.Catalog, candidate.value as TextAsset); Refresh(); Show("已加入目录；请保存目录。", false); }, "add-catalog-level");
            feedback = new HelpBox("加入前须保存关卡，并具有对应当前内容的有效参考解法。开发测试关不会自动加入。", HelpBoxMessageType.Info); rootVisualElement.Add(feedback);
            list = new ScrollView { name = "catalog-entries", style = { flexGrow = 1, minHeight = 160 } }; rootVisualElement.Add(list);
            problems = new ScrollView { name = "catalog-problems", style = { maxHeight = 150 } }; rootVisualElement.Add(problems);
            Refresh();
        }
        private static VisualElement Row(VisualElement parent)
        { var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 6, marginBottom = 6 } }; parent.Add(row); return row; }
        private void AddButton(VisualElement parent, string text, Action action, string name)
        {
            parent.Add(new Button(() => rootVisualElement.schedule.Execute(() => { try { action(); } catch (Exception error) { Show(error.Message, true); } })) { text = text, name = name, style = { minHeight = 26 } });
        }
        private void Show(string message, bool error = false) { feedback.text = message; feedback.messageType = error ? HelpBoxMessageType.Error : HelpBoxMessageType.Info; }
        private void UseCurrent()
        {
            var window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().FirstOrDefault();
            if (!window || window.Document.isLiveDraft) throw new InvalidOperationException("请先在关卡编辑器打开作者关卡。");
            if (window.Document.IsDirty || string.IsNullOrEmpty(window.Document.filePath)) throw new InvalidOperationException("当前作者文档尚未保存；请先保存关卡与参考解法。");
            string fullPath = Path.GetFullPath(window.Document.filePath).Replace('\\', '/');
            string project = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
            if (!fullPath.StartsWith(project, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("请先将关卡另存到当前工程的 Assets 文件夹内。");
            candidate.value = AssetDatabase.LoadAssetAtPath<TextAsset>(fullPath.Substring(project.Length));
            if (!candidate.value) throw new InvalidOperationException("关卡尚未导入为 TextAsset，请检查保存位置。");
        }
        private void Refresh()
        {
            if (list == null) return;
            var catalog = CampaignAuthoring.Catalog; list.Clear(); problems.Clear();
            if (!catalog) { state.text = "正式目录资源缺失：" + CampaignAuthoring.CatalogPath; return; }
            bool editable = !EditorApplication.isPlayingOrWillChangePlaymode;
            state.text = (EditorUtility.IsDirty(catalog) ? "● 未保存" : "已保存") + " · " + CampaignAuthoring.CatalogPath + (editable ? "" : " · 试玩中只读");
            var report = CampaignAuthoring.Validate(catalog);
            for (int i = 0; i < report.Entries.Count; i++)
            {
                int index = i; var entry = report.Entries[i];
                var row = Row(list); row.name = "catalog-entry-" + i;
                var details = new VisualElement { style = { flexGrow = 1, minWidth = 320 } }; row.Add(details);
                details.Add(new Label($"{i + 1}. " + (entry.Level == null ? "无效关卡" : entry.Level.id) +
                    (entry.AssetPath.Contains("/test_levels/") ? "（开发测试关，已显式加入）" : "")) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                details.Add(new Label("结构：" + entry.Structure + "  |  解法：" + entry.Solution) { style = { whiteSpace = WhiteSpace.Normal } });
                AddButton(row, "打开", () => LevelEditorWindow.OpenWindow().OpenAsset(entry.AssetPath), "catalog-open-" + i);
                AddButton(row, "↑", () => { CampaignAuthoring.Move(catalog, index, index - 1); Refresh(); }, "catalog-up-" + i);
                AddButton(row, "↓", () => { CampaignAuthoring.Move(catalog, index, index + 1); Refresh(); }, "catalog-down-" + i);
                AddButton(row, "移出", () => { CampaignAuthoring.Remove(catalog, index); Refresh(); Show("已移出目录，文件保留；请保存目录。"); }, "catalog-remove-" + i);
                row.Q<Button>("catalog-open-" + i).SetEnabled(editable && entry.Asset);
                row.Q<Button>("catalog-up-" + i).SetEnabled(editable && i > 0);
                row.Q<Button>("catalog-down-" + i).SetEnabled(editable && i < report.Entries.Count - 1);
                row.Q<Button>("catalog-remove-" + i).SetEnabled(editable);
            }
            foreach (string name in new[] { "save-catalog", "catalog-undo", "catalog-redo", "use-author-level", "add-catalog-level" }) rootVisualElement.Q<Button>(name)?.SetEnabled(editable);
            DrawProblems(report);
        }
        private void ValidateCatalog()
        {
            Refresh(); var report = CampaignBuildGuard.Inspect(CampaignAuthoring.Catalog);
            DrawProblems(report); Show(report.IsValid ? report.Summary : $"发现 {report.Problems.Count(p => p.IsError)} 个错误，详见下方列表。", !report.IsValid);
        }
        private void DrawProblems(CatalogValidation report)
        {
            problems.Clear();
            foreach (var issue in report.Problems)
            {
                var captured = issue;
                var button = new Button(() =>
                {
                    if (captured.AssetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(captured.AssetPath))
                        LevelEditorWindow.OpenWindow().OpenAsset(captured.AssetPath, captured.Cell);
                    else EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(captured.AssetPath));
                }) { text = (issue.IsError ? "错误 · " : "提示 · ") + issue, style = { whiteSpace = WhiteSpace.Normal, minHeight = 25 } };
                problems.Add(button);
            }
        }
    }
}
