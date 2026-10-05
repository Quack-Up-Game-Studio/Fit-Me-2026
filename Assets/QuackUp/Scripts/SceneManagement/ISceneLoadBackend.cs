using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace QuackUp.SceneManagement
{
    public interface ISceneLoadOperation
    {
        float Progress { get; }
        bool IsDone { get; }
        bool AllowSceneActivation { get; set; }
        UniTask WaitForCompletion();
    }

    public interface ISceneLoadBackend
    {
        int GetActiveSceneHandle();
        string GetScenePath(int sceneHandle);
        ISceneLoadOperation BeginLoad(string path, LoadSceneMode mode);
        bool SetActiveScene(int sceneHandle);
        UniTask UnloadScene(int sceneHandle);
        IDisposable SubscribeLoaded(Action<int, LoadSceneMode> callback);
        IDisposable SubscribeActiveChanged(Action<int, int> callback);
    }
}
