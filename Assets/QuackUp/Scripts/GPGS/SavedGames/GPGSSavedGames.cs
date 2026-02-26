using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;

namespace QuackUp.GPGS
{
    [Serializable]
    public struct SaveUIConfig
    {
        public uint maxNumToDisplay;
        public bool allowCreateNew;
        public bool allowDelete;
    }

    public struct GPGSSaveData
    {
        public TimeSpan? TotalPlaytime;
        public Texture2D SavedImage;
        public byte[] Data;
    }
    
    public class GPGSSavedGames : IDisposable
    {
        private readonly GPGSSavedGamesConfig _config;
        private readonly GPGSAuthenticationManager _authenticationManager;
        
        private readonly Subject<Tuple<bool, byte[]>> _onLoadFromService = new();
        public Observable<Tuple<bool, byte[]>> OnLoadFromService => _onLoadFromService;
        
        private IDisposable _subscriptions;
        private bool _throttlingSave;
        private GPGSSaveData? _queuedSaveData;

        [Inject]
        public GPGSSavedGames(
            GPGSSavedGamesConfig config,
            GPGSAuthenticationManager authenticationManager)
        {
            _config = config;
            _authenticationManager = authenticationManager;
            Subscribe();
        }
        
        #region Events

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _authenticationManager.OnAuthenticationResult
                .Subscribe(OnFinishedAuthentication)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnFinishedAuthentication(SignInStatus status)
        {
            if (!_config.LoadAutomaticallyAfterAuthentication) return;
            if (status is not SignInStatus.Success) return;
            Debug.Log("GPGS Authenticated, ready to use saved games.");
            LoadFromService().Forget();
        }
        #endregion
        
        #region Helpers
        
        private async UniTask<Tuple<bool, ISavedGameMetadata>> ShowSaveSelectionUI(SaveUIConfig config) 
        {
            var tcs = new UniTaskCompletionSource<bool>();
            ISavedGameMetadata result = null;
            ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;
            savedGameClient.ShowSelectSavedGameUI("Select saved game",
                config.maxNumToDisplay, config.allowCreateNew, config.allowDelete, 
                (status, metadata) =>
                {
                    result = metadata;
                    OnSavedGameSelected(status, tcs);
                });
            await tcs.Task;
            return tcs.GetResult(0)
                ? new Tuple<bool, ISavedGameMetadata>(true, result) 
                : new Tuple<bool, ISavedGameMetadata>(false, null);
        }
        
        private void OnSavedGameSelected(SelectUIStatus status, UniTaskCompletionSource<bool> tcs)
        {
            if (status == SelectUIStatus.SavedGameSelected) 
            {
                tcs.TrySetResult(true);
            } 
            else 
            {
                Debug.LogWarning($"Failed to select saved game: {status}");
                tcs.TrySetResult(false);
            }
        }

        private async UniTask<Tuple<bool, IList<ISavedGameMetadata>>> TryFetchSaveGames()
        {
            ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;
            // create Task from callback
            var tcs = new UniTaskCompletionSource<bool>();
            var result = new List<ISavedGameMetadata>();
            savedGameClient.FetchAllSavedGames(DataSource.ReadCacheOrNetwork, (status, games) =>
            {
                result = games;
                OnFetchedSavedGames(status, tcs);
            });
            await tcs.Task;
            return tcs.GetResult(0)
                ? new Tuple<bool, IList<ISavedGameMetadata>>(true, result) 
                : new Tuple<bool, IList<ISavedGameMetadata>>(false, null);
        }
        
        private void OnFetchedSavedGames(SavedGameRequestStatus status, UniTaskCompletionSource<bool> tcs)
        {
            if (status == SavedGameRequestStatus.Success)
            {
                tcs.TrySetResult(true);
            }
            else
            {
                Debug.LogWarning($"Failed to fetch saved games: {status}");
                tcs.TrySetResult(false);
            }
        }
        
        private async UniTask<Tuple<bool, ISavedGameMetadata>> TryOpenSavedGame(string filename, bool newSave = false) 
        {
            ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;
            // create Task from callback
            var tcs = new UniTaskCompletionSource<bool>();
            ISavedGameMetadata result = null;
            if (newSave)
            {
                var saveGames = await TryFetchSaveGames();
                if (!saveGames.Item1) return new Tuple<bool, ISavedGameMetadata>(false, null);
                if (saveGames.Item2 != null)
                {
                    var count = saveGames.Item2.Count;
                    filename = $"save_{count + 1}";
                }
            }
            Debug.Log("Opening saved game: " + filename);
            savedGameClient.OpenWithAutomaticConflictResolution(filename, DataSource.ReadCacheOrNetwork,
                ConflictResolutionStrategy.UseLongestPlaytime, (status, game) =>
                {
                    result = game;
                    OnSavedGameOpened(status, tcs);
                });
            await tcs.Task;
            Debug.Log($"result is null: {result == null}, status: {tcs.GetResult(0)}");
            return tcs.GetResult(0)
                ? new Tuple<bool, ISavedGameMetadata>(true, result) 
                : new Tuple<bool, ISavedGameMetadata>(false, null);
        }
        
        private void OnSavedGameOpened(SavedGameRequestStatus status, UniTaskCompletionSource<bool> tcs) 
        {
            if (status == SavedGameRequestStatus.Success) 
            {
                Debug.Log("Successfully opened saved game.");
                tcs.TrySetResult(true);
            } 
            else 
            {
                Debug.LogWarning($"Failed to open saved game: {status}");
                tcs.TrySetResult(false);
            }
        }

        private async UniTask<Tuple<bool, ISavedGameMetadata>> TryGetUnopenedSavedGame(bool allowSaveSelection, SaveUIConfig? config = null)
        {
            ISavedGameMetadata unopenedSaveGame;
            if (allowSaveSelection && config != null)
            {
                var selectionResult = await ShowSaveSelectionUI(config.Value);
                if (!selectionResult.Item1) return new(false, null);
                unopenedSaveGame = selectionResult.Item2;
            }
            else
            {
                var allSaveGamesResult = await TryFetchSaveGames();
                if (!allSaveGamesResult.Item1) return new(false, null);
                var firstSave = allSaveGamesResult.Item2.Count > 0 ? allSaveGamesResult.Item2[0] : null;
                unopenedSaveGame = firstSave;
            }
            return new (true, unopenedSaveGame);
        }
        #endregion

        #region Save

        public async UniTask<bool> SaveToService(GPGSSaveData eventData, bool allowSaveSelection = false)
        {
            if (_throttlingSave)
            {
                Debug.LogWarning("Save operation is already in progress. Throttling additional save requests.");
                _queuedSaveData = eventData; // Store the latest save data to be saved after the current operation finishes
                return false;
            }
            if (!PlayGamesPlatform.Instance.IsAuthenticated()) return false;
            _throttlingSave = true;
            var unopenedSaveGameResult = await TryGetUnopenedSavedGame(allowSaveSelection, _config.SaveUIConfig);
            if (!unopenedSaveGameResult.Item1)
            {
                Debug.Log("No save game selected or available to save.");
                CheckSaveInQueue();
                return false;
            }
            var newSave = unopenedSaveGameResult.Item2 == null;
            var fileName = newSave ? "save_0" : unopenedSaveGameResult.Item2.Filename;
            var openResult = await TryOpenSavedGame(fileName, newSave);
            if (!openResult.Item1 || openResult.Item2 == null)
            {
                Debug.LogWarning("Failed to open the selected save game.");
                CheckSaveInQueue();
                return false;
            }
            var openedSavedGame = openResult.Item2;
            var data = eventData.Data;
            var saveResult = await TrySaveToService(openedSavedGame, data, eventData.TotalPlaytime, eventData.SavedImage);
            if (!saveResult.Item1 || saveResult.Item2 == null)
            {
                Debug.LogWarning("Failed to save the game to the cloud.");
                CheckSaveInQueue();
                return false;
            }
            Debug.Log("Game saved to cloud successfully.");
            CheckSaveInQueue();
            return true;
        }
        
        private void CheckSaveInQueue()
        {
            _throttlingSave = false;
            if (_queuedSaveData.HasValue)
            {
                var saveData = _queuedSaveData.Value;
                _queuedSaveData = null; // Clear the queued save data before starting the save operation to prevent potential infinite loops
                SaveToService(saveData).Forget();
            }
        }

        private async UniTask<Tuple<bool, ISavedGameMetadata>> TrySaveToService(ISavedGameMetadata game, byte[] savedData, 
            TimeSpan? totalPlaytime = null, Texture2D savedImage = null)
        {
            ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;
            SavedGameMetadataUpdate.Builder builder = new SavedGameMetadataUpdate.Builder();
            builder = builder
                    .WithUpdatedDescription("Saved game at " + DateTime.Now);
            if (totalPlaytime != null)
                builder = builder.WithUpdatedPlayedTime(totalPlaytime.Value);
            if (savedImage) 
            {
                var pngData = savedImage.EncodeToPNG();
                builder = builder.WithUpdatedPngCoverImage(pngData);
            }
            else
            {
                try
                {
                    var defaultPngData = _config.DefaultSavedImage.texture.Decompress().EncodeToPNG();
                    builder = builder.WithUpdatedPngCoverImage(defaultPngData);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to encode default saved image to PNG: {e.Message}");
                }
            }
            SavedGameMetadataUpdate updatedMetadata = builder.Build();
            var tcs = new UniTaskCompletionSource<bool>();
            ISavedGameMetadata result = null;
            savedGameClient.CommitUpdate(game, updatedMetadata, savedData,
                (status, updatedGame) =>
                {
                    result = updatedGame;
                    OnSavedGameWritten(status, updatedGame, tcs);
                });
            await tcs.Task;
            return tcs.GetResult(0)
                ? new Tuple<bool, ISavedGameMetadata>(true, result) 
                : new Tuple<bool, ISavedGameMetadata>(false, null);
        }
        
        private void OnSavedGameWritten(SavedGameRequestStatus status, ISavedGameMetadata game, UniTaskCompletionSource<bool> tcs)
        {
            if (status == SavedGameRequestStatus.Success)
            {
                tcs.TrySetResult(true);
                Debug.Log("Successfully saved game: " + game.Filename);
            }
            else
            {
                tcs.TrySetResult(false);
                Debug.LogWarning($"Failed to save game: {status}");
            }
        }
        #endregion
        
        #region Load
        public async UniTask<Tuple<bool, byte[]>> LoadFromService(bool allowLoadSelection = false)
        {
            if (!PlayGamesPlatform.Instance.IsAuthenticated())
            {
                _onLoadFromService.OnNext(new Tuple<bool, byte[]>(false, null));
                return new Tuple<bool, byte[]>(false, null);
            }
            var unopenedSaveGameResult = await TryGetUnopenedSavedGame(allowLoadSelection, _config.LoadUIConfig);
            if ((!unopenedSaveGameResult.Item1 || unopenedSaveGameResult.Item2 == null) && allowLoadSelection)
            {
                Debug.Log("No save game selected or available to load.");
                _onLoadFromService.OnNext(new Tuple<bool, byte[]>(false, null));
                return new Tuple<bool, byte[]>(false, null);
            }
            var newSave = allowLoadSelection && unopenedSaveGameResult.Item2 == null;
            var fileName = unopenedSaveGameResult.Item2 == null ? "save_0" : unopenedSaveGameResult.Item2.Filename;
            var openResult = await TryOpenSavedGame(fileName, newSave);
            if (!openResult.Item1 || openResult.Item2 == null)
            {
                Debug.LogWarning("Failed to open the selected save game.");
                _onLoadFromService.OnNext(new Tuple<bool, byte[]>(false, null));
                return new Tuple<bool, byte[]>(false, null);
            }
            var openedSavedGame = openResult.Item2;
            var loadResult = await TryLoadSavedGame(openedSavedGame);
            if (!loadResult.Item1 || loadResult.Item2 == null)
            {
                Debug.LogWarning("Failed to load the selected save game.");
                _onLoadFromService.OnNext(new Tuple<bool, byte[]>(false, null));
                return new Tuple<bool, byte[]>(false, null);
            }
            Debug.Log("Game loaded from cloud successfully.");
            _onLoadFromService.OnNext(loadResult);
            return loadResult;
        }

        private async UniTask<Tuple<bool, byte[]>> TryLoadSavedGame(ISavedGameMetadata savedGame)
        {
            ISavedGameClient savedGameClient = PlayGamesPlatform.Instance.SavedGame;
            var tcs = new UniTaskCompletionSource<bool>();
            byte[] result = null;
            savedGameClient.ReadBinaryData(savedGame, (status, data) =>
            {
                result = data;
                OnSavedGameDataRead(status, data, tcs);
            });
            await tcs.Task;
            return tcs.GetResult(0) 
                ? new Tuple<bool, byte[]>(true, result) 
                : new Tuple<bool, byte[]>(false, null);
        }
        
        private void OnSavedGameDataRead(SavedGameRequestStatus status, byte[] data, UniTaskCompletionSource<bool> tcs)
        {
            if (status == SavedGameRequestStatus.Success)
            {
                tcs.TrySetResult(true);
            }
            else
            {
                Debug.LogWarning($"Failed to read saved game data: {status}");
                tcs.TrySetResult(false);
            }
        }
        #endregion
    }
}