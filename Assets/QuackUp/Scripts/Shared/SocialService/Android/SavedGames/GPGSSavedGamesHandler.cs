using System;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Shared;
using QuackUp.GPGS;
using QuackUp.Save;
using R3;
using VContainer;

namespace FitMe.SocialService.Android
{
    public class GPGSSavedGamesHandler : ICloudSaveService, IDisposable
    {
        private readonly GPGSSavedGames _gpgsSavedGames;
        private readonly MessagePackSaveManager _messagePackSaveManager;

        public Observable<Tuple<bool, byte[]>> OnLoadFromService => _gpgsSavedGames.OnLoadFromService;
        private IDisposable _subscriptions;
        
        [Inject]
        public GPGSSavedGamesHandler(
            GPGSSavedGames gpgsSavedGames,
            MessagePackSaveManager messagePackSaveManager)
        {
            _gpgsSavedGames = gpgsSavedGames;
            _messagePackSaveManager = messagePackSaveManager;
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            OnLoadFromService.Subscribe(OnSaveLoaded)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }
        
        private void OnSaveLoaded(Tuple<bool, byte[]> result)
        {
            _messagePackSaveManager.LoadFromZipBytes(result.Item2);
        }
        
        public UniTask<bool> SaveToService(byte[] data)
        {
            var playerSaveData = _messagePackSaveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>()
                .GetSaveData<PlayerRecordSaveData>();
            var totalPlayTime = playerSaveData.TotalPlayTime;
            return _gpgsSavedGames.SaveToService(new GPGSSaveData
            {
                Data = data,
                TotalPlaytime = totalPlayTime,
            }, false);
        } 

        public UniTask<Tuple<bool, byte[]>> LoadFromService()
        {
            return _gpgsSavedGames.LoadFromService(false);
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
    }
}
