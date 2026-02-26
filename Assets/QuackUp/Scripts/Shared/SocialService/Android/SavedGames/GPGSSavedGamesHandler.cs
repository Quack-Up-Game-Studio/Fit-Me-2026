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

        public Observable<bool> OnSyncResult => _onSyncResult;
        private readonly Subject<bool> _onSyncResult = new();
        
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
            _gpgsSavedGames.OnLoadFromService
                .Subscribe(OnSaveLoaded)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }
        
        private void OnSaveLoaded(Tuple<bool, byte[]> result)
        {
            if (!result.Item1)
            {
                _onSyncResult.OnNext(false);
                return;
            }
            _messagePackSaveManager.LoadFromZipBytes(result.Item2);
            _onSyncResult.OnNext(true);
        }
        
        public UniTask<bool> SaveToService()
        {
            var playerSaveData = _messagePackSaveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>()
                .GetSaveData<PlayerRecordSaveData>();
            var totalPlayTime = playerSaveData.TotalPlayTime;
            var zipBytes = _messagePackSaveManager.GetZipBytes();
            return _gpgsSavedGames.SaveToService(new GPGSSaveData
            {
                Data = zipBytes,
                TotalPlaytime = totalPlayTime,
            }, false);
        } 

        public async UniTask<bool> LoadFromService()
        {
            var result = await _gpgsSavedGames.LoadFromService(false);
            return result.Item1;
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
    }
}
