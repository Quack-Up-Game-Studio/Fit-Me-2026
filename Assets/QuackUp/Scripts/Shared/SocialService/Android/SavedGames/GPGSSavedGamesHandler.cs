using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Shared;
using GooglePlayGames;
using QuackUp.GPGS;
using QuackUp.Save;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.SocialService.Android
{
    public class GPGSSavedGamesHandler : ICloudSaveService, IDisposable
    {
        private readonly GPGSSavedGames _gpgsSavedGames;
        private readonly MessagePackSaveManager _messagePackSaveManager;
        private readonly RemoteSaveResolver _remoteSaveResolver;

        public Observable<bool> OnSyncResult => _onSyncResult;
        private readonly Subject<bool> _onSyncResult = new();
        
        private IDisposable _subscriptions;
        
        [Inject]
        public GPGSSavedGamesHandler(
            GPGSAuthenticationManager authenticationManager,
            GPGSSavedGames gpgsSavedGames,
            MessagePackSaveManager messagePackSaveManager,
            RemoteSaveResolver remoteSaveResolver)
        {
            _gpgsSavedGames = gpgsSavedGames;
            _messagePackSaveManager = messagePackSaveManager;
            _remoteSaveResolver = remoteSaveResolver;
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _gpgsSavedGames.OnLoadFromService
                .SubscribeAwait((x, ct) => OnSaveLoaded(x, ct), AwaitOperation.Switch)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }
        
        private async UniTask OnSaveLoaded((bool success, byte[] bytes) result, CancellationToken cancellationToken)
        {
            if (!result.success)
            {
                _onSyncResult.OnNext(false);
                return;
            }
            var deserializedLocal = _messagePackSaveManager.DeserializeSaveDataFromZipBytes(_messagePackSaveManager.GetZipBytes());
            var deserializedRemote = _messagePackSaveManager.DeserializeSaveDataFromZipBytes(result.bytes);
            var conflictSolution = await _remoteSaveResolver.ResolveConflictAsync(deserializedLocal, deserializedRemote);
            if (cancellationToken.IsCancellationRequested) return;
            switch (conflictSolution)
            {
                case ConflictSolution.UseLocal:
                    _messagePackSaveManager.LoadFromDeserializedData(deserializedLocal);
                    DebugUtils.Log("Using local save data.");
                    _onSyncResult.OnNext(true);
                    break;
                case ConflictSolution.UseRemote:
                    _messagePackSaveManager.LoadFromDeserializedData(deserializedRemote);
                    DebugUtils.Log("Using remote save data.");
                    _onSyncResult.OnNext(true);
                    break;
                case ConflictSolution.Abort:
                    DebugUtils.Log("Aborting sync.");
                    _onSyncResult.OnNext(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public async UniTask<bool> SaveToService()
        {
            if (!PlayGamesPlatform.Instance.IsAuthenticated()) return false;
            var saveObject = _messagePackSaveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>();
            var saveData = saveObject.GetSaveData<PlayerRecordSaveData>();
            var totalPlayTime = saveData.TotalPlayTime;
            var playerId = PlayGamesPlatform.Instance.GetUserId();
            DebugUtils.Log($"Saving with player ID: {playerId} and total playtime: {totalPlayTime:g}.");
            saveData.PlayerID = playerId;
            _messagePackSaveManager.Save(saveObject);
            var zipBytes = _messagePackSaveManager.GetZipBytes();
            var result = await _gpgsSavedGames.SaveToService(new GPGSSaveData
            {
                Data = zipBytes,
                TotalPlaytime = totalPlayTime,
            }, false);
            DebugUtils.Log($"Save completed with result: {result}");
            return result;
            
        } 

        public async UniTask<bool> LoadFromService()
        {
            var result = await _gpgsSavedGames.LoadFromService(false);
            return result.success;
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
    }
}
