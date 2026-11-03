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
        private int _framesSinceFlush;
        private bool _disposed;

        public PerformanceDiagnosticsRecorder()
        {
            _mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            _renderThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
            _memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1);
        }

        public void Start()
        {
            string fileName = FilePrefix + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv";
            string path = Path.Combine(Application.persistentDataPath, fileName);
            _writer = new StreamWriter(path, false, new UTF8Encoding(false), 4096);
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
            _writer.WriteLine("# frame,utc,unscaledDeltaTimeMs,mainThreadLastFrameNs,renderThreadLastFrameNs,totalUsedMemoryBytes,scene,marker");
            _writer.Flush();
            AddMarker("recording_started");
            Application.focusChanged += OnFocusChanged;
            Application.pauseStateChanged += OnPauseStateChanged;
            Application.lowMemory += OnLowMemory;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Debug.Log("Performance diagnostics recording to " + path);
        }

        public void Tick()
        {
            if (_writer == null || _disposed) return;

            _pendingRows.Append(Time.frameCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append((Time.unscaledDeltaTime * 1000f).ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                .Append(LastValue(_mainThreadRecorder)).Append(',')
                .Append(LastValue(_renderThreadRecorder)).Append(',')
                .Append(LastValue(_memoryRecorder)).Append(',')
                .Append(Csv(SceneManager.GetActiveScene().name)).AppendLine(",");

            _framesSinceFlush++;
            if (_framesSinceFlush >= FlushIntervalFrames || _pendingRows.Length >= MaxPendingRows * 128)
                Flush();
        }

        private void OnFocusChanged(bool hasFocus) => AddMarker(hasFocus ? "focus_gained" : "focus_lost");

        private void OnPauseStateChanged(PauseState pauseState)
        {
            AddMarker(pauseState == PauseState.Paused ? "app_paused" : "app_resumed");
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Application.focusChanged -= OnFocusChanged;
            Application.pauseStateChanged -= OnPauseStateChanged;
            Application.lowMemory -= OnLowMemory;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
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
