using KToolkit;
using UnityEngine.UI;

namespace Sokoban.UI
{
    [KUI_Info("screens/Hud", nameof(HudPage))]
    public sealed class HudPage : StationPage
    {
        public override void OnStart()
        {
            Bind("Shortcuts/Undo", Runner.Undo);
            Bind("Shortcuts/Restart", Runner.Restart);
            Bind("Shortcuts/View", Runner.ToggleCamera);
            Bind("Header/Pause", () => Runner.SetPaused(true));
        }
        public override void Refresh()
        {
            if (Runner.Session == null) return;
            Get<Text>("Header/Title").text = (Runner.IsPlaytest ? "试玩  " : (Runner.CampaignIndex + 1).ToString("00") + "  ") + Runner.Definition.title;
            Get<Text>("Header/Power").text = $"供电  {Runner.PoweredGoalCount}/{Runner.GoalCount}";
            Get<Text>("Header/Moves").text = $"移动  {Runner.Session.State.Moves}";
            Get<Text>("Header/Pushes").text = $"推动  {Runner.Session.State.Pushes}";
            Get<Text>("Message").text = Runner.Message;
            Get<Text>("ViewLabel").text = Runner.Cameras.TopDown ? "N ↑  俯视 · V 第三人称" : "第三人称 · 鼠标环绕 · V 俯视";
            Get<Button>("Shortcuts/Undo").interactable = Runner.Session.UndoCount > 0;
        }
    }
}
