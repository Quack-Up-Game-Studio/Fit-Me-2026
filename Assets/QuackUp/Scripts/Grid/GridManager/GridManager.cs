using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using MessagePipe;
using ObservableCollections;
using QuackUp.Audio;
using QuackUp.SceneManagement;
using QuackUp.Utils;
using R3;
using Redcode.Extensions;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace FitMe.Grid
{
    [Flags]
    public enum GridType
    {
        None = 0,
        Rectangle = 1 << 0,
        Custom = 1 << 1,
        All = Rectangle | Custom
    }
    
    public enum FitType
    {
        FitMe,
        Combo,
        None
    }
    
    [Flags]
    public enum EndlessType
    {
        None = 0,
        Preset = 1 << 0,
        Generated = 1 << 1,
        All = Preset | Generated
    }
    
    public enum GridOffsetType
    {
        Automatic,
        Custom
    }
    
    public enum PresetRandomType
    {
        Random,
        Ordinal
    }

    public enum ScoreTypes
    {
        FitMe,
        Chain,
        Placement,
    }

    public record GridBlockData
    {
        public readonly BlockInstance BlockInstance;
        public readonly IDisposable Subscription;
        
        public GridBlockData(BlockInstance blockInstance, IDisposable subscription)
        {
            BlockInstance = blockInstance;
            Subscription = subscription;
        }
    }

    public struct FitTypeEvent
    {
        public readonly FitType FitType;
        public readonly BlockInstance Block;
        public FitTypeEvent(FitType fitType, BlockInstance block)
        {
            FitType = fitType;
            Block = block;
        }
    }

    public struct ScoreEvent
    {
        public readonly ScoreTypes ScoreType;
        public readonly List<BlockInstance> Contacts;
        public readonly Vector3 WorldPosition;
        
        public ScoreEvent(ScoreTypes scoreType, List<BlockInstance> contactCount = null, Vector3 worldPosition = default)
        {
            ScoreType = scoreType;
            Contacts = contactCount ?? new List<BlockInstance>();
            WorldPosition = worldPosition;
        }
    }
    
    [ShowOdinSerializedPropertiesInInspector]
    [Serializable]
    public class GridManager : IDisposable
    {
        #region Inspector
        [field: Title("Grid Debug")] 
        [field: SerializeField, InlineEditor]
        public GridPreset CurrentGridPreset { get; private set; }
        public ShapeLevelRuntimeData CurrentShapeLevelRuntimeData { get; private set; }
        public Vector2Int CurrentGridSize => CurrentShapeLevelRuntimeData != null
            ? CurrentShapeLevelRuntimeData.GridSize
            : CurrentGridPreset.GridSize;
        [field: SerializeField, Sirenix.OdinInspector.ReadOnly] 
        public Vector2Int CurrentOffset { get; private set; } = new(0, 0);
        #if UNITY_EDITOR
        [TableMatrix(SquareCells = true, HorizontalTitle = "Cell Array", IsReadOnly = true,
            DrawElementMethod = nameof(DrawCellArrayMatrix), Transpose = true)]
        #endif
        [SerializeField] private CellInstance[,] _cellArray = {};
        #if UNITY_EDITOR
        [TableMatrix(SquareCells = true, HorizontalTitle = "Vacant Schema", IsReadOnly = true,
            DrawElementMethod = nameof(DrawVacantSchemaMatrix), Transpose = true)]
        #endif
        [SerializeField] private int[,] _vacantSchema = {};
        
        [Button("Test Fit-me")]
        private void TestFitMe()
        {
            FitMe(new List<BlockInstance>()).Forget();
        }
        #endregion

        #region Fields and Properties
        
        private readonly UnityEngine.Grid _grid;
        private readonly GridManagerConfig _config;
        private readonly CellFactory _cellFactory;
        private readonly IAudioManager _audioManager;
        private readonly IMessageHub _messageHub;
        private readonly ILevelManager _levelManager;
        
        private IDisposable _subscriptions;
        private bool _isDisposed;
        
        private List<CellInstance> _previousValidationCells = new();
        private List<List<BlockInstance>> _allContacts = new();
        private readonly ObservableList<GridBlockData> _blockOnGrid = new();
        public IReadOnlyObservableList<GridBlockData> BlocksOnGrid => _blockOnGrid;
        public Observable<Unit> OnCellsCreated => _onCellsCreated;
        private readonly Subject<Unit> _onCellsCreated = new();
        // Borrowed for this synchronous placement attempt only. Subscribers may
        // Cancel() during the callback; they must not retain or dispose the source.
        public Observable<(BlockInstance instance, CancellationTokenSource placeCancellation)> OnAboutToPlaceBlock => _onAboutToPlaceBlock;
        private readonly Subject<(BlockInstance instance, CancellationTokenSource placeCancellation)> _onAboutToPlaceBlock = new();
        public Observable<BlockInstance> OnBlockPlaced => _onBlockPlaced;
        private readonly Subject<BlockInstance> _onBlockPlaced = new();
        public Observable<ScoreEvent> OnScoreAdded => _onScoreAdded;
        private readonly Subject<ScoreEvent> _onScoreAdded = new();
        public Observable<FitTypeEvent> OnFitCheck => _onFitCheck;
        private readonly Subject<FitTypeEvent> _onFitCheck = new();
        public Observable<Unit> OnAboutToClearGrid => _onAboutToClearGrid;
        private readonly Subject<Unit> _onAboutToClearGrid = new();
        public Observable<Unit> OnClearGrid => _onClearGrid;
        private readonly Subject<Unit> _onClearGrid = new();
        private int _currentPresetIndex;
        public bool IsGameplay { get; private set; }
        #endregion
        
        [Inject]
        public GridManager(
            UnityEngine.Grid grid,
            GridManagerConfig config,
            CellFactory cellFactory,
            IAudioManager audioManager,
            [Key(GridManagerMessageHub.GridManagerMessageHubKey)] IMessageHub messageHub)
        {
            _grid = grid;
            _config = config;
            _cellFactory = cellFactory;
            _audioManager = audioManager;
            _messageHub = messageHub;
            _grid.cellSize = config.CellSize;
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _messageHub.GetObservable<LoadSceneStageEvent>()
                .Where(x => x.Stage == LoadSceneStage.FinishLoading)
                .Select(x => x.NextSceneType)
                .Subscribe(OnFinishedLoading)
                .AddTo(ref disposableBuilder);
            _messageHub
                .Subscribe<SpawnWithBlockPresetEvent>(x => OnSpawnGridWithBlockPreset(x.BlockPreset))
                .AddTo(ref disposableBuilder);
            _messageHub
                .Subscribe<StartCreateGridEvent>(_ => StartGameplay())
                .AddTo(ref disposableBuilder);
            _messageHub
                .Subscribe<SpawnWithGridPresetEvent>(x => OnSpawnGridWithGridPreset(x.GridPreset))
                .AddTo(ref disposableBuilder);
            _messageHub
                .Subscribe<SpawnWithShapeLevelEvent>(OnSpawnWithShapeLevel)
                .AddTo(ref disposableBuilder);
            _messageHub
                .Subscribe<ClearGridEvent>(OnContinue)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _subscriptions?.Dispose();
            _blockOnGrid.ForEach(x => x.Subscription?.Dispose());
            _onCellsCreated.Dispose();
            _onAboutToPlaceBlock.Dispose();
            _onBlockPlaced.Dispose();
            _onScoreAdded.Dispose();
            _onFitCheck.Dispose();
            _onAboutToClearGrid.Dispose();
            _onClearGrid.Dispose();
        }

        #region Events

        private void OnFinishedLoading(SceneType sceneType)
        {
            DebugUtils.Log("GridManager: OnFinishedLoading " + sceneType);
            IsGameplay = sceneType is SceneType.Gameplay or SceneType.Tutorial;
        }

        private void OnSpawnGridWithGridPreset(GridPreset preset)
        {
            if (!IsGameplay) return;
            CurrentGridPreset = preset;
            CreateCells();
        }

        private void OnSpawnWithShapeLevel(SpawnWithShapeLevelEvent eventData)
        {
            CurrentShapeLevelRuntimeData = eventData.RuntimeData;
            CurrentGridPreset = CurrentShapeLevelRuntimeData?.Board;
        }
        
        private void StartGameplay()
        {
            if (!IsGameplay) return;
            if (CurrentShapeLevelRuntimeData == null)
                SetUpGameplayGridPreset();
            else
                CurrentGridPreset = CurrentShapeLevelRuntimeData.Board;
            CreateCells();
        }

        private void OnSpawnGridWithBlockPreset(BlockPreset preset)
        {
            if (IsGameplay) return;
            SetUpMainMenuGridPreset(preset);
            CreateCells();
        }

        private void OnContinue(ClearGridEvent data)
        {
            if (data.ShouldClearGrid)
            {
                ClearGrid(data.ShouldDestroyObstacles).Forget();
            }
        }
        #endregion
        
        #region Grid Generation
        private void SetUpGameplayGridPreset()
        {
            var currentEndlessType = _config.EndlessType;
            if (_config.EndlessType is EndlessType.All)
            {
                currentEndlessType = Random.Range(0, 2) == 0 ? EndlessType.Preset : EndlessType.Generated;
            }
            if (currentEndlessType is EndlessType.Preset && _config.GridPresets.Count > 0)
            {
                GetPreset();
                return;
            }

            var newGridPreset = ScriptableObject.CreateInstance<GridPreset>();
            newGridPreset.name = "Auto-Generated Grid Preset";
            if (_config.GeneratedGridType is GridType.All) newGridPreset.PresetGridType = Random.Range(0, 2) == 0 ? GridType.Rectangle : GridType.Custom;
            else newGridPreset.PresetGridType = _config.GeneratedGridType;
            int randomX = Random.Range(_config.RandomGridXRange.x, _config.RandomGridXRange.y + 1);
            int randomY = Random.Range(_config.RandomGridYRange.x, _config.RandomGridYRange.y + 1);
            newGridPreset.GridSize = new Vector2Int(randomX, randomY);
            if (newGridPreset.PresetGridType is GridType.Custom)
            {
                newGridPreset.customGrid = new int[randomY, randomX];
                var row = newGridPreset.customGrid.GetLength(0);
                var column = newGridPreset.customGrid.GetLength(1);
                for (int x = 0; x < row; x++)
                {
                    var hasBridge = Random.Range(0, 2) == 0;
                    var bridgeIndices = new int[column];
                    if (hasBridge)
                    {
                        var bridgeWidth = Random.Range(_config.BridgeWidthRange.x, _config.BridgeWidthRange.y + 1);
                        bridgeIndices = GetBridgeIndex(bridgeWidth, column);
                    }
                    for (int y = 0; y < column; y++)
                    {
                        if (hasBridge) newGridPreset.customGrid[x, y] = bridgeIndices[y];
                        else newGridPreset.customGrid[x, y] = 1;
                    }
                }
            }
            CurrentGridPreset = newGridPreset;
        }

        private void GetPreset()
        {
            if (_config.PresetRandomType is PresetRandomType.Random)
            {
                CurrentGridPreset = _config.GridPresets.GetRandomElement();
            }
            else
            {
                CurrentGridPreset = _config.GridPresets[_currentPresetIndex];
                _currentPresetIndex++;
                if (_currentPresetIndex >= _config.GridPresets.Count)
                {
                    _currentPresetIndex = 0;
                }
            }
        }

        private void SetUpMainMenuGridPreset(BlockPreset blockPreset)
        {
            var newGridPreset = ScriptableObject.CreateInstance<GridPreset>();
            newGridPreset.name = $"{blockPreset.name} Grid Preset";
            newGridPreset.PresetGridType = GridType.Custom;
            var blockSchema = blockPreset.BlockSchemas[0].schema;
            newGridPreset.customGrid = blockSchema;
            var row = newGridPreset.customGrid.GetLength(0);
            var column = newGridPreset.customGrid.GetLength(1);
            newGridPreset.GridSize = new Vector2Int(column, row);
            CurrentGridPreset = newGridPreset;
        }

        private int[] GetBridgeIndex(int bridgeWidth, int columnCount)
        {
            var divisible = columnCount % bridgeWidth == 0 ? 0 : 1;
            var maxBridge = Mathf.FloorToInt(columnCount / (float)bridgeWidth) + divisible;
            var bridgeCount = Random.Range(1, maxBridge);
            var possibleRanges = new List<(int start, int end)>();
            for (var i = 0; i <= columnCount - bridgeWidth; i++)
            {
                possibleRanges.Add((i, i + bridgeWidth - 1));
            }
            var shuffledRanges = possibleRanges.Shuffled().ToList();
            //pick the ones where there are no overlap
            var validRanges = new List<(int start, int end)>();
            foreach (var range in shuffledRanges)
            {
                validRanges.Add(range);
                //TODO: Make a tree search
                var nonOverlapRange = shuffledRanges.Where(x => 
                        range.start > x.end || range.end < x.start).Take(bridgeCount);
                validRanges.AddRange(nonOverlapRange);
                if (validRanges.Count >= bridgeCount) break;
                validRanges.Clear();
            }
            //create bool array from validRanges
            var bridgeIndices = new int[columnCount];
            if (validRanges.Count == 0)
            {
                DebugUtils.LogWarning("No valid ranges found, creating a full bridge.");
                for (var i = 0; i < columnCount; i++)
                {
                    bridgeIndices[i] = 1;
                }
                return bridgeIndices;
            }
            foreach (var range in validRanges)
            {
                DebugUtils.Log($"Adding bridge from {range.start} to {range.end}");
                for (var i = range.start; i <= range.end; i++)
                {
                    bridgeIndices[i] = 1;
                }
            }
            return bridgeIndices;
        }
        
        public void DestroyGrid()
        {
            ClearHintFlash();
            DebugUtils.Log("Destroying grid...");
            ResetPreviousValidationCells();
            foreach (var cell in _cellArray)
            {
                cell?.ViewModel.DestroyCommand.Execute(Unit.Default);
            }
            _cellArray = new CellInstance[0, 0];
            _vacantSchema = new int[0, 0];
            // SetUpGameplayGridPreset();
            // CreateCells();
        }
        
        private void UpdateGridOffset()
        {
            var offset = GridUtils.CalculateGridOffset(_config, CurrentGridSize);
            CurrentOffset = offset;
            if (CurrentGridSize.x % 2 != 0)
            {
                _grid.transform.SetPositionX(-_grid.cellSize.x / 2f + _config.CustomGridPositionOffsetX);
            }
            else
            {
                _grid.transform.SetPositionX(0f + _config.CustomGridPositionOffsetX);
            }

            if (CurrentGridSize.y % 2 != 0)
            {
                _grid.transform.SetPositionY(_grid.cellSize.y / 2f + _config.CustomGridPositionOffsetY);
            }
            else
            {
                _grid.transform.SetPositionY(0f + _config.CustomGridPositionOffsetY);
            }
        }
        
        /// <summary>
        /// Create the cells
        /// </summary>
        private void CreateCells()
        {
            UpdateGridOffset();
            var row = CurrentGridSize.y;
            var column = CurrentGridSize.x;
            var cellSize = _grid.cellSize.x;
            _cellArray = new CellInstance[row, column];
            var playableMask = GetCurrentPlayableMask();
            for (var x = 0; x < row; x++)
            for (var y = 0; y < column; y++)
            {
                if (playableMask != null && playableMask[x, y] == 0) continue;
                var halfSize = cellSize / 2;
                var spawnPosition =
                    (Vector3)(new Vector2(halfSize, halfSize) +
                              new Vector2(y + CurrentOffset.x, CurrentOffset.y - x) * cellSize);
                //+ _grid.transform.position;
                var cell = _cellFactory.Create(spawnPosition, Quaternion.identity);
                cell.GameObject.transform.localScale = Vector3.one * cellSize;
                cell.GameObject.name = $"Cell {x}_{y}";
                cell.Model.ArrayIndex.Value = new Vector2Int(x, y);
                cell.Model.GridIndex.Value = GridUtils.ArrayToGridIndex(new Vector2Int(x, y), CurrentOffset);
                _cellArray[x, y] = cell;
            }
            RefreshCellOutlines();
            _onCellsCreated.OnNext(Unit.Default);
        }

        private int[,] GetCurrentPlayableMask()
        {
            if (CurrentShapeLevelRuntimeData != null)
                return CurrentShapeLevelRuntimeData.PlayableMask;
            if (!CurrentGridPreset || CurrentGridPreset.PresetGridType != GridType.Custom)
                return null;
            return CurrentGridPreset.customGrid;
        }

        #endregion
        
        #region Blocks
        private bool TryResolvePlacementCells(
            IReadOnlyList<AtomInstance> atoms,
            out List<CellInstance> resolvedCells)
        {
            resolvedCells = new List<CellInstance>(atoms?.Count ?? 0);
            if (atoms == null || atoms.Count == 0) return false;

            var targetIndexes = new List<Vector2Int>(atoms.Count);
            var resolvedIndexes = new HashSet<Vector2Int>();
            var stageMask = GetCurrentPlayableMask();
            var allAtomsResolved = true;

            foreach (var atom in atoms)
            {
                if (atom?.GameObject == null)
                {
                    allAtomsResolved = false;
                    continue;
                }

                var cellPosition = atom.GameObject.transform.position.WithZ(0);
                var cell = GetCellByPosition(cellPosition);
                if (cell == null)
                {
                    allAtomsResolved = false;
                    continue;
                }

                var targetIndex = cell.Model.ArrayIndex.Value;
                targetIndexes.Add(targetIndex);
                if (resolvedIndexes.Add(targetIndex))
                    resolvedCells.Add(cell);
            }

            if (!allAtomsResolved || targetIndexes.Count != atoms.Count)
                return false;

            if (!BlockPlacementValidator.TryValidate(
                    CurrentGridSize.y,
                    CurrentGridSize.x,
                    stageMask,
                    targetIndexes,
                    index => GetCellByArrayIndex(index).Model.CurrentAtom.Value != null,
                    out _))
                return false;

            return true;
        }

        /// <summary>
        /// Validate the placement of the block and change the color of the cells
        /// </summary>
        /// <param name="blockModel">Block to validate</param>
        /// <returns>true if the placement is valid, false otherwise</returns>
        public bool ValidatePlacement(BlockModel blockModel)
        {
            ResetPreviousValidationCells();
            var isValid = TryResolvePlacementCells(blockModel?.Atoms, out var cells);
            _previousValidationCells = cells;
            var cellState = isValid ? CellState.CanBePlaced : CellState.CannotBePlaced;
            _previousValidationCells.ForEach(cell => cell.Model.State.Value = cellState);
            return isValid;
        }

        /// <summary>
        /// Place the block in the grid
        /// </summary>
        /// <param name="blockInstance">Block to place</param>
        /// <param name="updateGrid">Update the grid after placement, true by default</param>
        /// <param name="isObstacle">Is the block an obstacle, false by default</param>
        /// <returns>true if the placement is valid, false otherwise</returns>
        public bool TryPlaceBlock(BlockInstance blockInstance, bool updateGrid = true)
        {
            ResetPreviousValidationCells();
            var blockViewTransform = blockInstance.GameObject.transform;
            var cellSize = _grid.cellSize.x;
            blockViewTransform.localScale = Vector3.one * cellSize;
            if (!TryResolvePlacementCells(blockInstance.Model.Atoms, out var cells)) return false;

            using var cancellationTokenSource = new CancellationTokenSource();
            _onAboutToPlaceBlock.OnNext((blockInstance, cancellationTokenSource));
            if (cancellationTokenSource.IsCancellationRequested ||
                !BlockPlacementValidator.TryValidate(
                    CurrentGridSize.y,
                    CurrentGridSize.x,
                    GetCurrentPlayableMask(),
                    cells.ConvertAll(cell => cell.Model.ArrayIndex.Value),
                    index => GetCellByArrayIndex(index).Model.CurrentAtom.Value != null,
                    out _))
            {
                return false;
            }
            for (var i = 0; i < blockInstance.Model.Atoms.Count; i++) 
            {
                var atom = blockInstance.Model.Atoms[i];
                cells[i].Model.CurrentAtom.Value = atom;
            }
            // Atom outlines stay in GenerateAtom's block-local schema, independent of board occupancy.
            blockInstance.Model.BlockCells = cells;

            var firstCellPosition = cells[0].GameObject.transform.position;
            var firstAtomPosition = blockInstance.Model.Atoms[0].GameObject.transform.position;
            var blockPositionRelativeToAtom = firstCellPosition - firstAtomPosition;
            blockViewTransform.position += blockPositionRelativeToAtom;
            var subscription = blockInstance.Model.UpdateGridCommand
                .Subscribe(_ => UpdateBlockOnGrid(blockInstance));
            _blockOnGrid.Add(new(blockInstance, subscription));
            blockInstance.ViewModel.BlockInteractionState.Value = BlockInteractionState.PlacedOnGrid;
            blockInstance.GameObject.transform.SetParent(_grid.transform);
            //blockView.ResetSortingLayer();
            ReorderRenderingOrder();
            if (updateGrid)
            {
                //OnScoreAdded.OnNext(new(ScoreTypes.Placement, worldPosition: blockViewTransform.position));
                var fit = UpdateBlockOnGrid(blockInstance);
                _onFitCheck?.OnNext(new FitTypeEvent(fit, blockInstance));
            }
            _onBlockPlaced?.OnNext(blockInstance);
            return true;
        }
        
        /// <summary>
        /// Update the block on the grid, check for contacts and validate placement
        /// </summary>
        /// <param name="blockInstance"></param>
        /// <returns>FitType indicating the result of the update</returns>
        public FitType UpdateBlockOnGrid(BlockInstance blockInstance)
        {
            var contacts = new List<BlockInstance>();
            var hasContact = CheckForContact(blockInstance, contacts);
            AddContact(contacts, blockInstance);
            if (!hasContact)
            {
                _onScoreAdded.OnNext(new(ScoreTypes.Placement, worldPosition: blockInstance.GameObject.transform.position));
            }
            else
            {
                Combo(contacts).Forget();
            }
            if (CreateVacantSchema(out var vacantSchema, out _)) 
                return !hasContact ? FitType.None : FitType.Combo;
            _vacantSchema = vacantSchema;
            var longestChain = _allContacts.OrderByDescending(x => x.Count).FirstOrDefault();
            FitMe(longestChain).Forget();
            return FitType.FitMe;
        }

        private async UniTask FitMe(List<BlockInstance> contacts)
        {
            if (!IsGameplay) return;
            await ClearGrid(true);
            //PlayerDataManager.Instance.SaveBlockDestroyed(FitType.FitMe, blocksToSave);
            DestroyGrid();
            _onScoreAdded.OnNext(new(ScoreTypes.FitMe, contacts, worldPosition:_grid.GetGridCenter(CurrentGridSize, CurrentOffset)));
        }

        private async UniTask Combo(List<BlockInstance> contacts)
        {
            var middleOfBlocks = contacts.Select(block => block.GameObject.transform.position)
                .Aggregate(Vector3.zero, (current, position) => current + position) / contacts.Count;
            List<(BlockState beforeExplodeState, BlockColor blockType)> blocksToSave = 
                _blockOnGrid.Select(x => (x.BlockInstance.Model.BlockState.CurrentValue, x.BlockInstance.Model.BlockColor.CurrentValue)).ToList();
            //AudioManager.Instance.PlayAudioOneShot(stackExplodeSfx, transform.position);
            var gridBlockData = _blockOnGrid.Where(x => contacts.Contains(x.BlockInstance)).ToList();
            //await UniTask.WhenAll(gridBlockData.Select(block => RemoveBlock(block, FitType.Combo, true)));
            //PlayerDataManager.Instance.SaveBlockDestroyed(FitType.Combo, blocksToSave);
            _onScoreAdded.OnNext(new(ScoreTypes.Chain, contacts, middleOfBlocks));
            //BlockManager.Instance.GameOverCheck().Forget();
        }

        public async UniTask RemoveBlock(BlockInstance blockInstance, FitType fitType, bool destroy = false)
        {
            var gridBlockData = _blockOnGrid.FirstOrDefault(x => x.BlockInstance == blockInstance);
            if (gridBlockData == null)
            {
                DebugUtils.LogWarning("Block not found on grid");
                return;
            }
            await RemoveBlock(gridBlockData, fitType, destroy);
        }
        
        /// <summary>
        /// Remove the block from the grid
        /// </summary>
        /// <param name="gridBlockData">Block to remove</param>
        /// <param name="destroy">Destroy the block, false by default</param>
        public async UniTask RemoveBlock(GridBlockData gridBlockData, FitType fitType, bool destroy = false)
        {
            _blockOnGrid.Remove(gridBlockData);
            _allContacts.RemoveAll(x => x.Contains(gridBlockData.BlockInstance));
            //OnBlockDestroyed?.Invoke(gridBlockData.Block);
            var atoms = new List<AtomInstance>(gridBlockData.BlockInstance.Model.Atoms);
            if (IsGameplay)
            {
                gridBlockData.BlockInstance.Model.BlockState.Value = BlockState.Exploding;
                var promise = new Promise<Unit>();
                gridBlockData.BlockInstance.ViewModel.ExplodeCommand.Execute(
                    new ExplodeCommandData(promise, fitType, destroy));
                await promise.Task;
            }
            foreach (var atom in atoms)
            {
                var cell = GetCellByPosition(atom.GameObject.transform.position);
                if (cell == null || cell.Model.CurrentAtom.Value != atom)
                {
                    continue;
                }
                cell.Model.CurrentAtom.Value = null;
            }

            gridBlockData.Subscription.Dispose();
        }

        /// <summary>
        /// Reset the grid, remove all blocks and create new cells based on the current grid preset
        /// </summary>
        /// <param name="playSound"></param>
        public async UniTask ResetGrid(bool playSound = true)
        {
            await ClearGrid(true, playSound);
            DestroyGrid();
            OnSpawnGridWithGridPreset(CurrentGridPreset);
        }

        public async UniTask ClearGrid(bool destroyObstacle = true, bool playSound = true)
        {
            ClearHintFlash();
            _onAboutToClearGrid?.OnNext(Unit.Default);
            if (playSound) _audioManager.PlayAudioOneShot(_config.FitExplodeSfx, Vector3.zero);
            if (destroyObstacle)
            {
                await RemoveAllBlocks(true);
            }
            else
            {
                var excludeObstacle = _blockOnGrid
                    .Where(x => x.BlockInstance.Model.BlockState.CurrentValue is not BlockState.Obstacle)
                    .ToList();
                await UniTask.WhenAll(excludeObstacle.Select(block => RemoveBlock(block, FitType.FitMe, true)));
            }
            _onClearGrid?.OnNext(Unit.Default);
        }
    
        /// <summary>
        /// Remove all blocks from the grid
        /// </summary>
        /// <param name="destroy">Destroy the blocks, false by default</param>
        private async UniTask RemoveAllBlocks(bool destroy = false)
        {
            List<GridBlockData> blocksToRemove = new List<GridBlockData>(_blockOnGrid);
            await UniTask.WhenAll(blocksToRemove.Select(block => RemoveBlock(block, FitType.FitMe, destroy)));
        }
        
        /// <summary>
        /// Reset the color of the previous validation cells
        /// </summary>
        public void ResetPreviousValidationCells()
        {
            if (_previousValidationCells.Count == 0) return;
            _previousValidationCells.ForEach(cell => cell.Model.State.Value = CellState.None);
            _previousValidationCells.Clear();
        }
        
        // Board silhouette follows existing floor cells, never CurrentAtom occupancy.
        // Row increases downward. These flags are distinct from placed/preview atom outlines.
        private void RefreshCellOutlines()
        {
            if (_cellArray == null) return;
            var rows = _cellArray.GetLength(0);
            var columns = _cellArray.GetLength(1);
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var cell = _cellArray[row, column];
                if (cell == null) continue;
                cell.Model.BaseOutlineDiagonals.Value = new Vector4(
                    Exists(row - 1, column - 1) ? 1 : 0,
                    Exists(row - 1, column + 1) ? 1 : 0,
                    Exists(row + 1, column - 1) ? 1 : 0,
                    Exists(row + 1, column + 1) ? 1 : 0);
                cell.Model.BaseOutlineEdges.Value = new Vector4(
                    Exists(row, column - 1) ? 0 : 1,
                    Exists(row, column + 1) ? 0 : 1,
                    Exists(row - 1, column) ? 0 : 1,
                    Exists(row + 1, column) ? 0 : 1);
            }
            bool Exists(int row, int column) => row >= 0 && row < rows && column >= 0 &&
                column < columns && _cellArray[row, column] != null;
        }

        /// <summary>
        /// Reorder the rendering order of the blocks on the grid
        /// </summary>
        public void ReorderRenderingOrder()
        {
            for (var i = 0; i < _blockOnGrid.Count; i++)
            {
                var gridBlockData = _blockOnGrid[i];
                gridBlockData.BlockInstance.ViewModel.SetSortingOrderCommand.Execute(i * 2);
            }
        }
        #endregion
    
        #region Contacts
        /// <summary>
        /// Check for contact with other blocks
        /// </summary>
        /// <param name="blockInstance">Current block</param>
        /// <param name="contactedBlocks">List of contacted blocks</param>
        /// <returns>true if the contacted blocks count is greater than or equal to the destroy threshold, false otherwise</returns>
        private bool CheckForContact(BlockInstance blockInstance, List<BlockInstance> contactedBlocks)
        {
            var currentColor = blockInstance.Model.BlockColor.CurrentValue;
            ContactChain.Collect(
                blockInstance,
                GetAdjacentContactBlocks,
                adjacentBlock => adjacentBlock != null &&
                                 adjacentBlock.Model.BlockState.CurrentValue is not (BlockState.Infected or BlockState.Exploding or BlockState.Obstacle) &&
                                 adjacentBlock.Model.BlockColor.CurrentValue == currentColor,
                contactedBlocks);
            return contactedBlocks.Count >= _config.ComboThreshold;
        }

        private IEnumerable<BlockInstance> GetAdjacentContactBlocks(BlockInstance blockInstance)
        {
            foreach (var cell in blockInstance.Model.BlockCells)
            {
                var cellX = cell.Model.ArrayIndex.Value[0];
                var cellY = cell.Model.ArrayIndex.Value[1];
                yield return GetAdjacentBlock(GetCellByArrayIndex(cellX - 1, cellY));
                yield return GetAdjacentBlock(GetCellByArrayIndex(cellX + 1, cellY));
                yield return GetAdjacentBlock(GetCellByArrayIndex(cellX, cellY - 1));
                yield return GetAdjacentBlock(GetCellByArrayIndex(cellX, cellY + 1));
            }
        }

        private static BlockInstance GetAdjacentBlock(CellInstance adjacentCell)
        {
            if (adjacentCell?.Model.CurrentAtom.Value == null) return null;
            return adjacentCell.Model.CurrentAtom.Value.Model.ParentBlock.Value;
        }

        private void AddContact(List<BlockInstance> contact, BlockInstance blockInstance)
        {
            //Remove contact with this block
            _allContacts.RemoveAll(x => x.Contains(blockInstance));
            _allContacts.Add(contact);
        }

        public bool TryCreateHintBoardMasks(
            out int[,] playableMask, out int[,] occupiedMask, out int[,] obstacleMask)
        {
            playableMask = null;
            occupiedMask = null;
            obstacleMask = null;
            if (CurrentShapeLevelRuntimeData == null || _cellArray == null || _cellArray.Length == 0)
                return false;

            var playableMaskSource = GetCurrentPlayableMask();
            var rows = _cellArray.GetLength(0);
            var columns = _cellArray.GetLength(1);
            if (playableMaskSource == null || playableMaskSource.GetLength(0) != rows ||
                playableMaskSource.GetLength(1) != columns)
                return false;

            playableMask = (int[,])playableMaskSource.Clone();
            occupiedMask = new int[rows, columns];
            obstacleMask = new int[rows, columns];
            var freeCellCount = 0;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var cell = _cellArray[row, column];
                if (cell == null || cell.Model == null)
                {
                    if (playableMask[row, column] == 1) return false;
                    continue;
                }

                var atom = cell.Model.CurrentAtom.Value;
                if (atom == null)
                {
                    if (playableMask[row, column] == 1) freeCellCount++;
                    continue;
                }

                occupiedMask[row, column] = 1;
                var block = atom.Model.ParentBlock.Value;
                if (block == null) return false;
                if (block.Model.BlockState.CurrentValue == BlockState.Obstacle)
                    obstacleMask[row, column] = 1;
            }
            return freeCellCount > 0;
        }

        public void SetHintHighlights(IReadOnlyList<TilingCell> cells)
        {
            ClearHintFlash();
            if (cells == null) return;
            for (var index = 0; index < cells.Count; index++)
            {
                var coordinate = cells[index];
                var cell = GetCellByArrayIndex(coordinate.Row, coordinate.Column);
                if (cell == null) continue;
                cell.Model.HintHighlighted.Value = true;
                cell.Model.HintFlashVisible.Value = true;
            }
        }

        public void SetHintFlashVisible(bool visible)
        {
            if (_cellArray == null) return;
            foreach (var cell in _cellArray)
            {
                if (cell?.Model.HintHighlighted.Value == true)
                    cell.Model.HintFlashVisible.Value = visible;
            }
        }

        public void ClearHintFlash()
        {
            if (_cellArray == null) return;
            foreach (var cell in _cellArray)
            {
                if (cell?.Model == null) continue;
                cell.Model.HintHighlighted.Value = false;
                cell.Model.HintFlashVisible.Value = false;
            }
        }

        /// <summary>
        /// Create a schema of the vacant cells, 1 is vacant, 0 is occupied
        /// </summary>
        /// <returns>true if there are vacant cells, false otherwise</returns>
        public bool CreateVacantSchema(out int[,] vacantSchema, out int vacantCount)
        {
            return VacantSchemaSolver.Create(
                _cellArray,
                CurrentGridSize.y,
                CurrentGridSize.x,
                out vacantSchema,
                out vacantCount);
        }

        /// <summary>
        /// Check if the block can be placed in the grid
        /// </summary>
        /// <param name="blockToCheck">Blocks to check</param>
        /// <param name="availableBlocks">Available blocks</param>
        /// <returns>true if the block can be placed, false otherwise</returns>
        public bool CheckAvailableBlock(List<BlockModel> blockToCheck, out List<BlockModel> availableBlocks)
        {
            CreateVacantSchema(out var vacantSchema, out _);
            _vacantSchema = vacantSchema;
            if (GridFitChecker.CheckAvailableBlocks(vacantSchema, blockToCheck, out availableBlocks)) return true;
            DebugUtils.Log("No blocks can be placed");
            return false;
        }
        
        public bool CompareSchema(BlockModel block, float zAngle)
        {
            var index = (int)zAngle / 90;
            if (!CompareSchema(block, index)) return false;
            return true;
        }
        
        public bool CompareSchema(BlockModel blockModel, int schemaIndex)
        {
            if (schemaIndex >= 0 && schemaIndex < blockModel.BlockSchemas.Count)
                return GridFitChecker.CanFit(_vacantSchema, blockModel.BlockSchemas[schemaIndex].schema);
            DebugUtils.LogError("Invalid schema index: " + schemaIndex);
            return false;
        }
        #endregion
        
        /// <summary>
        /// Get the cell by array index
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>A Cell if it exists, null otherwise</returns>
        public CellInstance GetCellByArrayIndex(int x, int y)
        {
            if (x < 0 || x >= CurrentGridSize.y || y < 0 || y >= CurrentGridSize.x)
            {
                return null;
            }
            return _cellArray[x, y];
        }
        
        public CellInstance GetCellByArrayIndex(Vector2Int index)
        {
            return GetCellByArrayIndex(index.x, index.y);
        }
        
        public CellInstance GetCellByGridIndex(int x, int y)
        {
            return GetCellByGridIndex(new Vector2Int(x, y));
        }
        
        public CellInstance GetCellByGridIndex(Vector2Int gridIndex)
        {
            return GetCellByArrayIndex(GridUtils.GridToArrayIndex(gridIndex, CurrentOffset));
        }
        
        /// <summary>
        /// Get the cell by position, it will be rounded to the nearest cell
        /// </summary>
        /// <param name="position">Position to try to get a cell</param>
        /// <returns>A Cell if it exists, null otherwise</returns>
        public CellInstance GetCellByPosition(Vector3 position)
        {
            var worldToCell = _grid.WorldToCell(position);
            int x = worldToCell.x;
            int y = worldToCell.y;
            return GetCellByGridIndex(x, y);
        }
        
#if UNITY_EDITOR
        #region Table Matrix
        private static int DrawVacantSchemaMatrix(Rect rect, int value)
        {
            EditorGUI.DrawRect(rect.Padding(1), value == 1 ? Color.green : Color.grey);
            return value;
        }
        
        private static CellInstance DrawCellArrayMatrix(Rect rect, CellInstance instance)
        {
            if (instance == null) return null;
            EditorGUI.DrawRect(rect.Padding(1), instance.Model.CurrentAtom.Value != null ? Color.green : Color.grey);
            return instance;
        }
        #endregion
#endif
    }
}
