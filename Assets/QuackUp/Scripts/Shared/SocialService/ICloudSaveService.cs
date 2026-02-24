using System;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;

namespace FitMe.Shared
{
    public interface ICloudSaveService
    {
        UniTask<bool> SaveToService(byte[] data);
        UniTask<Tuple<bool, byte[]>> LoadFromService();
        Observable<Tuple<bool, byte[]>> OnLoadFromService { get; }
    }
    
    [Serializable]
    public class MockCloudSaveServiceInstaller : IInstaller
    {
        [ShowInInspector] private InspectorPlaceholder _title;
        
        public void Install(IContainerBuilder builder)
        {
            builder.Register<MockCloudSaveService>(Lifetime.Singleton)
                .As<ICloudSaveService>();
        }
    }
    
    public class MockCloudSaveService : ICloudSaveService
    {
        public UniTask<bool> SaveToService(byte[] data)
        {
            DebugUtils.Log("Mock saved");
            return UniTask.FromResult(false);
        }

        public UniTask<Tuple<bool, byte[]>> LoadFromService()
        {
            DebugUtils.Log("Mock loaded");
            return UniTask.FromResult(new Tuple<bool, byte[]>(false, Array.Empty<byte>()));
        }

        public Observable<Tuple<bool, byte[]>> OnLoadFromService { get; } = new Subject<Tuple<bool, byte[]>>();
    }
}