# Android Performance Diagnostics Build

This opt-in recorder writes lightweight CSV frame/lifecycle diagnostics to `Application.persistentDataPath`. It is not a replacement for Unity Profiler: it captures unscaled frame delta, available `ProfilerRecorder` last values for Main Thread, Render Thread, and Total Used Memory, plus focus, low-memory, and active-scene markers. The file includes device, OS, Unity/application version, graphics, quality, and target frame-rate metadata.

## Build

1. In **Project Settings → Player → Android → Script Compilation**, add `FITME_PERFORMANCE_DIAGNOSTICS` to the Android scripting define symbols for the diagnostic build only.
2. Enable **Development Build** in the Android Build Profile. Do not enable Deep Profile for tester captures; it adds substantial overhead.
3. Build/install the APK. Normal release builds must omit `FITME_PERFORMANCE_DIAGNOSTICS` and have no recorder enabled.

## Tester capture and submission

1. Launch the diagnostic build and reproduce the issue, including cold launch and background/resume as applicable.
2. Keep the app alive for several seconds after the reproduction; rows flush every 120 frames, and the app closes by flushing pending data.
3. Find the file named `fitme-performance-*.csv` in the app's private `Application.persistentDataPath` (normally `/storage/emulated/0/Android/data/<application-id>/files/`). Use Android's Files app or a file-transfer utility; if scoped storage prevents access, use a file-sharing/export UI or pull it via Android Debug Bridge. Send the CSV to the developer with a short description of the actions and approximate hitch time.
4. Preserve the app data until the file is copied. Uninstalling or clearing app storage deletes the CSV.

## Interpretation and limits

`unscaledDeltaTimeMs` is the observed frame interval, so large values identify hitch frames even if frame-rate limiting is active. Profiler recorder availability is reported in the metadata; blank counter columns mean a counter was unavailable on that Unity/player/device, not zero cost. Recorder values are last sampled profiler-counter values, not GPU frame time. This first version does not record GPU timing, Android scheduler/thermal state, or full Unity Profiler samples. Use connected Unity Profiler or Android system traces for deeper attribution. Compare diagnostic-build results cautiously against release because Development Build and sampling introduce overhead.
