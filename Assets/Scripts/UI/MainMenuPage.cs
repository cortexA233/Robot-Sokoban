using KToolkit;
using UnityEngine.UI;

namespace Sokoban.UI
{
    [KUI_Info("screens/MainMenu", nameof(MainMenuPage))]
    public sealed class MainMenuPage : StationPage
    {
        public override void OnStart()
        {
            Bind("Content/Start", () => UI.EnterLevel(Runner.InitialCampaignIndex));
            Bind("Content/Levels", UI.OpenLevelSelection);
            Bind("Content/Settings", UI.OpenSettings);
            Bind("Content/Quit", UI.Quit);
        }
        public override void Refresh()
        {
            Get<Text>("Content/Message").text = Runner.Error ?? "";
            Get<Button>("Content/Start").interactable = Runner.CampaignLevelCount > 0;
        }
    }
}
