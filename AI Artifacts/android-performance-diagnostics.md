# Android Performance Diagnostics Build

This opt-in recorder writes lightweight CSV frame/lifecycle diagnostics to `Application.persistentDataPath`. It is not a replacement for Unity Profiler: it captures unscaled frame delta, available `ProfilerRecorder` last values for Main Thread, Render Thread, and Total Used Memory, plus app pause/resume, focus, low-memory, active-scene, block-explosion, and pooled-VFX markers. The file includes device, OS, Unity/application version, graphics, quality, and target frame-rate metadata.

## Build

1. Open the Unity project with its configured Unity version and select **Fit Me → Build Android Performance Diagnostics APK**. The build helper uses `BuildOptions.Development` and adds `FITME_PERFORMANCE_DIAGNOSTICS` only to that player build; it does not change Player Settings scripting symbols.
2. The APK is written to `Builds/Android/FitMe-Performance-Diagnostics.apk`. Do not enable Deep Profile for tester captures; it adds substantial overhead.
3. This test APK is debug-signed; the helper temporarily disables custom-keystore signing and restores the project's previous setting. Install and distribute it only as a tester build, not as a release. Normal release builds omit `FITME_PERFORMANCE_DIAGNOSTICS` and have no recorder or share control enabled.

## Tester capture and submission

1. Launch the diagnostic build and reproduce the issue, including cold launch and background/resume as applicable.
2. Keep the app alive for several seconds after the reproduction; rows flush every 120 frames, and the app closes by flushing pending data. A force-stop or OS process kill can lose the unflushed tail.
3. Open **Settings → Share Performance Log**. On diagnostic Android builds this creates the button in the settings panel and opens Android's standard share chooser with the current CSV attached. Pick a messaging, mail, or file-sharing app and send the attachment to the developer with a short description of the actions and approximate hitch time.
4. If sharing fails or the chooser has no suitable app, the CSV remains in app-private `Application.persistentDataPath` (normally `/storage/emulated/0/Android/data/<application-id>/files/`). Preserve app data until it is copied; uninstalling or clearing app storage deletes it.

## Interpretation and limits

`unscaledDeltaTimeMs` is the observed frame interval, so large values identify hitch frames even if frame-rate limiting is active. Profiler recorder availability is reported in the metadata; blank counter columns mean a counter was unavailable on that Unity/player/device, not zero cost. Recorder values are last sampled profiler-counter values, not GPU frame time.

The marker column records `block_explode_start` and `block_explode_animation_complete` with block instance ID, color, and fit type. Each pooled effect also records `vfx_started` and `vfx_returned`; both rows include effect kind, prefab, and block ID (`0` for placement VFX). Start rows include pool setup prewarm creations, whether checkout instantiated an object on demand, cumulative pool creations, active count and peak per prefab, and aggregate active/peak count. These are event snapshots, not per-particle samples. Correlate the event timestamps and active-count intervals with adjacent frame rows; the logs show overlap and pool misses but do not prove that VFX caused a hitch.

App lifecycle markers include `app_paused`, `app_resumed`, and `app_background_duration_ms`, measured monotonically between Unity's pause callbacks (not as an Android scheduler/idle-time metric). The first 30 frame rows after `OnApplicationPause(false)` carry `post_resume_frame_index` and `callback_to_update_ms` in the marker column. On index 1, `unscaled_delta_includes_background=1` warns that `unscaledDeltaTimeMs` and profiler last-values may span the suspended interval; use the callback-to-update timing and subsequent frame rows to assess active recovery.

This recorder does not capture GPU timing, Android scheduler/thermal state, or full Unity Profiler samples. Use connected Unity Profiler or Android system traces for deeper attribution. Compare diagnostic-build results cautiously against release because Development Build and sampling introduce overhead. The share button, VFX/lifecycle events, and Android FileProvider integration are compiled only for diagnostic builds; regular releases do not expose or record them.
