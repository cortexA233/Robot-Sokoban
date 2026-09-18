using KToolkit;
using UnityEngine.UI;

namespace Sokoban.UI
{
    [KUI_Info("screens/MainMenu", nameof(MainMenuPage))]
    public sealed class MainMenuPage : StationPage
    {
        public override void OnStart()
        {
            Bind("Content/Actions/Start", () => UI.EnterLevel(Runner.ContinueIndex >= 0 ? Runner.ContinueIndex : Runner.InitialCampaignIndex));
            Bind("Content/Actions/Levels", UI.OpenLevelSelection);
            Bind("Content/Actions/Settings", UI.OpenSettings);
            Bind("Content/Actions/Help", UI.OpenHelp);
            Bind("Content/Actions/NewGame", () => UI.EnterLevel(0));
            Bind("Content/Actions/Retry", Runner.RetryProgressSave);
            Bind("Content/Actions/Quit", UI.Quit);
        }
        public override void Refresh()
        {
            int recent = Runner.ContinueIndex;
            Get<Text>("Content/Message").text = Runner.Error ?? Runner.Progress.Status;
            Get<Text>("Content/Summary").text = $"{Runner.CompletedLevelCount} / {Runner.CampaignLevelCount} 已完成";
            Get<Text>("Content/ContinueHint").text = recent >= 0 ? $"从第 {recent + 1:00} 关起点继续" : "";
            Get<Text>("Content/Actions/Start/Label").text = recent >= 0 ? "继续游戏" : "开始游戏";
            Get<Button>("Content/Actions/Start").interactable = Runner.CampaignLevelCount > 0;
            transform.Find("Content/Actions/NewGame").gameObject.SetActive(recent >= 0);
            transform.Find("Content/Actions/Retry").gameObject.SetActive(Runner.Progress.PendingSave);
        }
    }
}
