using System;
using System.Collections;
using System.Collections.Generic;
using KToolkit;
using NUnit.Framework;
using Sokoban.Domain;
using Sokoban.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Sokoban.Tests
{
    public sealed class StationUITests
    {
        private LevelRunner runner;
        private float volume, timeScale;
        private readonly Dictionary<string, string> prefs = new Dictionary<string, string>();

        [UnitySetUp] public IEnumerator SetUp()
        {
            timeScale = Time.timeScale; volume = AudioListener.volume;
            foreach (string key in new[] { "Volume", "Sensitivity", "InvertY" })
            {
                string full = GamePreferences.KeyPrefix + key;
                prefs[full] = !PlayerPrefs.HasKey(full) ? null : key == "InvertY"
                    ? PlayerPrefs.GetInt(full).ToString() : PlayerPrefs.GetFloat(full).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            LevelRunner.PlaytestDefinition = null;
            runner = new GameObject("UGUI integration test").AddComponent<LevelRunner>(); runner.Progress = new PlayerProgress(() => null, _ => { });
            yield return null;
            runner.enabled = false;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = timeScale;
            if (runner) Object.Destroy(runner.gameObject);
            yield return null;
            foreach (var entry in prefs)
            {
                if (entry.Value == null) PlayerPrefs.DeleteKey(entry.Key);
                else if (entry.Key.EndsWith("InvertY")) PlayerPrefs.SetInt(entry.Key, int.Parse(entry.Value));
                else PlayerPrefs.SetFloat(entry.Key, float.Parse(entry.Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            PlayerPrefs.Save(); prefs.Clear(); AudioListener.volume = volume;
            LevelRunner.PlaytestDefinition = null;
        }
        private T Page<T>() where T : KUIBase => (T)KUIManager.instance.GetFirstUIWithType<T>();
        private IEnumerator Click<T>(string path) where T : KUIBase
        {
            // Graphics register their raycast depth after the newly opened page renders.
            yield return null;
            Canvas.ForceUpdateCanvases();
            var button = Page<T>().transform.Find(path).GetComponent<Button>();
            Assert.That(button.IsInteractable(), Is.True);
            var position = RectTransformUtility.WorldToScreenPoint(null, button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "UGUI must be raycastable.");
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), "Another panel obscures the button.");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private IEnumerator WaitForTransition()
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (runner.UI.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(runner.UI.IsTransitioning, Is.False);
            Assert.That(runner.NavigationLocked, Is.False);
        }

        [UnityTest] public IEnumerator BootMenuStartsThroughOpaqueCoverAndRejectsDuplicateNavigation()
        {
            Assert.That(runner.Session, Is.Null);
            Assert.That(Page<MainMenuPage>().gameObject.activeSelf, Is.True);
            Assert.That(Page<HudPage>().gameObject.activeSelf, Is.False);
            Assert.That(Page<MainMenuPage>().transform.Find("Content/Title").GetComponent<Text>().font.HasCharacter('空'), Is.True);
            yield return Click<MainMenuPage>("Content/Actions/Start");
            Assert.That(runner.NavigationLocked, Is.True);
            Assert.That(runner.UI.EnterLevel(2), Is.False);
            Assert.That(runner.SelectLevel(2), Is.False);
            Assert.That(runner.TryMove(Direction.E), Is.False);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(runner.Session, Is.Null, "New level cannot appear before the cover closes.");
            Assert.That(runner.UI.Fade.Coverage, Is.InRange(.01f, .99f));
            yield return WaitForTransition();
            Assert.That(runner.Definition.id, Is.EqualTo("L04"));
            Assert.That(Page<MainMenuPage>().gameObject.activeSelf, Is.False);
            Assert.That(Page<HudPage>().gameObject.activeSelf, Is.True);
            Assert.That(runner.UI.Fade.gameObject.activeSelf, Is.False);
        }

        [UnityTest] public IEnumerator SelectingRowUsesCatalogAndStartsOnlyAfterEnterButton()
        {
            yield return Click<MainMenuPage>("Content/Actions/Levels");
            yield return Click<LevelSelectPage>("Content/List/Viewport/Rows/Level02");
            Assert.That(runner.Session, Is.Null);
            yield return Click<LevelSelectPage>("Content/Enter");
            yield return WaitForTransition();
            Assert.That(runner.Definition.id, Is.EqualTo("L05"));
            Assert.That(runner.Session.State.Moves, Is.Zero);
            Assert.That(Page<HudPage>().transform.Find("Header/Stage").GetComponent<Text>().text, Is.EqualTo("关卡 02"));
        }

        [UnityTest] public IEnumerator LastLevelIsReachableThroughSelection()
        {
            yield return Click<MainMenuPage>("Content/Actions/Levels");
            var page = Page<LevelSelectPage>();
            Assert.That(page.transform.Find("Content/List/Viewport/Rows/Level09/Label").GetComponent<Text>().text, Is.EqualTo("关卡 09"));
            page.transform.Find("Content/List").GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
            yield return null;
            yield return Click<LevelSelectPage>("Content/List/Viewport/Rows/Level09");
            yield return Click<LevelSelectPage>("Content/Enter");
            yield return WaitForTransition();
            Assert.That(runner.Definition.id, Is.EqualTo("L12"));
            Assert.That(runner.GoalCount, Is.EqualTo(2)); Assert.That(runner.PoweredGoalCount, Is.Zero);
        }

        [UnityTest] public IEnumerator PauseSelectionBackPreservesSessionAndSettingsPersist()
        {
            runner.SelectLevel(0);
            runner.TryMove(Direction.E);
            yield return new WaitForSecondsRealtime(.1f);
            runner.SetPaused(true);
            var state = runner.Session.State;
            yield return Click<PausePage>("Content/Levels");
            yield return Click<LevelSelectPage>("Content/Back");
            Assert.That(runner.Paused, Is.True);
            Assert.That(runner.Session.State, Is.SameAs(state));
            yield return Click<PausePage>("Content/Settings");
            Page<SettingsPage>().transform.Find("Content/Volume").GetComponent<Slider>().value = .35f;
            Page<SettingsPage>().transform.Find("Content/Sensitivity").GetComponent<Slider>().value = 1.75f;
            Page<SettingsPage>().transform.Find("Content/Invert").GetComponent<Toggle>().isOn = true;
            yield return Click<SettingsPage>("Content/Back");
            Assert.That(runner.Paused, Is.True);
            var reloaded = new GamePreferences();
            Assert.That(reloaded.Volume, Is.EqualTo(.35f).Within(.001f));
            Assert.That(reloaded.Sensitivity, Is.EqualTo(1.75f).Within(.001f));
            Assert.That(reloaded.InvertY, Is.True);
            Assert.That(runner.Cameras.MouseSensitivity, Is.EqualTo(1.75f));
            Assert.That(runner.Cameras.InvertVertical, Is.True);
            yield return Click<PausePage>("Content/Resume");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(runner.Paused, Is.False);
            Assert.That(runner.Presenter.Busy, Is.False);
        }

        [UnityTest, Timeout(20000)] public IEnumerator CompletionButtonAdvancesWithFadeAndFreshCounters()
        {
            runner.SelectLevel(0);
            var proof = JsonUtility.FromJson<SolutionRecord>(Resources.Load<TextAsset>("configs/solutions/L04.solution").text);
            foreach (char command in proof.commands)
            {
                runner.TryMove((Direction)Enum.Parse(typeof(Direction), command.ToString()));
                while (runner.Presenter.Busy) yield return null;
            }
            Assert.That(Page<CompletionPage>().gameObject.activeSelf, Is.True);
            Assert.That(Page<CompletionPage>().transform.Find("Content/Stats").GetComponent<Text>().text, Does.Contain("推动 " + proof.expectedPushes));
            yield return Click<CompletionPage>("Content/Next");
            Assert.That(runner.Definition.id, Is.EqualTo("L04"));
            yield return WaitForTransition();
            Assert.That(runner.Definition.id, Is.EqualTo("L05"));
            Assert.That(runner.Session.State.Moves, Is.Zero);
            Assert.That(runner.Session.UndoCount, Is.Zero);
            Assert.That(Page<CompletionPage>().gameObject.activeSelf, Is.False);
            Assert.That(Object.FindObjectsOfType<BoardView>().Length, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator MenuReturnWorksAtZeroTimeScaleAndDiscardsOldAction()
        {
            runner.SelectLevel(0); runner.TryMove(Direction.E);
            yield return new WaitForSecondsRealtime(.08f);
            runner.SetPaused(true); Time.timeScale = 0;
            yield return Click<PausePage>("Content/Menu");
            yield return WaitForTransition();
            Assert.That(runner.Session, Is.Null);
            Assert.That(Page<MainMenuPage>().gameObject.activeSelf, Is.True);
            Assert.That(Object.FindObjectsOfType<BoardView>().Length, Is.Zero);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Time.timeScale = 1;
            yield return Click<MainMenuPage>("Content/Actions/Start");
            yield return WaitForTransition();
            Assert.That(runner.Session.State.Moves, Is.Zero);
            Assert.That(runner.Completed, Is.False);
        }

        [UnityTest] public IEnumerator DestroyDuringFadeLeavesNoPagesOrDelayedLoads()
        {
            var controller = runner.UI;
            yield return Click<MainMenuPage>("Content/Actions/Start");
            yield return new WaitForSecondsRealtime(.2f);
            Object.Destroy(runner.gameObject); runner = null;
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(controller.Fade.IsFading, Is.False);
            Assert.That(KUIManager.instance.DebugGetUIList().Count, Is.Zero);
            Assert.That(Object.FindObjectsOfType<BoardView>().Length, Is.Zero);
            Assert.That(Object.FindObjectsOfType<CameraRig>().Length, Is.Zero);
        }

        [UnityTest] public IEnumerator DisposingAfterCanvasObjectsAreGoneStillReleasesPageWrappers()
        {
            var controller = runner.UI;
            var pages = KUIManager.instance.DebugGetUIList().ToArray();
            foreach (var page in pages)
                if ((page is StationPage || page is GeneralFadePage) && page.gameObject) Object.DestroyImmediate(page.gameObject);
            Assert.DoesNotThrow(() => controller.Dispose());
            Assert.DoesNotThrow(() => controller.Dispose(), "Repeated cleanup must be harmless.");
            foreach (var page in pages)
                if (page is StationPage || page is GeneralFadePage) Assert.That(page.isDestroyed, Is.True);
            Object.Destroy(runner.gameObject); runner = null;
            yield return null;
            Assert.That(KUIManager.instance.DebugGetUIList().Count, Is.Zero);
        }

        [UnityTest] public IEnumerator SettingsSliderGraphicsStayInsideTracksAtBothEnds()
        {
            yield return Click<MainMenuPage>("Content/Actions/Settings");
            yield return null;
            foreach (string name in new[] { "Volume", "Sensitivity" })
            {
                var slider = Page<SettingsPage>().transform.Find("Content/" + name).GetComponent<Slider>();
                foreach (float normalized in new[] { 0f, .5f, 1f })
                {
                    slider.normalizedValue = normalized;
                    Canvas.ForceUpdateCanvases();
                    var root = (RectTransform)slider.transform;
                    foreach (var graphic in new[] { slider.fillRect, slider.handleRect })
                    {
                        var corners = new Vector3[4]; graphic.GetWorldCorners(corners);
                        foreach (var corner in corners)
                        {
                            var local = root.InverseTransformPoint(corner);
                            Assert.That(local.x, Is.InRange(root.rect.xMin - .1f, root.rect.xMax + .1f), name + " graphic overflows horizontally.");
                            Assert.That(local.y, Is.InRange(root.rect.yMin - .1f, root.rect.yMax + .1f), name + " graphic overflows vertically.");
                        }
                    }
                }
            }
        }

        [UnityTest] public IEnumerator EscapeResumesAndHeldMoveCannotLeakThroughTransition()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>("UI test keyboard");
            runner.enabled = true;
            try
            {
                runner.UI.EnterLevel(0);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
                yield return WaitForTransition();
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(runner.Session.State.Moves, Is.Zero, "A held key from the menu must be released first.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                Assert.That(runner.Paused, Is.True);
                Assert.That(Page<PausePage>().gameObject.activeSelf, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                Assert.That(runner.Paused, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(runner.Session.State.Moves, Is.EqualTo(1));
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest] public IEnumerator HudUndoAndRestartUseTheRealSession()
        {
            runner.SelectLevel(0); Assert.That(runner.Cameras.TopDown, Is.True);
            runner.TryMove(Direction.E);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Page<HudPage>().transform.Find("Header/Moves").GetComponent<Text>().text, Does.Contain("1"));
            yield return Click<HudPage>("Shortcuts/Undo");
            Assert.That(runner.Session.State.Moves, Is.Zero);
            runner.TryMove(Direction.E); yield return new WaitForSecondsRealtime(.4f);
            yield return Click<HudPage>("Shortcuts/Restart");
            Assert.That(runner.Session.State.Moves, Is.Zero);
            Assert.That(runner.Session.UndoCount, Is.Zero);
        }

        [UnityTest] public IEnumerator MenuRemovesFirstLevelShortcutButKeepsContinueSelectionAndSaveRetry()
        {
            var menu = Page<MainMenuPage>();
            Assert.That(menu.transform.Find("Content/Actions/NewGame"), Is.Null);
            Assert.That(menu.transform.Find("Content/Actions/Start/Label").GetComponent<Text>().text, Is.EqualTo("开始游戏"));
            var level = LevelJson.Read(Resources.Load<TextAsset>("configs/levels/L07").text);
            bool failSave = true;
            runner.Progress = new PlayerProgress(() => null, _ => { if (failSave) throw new System.IO.IOException("Fixture"); });
            runner.Progress.Complete(level, SolutionRecord.ReplayCommands(level, "SEENSEENNN"));
            runner.UI.Refresh(); yield return null;
            Assert.That(menu.transform.Find("Content/Actions/NewGame"), Is.Null);
            Assert.That(menu.transform.Find("Content/Actions/Start/Label").GetComponent<Text>().text, Is.EqualTo("继续游戏"));
            Assert.That(menu.transform.Find("Content/Actions/Retry").gameObject.activeSelf, Is.True);
            failSave = false; yield return Click<MainMenuPage>("Content/Actions/Retry");
            Assert.That(menu.transform.Find("Content/Actions/Retry").gameObject.activeSelf, Is.False);
            yield return Click<MainMenuPage>("Content/Actions/Levels");
            yield return Click<LevelSelectPage>("Content/List/Viewport/Rows/Level01");
            yield return Click<LevelSelectPage>("Content/Enter"); yield return WaitForTransition();
            Assert.That(runner.CampaignIndex, Is.Zero);
            Assert.That(runner.Progress.Best(level).moves, Is.EqualTo(10));
        }

        [UnityTest] public IEnumerator MainMenuHoverDoesNotFlashDarkerThanItsEndpoints()
        { yield return HoverDoesNotFlash(Page<MainMenuPage>().transform.Find("Content/Actions/Settings").GetComponent<Button>()); }

        [UnityTest] public IEnumerator HelpRestoresTheCallerPauseStateAndRemovedCopyHasNoControls()
        {
            yield return Click<MainMenuPage>("Content/Actions/Help");
            Assert.That(Page<HelpPage>().gameObject.activeSelf, Is.True);
            yield return Click<HelpPage>("Content/Back");
            Assert.That(Page<MainMenuPage>().gameObject.activeSelf, Is.True);
            runner.SelectLevel(0); runner.UI.OpenHelp();
            Assert.That(runner.Paused, Is.True); runner.UI.CloseHelp();
            Assert.That(runner.Paused, Is.False);
            runner.SetPaused(true); yield return Click<PausePage>("Content/Help");
            yield return Click<HelpPage>("Content/Back");
            Assert.That(runner.Paused, Is.True);
            Assert.That(Page<PausePage>().gameObject.activeSelf, Is.True);
            Assert.That(Page<PausePage>().transform.Find("Content/Subtitle"), Is.Null);
            Assert.That(Page<HudPage>().transform.Find("Header/Title"), Is.Null);
            Assert.That(Page<HudPage>().transform.Find("MessageBacking"), Is.Null);
            Assert.That(Page<CompletionPage>().transform.Find("Content/Title"), Is.Null);
        }

        [UnityTest] public IEnumerator PointerAndKeyboardFocusRemainStableAcrossRefreshAndPageChanges()
        {
            var module = EventSystem.current.currentInputModule;
            bool enabled = module && module.enabled; if (module) module.enabled = false;
            try
            {
                var start = Page<MainMenuPage>().transform.Find("Content/Actions/Start").GetComponent<Button>();
                var settings = Page<MainMenuPage>().transform.Find("Content/Actions/Settings").GetComponent<Button>();
                start.Select();
                ExecuteEvents.Execute(start.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down, moveVector = Vector2.down }, ExecuteEvents.moveHandler);
                var keyboardFocus = EventSystem.current.currentSelectedGameObject;
                Assert.That(keyboardFocus.name, Is.EqualTo("Levels"));
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(settings.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                for (int i = 0; i < 6; i++) { runner.UI.Refresh(); yield return null; Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(keyboardFocus)); }
                ExecuteEvents.Execute(settings.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(settings.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(settings.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(runner.UI.SettingsOpen, Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(Page<SettingsPage>().transform), Is.True);
                runner.UI.CloseSettings(); yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(start.gameObject));
                runner.OpenLevelSelect(); yield return null;
                var row = Page<LevelSelectPage>().transform.Find("Content/List/Viewport/Rows/Level09").GetComponent<Button>();
                row.Select(); yield return null;
                var viewport = Page<LevelSelectPage>().transform.Find("Content/List/Viewport").GetComponent<RectTransform>();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row.transform);
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 1));
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(viewport.rect.yMax + 1));
            }
            finally { if (module) module.enabled = enabled; }
        }

        [UnityTest] public IEnumerator PauseHoverDoesNotFlashDarkerThanItsEndpoints()
        {
            runner.SelectLevel(0); runner.SetPaused(true);
            yield return HoverDoesNotFlash(Page<PausePage>().transform.Find("Content/Restart").GetComponent<Button>());
        }

        private IEnumerator HoverDoesNotFlash(Button button)
        {
            var module = EventSystem.current.currentInputModule;
            bool moduleEnabled = module && module.enabled;
            if (module) module.enabled = false;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            try
            {
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.2f);
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)button.transform;
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button));
                var paper = new Color32(247, 247, 244, 255);
                float start = VisibleBrightness(button.targetGraphic, paper);
                pointer.position = point;
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                float minimum = start;
                float until = Time.realtimeSinceStartup + 1.3f;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    minimum = Mathf.Min(minimum, VisibleBrightness(button.targetGraphic, paper));
                }
                float end = VisibleBrightness(button.targetGraphic, paper);
                TestContext.WriteLine($"{button.name}: start={start:F4}, minimum={minimum:F4}, end={end:F4}");
                Assert.That(button.targetGraphic.canvasRenderer.GetColor().a, Is.GreaterThan(.99f), "Pointer must reach the actual hover state.");
                Assert.That(minimum, Is.GreaterThanOrEqualTo(Mathf.Min(start, end) - .015f), "Hover briefly darkened below both displayed endpoint colors.");
            }
            finally
            {
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                if (module) module.enabled = moduleEnabled;
            }
        }

        private static float VisibleBrightness(Graphic graphic, Color background)
        {
            Color tint = graphic.canvasRenderer.GetColor() * graphic.color;
            Color visible = Color.Lerp(background, tint, tint.a);
            return (visible.r + visible.g + visible.b) / 3f;
        }
    }
}
