#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using OrbitOrchard.Services;
using UnityEngine;
using UnityEngine.Profiling;

namespace IdleClinic.Services
{
    /// <summary>Opt-in local development diagnostics. No UI, uploads, automatic creation or gameplay data.</summary>
    [DisallowMultipleComponent]
    public sealed class ClinicPerformanceRecorder : MonoBehaviour
    {
        public const string FileName = "clinic-performance-v1.json";
        public string ReportPath => Path.Combine(Application.persistentDataPath, FileName);
        private const int HistoryCapacity = 120;
        private readonly ClinicFrameWindow frames = new ClinicFrameWindow(3600);
        private readonly ClinicPerformanceSample[] history = new ClinicPerformanceSample[HistoryCapacity];
        private readonly string sessionId = Guid.NewGuid().ToString("N");
        private readonly string startedUtc = DateTime.UtcNow.ToString("O");
        private int historyCount, historyNext, maximumThermalState = -1;
        private long totalFrameCount, peakUnityAllocatedBytes, peakManagedUsedBytes;
        private double elapsedSeconds, nextReportSeconds = 30, nextThermalSeconds;
        private bool paused, skipNextFrame = true, reportedWriteFailure;
        private readonly Queue<ClinicAudioSample> audioHistory = new Queue<ClinicAudioSample>();
        private double nextAudioSeconds;

        private void OnEnable() { skipNextFrame = true; }

        private void Update()
        {
            if (paused) return;
            if (skipNextFrame) { skipNextFrame = false; return; }
            var delta = (double)Time.unscaledDeltaTime;
            if (!frames.AddSeconds(delta)) return;
            totalFrameCount++;
            elapsedSeconds += delta;
            if (elapsedSeconds >= nextAudioSeconds)
            {
                SampleAudio();
                nextAudioSeconds = elapsedSeconds + .5;
            }
            if (elapsedSeconds >= nextThermalSeconds)
            {
                ReadThermalState();
                nextThermalSeconds = elapsedSeconds + 5;
            }
            if (elapsedSeconds >= nextReportSeconds)
            {
                WriteReport();
                nextReportSeconds = elapsedSeconds + 30;
            }
        }

        private int ReadThermalState()
        {
            var state = AppleServices.Instance != null ? AppleServices.Instance.ThermalState : -1;
            if (state < -1 || state > 3) state = -1;
            maximumThermalState = Math.Max(maximumThermalState, state);
            return state;
        }

        /// <summary>Writes a bounded report atomically; collection continues after an I/O failure.</summary>
        public void WriteReport()
        {
            if (totalFrameCount == 0) return;
            var summary = frames.Summarize();
            var allocated = Profiler.GetTotalAllocatedMemoryLong();
            var reserved = Profiler.GetTotalReservedMemoryLong();
            var managed = Profiler.GetMonoUsedSizeLong();
            peakUnityAllocatedBytes = Math.Max(peakUnityAllocatedBytes, allocated);
            peakManagedUsedBytes = Math.Max(peakManagedUsedBytes, managed);
            var sample = new ClinicPerformanceSample
            {
                totalFrameCount = totalFrameCount, elapsedActiveSeconds = elapsedSeconds,
                windowFrameCount = summary.frameCount, windowElapsedSeconds = summary.elapsedSeconds,
                medianFrameMs = summary.medianFrameMs, p95FrameMs = summary.p95FrameMs,
                maximumFrameMs = summary.maximumFrameMs,
                unityAllocatedBytes = allocated, unityReservedBytes = reserved, managedUsedBytes = managed,
                memoryCountersAvailable = allocated > 0, thermalState = ReadThermalState(),
                musicPlaying = GetComponent<ClinicAudio>()?.MusicPlaying ?? false,
                musicTime = GetComponent<ClinicAudio>()?.MusicTime ?? 0,
                activeEffectVoices = GetComponent<ClinicAudio>()?.ActiveEffectVoices ?? 0,
                audioOutputRms = ReadAudioRms()
            };
            history[historyNext] = sample;
            historyNext = (historyNext + 1) % HistoryCapacity;
            historyCount = Math.Min(historyCount + 1, HistoryCapacity);
            var snapshots = new ClinicPerformanceSample[historyCount];
            for (var i = 0; i < historyCount; i++)
                snapshots[i] = history[(historyNext - historyCount + i + HistoryCapacity) % HistoryCapacity];
            var report = new ClinicPerformanceReport
            {
                sessionId = sessionId, startedUtc = startedUtc, capturedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion, appVersion = Application.version,
                isEditor = Application.isEditor, targetFrameRate = Application.targetFrameRate,
                screenWidth = Screen.width, screenHeight = Screen.height,
                maximumThermalState = maximumThermalState,
                peakSampledUnityAllocatedBytes = peakUnityAllocatedBytes,
                peakSampledManagedUsedBytes = peakManagedUsedBytes,
                latest = sample, samples = snapshots, audioSamples = audioHistory.ToArray()
            };
            try
            {
                var path = ReportPath;
                Directory.CreateDirectory(Application.persistentDataPath);
                var temporary = path + ".tmp";
                var bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(report, true));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                reportedWriteFailure = false;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                if (!reportedWriteFailure) Debug.LogWarning("Clinic performance report could not be written: " + error.GetType().Name);
                reportedWriteFailure = true;
            }
        }

        private void SampleAudio()
        {
            var audio = GetComponent<ClinicAudio>();
            if (audio == null) return;
            if (audioHistory.Count == 360) audioHistory.Dequeue();
            audioHistory.Enqueue(new ClinicAudioSample
            {
                elapsedActiveSeconds = elapsedSeconds, musicEnabled = audio.MusicEnabled,
                effectsEnabled = audio.EffectsEnabled, musicPlaying = audio.MusicPlaying,
                musicTime = audio.MusicTime, musicStarts = audio.MusicStarts,
                effectStarts = audio.EffectStarts, activeEffectVoices = audio.ActiveEffectVoices,
                lastEffect = audio.LastEffect, outputRms = ReadAudioRms()
            });
        }

        private readonly float[] audioSamples = new float[256];
        private float ReadAudioRms()
        {
            AudioListener.GetOutputData(audioSamples, 0);
            double energy = 0;
            foreach (var value in audioSamples) energy += value * value;
            return (float)Math.Sqrt(energy / audioSamples.Length);
        }

        private void OnApplicationPause(bool value)
        {
            if (value && !paused) WriteReport();
            paused = value;
            if (!value) skipNextFrame = true;
        }
        private void OnApplicationQuit() { if (!paused) WriteReport(); }
        private void OnDisable() { if (!paused) WriteReport(); }
    }

    /// <summary>Allocation-free frame sampling; percentile sorting occurs only when a report is requested.</summary>
    public sealed class ClinicFrameWindow
    {
        private readonly double[] values, sorted;
        private int count, next;
        public int Count => count;
        public ClinicFrameWindow(int capacity)
        {
            if (capacity < 1 || capacity > 3600) throw new ArgumentOutOfRangeException(nameof(capacity));
            values = new double[capacity]; sorted = new double[capacity];
        }
        public bool AddSeconds(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return false;
            values[next] = seconds * 1000;
            next = (next + 1) % values.Length; count = Math.Min(count + 1, values.Length);
            return true;
        }
        public ClinicFrameSummary Summarize()
        {
            if (count == 0) return new ClinicFrameSummary();
            double total = 0;
            for (var i = 0; i < count; i++) { sorted[i] = values[i]; total += values[i]; }
            Array.Sort(sorted, 0, count);
            var middle = count / 2;
            return new ClinicFrameSummary
            {
                frameCount = count, elapsedSeconds = total / 1000,
                medianFrameMs = count % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle],
                p95FrameMs = sorted[Math.Max(0, (int)Math.Ceiling(count * .95) - 1)],
                maximumFrameMs = sorted[count - 1]
            };
        }
    }

    public struct ClinicFrameSummary
    {
        public int frameCount;
        public double elapsedSeconds, medianFrameMs, p95FrameMs, maximumFrameMs;
    }
    [Serializable] public sealed class ClinicPerformanceSample
    {
        public long totalFrameCount;
        public double elapsedActiveSeconds;
        public int windowFrameCount;
        public double windowElapsedSeconds, medianFrameMs, p95FrameMs, maximumFrameMs;
        public long unityAllocatedBytes, unityReservedBytes, managedUsedBytes;
        public bool memoryCountersAvailable, musicPlaying;
        public float musicTime, audioOutputRms;
        public int activeEffectVoices;
        public int thermalState;
    }
    [Serializable] public sealed class ClinicAudioSample
    {
        public double elapsedActiveSeconds;
        public bool musicEnabled, effectsEnabled, musicPlaying;
        public float musicTime, outputRms;
        public int musicStarts, effectStarts, activeEffectVoices;
        public string lastEffect;
    }
    [Serializable] public sealed class ClinicPerformanceReport
    {
        public int schemaVersion = 1;
        public string sessionId, startedUtc, capturedUtc, unityVersion, appVersion;
        public bool isEditor;
        public int targetFrameRate, screenWidth, screenHeight, maximumThermalState;
        public long peakSampledUnityAllocatedBytes, peakSampledManagedUsedBytes;
        public ClinicPerformanceSample latest;
        public ClinicPerformanceSample[] samples;
        public ClinicAudioSample[] audioSamples;
    }
}
#endif
