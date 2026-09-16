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
        protected void Bind(string path, UnityAction action) => Get<Button>(path).onClick.AddListener(action);
        public void Present(bool visible, bool interactive)
        {
            bool opening = visible && !gameObject.activeSelf;
            gameObject.SetActive(visible);
            group.interactable = interactive;
            group.blocksRaycasts = interactive;
            if (opening && interactive && !(this is HudPage)) FocusFirst();
        }
        public void FocusFirst()
        {
            var button = gameObject.GetComponentInChildren<Selectable>();
            if (button && button.IsInteractable() && EventSystem.current) button.Select();
        }
        public virtual void Refresh() { }
    }
}
