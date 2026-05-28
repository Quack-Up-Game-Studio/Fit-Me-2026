#if UNITY_ANDROID
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using GooglePlayGames;
using QuackUp.GPGS;
using QuackUp.Save;
using QuackUp.SocialService;
using QuackUp.Utils;
using R3;
using VContainer;
using VContainer.Unity;

namespace FitMe.SocialService.Android
{
    public class GPGSSavedGamesHandler : ICloudSaveService, IInitializable, IDisposable
    {
        private readonly GPGSSavedGames _gpgsSavedGames;
        private readonly MessagePackSaveManager _messagePackSaveManager;
        private readonly RemoteSaveResolver _remoteSaveResolver;
        private readonly GPGSAuthenticationManager _authenticationManager;
        private readonly GPGSAuthenticationManagerConfig _authConfig;
        private readonly GPGSSavedGamesConfig _saveGamesConfig;

        private readonly UniTaskCompletionSource _cloudLoadCompletedTcs = new();

        public Observable<bool> OnSyncResult => _onSyncResult;
        private readonly Subject<bool> _onSyncResult = new();
        
        private IDisposable _subscriptions;
        
        [Inject]
        public GPGSSavedGamesHandler(
            GPGSAuthenticationManager authenticationManager,
            GPGSAuthenticationManagerConfig authConfig,
            GPGSSavedGames gpgsSavedGames,
            GPGSSavedGamesConfig saveGamesConfig,
            MessagePackSaveManager messagePackSaveManager,
            RemoteSaveResolver remoteSaveResolver)
        {
            _authenticationManager = authenticationManager;
            _authConfig = authConfig;
            _gpgsSavedGames = gpgsSavedGames;
            _saveGamesConfig = saveGamesConfig;
            _messagePackSaveManager = messagePackSaveManager;
            _remoteSaveResolver = remoteSaveResolver;
            Subscribe();
        }

        public void Initialize()
        {
            InitializeAsync().Forget();
        }

        private async UniTaskVoid InitializeAsync()
        {
            try
            {
                if (!_authConfig.AutoAuthenticateOnStart)
                {
                    DebugUtils.Log("GPGSSavedGamesHandler: AutoAuthenticateOnStart is disabled. Marking save data ready.");
                    _messagePackSaveManager.MarkSaveDataReady();
                    return;
                }

                DebugUtils.Log("GPGSSavedGamesHandler: Waiting for silent authentication result...");
                var status = await _authenticationManager.OnAuthenticationResult.FirstAsync();
                DebugUtils.Log($"GPGSSavedGamesHandler: Silent authentication completed with status: {status}");

                if (status != GooglePlayGames.BasicApi.SignInStatus.Success)
                {
                    DebugUtils.Log("GPGSSavedGamesHandler: Silent authentication failed. Marking save data ready.");
                    _messagePackSaveManager.MarkSaveDataReady();
                    return;
                }

                if (!_saveGamesConfig.LoadAutomaticallyAfterAuthentication)
                {
                    DebugUtils.Log("GPGSSavedGamesHandler: LoadAutomaticallyAfterAuthentication is disabled. Marking save data ready.");
                    _messagePackSaveManager.MarkSaveDataReady();
                    return;
                }

                DebugUtils.Log("GPGSSavedGamesHandler: Waiting for cloud save load...");
                var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(8));
                var completedTaskIndex = await UniTask.WhenAny(_cloudLoadCompletedTcs.Task, timeoutTask);
                if (completedTaskIndex == 1)
                {
                    DebugUtils.LogWarning("GPGSSavedGamesHandler: Cloud save load timed out. Forcing save data ready.");
                    _messagePackSaveManager.MarkSaveDataReady();
                }
            }
            catch (Exception ex)
            {
                DebugUtils.LogError($"GPGSSavedGamesHandler: Exception in InitializeAsync: {ex}");
                _messagePackSaveManager.MarkSaveDataReady();
            }
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
            try
            {
                DeserializedSaveData deserializedLocal = null;
                DeserializedSaveData deserializedRemote = null;
                
                var localBytes = _messagePackSaveManager.GetZipBytes();
                if (localBytes != null)
                {
                    try
                    {
                        deserializedLocal = _messagePackSaveManager.DeserializeSaveDataFromZipBytes(localBytes);
                    }
                    catch (Exception e)
                    {
                        DebugUtils.LogError($"Error during local save data loading: {e}");
                        _messagePackSaveManager.ResetAll();
                        _onSyncResult.OnNext(false);
                        return;
                    }
                }
                
                if (!result.success)
                {
                    HandleReset(deserializedLocal);
                    _onSyncResult.OnNext(false);
                    return;
                }
                
                try
                {
                    deserializedRemote = _messagePackSaveManager.DeserializeSaveDataFromZipBytes(result.bytes);
                }
                catch (Exception e)
                {
                    DebugUtils.LogError($"Error during remote save data deserialization: {e}");
                    HandleReset(deserializedLocal);
                    _onSyncResult.OnNext(false);
                    return;
                }
                
                if (deserializedLocal == null && deserializedRemote != null)
                {
                    _messagePackSaveManager.LoadFromDeserializedData(deserializedRemote);
                    DebugUtils.Log("Using remote save data directly as no local save data exists.");
                    _onSyncResult.OnNext(true);
                    return;
                }
                
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
            finally
            {
                _cloudLoadCompletedTcs.TrySetResult();
                _messagePackSaveManager.MarkSaveDataReady();
            }
        }

        private void HandleReset(DeserializedSaveData local)
        {
            var localNull = local == null;
            if (localNull)
            {
                _messagePackSaveManager.ResetAll();
                return;
            }

            var localPlayerNull = !local.TryGetFirstSaveDataOfType(out PlayerRecordSaveData localPlayerData);
            if (localPlayerNull)
            {
                _messagePackSaveManager.ResetAll();
                return;
            }
            
            var localId = localPlayerData.PlayerID;
            if (string.IsNullOrEmpty(localId))
            {
                return;
            }
            if (!PlayGamesPlatform.Instance.IsAuthenticated())
            {
                DebugUtils.LogWarning($"Not authenticated but local player ID exists: {localId}. Resetting save data to prevent using save data from a different player.");
                _messagePackSaveManager.ResetAll();
                return;
            }
            var authenticatedId = PlayGamesPlatform.Instance.GetUserId();
            if (!localId.Equals(authenticatedId))
            {
                DebugUtils.LogWarning($"Authenticated player ID: {authenticatedId} does not match local player ID: {localId}. Resetting save data to prevent using save data from a different player.");
                _messagePackSaveManager.ResetAll();
                return;
            }
            DebugUtils.Log($"Authenticated player ID matches local player ID: {authenticatedId}.");

        }
        
        public async UniTask<bool> SaveToService(SaveToServiceParameters parameters)
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
            }, parameters.ShowSelectionUI);
            DebugUtils.Log($"Save completed with result: {result}");
            return result;
            
        } 

        public async UniTask<bool> LoadFromService(LoadFromServiceParameters parameters)
        {
            var result = await _gpgsSavedGames.LoadFromService(parameters.ShowSelectionUI);
            return result.success;
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }
    }
}
#endif
