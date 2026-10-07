using System;
using Debug = QuackUp.Utils.DebugUtils;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using PrimeTween;
using QuackUp.Audio;
using QuackUp.Utils;
using R3;
using Redcode.Extensions;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace QuackUp.SceneManagement
{

    #region Events
    public struct LoadingSceneAnimationFinishedEvent { }
    public struct LoadSceneStageEvent
    {
        public LoadSceneStage Stage { get; private set; }
        public SceneType PreviousSceneType { get; set; }
        public SceneType NextSceneType { get; set; }

        public LoadSceneStageEvent(LoadSceneStage stage, SceneType previousSceneType, SceneType nextSceneType)
        {
            Stage = stage;
            PreviousSceneType = previousSceneType;
            NextSceneType = nextSceneType;
        }
    }
    #endregion

    #region Enums

    public enum SceneType
    {
        Splash,
        MainMenu,
        Loading,
        LevelSelect,
        ModeSelect,
        Gameplay,
        Tutorial,
    }

    public enum LoadSceneStage
    {
        StartOut,
        FinishOut,
        StartLoading,
        FinishLoading,
        StartIn,
        FinishIn,
        CancelledBeforeLoad = 6
    }

        #endregion
    
    [Serializable]
    public class LoadSceneManager : IDisposable, IStartable
    {
        #region Inspectors
        [Title("Debug")]
        [SerializeField] private SceneType debugSceneType;
        [Button("Debug Load Scene")]
        private void DebugLoadScene()
        {
            LoadScene(debugSceneType, LoadSceneMode.Single, false).Forget();
        }
        #endregion

        #region Properties

        public string NextScene { get; private set; }
        public LoadSceneMode LoadSceneMode { get; private set; }
        public bool FirstSceneLoaded { get; private set; }
        public SceneType PreviousSceneType { get; private set; }
        public SceneType CurrentSceneType { get; private set; }
        public SceneType NextSceneType { get; private set; }


        #endregion
        
        #region Fields
        private readonly LoadSceneManagerConfig _config;
        private readonly IAudioManager _audioManager;
        private readonly ITransitionable _currentTransitionScreen;
        private readonly IPublisher<LoadSceneStageEvent> _loadSceneStageEventPublisher;
        private readonly ISceneLoadBackend _backend;
        private Request _request;
        private bool _disposed;

        private sealed class Request
        {
            public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            public readonly SceneType Previous, Next, EffectiveScene;
            public readonly string Path;
            public readonly LoadSceneMode Mode;
            public readonly int SourceHandle;
            public ISceneLoadOperation Operation;
            public Exception CallbackError;
            public bool RevealStarted;
            public bool RevealCompleted;
            public IDisposable Loaded, ActiveChanged;
            public readonly UniTaskCompletionSource Revealed = new UniTaskCompletionSource();
            public readonly UniTaskCompletionSource Stopped = new UniTaskCompletionSource();
            public Request(SceneType previous, SceneType next, SceneType effectiveScene, string path, LoadSceneMode mode, int sourceHandle)
            { Previous = previous; Next = next; EffectiveScene = effectiveScene; Path = path; Mode = mode; SourceHandle = sourceHandle; }
            public void Detach()
            {
                var loaded = Loaded; Loaded = null;
                var active = ActiveChanged; ActiveChanged = null;
                try { loaded?.Dispose(); }
                finally { active?.Dispose(); }
            }
        }
        #endregion

        #region Injection

        [Inject]
        public LoadSceneManager(
            LoadSceneManagerConfig config,
            IAudioManager audioManager,
            ITransitionable transitionScreen,
            IPublisher<LoadSceneStageEvent> loadSceneStageEventPublisher,
            ISceneLoadBackend backend)
        {
            _config = config;
            _backend = backend;
            _audioManager = audioManager;
            _loadSceneStageEventPublisher = loadSceneStageEventPublisher;
            _currentTransitionScreen = transitionScreen;
        }

        #endregion

        #region Life Cycle

        public void Start()
        {
            if (_disposed || FirstSceneLoaded) return;
            var currentSceneName = _backend.GetScenePath(_backend.GetActiveSceneHandle());
            _config.TryGetSceneType(currentSceneName, out var sceneType);
            CurrentSceneType = sceneType ?? SceneType.MainMenu;
            PreviousSceneType = CurrentSceneType;
            _loadSceneStageEventPublisher.Publish(new LoadSceneStageEvent(LoadSceneStage.FinishLoading, PreviousSceneType, CurrentSceneType));
            _loadSceneStageEventPublisher.Publish(new LoadSceneStageEvent(LoadSceneStage.FinishIn, PreviousSceneType, CurrentSceneType));
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var request = _request;
            if (request != null)
            {
                try { request.Detach(); }
                finally
                {
                    try { request.Cancellation.Cancel(); }
                    finally
                    {
                        if (request.Operation != null) request.Operation.AllowSceneActivation = true;
                        request.Stopped.TrySetResult();
                        request.Revealed.TrySetResult();
                    }
                }
            }
        }
        
        #region Scene Loading

        public async UniTask ReloadScene(LoadSceneMode loadSceneMode, bool useLoadingScene)
        {
            var currentSceneName = _backend.GetScenePath(_backend.GetActiveSceneHandle());
            var sceneEntry = _config.SceneReferences
                .FirstOrDefault(x => x.Value.Path == currentSceneName);
            if (sceneEntry.Value == null)
            {
                Debug.LogError($"Current scene '{currentSceneName}' not found in the dictionary.");
                return;
            }

            await LoadScene(sceneEntry.Key, loadSceneMode, useLoadingScene);
        }
        
        public async UniTask LoadScene(SceneType sceneType, LoadSceneMode loadSceneMode, bool useLoadingScene)
        {
            if (_disposed || _request != null) return;
            if (!_config.SceneReferences.TryGetValue(sceneType, out var reference))
            {
                Debug.LogError($"Scene {sceneType} not found in the dictionary.");
                return;
            }
            var path = reference.Path;
            if (useLoadingScene)
            {
                if (!_config.SceneReferences.TryGetValue(SceneType.Loading, out var loading))
                {
                    Debug.LogError("Loading scene not found in the dictionary.");
                    return;
                }
                // Existing feature loads only the loading scene, not a second destination.
                path = loading.Path;
            }
            var request = new Request(CurrentSceneType, sceneType, useLoadingScene ? SceneType.Loading : sceneType, path, loadSceneMode, _backend.GetActiveSceneHandle());
            _request = request;
            NextScene = reference.Path;
            LoadSceneMode = request.Mode;
            PreviousSceneType = request.Previous;
            NextSceneType = request.Next;
            var handedOff = false;
            try
            {
                Publish(request, LoadSceneStage.StartOut);
                _audioManager.PlayAudioOneShot(_config.TransitionSfx, Vector3.zero);
                request.Cancellation.Token.ThrowIfCancellationRequested();
                await _currentTransitionScreen.TransitionIn(request.Cancellation.Token);
                request.Cancellation.Token.ThrowIfCancellationRequested();
                Publish(request, LoadSceneStage.FinishOut);
                request.Cancellation.Token.ThrowIfCancellationRequested();
                NextScene = request.Path;
                handedOff = true;
                RunLoad(request).Forget();
            }
            catch (OperationCanceledException) when (request.Cancellation.IsCancellationRequested)
            {
                await Recover(request);
            }
            catch (Exception error)
            {
                try { await Recover(request); }
                catch
                {
                    UniTask.FromException(error).Forget();
                    throw;
                }
                throw;
            }
            finally
            {
                if (!handedOff) Release(request);
            }
        }

        private void Publish(Request request, LoadSceneStage stage)
        {
            if (!_disposed && request.CallbackError == null)
                _loadSceneStageEventPublisher.Publish(new LoadSceneStageEvent(stage, request.Previous, request.Next));
        }

        private async UniTask Recover(Request request)
        {
            if (_disposed) return;
            await UniTask.WhenAny(ObserveView(_currentTransitionScreen.TransitionOut()), request.Stopped.Task);
            if (!_disposed)
                _loadSceneStageEventPublisher.Publish(new LoadSceneStageEvent(
                    LoadSceneStage.CancelledBeforeLoad, request.Previous, request.Previous));
        }

        private async UniTask ObserveView(UniTask task)
        {
            try { await task; }
            catch (Exception error)
            {
                if (!_disposed) throw;
                // A detached borrowed view can still fault after shutdown. Observe it.
                UniTask.FromException(error).Forget();
            }
        }

        private async UniTask RunLoad(Request request)
        {
            try
            {
                request.Cancellation.Token.ThrowIfCancellationRequested();
                request.ActiveChanged = _backend.SubscribeActiveChanged((previous, current) =>
                {
                    try
                    {
                        if (_disposed || _backend.GetScenePath(current) != request.Path) return;
                        request.ActiveChanged?.Dispose();
                        request.ActiveChanged = null;
                        request.RevealStarted = true;
                        Reveal(request, previous).Forget();
                    }
                    catch (Exception error) { request.CallbackError ??= error; }
                });
                request.Operation = _backend.BeginLoad(request.Path, request.Mode);
                if (request.Operation == null) throw new InvalidOperationException("Async operation is null.");
                request.Operation.AllowSceneActivation = false;
                request.Loaded = _backend.SubscribeLoaded((scene, mode) =>
                {
                    try
                    {
                        if (_disposed || mode != request.Mode || _backend.GetScenePath(scene) != request.Path) return;
                        request.Loaded?.Dispose();
                        request.Loaded = null;
                        CurrentSceneType = request.EffectiveScene;
                        FirstSceneLoaded = true;
                        Time.timeScale = 1f;
                        _backend.SetActiveScene(scene);
                        Publish(request, LoadSceneStage.FinishLoading);
                    }
                    catch (Exception error)
                    {
                        request.CallbackError = error;
                        if (!request.RevealStarted) request.Revealed.TrySetResult();
                    }
                });
                Publish(request, LoadSceneStage.StartLoading);
                try
                {
                    if (!_config.MinimumLoadingScreenDuration)
                        await UniTask.WaitUntil(() => request.Operation.Progress >= 0.9f,
                            cancellationToken: request.Cancellation.Token);
                    else
                        await UniTask.WhenAll(UniTask.WaitUntil(() => request.Operation.Progress >= 0.9f,
                                cancellationToken: request.Cancellation.Token),
                            UniTask.WaitForSeconds(_config.LoadingScreenDuration, ignoreTimeScale: true,
                                cancellationToken: request.Cancellation.Token));
                }
                catch (OperationCanceledException) when (request.Cancellation.IsCancellationRequested) { }
                if (!_disposed)
                {
                    CurrentSceneType = request.EffectiveScene;
                    FirstSceneLoaded = true;
                    Time.timeScale = 1f;
                }
                request.Operation.AllowSceneActivation = true;
                await request.Operation.WaitForCompletion();
                if (request.RevealStarted || request.CallbackError == null) await request.Revealed.Task;
                if (request.CallbackError != null)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(request.CallbackError).Throw();
            }
            catch (Exception error)
            {
                request.CallbackError ??= error;
                if (request.Operation != null)
                {
                    request.Operation.AllowSceneActivation = true;
                    await request.Operation.WaitForCompletion();
                    if (!_disposed)
                    {
                        if (request.RevealStarted) await request.Revealed.Task;
                        else
                        {
                            if (request.Mode == LoadSceneMode.Additive) await _backend.UnloadScene(request.SourceHandle);
                            if (!_disposed)
                                await UniTask.WhenAny(ObserveView(_currentTransitionScreen.TransitionOut()), request.Stopped.Task);
                        }
                    }
                }
                else await Recover(request);
                throw;
            }
            finally
            {
                Release(request);
            }
        }

        private async UniTask Reveal(Request request, int source)
        {
            try
            {
                if (request.Mode == LoadSceneMode.Additive) await _backend.UnloadScene(source);
                if (_disposed) return;
                Publish(request, LoadSceneStage.StartIn);
                if (_disposed) return;
                await _currentTransitionScreen.TransitionOut();
                request.RevealCompleted = true;
                Publish(request, LoadSceneStage.FinishIn);
            }
            catch (Exception error)
            {
                request.CallbackError ??= error;
                if (!_disposed && !request.RevealCompleted)
                {
                    try
                    {
                        await _currentTransitionScreen.TransitionOut();
                    }
                    catch (Exception fallbackError)
                    {
                        request.CallbackError ??= fallbackError;
                    }
                }
            }
            finally { request.Revealed.TrySetResult(); }
        }

        private void Release(Request request)
        {
            try { request.Detach(); }
            finally
            {
                request.Cancellation.Dispose();
                if (ReferenceEquals(_request, request)) _request = null;
            }
        }

        public void CancelLoadScene() => _request?.Cancellation.Cancel();
        #endregion
    }
}
