using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using R3;

namespace QuackUp.SocialService
{
    public interface ICloudSaveService
    {
        /// <summary>
        /// Save data to the cloud service.
        /// </summary>
        /// <returns></returns>
        UniTask<bool> SaveToService(SaveToServiceParameters parameters);
        /// <summary>
        /// Load data from the cloud service but do not directly apply it to the game data.
        /// Instead, the implementation should handle the loading process and communicate the result through the <see cref="OnSyncResult"/> observable.
        /// </summary>
        /// <returns></returns>
        UniTask<bool> LoadFromService(LoadFromServiceParameters parameters);
        /// <summary>
        /// An observable that emits the result of the synchronization process after loading from the cloud service.
        /// </summary>
        Observable<bool> OnSyncResult { get; }
    }
    
    public class MockCloudSaveService : ICloudSaveService
    {
        public UniTask<bool> SaveToService(SaveToServiceParameters parameters)
        {
            DebugUtils.Log("Mock saved");
            return UniTask.FromResult(false);
        }

        public UniTask<bool> LoadFromService(LoadFromServiceParameters parameters)
        {
            DebugUtils.Log("Mock loaded");
            return UniTask.FromResult(false);
        }

        public Observable<bool> OnSyncResult { get; } = new Subject<bool>();
    }
    
    public class SaveToServiceParameters
    {
        public bool ShowSelectionUI { get; private set; }
        
        private SaveToServiceParameters() { }
        
        public static SaveToServiceParameters Default => new();
        
        public class Builder
        {
            private Builder() { }
            public static Builder CreateBuilder() => new();
            private readonly SaveToServiceParameters _parameters = new();
            
            public Builder WithShowSelectionUI(bool show)
            {
                _parameters.ShowSelectionUI = show;
                return this;
            }
            
            public SaveToServiceParameters Build()
            {
                return _parameters;
            }
        }
        
    }
    
    public class LoadFromServiceParameters
    {
        public bool ShowSelectionUI { get; private set; }
        
        private LoadFromServiceParameters() { }
        
        public static LoadFromServiceParameters Default => new();
        
        public class Builder
        {
            private Builder() { }
            public static Builder CreateBuilder() => new();
            private readonly LoadFromServiceParameters _parameters = new();
            
            public Builder WithShowSelectionUI(bool show)
            {
                _parameters.ShowSelectionUI = show;
                return this;
            }
            
            public LoadFromServiceParameters Build()
            {
                return _parameters;
            }
        }
        
    }
}