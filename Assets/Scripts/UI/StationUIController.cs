using System;
using System.Collections.Generic;
using KToolkit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sokoban.UI
{
    public sealed class StationUIController : IDisposable
    {
        public LevelRunner Runner { get; }
        public GamePreferences Preferences { get; } = new GamePreferences();
        public bool SettingsOpen { get; private set; }
        public bool IsTransitioning => Fade.IsFading;
        public GeneralFadePage Fade { get; private set; }
        private readonly List<StationPage> pages = new List<StationPage>();
        private MainMenuPage menu;
        private HudPage hud;
        private LevelSelectPage selection;
        private PausePage pause;
        private CompletionPage completion;
        private SettingsPage settings;
        private Camera menuCamera;
        private bool pausedBeforeSettings, disposed, selectionWasOpen;

        public StationUIController(LevelRunner runner)
        {
            Runner = runner;
            try
            {
                var canvas = KUIManager.instance.GetCanvas();
                canvas.sortingOrder = 100;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                var cameraObject = new GameObject("Menu background camera", typeof(Camera));
                cameraObject.transform.SetParent(runner.transform, false);
                menuCamera = cameraObject.GetComponent<Camera>();
                menuCamera.clearFlags = CameraClearFlags.SolidColor;
                menuCamera.backgroundColor = new Color(.969f, .969f, .957f);
                menuCamera.cullingMask = 0; menuCamera.depth = -10;
                menu = Create<MainMenuPage>(); hud = Create<HudPage>();
                selection = Create<LevelSelectPage>(); pause = Create<PausePage>();
                completion = Create<CompletionPage>(); settings = Create<SettingsPage>();
                Fade = KUIManager.instance.CreateUI<GeneralFadePage>();
                if (Fade == null) throw new InvalidOperationException("GeneralFade UI prefab is missing.");
                Runner.Changed += Refresh;
                Refresh();
            }
            catch { Dispose(); throw; }
        }
        private T Create<T>() where T : StationPage, new()
        {
            var page = KUIManager.instance.CreateUI<T>(this);
            if (page == null) throw new InvalidOperationException(typeof(T).Name + " UI prefab is missing.");
            pages.Add(page); page.Present(false, false); return page;
        }
        public void Refresh()
        {
            if (disposed) return;
            bool hasGame = Runner.Session != null;
            bool choosing = Runner.LevelSelectionOpen;
            bool playing = hasGame && !choosing && !SettingsOpen;
            bool unlocked = !Runner.NavigationLocked && !Runner.DebugInputCaptured;
            if (choosing && !selectionWasOpen) selection.SelectCurrent();
            selectionWasOpen = choosing;
            menu.Present(!hasGame && !choosing && !SettingsOpen, unlocked);
            hud.Present(playing, unlocked && !Runner.Paused && !Runner.Completed);
            selection.Present(choosing && !SettingsOpen, unlocked);
            pause.Present(playing && Runner.Paused && !Runner.Completed, unlocked);
            completion.Present(playing && Runner.Completed, unlocked);
            settings.Present(SettingsOpen, unlocked);
            foreach (var page in pages) if (page.gameObject.activeSelf) page.Refresh();
            menuCamera.enabled = !hasGame;
            if (Fade?.gameObject && Fade.gameObject.activeSelf) Fade.transform.SetAsLastSibling();
        }
        public void ReadInput()
        {
            if (disposed || Runner.NavigationLocked) return;
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (SettingsOpen) CloseSettings();
            else if (Runner.LevelSelectionOpen) Runner.CloseLevelSelect();
            else if (Runner.Session != null && !Runner.Completed) Runner.SetPaused(!Runner.Paused);
        }
        public bool EnterLevel(int index)
        {
            if (Runner.IsPlaytest || Runner.IsLiveSandbox || Runner.LiveEditing || index < 0 || index >= Runner.CampaignLevelCount || Runner.NavigationLocked) return false;
            var axis = Runner.Session == null ? FadeAxis.Horizontal : FadeAxis.Vertical;
            return Transition(axis, () => Runner.SelectCoveredLevel(index));
        }
        public bool NextLevel() => Runner.CanGoNext && EnterLevel(Runner.CampaignIndex + 1);
        public bool ReturnToMainMenu()
        {
            if (Runner.IsPlaytest || Runner.IsLiveSandbox || Runner.LiveEditing || Runner.NavigationLocked) return false;
            return Transition(FadeAxis.Horizontal, Runner.ReturnToMenu);
        }
        private bool Transition(FadeAxis axis, Action change)
        {
            if (disposed || Runner.NavigationLocked) return false;
            Preferences.Save(); SettingsOpen = false;
            Runner.SetNavigationLocked(true);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            Fade.Play(axis, () =>
            {
                try { change(); }
                catch (Exception exception) { Runner.ReportError(exception); }
                Refresh();
            }, () =>
            {
                if (disposed) return;
                Runner.SetNavigationLocked(false);
                foreach (var page in pages)
                    if (page.gameObject.activeSelf && !(page is HudPage)) page.FocusFirst();
            });
            return true;
        }
        public void OpenLevelSelection() { if (!disposed && !SettingsOpen) Runner.OpenLevelSelect(); }
        public void Replay()
        {
            if (Runner.NavigationLocked) return;
            Runner.SetPaused(false); Runner.Restart();
        }
        public void OpenSettings()
        {
            if (Runner.NavigationLocked || SettingsOpen) return;
            pausedBeforeSettings = Runner.Paused; SettingsOpen = true;
            Runner.SetPaused(true);
        }
        public void CloseSettings()
        {
            if (!SettingsOpen || Runner.NavigationLocked) return;
            Preferences.Save(); SettingsOpen = false; Runner.SetPaused(pausedBeforeSettings);
        }
        public void Quit()
        {
            if (Runner.NavigationLocked) return;
            Preferences.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Runner.Changed -= Refresh;
            Fade?.Cancel();
            if (Fade != null && !Fade.isDestroyed) Fade.DestroySelf();
            foreach (var page in pages)
                if (!page.isDestroyed)
                {
                    // Editor Play Mode can destroy the canvas before the runner disposes its page wrappers.
                    if (page.gameObject) page.gameObject.SetActive(false);
                    page.DestroySelf();
                }
            pages.Clear();
            if (menuCamera) UnityEngine.Object.Destroy(menuCamera.gameObject);
            if (Runner) Runner.SetNavigationLocked(false);
        }
    }
}
