using System;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using R3;

namespace FitMe.Shared
{
    public interface ICloudSaveService
    {
        UniTask<bool> SaveToService(byte[] data);
        UniTask<Tuple<bool, byte[]>> LoadFromService();
        Observable<Tuple<bool, byte[]>> OnLoadFromService { get; }
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