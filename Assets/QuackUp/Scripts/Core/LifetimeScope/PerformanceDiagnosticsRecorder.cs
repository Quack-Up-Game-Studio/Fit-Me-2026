#if FITME_PERFORMANCE_DIAGNOSTICS
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace QuackUp.Core
{
    [Preserve]
    public sealed class PerformanceDiagnosticsRecorder : IStartable, ITickable, IDisposable
    {
        private const int FlushIntervalFrames = 120;
        private const int MaxPendingRows = 256;
        private const string FilePrefix = "fitme-performance-";

        private readonly StringBuilder _pendingRows = new();
        private readonly ProfilerRecorder _mainThreadRecorder;
        private readonly ProfilerRecorder _renderThreadRecorder;
        private readonly ProfilerRecorder _memoryRecorder;
        private StreamWriter _writer;
        private string _logPath;
        private PerformanceDiagnosticsPauseHandler _pauseHandler;
        private int _framesSinceFlush;
        private bool _disposed;
        private double? _pauseStartedRealtimeSeconds;
        private readonly PerformanceResumeFrameWindow _postResumeFrameWindow = new(30);

        public PerformanceDiagnosticsRecorder()
        {
            _mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            _renderThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
            _memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1);
        }

        public void Start()
        {
            string fileName = FilePrefix + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv";
            _logPath = Path.Combine(Application.persistentDataPath, fileName);
            _writer = new StreamWriter(_logPath, false, new UTF8Encoding(false), 4096);
            _writer.WriteLine("# Fit Me performance diagnostics");
            _writer.WriteLine("# utc," + Csv(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
            _writer.WriteLine("# applicationVersion," + Csv(Application.version));
            _writer.WriteLine("# unityVersion," + Csv(Application.unityVersion));
            _writer.WriteLine("# deviceModel," + Csv(SystemInfo.deviceModel));
            _writer.WriteLine("# deviceName," + Csv(SystemInfo.deviceName));
            _writer.WriteLine("# operatingSystem," + Csv(SystemInfo.operatingSystem));
            _writer.WriteLine("# processor," + Csv(SystemInfo.processorType));
            _writer.WriteLine("# processorCount," + SystemInfo.processorCount.ToString(CultureInfo.InvariantCulture));
            _writer.WriteLine("# systemMemoryMb," + SystemInfo.systemMemorySize.ToString(CultureInfo.InvariantCulture));
            _writer.WriteLine("# graphicsDevice," + Csv(SystemInfo.graphicsDeviceName));
            _writer.WriteLine("# graphicsApi," + Csv(SystemInfo.graphicsDeviceType.ToString()));
            _writer.WriteLine("# graphicsMemoryMb," + SystemInfo.graphicsMemorySize.ToString(CultureInfo.InvariantCulture));
            _writer.WriteLine("# targetFrameRate," + Application.targetFrameRate.ToString(CultureInfo.InvariantCulture));
            _writer.WriteLine("# qualityLevel," + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            _writer.WriteLine("# profilerRecorderAvailability,mainThread=" + _mainThreadRecorder.Valid + ",renderThread=" + _renderThreadRecorder.Valid + ",usedMemory=" + _memoryRecorder.Valid);
            _writer.WriteLine("# postResumeFrameSamples,30");
            _writer.WriteLine("# frame,utc,unscaledDeltaTimeMs,mainThreadLastFrameNs,renderThreadLastFrameNs,totalUsedMemoryBytes,scene,marker");
            _writer.Flush();
            AddMarker("recording_started");
            Application.focusChanged += OnFocusChanged;
            Application.lowMemory += OnLowMemory;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            var pauseHandlerObject = new GameObject(nameof(PerformanceDiagnosticsPauseHandler));
            UnityEngine.Object.DontDestroyOnLoad(pauseHandlerObject);
            _pauseHandler = pauseHandlerObject.AddComponent<PerformanceDiagnosticsPauseHandler>();
            _pauseHandler.PauseChanged += OnPauseStateChanged;
            Debug.Log("Performance diagnostics recording to " + _logPath);
        }

        public void Tick()
        {
            if (_writer == null || _disposed) return;

            var frameMarker = string.Empty;
            if (_postResumeFrameWindow.TryCapture(Time.realtimeSinceStartupAsDouble,
                    out var postResumeFrameIndex, out var callbackToUpdateMilliseconds))
            {
                frameMarker = "post_resume_frame_index=" + postResumeFrameIndex +
                              ";callback_to_update_ms=" + callbackToUpdateMilliseconds.ToString("F3", CultureInfo.InvariantCulture);
                if (postResumeFrameIndex == 1)
                    frameMarker += ";unscaled_delta_includes_background=1";
            }

            _pendingRows.Append(Time.frameCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append((Time.unscaledDeltaTime * 1000f).ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                .Append(LastValue(_mainThreadRecorder)).Append(',')
                .Append(LastValue(_renderThreadRecorder)).Append(',')
                .Append(LastValue(_memoryRecorder)).Append(',')
                .Append(Csv(SceneManager.GetActiveScene().name)).Append(',')
                .Append(Csv(frameMarker)).AppendLine();

            _framesSinceFlush++;
            if (_framesSinceFlush >= FlushIntervalFrames || _pendingRows.Length >= MaxPendingRows * 128)
                Flush();
        }

        private void OnFocusChanged(bool hasFocus) => AddMarker(hasFocus ? "focus_gained" : "focus_lost");

        private void OnPauseStateChanged(bool isPaused)
        {
            if (isPaused)
            {
                _pauseStartedRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
                AddMarker("app_paused");
                return;
            }

            var resumedAtRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
            AddMarker("app_resumed");
            if (!_pauseStartedRealtimeSeconds.HasValue) return;

            var backgroundDurationMilliseconds =
                (resumedAtRealtimeSeconds - _pauseStartedRealtimeSeconds.Value) * 1000d;
            AddMarker("app_background_duration_ms=" +
                      backgroundDurationMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
            _pauseStartedRealtimeSeconds = null;
            _postResumeFrameWindow.Begin(resumedAtRealtimeSeconds);
        }

        public void RecordEvent(string eventName, string details)
        {
            if (string.IsNullOrEmpty(eventName)) return;
            AddMarker(string.IsNullOrEmpty(details) ? eventName : eventName + ":" + details);
        }

        private void OnLowMemory() => AddMarker("low_memory");

        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            AddMarker("scene_changed:" + previous.name + "->" + current.name);
        }

        private void AddMarker(string marker)
        {
            if (_writer == null || _disposed) return;
            _pendingRows.Append(Time.frameCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                .Append(",,,,,").Append(Csv(SceneManager.GetActiveScene().name)).Append(',')
                .Append(Csv(marker)).AppendLine();
        }

        private void Flush()
        {
            if (_writer == null || _pendingRows.Length == 0) return;
            _writer.Write(_pendingRows);
            _writer.Flush();
            _pendingRows.Clear();
            _framesSinceFlush = 0;
        }

        private static string LastValue(ProfilerRecorder recorder)
        {
            return recorder.Valid && recorder.Count > 0
                ? recorder.LastValue.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public void ShareLatestLog()
        {
            if (string.IsNullOrEmpty(_logPath) || !File.Exists(_logPath))
            {
                Debug.LogWarning("No performance diagnostics log is available to share.");
                return;
            }

            Flush();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var sharePlugin = new AndroidJavaClass("com.fitme.diagnostics.PerformanceLogShare");
                if (!sharePlugin.CallStatic<bool>("share", activity, _logPath))
                    Debug.LogWarning("The Android share sheet could not open for the performance log.");
            }
            catch (Exception exception)
            {
                Debug.LogError("Failed to share performance diagnostics log: " + exception);
            }
#else
            Debug.Log("Performance diagnostics log is available at " + _logPath);
#endif
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Application.focusChanged -= OnFocusChanged;
            Application.lowMemory -= OnLowMemory;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (_pauseHandler != null)
            {
                _pauseHandler.PauseChanged -= OnPauseStateChanged;
                UnityEngine.Object.Destroy(_pauseHandler.gameObject);
                _pauseHandler = null;
            }
            if (_writer != null)
            {
                _pendingRows.Append(Time.frameCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                    .Append(",,,,,").Append(Csv(SceneManager.GetActiveScene().name)).AppendLine(",recording_stopped");
                _writer.Write(_pendingRows);
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }
            _mainThreadRecorder.Dispose();
            _renderThreadRecorder.Dispose();
            _memoryRecorder.Dispose();
        }
    }
}
#endif
