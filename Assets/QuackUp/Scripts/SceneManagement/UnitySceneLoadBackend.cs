using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuackUp.SceneManagement
{
    public sealed class UnitySceneLoadBackend : ISceneLoadBackend
    {
        private static Scene FindScene(int handle)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.handle == handle) return scene;
            }
            return default;
        }
        public int GetActiveSceneHandle() => SceneManager.GetActiveScene().handle;
        public string GetScenePath(int sceneHandle) => FindScene(sceneHandle).path;
        public ISceneLoadOperation BeginLoad(string path, LoadSceneMode mode)
        {
            var operation = SceneManager.LoadSceneAsync(path, mode);
            return operation == null ? null : new Operation(operation);
        }
        public bool SetActiveScene(int sceneHandle) => SceneManager.SetActiveScene(FindScene(sceneHandle));
        public async UniTask UnloadScene(int sceneHandle)
        {
            var operation = SceneManager.UnloadSceneAsync(FindScene(sceneHandle));
            if (operation != null) await operation;
        }
        public IDisposable SubscribeLoaded(Action<int, LoadSceneMode> callback)
        {
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> handler = (scene, mode) => callback(scene.handle, mode);
            SceneManager.sceneLoaded += handler;
            return new Subscription(() => SceneManager.sceneLoaded -= handler);
        }
        public IDisposable SubscribeActiveChanged(Action<int, int> callback)
        {
            UnityEngine.Events.UnityAction<Scene, Scene> handler = (previous, current) => callback(previous.handle, current.handle);
            SceneManager.activeSceneChanged += handler;
            return new Subscription(() => SceneManager.activeSceneChanged -= handler);
        }
        private sealed class Subscription : IDisposable
        {
            private Action _dispose;
            public Subscription(Action dispose) => _dispose = dispose;
            public void Dispose() { var dispose = _dispose; _dispose = null; dispose?.Invoke(); }
        }
        private sealed class Operation : ISceneLoadOperation
        {
            private readonly AsyncOperation _operation;
            public Operation(AsyncOperation operation) => _operation = operation;
            public float Progress => _operation.progress;
            public bool IsDone => _operation.isDone;
            public bool AllowSceneActivation { get => _operation.allowSceneActivation; set => _operation.allowSceneActivation = value; }
            public async UniTask WaitForCompletion() { await _operation; }
        }
    }
}
