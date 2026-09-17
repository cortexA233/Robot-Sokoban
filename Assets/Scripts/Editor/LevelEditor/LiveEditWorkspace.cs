using System;
using System.IO;
using System.Linq;
using Sokoban.Domain;
using UnityEditor;
using UnityEngine;

namespace Sokoban.Editor
{
    // Owns a separate author document. The serialized definition keeps its wire
    // version; current placements are materialized only in this temporary draft.
    public sealed class LiveEditWorkspace : ScriptableObject
    {
        public const string RecoveryPath = "Library/SokobanDrafts/Live/Workspace.json";
        [SerializeField] private LevelDocument draft;
        [SerializeField] private LevelDocument author;
        [SerializeField] private string sessionId, sourceJson, authorHash, authorSolution, draftPath, appliedHash;
        [SerializeField] private int revision;
        [NonSerialized] private LevelRunner runner;
        public LevelDocument Draft => draft;
        public LevelDocument Author => author;
        public LevelRunner Runner => runner;
        public bool IsAttached => runner && runner.Session != null && runner.SessionId == sessionId && Application.isPlaying;
        public bool Editing => IsAttached && runner.LiveEditing;
        public bool HasUnappliedChanges => draft && LevelJson.Hash(draft.level) != appliedHash;
        public string SourceHash => LevelJson.Hash(LevelJson.Read(sourceJson));
        public string BackupPath => draftPath;

        public static LiveEditWorkspace Capture(LevelRunner source, LevelDocument authorDocument)
        {
            if (!source || source.Session == null) throw new InvalidOperationException("当前没有可捕获局面。");
            var workspace = CreateInstance<LiveEditWorkspace>(); workspace.hideFlags = HideFlags.HideAndDontSave;
            workspace.runner = source; workspace.author = authorDocument;
            workspace.authorHash = authorDocument?.level == null ? "" : LevelJson.Hash(authorDocument.level);
            workspace.authorSolution = Proof(authorDocument);
            workspace.sourceJson = LevelJson.Write(source.Definition);
            workspace.sessionId = source.SessionId; workspace.revision = source.Revision;
            workspace.draftPath = "Library/SokobanDrafts/Live/" + Guid.NewGuid().ToString("N") + ".json";
            workspace.draft = CreateInstance<LevelDocument>(); workspace.draft.hideFlags = HideFlags.HideAndDontSave;
            workspace.draft.isLiveDraft = true; workspace.draft.draftPath = workspace.draftPath;
            workspace.draft.level = BoardSnapshot.Capture(source.Definition, source.Session.State).ApplyTo(source.Definition);
            workspace.appliedHash = LevelJson.Hash(workspace.draft.level);
            workspace.draft.savedHash = workspace.appliedHash;
            try { workspace.Backup(); source.BeginLiveEditing(); return workspace; }
            catch { DestroyImmediate(workspace.draft); DestroyImmediate(workspace); throw; }
        }
        private static string Proof(LevelDocument document) => document?.solution == null ? "" : JsonUtility.ToJson(document.solution);
        public void Resume()
        {
            if (!IsAttached) throw new InvalidOperationException("来源会话已结束；草稿可保存为副本。请从当前游戏重新捕获。 ");
            runner.BeginLiveEditing(); revision = runner.Revision;
        }
        public bool Apply(out string error)
        {
            error = null;
            if (!Editing) { error = "请先进入现场编辑；原草稿保留。"; return false; }
            var report = LevelValidator.ValidateLive(draft.level);
            if (!report.IsValid) { error = string.Join("\n", report.Issues.Where(i => i.IsError)); return false; }
            if (!runner.TryApplyLiveDraft(draft.level.Copy(), BoardSnapshot.FromDefinition(draft.level), sessionId, revision, out error)) return false;
            revision = runner.Revision; appliedHash = LevelJson.Hash(draft.level); Backup(); return true;
        }
        public void ReleaseInput() { if (IsAttached && runner.LiveEditing) runner.SuspendLiveEditing(); }
        public bool End(out string error)
        {
            error = null;
            if (IsAttached && !runner.EndLiveSandbox(out error)) return false;
            runner = null; Backup(); return true;
        }
        public void BringBack()
        {
            if (!author || author.level == null || author.level.id != draft.level.id || LevelJson.Hash(author.level) != authorHash || Proof(author) != authorSolution)
                throw new InvalidOperationException("原作者文档不匹配或已另行修改，请另存副本。双方内容均已保留。");
            ValidateAuthor();
            author.Change("带回现场关卡修改", () => author.level = draft.level.Copy());
            authorHash = LevelJson.Hash(author.level); authorSolution = Proof(author); Backup();
        }
        public void SaveCopy(string path)
        {
            ValidateAuthor();
            if (string.IsNullOrEmpty(path)) return;
            if (author && !string.IsNullOrEmpty(author.filePath) && string.Equals(Path.GetFullPath(path), Path.GetFullPath(author.filePath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("副本不能覆盖原作者文件。");
            var copy = draft.level.Copy(); copy.id = "level_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            LevelJson.AtomicWrite(path, LevelJson.Write(copy)); AssetDatabase.Refresh();
        }
        private void ValidateAuthor()
        {
            var report = LevelValidator.Validate(draft.level);
            if (!report.IsValid) throw new InvalidOperationException("转为作者关卡前请修复：\n" + string.Join("\n", report.Issues.Where(i => i.IsError)));
        }
        public void SaveDraft() { draft.savedHash = LevelJson.Hash(draft.level); draft.savedSolution = Proof(draft); Backup(); }
        public void Backup()
        {
            if (!draft || draft.level == null) return;
            draft.draftPath = draftPath; draft.Backup();
            LevelJson.AtomicWrite(RecoveryPath, JsonUtility.ToJson(new Recovery { path = draftPath, source = sourceJson,
                session = sessionId, revision = revision, applied = appliedHash, authorHash = authorHash, authorSolution = authorSolution }, true));
        }
        public void RestorePaths()
        {
            if (draft) { draft.isLiveDraft = true; draft.draftPath = draftPath; if (File.Exists(draftPath)) draft.RestoreDraft(); }
            // Rebind only to the exact live instance. A domain reload normally gives
            // the runner a new identity; keeping the draft detached is intentional.
            runner = UnityEngine.Object.FindObjectsOfType<LevelRunner>().FirstOrDefault(r => r.SessionId == sessionId && r.Session != null);
        }
        public static LiveEditWorkspace Recover(LevelDocument authorDocument)
        {
            var data = JsonUtility.FromJson<Recovery>(File.ReadAllText(RecoveryPath));
            if (data == null || string.IsNullOrEmpty(data.path) || !File.Exists(data.path)) throw new InvalidOperationException("现场草稿备份不存在。");
            // Recovery is data-only and never attaches a stale draft to a game.
            var workspace = CreateInstance<LiveEditWorkspace>(); workspace.hideFlags = HideFlags.HideAndDontSave;
            workspace.author = authorDocument; workspace.authorHash = data.authorHash; workspace.authorSolution = data.authorSolution;
            workspace.sourceJson = data.source; workspace.sessionId = data.session; workspace.revision = data.revision;
            workspace.appliedHash = data.applied; workspace.draftPath = data.path;
            workspace.draft = CreateInstance<LevelDocument>(); workspace.draft.hideFlags = HideFlags.HideAndDontSave;
            workspace.draft.isLiveDraft = true; workspace.draft.draftPath = data.path; workspace.draft.RestoreDraft(); return workspace;
        }
        [Serializable] private sealed class Recovery
        { public string path, source, session, applied, authorHash, authorSolution; public int revision; }
    }

    [InitializeOnLoad]
    public static class LiveEditBridge
    {
        static LiveEditBridge()
        {
            LevelRunner.LiveEditRequested -= Open; LevelRunner.LiveEditRequested += Open;
            LevelRunner.LiveCrateRequested -= Crate; LevelRunner.LiveCrateRequested += Crate;
        }
        private static void Open(LevelRunner runner) => LevelEditorWindow.OpenWindow().CaptureLive(runner);
        private static void Crate(LevelRunner runner, string kindOrId, Cell cell, bool delete)
        {
            var window = LevelEditorWindow.OpenWindow();
            if (!window.CaptureLive(runner)) return;
            try
            {
                window.Document.Change(delete ? "删除现场箱子" : "增加现场箱子", () =>
                {
                    if (delete) window.Document.level.crates = window.Document.level.crates.Where(c => c.id != kindOrId).ToArray();
                    else window.Document.Place(kindOrId == CrateDefinition.Cargo ? LevelBrush.CargoCrate : LevelBrush.Crate, cell, "N");
                });
                window.RefreshLive();
            }
            catch (Exception exception) { Debug.LogWarning(exception.Message); }
        }
    }
}
