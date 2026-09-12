using System;
using System.Collections.Generic;
using IdleClinic.Core;
using UnityEngine;

namespace IdleClinic.Services
{
    /// <summary>One persistent score and a bounded, rate-limited set of action voices.</summary>
    public sealed class ClinicAudio : MonoBehaviour
    {
        public const int MaximumVoices = 6;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, double> lastPlayed = new Dictionary<string, double>();
        private AudioSource music;
        private AudioSource[] voices;
        private bool musicEnabled = true, effectsEnabled = true, paused;
        private int previousDoorCount = -1, nextVoice;
        private float footstepClock, serviceClock;
        public int MusicStarts { get; private set; }
        public int EffectStarts { get; private set; }
        public string LastEffect { get; private set; } = "";
        public bool MusicEnabled => musicEnabled;
        public bool EffectsEnabled => effectsEnabled;
        public bool MusicPlaying => music != null && music.isPlaying;
        public float MusicTime => music == null ? 0 : music.time;
        public int ActiveEffectVoices
        {
            get { var count = 0; if (voices != null) foreach (var voice in voices) if (voice.isPlaying) count++; return count; }
        }

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (music != null) return;
            music = gameObject.AddComponent<AudioSource>();
            Configure(music, .28f);
            music.priority = 192;
            music.loop = true;
            music.clip = Resources.Load<AudioClip>("ClinicAudio/morning-rounds");
            voices = new AudioSource[MaximumVoices];
            for (var i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                Configure(voices[i], 1);
                voices[i].priority = 96;
            }
            foreach (var name in new[] { "payment", "collect", "care", "consultation", "pharmacy", "upgrade", "complete", "tap", "footstep", "door", "treatment", "construction", "taxi" })
                clips[name] = Resources.Load<AudioClip>("ClinicAudio/" + name);
        }

        private static void Configure(AudioSource source, float volume)
        {
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = volume;
            source.dopplerLevel = 0;
        }

        public void ApplyPreferences(ClinicPreferences preferences)
        {
            Initialize();
            musicEnabled = preferences.music;
            effectsEnabled = preferences.sound;
            music.mute = !musicEnabled;
            if (!effectsEnabled) foreach (var voice in voices) voice.Stop();
            UpdateMusic();
        }

        public void SetPaused(bool value)
        {
            paused = value;
            if (paused && voices != null) foreach (var voice in voices) voice.Stop();
            UpdateMusic();
            ResetWorldObservation();
        }

        private void UpdateMusic()
        {
            if (music == null) return;
            if (paused || !musicEnabled) { music.Pause(); return; }
            if (!Application.isPlaying || music.clip == null || music.isPlaying) return;
            // UnPause preserves the position; Play is needed only on first launch.
            music.UnPause();
            if (!music.isPlaying) { music.Play(); MusicStarts++; }
        }

        public void ResetWorldObservation()
        {
            previousDoorCount = -1;
            footstepClock = serviceClock = 0;
        }

        public void ObserveWorld(int movingActors, int activeServices, int doorOpeningCount, float delta)
        {
            if (previousDoorCount >= 0 && doorOpeningCount > previousDoorCount) Play("door", .4f, .3);
            previousDoorCount = doorOpeningCount;
            footstepClock += Mathf.Max(0, delta);
            serviceClock += Mathf.Max(0, delta);
            if (movingActors > 0 && footstepClock >= .38f)
            { footstepClock = 0; Play("footstep", .19f, .32); }
            if (activeServices > 0 && serviceClock >= 2.8f)
            { serviceClock = 0; Play("treatment", .16f, 2.6); }
            if (movingActors == 0) footstepClock = 0;
            if (activeServices == 0) serviceClock = 0;
        }

        public void PlayTap() => Play("tap", .35f, .07);

        public void PlayEvent(ClinicEventKind kind)
        {
            switch (kind)
            {
                case ClinicEventKind.PaymentReceived:
                case ClinicEventKind.TipReceived: Play("payment", .48f, .16); break;
                case ClinicEventKind.CashCollected: Play("collect", .62f, .1); break;
                case ClinicEventKind.TreatmentCompleted: Play("care", .44f, .22); break;
                case ClinicEventKind.ConsultationCompleted: Play("consultation", .4f, .22); break;
                case ClinicEventKind.DispensingCompleted: Play("pharmacy", .4f, .22); break;
                case ClinicEventKind.NurseHired:
                case ClinicEventKind.ReceptionistHired:
                case ClinicEventKind.DoctorHired:
                case ClinicEventKind.PharmacistHired:
                case ClinicEventKind.EquipmentUpgraded:
                case ClinicEventKind.StationAdded:
                case ClinicEventKind.StaffTrained:
                case ClinicEventKind.StationUpgraded:
                case ClinicEventKind.AmenityUpgraded: Play("upgrade", .54f, .15); break;
                case ClinicEventKind.ConstructionStarted: Play("construction", .55f, .25); break;
                case ClinicEventKind.ConstructionCompleted:
                case ClinicEventKind.DoctorsClinicUnlocked: Play("complete", .55f, .2); break;
                case ClinicEventKind.TaxiArrived:
                case ClinicEventKind.TaxiDeparted: Play("taxi", .28f, 1); break;
            }
        }

        private void Play(string name, float volume, double interval)
        {
            if (!effectsEnabled || paused || !Application.isPlaying || voices == null
                || !clips.TryGetValue(name, out var clip) || clip == null) return;
            var now = Time.unscaledTimeAsDouble;
            if (lastPlayed.TryGetValue(name, out var previous) && now - previous < interval) return;
            lastPlayed[name] = now;
            // Select an unused voice first. At the limit, replace the oldest round-robin voice.
            var selected = nextVoice;
            for (var i = 0; i < voices.Length; i++)
            {
                var index = (nextVoice + i) % voices.Length;
                if (!voices[index].isPlaying) { selected = index; break; }
            }
            var source = voices[selected];
            source.Stop();
            source.clip = clip;
            source.volume = volume;
            source.Play();
            EffectStarts++; LastEffect = name;
            nextVoice = (selected + 1) % voices.Length;
        }

        private void OnDestroy()
        {
            if (music != null) { music.Stop(); }
            if (voices != null) foreach (var voice in voices) if (voice != null) { voice.Stop(); }
        }
    }
}
