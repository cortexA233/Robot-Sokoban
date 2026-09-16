using KToolkit;
using UnityEngine.UI;

namespace Sokoban.UI
{
    [KUI_Info("screens/Settings", nameof(SettingsPage))]
    public sealed class SettingsPage : StationPage
    {
        private Slider volume, sensitivity;
        private Toggle invert;
        public override void OnStart()
        {
            volume = Get<Slider>("Content/Volume");
            sensitivity = Get<Slider>("Content/Sensitivity");
            invert = Get<Toggle>("Content/Invert");
            volume.onValueChanged.AddListener(_ => Apply());
            sensitivity.onValueChanged.AddListener(_ => Apply());
            invert.onValueChanged.AddListener(_ => Apply());
            Bind("Content/Back", UI.CloseSettings);
        }
        public override void Refresh()
        {
            volume.SetValueWithoutNotify(UI.Preferences.Volume);
            sensitivity.SetValueWithoutNotify(UI.Preferences.Sensitivity);
            invert.SetIsOnWithoutNotify(UI.Preferences.InvertY);
            Get<Text>("Content/VolumeValue").text = $"{UI.Preferences.Volume:P0}";
            Get<Text>("Content/SensitivityValue").text = $"{UI.Preferences.Sensitivity:F2} ×";
        }
        private void Apply()
        {
            UI.Preferences.Set(volume.value, sensitivity.value, invert.isOn, Runner.Cameras);
            Refresh();
        }
    }
}
