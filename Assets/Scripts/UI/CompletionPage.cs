using KToolkit;
using UnityEngine.UI;

namespace Sokoban.UI
{
    [KUI_Info("screens/Completion", nameof(CompletionPage))]
    public sealed class CompletionPage : StationPage
    {
        public override void OnStart()
        {
            Bind("Content/Next", () => { if (Runner.IsFinalCampaignLevel) UI.OpenLevelSelection(); else UI.NextLevel(); });
            Bind("Content/Replay", UI.Replay);
            Bind("Content/Levels", UI.OpenLevelSelection);
            Bind("Content/Menu", () => UI.ReturnToMainMenu());
            Bind("Content/Undo", Runner.Undo);
            Bind("Content/Save", Runner.SaveReference);
        }
        public override void Refresh()
        {
            if (Runner.Session == null) return;
            Get<Text>("Content/Title").text = Runner.CompletionHeading;
            Get<Text>("Content/Stats").text = $"移动 {Runner.Session.State.Moves}  ·  推动 {Runner.Session.State.Pushes}";
            Get<Text>("Content/Message").text = Runner.Message == Runner.Definition.completionText ? "" : Runner.Message;
            transform.Find("Content/Next").gameObject.SetActive(!Runner.IsPlaytest);
            Get<Text>("Content/Next/Label").text = Runner.IsFinalCampaignLevel ? "返回选关" : "下一关";
            transform.Find("Content/Levels").gameObject.SetActive(!Runner.IsPlaytest && !Runner.IsFinalCampaignLevel);
            transform.Find("Content/Menu").gameObject.SetActive(!Runner.IsPlaytest);
            transform.Find("Content/Save").gameObject.SetActive(Runner.IsPlaytest);
            Get<Button>("Content/Save").interactable = Runner.Session.ReferenceReplayValid;
        }
    }
}
