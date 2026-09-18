using KToolkit;
using UnityEngine.UI;

namespace Sokoban.UI
{
    [KUI_Info("screens/Pause", nameof(PausePage))]
    public sealed class PausePage : StationPage
    {
        public override void OnStart()
        {
            Bind("Content/Resume", () => Runner.SetPaused(false));
            Bind("Content/Restart", UI.Replay);
            Bind("Content/Levels", UI.OpenLevelSelection);
            Bind("Content/Settings", UI.OpenSettings);
            Bind("Content/Help", UI.OpenHelp);
            Bind("Content/Menu", () => UI.ReturnToMainMenu());
        }
        public override void Refresh()
        {
            Get<Text>("Content/Title").text = "暂停 · " + Runner.LevelLabel;
            transform.Find("Content/Levels").gameObject.SetActive(!Runner.IsPlaytest);
            transform.Find("Content/Menu").gameObject.SetActive(!Runner.IsPlaytest);
            transform.Find("Content/PlaytestHint").gameObject.SetActive(Runner.IsPlaytest);
        }
    }
}
