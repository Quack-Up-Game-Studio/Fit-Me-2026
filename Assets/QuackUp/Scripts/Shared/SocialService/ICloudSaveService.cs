using System;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using R3;

namespace FitMe.Shared
{
    public interface ICloudSaveService
    {
        /// <summary>
        /// Save data to the cloud service.
        /// </summary>
        /// <returns></returns>
        UniTask<bool> SaveToService();
        /// <summary>
        /// Load data from the cloud service but do not directly apply it to the game data.
        /// Instead, the implementation should handle the loading process and communicate the result through the <see cref="OnSyncResult"/> observable.
        /// </summary>
        /// <returns></returns>
        UniTask<bool> LoadFromService();
        /// <summary>
        /// An observable that emits the result of the synchronization process after loading from the cloud service.
        /// </summary>
        Observable<bool> OnSyncResult { get; }
    }
    
    public class MockCloudSaveService : ICloudSaveService
    {
        public UniTask<bool> SaveToService()
        {
            DebugUtils.Log("Mock saved");
            return UniTask.FromResult(false);
        }

        public UniTask<bool> LoadFromService()
        {
            DebugUtils.Log("Mock loaded");
            return UniTask.FromResult(false);
        }

        public Observable<bool> OnSyncResult { get; } = new Subject<bool>();
    }
}