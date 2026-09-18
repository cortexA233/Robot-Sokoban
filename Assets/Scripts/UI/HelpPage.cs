using KToolkit;

namespace Sokoban.UI
{
    [KUI_Info("screens/Help", nameof(HelpPage))]
    public sealed class HelpPage : StationPage
    {
        public override void OnStart() => Bind("Content/Back", UI.CloseHelp);
    }
}
