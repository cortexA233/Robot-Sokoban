using System;
using System.Collections;
using System.Linq;
using KToolkit;
using NUnit.Framework;
using Sokoban.Domain;
using Sokoban.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Sokoban.Tests
{
    public sealed class AudioFeedbackTests
    {
        private LevelRunner runner;
        private float originalVolume;
        private LevelDefinition Lab() => LevelJson.Read(Resources.Load<TextAsset>("configs/test_levels/LAB01_LowFriction").text);
        private AudioSource[] Voices => runner.Audio.GetComponents<AudioSource>();
        private bool Has(SoundCue cue) => Voices.Any(v => v.clip && v.clip.name == cue.ToString());

        [UnitySetUp] public IEnumerator SetUp()
        {
            originalVolume = AudioListener.volume;
            LevelRunner.PlaytestDefinition = null;
            runner = new GameObject("Audio integration test").AddComponent<LevelRunner>();
            yield return null;
            runner.enabled = false;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (runner) Object.Destroy(runner.gameObject);
            yield return null;
            AudioListener.volume = originalVolume;
            LevelRunner.PlaytestDefinition = null;
        }

        [Test] public void AllEightResourceClipsContainBoundedNonSilentPcm()
        {
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                var clip = Resources.Load<AudioClip>("audio/sfx/" + cue);
                Assert.That(clip, Is.Not.Null, cue.ToString());
                Assert.That(clip.channels, Is.EqualTo(1));
                Assert.That(clip.length, Is.InRange(.04f, .85f));
                var samples = new float[clip.samples];
                Assert.That(clip.GetData(samples, 0), Is.True);
                Assert.That(samples.Max(v => Mathf.Abs(v)), Is.InRange(.05f, .5f));
                Assert.That(Mathf.Abs(samples[0]), Is.LessThan(.001f));
                Assert.That(Mathf.Abs(samples[samples.Length - 1]), Is.LessThan(.001f));
            }
        }

        [UnityTest] public IEnumerator PushPowerLandingAndCompletionFollowPresentation()
        {
            runner.LoadLevel(Lab());
            Assert.That(runner.TryMove(Direction.E), Is.True);
            Assert.That(Has(SoundCue.Complete), Is.False);
            Assert.That(Has(SoundCue.Push), Is.False, "Contact has not happened yet.");
            yield return new WaitForSeconds(.25f);
            Assert.That(Has(SoundCue.Push), Is.True);
            Assert.That(Has(SoundCue.Land), Is.False, "Sliding crate has not landed.");
            yield return new WaitForSeconds(1);
            Assert.That(Has(SoundCue.PowerOn), Is.True);
            Assert.That(Has(SoundCue.Land), Is.True);
            Assert.That(Has(SoundCue.Complete), Is.True);
            Assert.That(runner.Completed, Is.True);
            Assert.That(runner.Session.State.Moves, Is.EqualTo(1));
            Assert.That(runner.Session.State.Pushes, Is.EqualTo(1));
            runner.Undo();
            Assert.That(Voices.All(v => !v.isPlaying && !v.clip), Is.True);
        }

        [UnityTest] public IEnumerator CancelBeforeContactNeverPlaysOldLandingOrCompletion()
        {
            runner.LoadLevel(Lab());
            runner.TryMove(Direction.E); runner.Undo();
            yield return new WaitForSeconds(1.1f);
            Assert.That(Voices.All(v => !v.clip), Is.True);
            runner.TryMove(Direction.E);
            yield return new WaitForSeconds(.25f);
            runner.Restart();
            yield return new WaitForSeconds(1.1f);
            Assert.That(Voices.All(v => !v.clip), Is.True);
            Assert.That(runner.Completed, Is.False);
            Assert.That(runner.Session.State.Moves, Is.Zero);
            runner.TryMove(Direction.E);
            yield return new WaitForSeconds(.25f);
            runner.SelectLevel(0);
            yield return new WaitForSeconds(1.1f);
            Assert.That(Voices.All(v => !v.clip), Is.True);
        }

        [UnityTest] public IEnumerator PauseStopsActionsAllowsUiAndResumesOnlyFutureEvents()
        {
            runner.LoadLevel(Lab()); runner.TryMove(Direction.E);
            yield return new WaitForSeconds(.25f);
            runner.SetPaused(true);
            Assert.That(Voices.All(v => !v.isPlaying), Is.True);
            Assert.That(runner.Audio.Play(SoundCue.Push), Is.False);
            Assert.That(runner.Audio.Play(SoundCue.Click), Is.True);
            yield return new WaitForSeconds(.4f);
            Assert.That(Has(SoundCue.Complete), Is.False);
            runner.SetPaused(false);
            yield return new WaitForSeconds(1);
            Assert.That(Has(SoundCue.Push), Is.False, "Paused contact must not replay.");
            Assert.That(Has(SoundCue.Land), Is.True);
            Assert.That(Has(SoundCue.Complete), Is.True);
        }

        [UnityTest] public IEnumerator MovementBlockedInputAndRateLimitUseRealGameCommands()
        {
            runner.LoadLevel(Lab());
            Assert.That(runner.TryMove(Direction.W), Is.False);
            Assert.That(Has(SoundCue.Blocked), Is.True);
            for (int i = 0; i < 20; i++) Assert.That(runner.Audio.Play(SoundCue.Blocked), Is.False);
            Assert.That(Voices.Count(v => v.clip && v.clip.name == "Blocked"), Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(runner.Audio.Play(SoundCue.Blocked), Is.True);
            Assert.That(runner.TryMove(Direction.N), Is.True);
            Assert.That(Has(SoundCue.Move), Is.True);
            yield return new WaitForSeconds(.4f);
            Assert.That(Has(SoundCue.Push), Is.False);
        }

        [UnityTest] public IEnumerator MovingOffSocketPlaysOnAndOffWithoutFalseCompletion()
        {
            var lab = Lab();
            lab.terrainRows[3] = "#.......#";
            lab.sockets = new[] { new SocketDefinition { id = "transient", x = 3, z = 3 },
                new SocketDefinition { id = "goal", x = 7, z = 4, isGoal = true } };
            runner.LoadLevel(lab); runner.TryMove(Direction.E);
            yield return new WaitForSeconds(.75f);
            Assert.That(Has(SoundCue.PowerOn), Is.True);
            Assert.That(runner.TryMove(Direction.E), Is.True);
            yield return new WaitForSeconds(.75f);
            Assert.That(Has(SoundCue.PowerOff), Is.True);
            Assert.That(Has(SoundCue.Complete), Is.False);
        }

        [UnityTest] public IEnumerator MenuButtonsAndLevelRowsClickWithOneListenerAcrossNavigation()
        {
            Assert.That(Object.FindObjectsOfType<AudioListener>().Count(v => v.enabled), Is.EqualTo(1));
            var menu = (MainMenuPage)KUIManager.instance.GetFirstUIWithType<MainMenuPage>();
            menu.transform.Find("Content/Levels").GetComponent<Button>().onClick.Invoke();
            Assert.That(Has(SoundCue.Click), Is.True);
            yield return new WaitForSeconds(.08f);
            var selection = (LevelSelectPage)KUIManager.instance.GetFirstUIWithType<LevelSelectPage>();
            selection.transform.Find("Content/List/Viewport/Rows/Level02").GetComponent<Button>().onClick.Invoke();
            Assert.That(Voices.Single(v => v.clip && v.clip.name == "Click").isPlaying, Is.True);
            runner.SelectLevel(0);
            yield return null;
            Assert.That(Object.FindObjectsOfType<AudioListener>().Count(v => v.enabled), Is.EqualTo(1));
            runner.TryMove(Direction.E);
            runner.UI.ReturnToMainMenu();
            yield return new WaitForSeconds(1.5f);
            Assert.That(runner.Session, Is.Null);
            Assert.That(Voices.All(v => !v.isPlaying), Is.True);
            Assert.That(Object.FindObjectsOfType<AudioListener>().Count(v => v.enabled), Is.EqualTo(1));
            var owner = runner.Audio;
            Object.Destroy(runner.gameObject); yield return null;
            Assert.That(owner == null, Is.True);
        }

        [UnityTest] public IEnumerator MutedAudioDoesNotChangeRulesOrCompletion()
        {
            AudioListener.volume = 0;
            runner.LoadLevel(Lab()); runner.TryMove(Direction.E);
            // Load applies saved settings, so mute after initialization as a player would.
            AudioListener.volume = 0;
            yield return new WaitForSeconds(1.2f);
            Assert.That(AudioListener.volume, Is.Zero);
            Assert.That(runner.Completed, Is.True);
            Assert.That(runner.Session.State.Pushes, Is.EqualTo(1));
            runner.Restart();
            Assert.That(runner.Session.State.Moves, Is.Zero);
        }

        [UnityTest] public IEnumerator ListenerMixContainsSignalAndMasterMuteSilencesIt()
        {
            AudioListener.volume = 1;
            runner.Audio.Play(SoundCue.Complete);
            var buffer = new float[1024];
            float peak = 0;
            for (int i = 0; i < 15; i++)
            {
                yield return new WaitForSecondsRealtime(.025f);
                AudioListener.GetOutputData(buffer, 0);
                peak = Mathf.Max(peak, buffer.Max(v => Mathf.Abs(v)));
            }
            Assert.That(peak, Is.GreaterThan(.001f), "The actual listener mix must contain audio.");
            AudioListener.volume = 0;
            runner.Audio.ResetActions(); runner.Audio.Play(SoundCue.Complete);
            yield return new WaitForSecondsRealtime(.15f);
            AudioListener.GetOutputData(buffer, 0);
            Assert.That(buffer.Max(v => Mathf.Abs(v)), Is.LessThan(.0001f));
        }

        [UnityTest] public IEnumerator AuthorPlaytestUsesAudioAndDestroysAllOwnedVoices()
        {
            Object.Destroy(runner.gameObject); yield return null;
            LevelRunner.PlaytestDefinition = Lab();
            runner = new GameObject("Audio author playtest").AddComponent<LevelRunner>();
            yield return null; runner.enabled = false;
            Assert.That(runner.IsPlaytest, Is.True);
            Assert.That(runner.TryMove(Direction.E), Is.True);
            yield return new WaitForSeconds(.25f);
            Assert.That(Has(SoundCue.Push), Is.True);
            var voices = Voices;
            Object.Destroy(runner.gameObject); yield return null;
            Assert.That(voices.All(v => !v), Is.True);
        }
    }
}
