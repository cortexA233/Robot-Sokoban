using KToolkit;
using UnityEngine;
using UnityEngine.UI;

namespace Sokoban.UI
{
    [KUI_Info("screens/Hud", nameof(HudPage))]
    public sealed class HudPage : StationPage
    {
        private string lastNotice;
        private float noticeUntil;
        public override void OnStart()
        {
            Bind("Shortcuts/Undo", Runner.Undo);
            Bind("Shortcuts/Restart", Runner.Restart);
            Bind("Shortcuts/View", Runner.ToggleCamera);
            Bind("Header/Pause", () => Runner.SetPaused(true));
            Bind("Shortcuts/Help", UI.OpenHelp);
        }
        public override void Refresh()
        {
            if (Runner.Session == null) return;
            Get<Text>("Header/Stage").text = Runner.LevelLabel;
            Get<Text>("Header/Power").text = $"目标供电  {Runner.PoweredGoalCount}/{Runner.GoalCount}";
            Get<Text>("Header/Moves").text = $"移动  {Runner.Session.State.Moves}";
            Get<Text>("Header/Pushes").text = $"推动  {Runner.Session.State.Pushes}";
            if (lastNotice != Runner.Message) { lastNotice = Runner.Message; noticeUntil = Time.unscaledTime + 4f; }
            Get<Text>("Notice/Text").text = Runner.Message;
            Get<Text>("Shortcuts/View/Label").text = Runner.Cameras.TopDown ? "V  第三人称" : "V  俯视";
            Get<Button>("Shortcuts/Undo").interactable = Runner.Session.UndoCount > 0;
            UpdateNotice();
        }
        public override void Update() { base.Update(); UpdateNotice(); }
        private void UpdateNotice() => transform.Find("Notice").gameObject.SetActive(!string.IsNullOrEmpty(lastNotice) && Time.unscaledTime < noticeUntil);
    }
}
