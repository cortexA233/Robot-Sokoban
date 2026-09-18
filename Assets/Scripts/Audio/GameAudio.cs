using Sokoban.Domain;
using UnityEngine;

namespace Sokoban
{
    public enum SoundCue { Move, Push, Blocked, Land, PowerOn, PowerOff, Complete, Click }

    // Session-owned, camera-independent 2D feedback. No gameplay state or delayed callbacks.
    public sealed class GameAudio : MonoBehaviour
    {
        private readonly AudioClip[] clips = new AudioClip[8];
        private readonly float[] nextAllowed = new float[8];
        private readonly AudioSource[] voices = new AudioSource[6];
        private AudioSource uiVoice;
        private PowerState power;
        private bool paused;
        private int nextVoice;

        private void Awake()
        {
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = Resources.Load<AudioClip>("audio/sfx/" + (SoundCue)i);
                if (!clips[i]) Debug.LogError("Missing sound cue: " + (SoundCue)i, this);
                nextAllowed[i] = float.NegativeInfinity;
            }
            for (int i = 0; i < voices.Length; i++) voices[i] = CreateVoice();
            uiVoice = CreateVoice();
        }

        private AudioSource CreateVoice()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false;
            source.spatialBlend = 0; source.volume = .65f;
            return source;
        }

        public bool Play(SoundCue cue)
        {
            int index = (int)cue;
            if (!isActiveAndEnabled || (paused && cue != SoundCue.Click) || !clips[index] || Time.unscaledTime < nextAllowed[index]) return false;
            nextAllowed[index] = Time.unscaledTime + (cue == SoundCue.Blocked ? .45f : cue == SoundCue.Click ? .04f : .09f);
            AudioSource voice = cue == SoundCue.Click ? uiVoice : voices[nextVoice++ % voices.Length];
            // A fixed voice pool bounds simultaneous effects even under dense circuit updates.
            voice.Stop(); voice.clip = clips[index]; voice.Play();
            return true;
        }

        public void BeginCommand(PowerState initialPower) => power = initialPower;

        public void ApplyPower(PowerState current)
        {
            bool on = false, off = false;
            if (power != null)
                foreach (var entry in current.Sockets)
                    if (power.Sockets.TryGetValue(entry.Key, out bool previous) && previous != entry.Value)
                    { on |= entry.Value; off |= !entry.Value; }
            power = current;
            if (on) Play(SoundCue.PowerOn);
            if (off) Play(SoundCue.PowerOff);
        }

        public void SetPaused(bool value)
        {
            paused = value;
            if (value) StopActions();
        }

        public void StopActions()
        {
            foreach (var voice in voices) if (voice) { voice.Stop(); voice.clip = null; }
        }

        public void ResetActions()
        {
            StopActions(); power = null; nextVoice = 0;
            for (int i = 0; i < nextAllowed.Length - 1; i++) nextAllowed[i] = float.NegativeInfinity;
        }

        private void OnDisable()
        {
            ResetActions();
            if (uiVoice) uiVoice.Stop();
        }
    }
}
