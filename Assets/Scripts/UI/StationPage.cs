using KToolkit;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sokoban.UI
{
    public abstract class StationPage : KUIPage
    {
        protected StationUIController UI;
        protected LevelRunner Runner => UI.Runner;
        private CanvasGroup group;
        public override void InitParams(params object[] args)
        {
            UI = (StationUIController)args[0];
            group = gameObject.GetComponent<CanvasGroup>();
        }
        protected T Get<T>(string path) where T : Component => transform.Find(path).GetComponent<T>();
        protected void Bind(string path, UnityAction action) => Bind(Get<Button>(path), action);
        protected void Bind(Button button, UnityAction action) => button.onClick.AddListener(() =>
        { Runner.Audio?.Play(SoundCue.Click); action(); });
        public void Present(bool visible, bool interactive)
        {
            bool opening = visible && !gameObject.activeSelf;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (visible) Refresh();
            if (group.interactable != interactive) group.interactable = interactive;
            if (group.blocksRaycasts != interactive) group.blocksRaycasts = interactive;
            if (opening && interactive && !(this is HudPage)) FocusFirst();
        }
        public virtual void FocusFirst()
        {
            if (!EventSystem.current) return;
            foreach (var control in gameObject.GetComponentsInChildren<Selectable>())
                if (control.IsInteractable() && control.navigation.mode != Navigation.Mode.None) { control.Select(); return; }
        }
        public virtual void Refresh() { }
    }
}
