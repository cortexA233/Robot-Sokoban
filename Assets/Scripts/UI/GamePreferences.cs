using UnityEngine;

namespace Sokoban.UI
{
    public sealed class GamePreferences
    {
        public const string KeyPrefix = "Sokoban.Settings.";
        public float Volume { get; private set; }
        public float Sensitivity { get; private set; }
        public bool InvertY { get; private set; }
        public GamePreferences()
        {
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyPrefix + "Volume", 1));
            Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(KeyPrefix + "Sensitivity", 1), .25f, 2.5f);
            InvertY = PlayerPrefs.GetInt(KeyPrefix + "InvertY", 0) != 0;
            Apply(null);
        }
        public void Set(float volume, float sensitivity, bool invertY, CameraRig camera)
        {
            Volume = Mathf.Clamp01(volume); Sensitivity = Mathf.Clamp(sensitivity, .25f, 2.5f); InvertY = invertY;
            PlayerPrefs.SetFloat(KeyPrefix + "Volume", Volume);
            PlayerPrefs.SetFloat(KeyPrefix + "Sensitivity", Sensitivity);
            PlayerPrefs.SetInt(KeyPrefix + "InvertY", InvertY ? 1 : 0);
            Apply(camera);
        }
        public void Apply(CameraRig camera)
        {
            AudioListener.volume = Volume;
            if (camera) { camera.MouseSensitivity = Sensitivity; camera.InvertVertical = InvertY; }
        }
        public void Save() => PlayerPrefs.Save();
    }
}
