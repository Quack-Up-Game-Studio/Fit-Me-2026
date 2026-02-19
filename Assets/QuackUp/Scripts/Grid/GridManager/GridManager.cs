using System;
using System.Collections.Generic;
using System.Linq;
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
using UnityEditor;
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
    public class GridManager : IDisposable
    {
        #region Inspector
        [field: Title("Grid Debug")] 
        [field: SerializeField, InlineEditor]
        public GridPreset CurrentGridPreset { get; private set; }
        public Vector2Int CurrentGridSize => CurrentGridPreset.GridSize;
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
            _onScoreAdded.OnNext(new
            (ScoreTypes.FitMe, 
                null, 
                _grid.GetGridCenter(CurrentGridSize, CurrentOffset)));
        }
        #endregion

        #region Fields and Properties
        
        private readonly UnityEngine.Grid _grid;
        private readonly GridManagerConfig _config;
        private readonly CellFactory _cellFactory;
        private readonly IAudioManager _audioManager;
        private readonly IMessageHub _messageHub;
        
        private IDisposable _subscriptions;
        
        private List<CellInstance> _previousValidationCells = new();
        private List<List<BlockInstance>> _allContacts = new();
        private readonly ObservableList<GridBlockData> _blockOnGrid = new();
        public IReadOnlyObservableList<GridBlockData> BlocksOnGrid => _blockOnGrid;
        public Observable<Unit> OnCellsCreated => _onCellsCreated;
        private readonly Subject<Unit> _onCellsCreated = new();
        public Observable<BlockInstance> OnBlockPlaced => _onBlockPlaced;
        private readonly Subject<BlockInstance> _onBlockPlaced = new();
        public Observable<ScoreEvent> OnScoreAdded => _onScoreAdded;
        private readonly Subject<ScoreEvent> _onScoreAdded = new();
        public Observable<FitTypeEvent> OnFitCheck => _onFitCheck;
        private readonly Subject<FitTypeEvent> _onFitCheck = new();
        public Observable<Unit> OnClearGrid => _onClearGrid;
        private readonly Subject<Unit> _onClearGrid = new();
        private int _currentPresetIndex;
        public SceneType CurrentSceneType { get; private set; }
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
                .Subscribe<ClearGridEvent>(x => ClearGrid(x.ShouldClearGrid).Forget())
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _onScoreAdded.Dispose();
            _onFitCheck.Dispose();
            _subscriptions.Dispose();
            _blockOnGrid.ForEach(x => x.Subscription?.Dispose());
        }

        #region Events

        private void OnFinishedLoading(SceneType sceneType)
        {
            DebugUtils.Log("GridManager: OnFinishedLoading " + sceneType);
            CurrentSceneType = sceneType;
        }

        private void OnSpawnGridWithGridPreset(GridPreset preset)
        {
            if (CurrentSceneType is not SceneType.Gameplay) return;
            CurrentGridPreset = preset;
            CreateCells();
        }
        
        private void StartGameplay()
        {
            if (CurrentSceneType is not SceneType.Gameplay) return;
            SetUpGameplayGridPreset();
            CreateCells();
        }

        private void OnSpawnGridWithBlockPreset(BlockPreset preset)
        {
            if (CurrentSceneType is SceneType.Gameplay) return;
            SetUpMainMenuGridPreset(preset);
            CreateCells();
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
        
        public void RegenerateGrid()
        {
            DebugUtils.Log("Regenerating grid...");
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
                _grid.transform.SetPositionX(-_grid.cellSize.x / 2f);
            }
            else
            {
                _grid.transform.SetPositionX(0f);
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
            for (var x = 0; x < row; x++)
            for (var y = 0; y < column; y++)
            {
                if (CurrentGridPreset.PresetGridType is GridType.Custom && CurrentGridPreset.customGrid[x, y] == 0) continue; 
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
            _onCellsCreated.OnNext(Unit.Default);
        }
        #endregion
        
        #region Blocks
        /// <summary>
        /// Validate the placement of the block and change the color of the cells
        /// </summary>
        /// <param name="blockModel">Block to validate</param>
        /// <returns>true if the placement is valid, false otherwise</returns>
        public bool ValidatePlacement(BlockModel blockModel)
        {
            var cells = new List<CellInstance>();
            foreach (var atom in blockModel.Atoms)
            {
                Vector3 atomPosition = atom.GameObject.transform.position;
                Vector3 cellPosition = new Vector3(atomPosition.x, atomPosition.y, 0);
                var cell = GetCellByPosition(cellPosition);
                if (cell == null || cell.Model.CurrentAtom.Value != null)
                {
                    continue;
                }
                cells.Add(cell);
            }
            if (_previousValidationCells.Count > 0)
            {
                ResetPreviousValidationCells();
            }
            _previousValidationCells = cells;
            if (cells.Count < blockModel.Atoms.Count)
            {
                cells.ForEach(cell => cell.Model.State.Value = CellState.CannotBePlaced);
                return false;
            }
            cells.ForEach(cell => cell.Model.State.Value = CellState.CanBePlaced);
            return true;
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
            var cells = new List<CellInstance>();
            foreach (var atom in blockInstance.Model.Atoms)
            {
                var atomPosition = atom.GameObject.transform.position.WithZ(0);
                var cell = GetCellByPosition(atomPosition);
                if (cell == null || cell.Model.CurrentAtom.Value != null)
                {
                    return false;
                }
                cells.Add(cell);
            }
            for (var i = 0; i < blockInstance.Model.Atoms.Count; i++) 
            {
                var atom = blockInstance.Model.Atoms[i];
                cells[i].Model.CurrentAtom.Value = atom;
            }
            var firstCellPosition = cells[0].GameObject.transform.position;
            var firstAtomPosition = blockInstance.Model.Atoms[0].GameObject.transform.position;
            var blockPositionRelativeToAtom = firstCellPosition - firstAtomPosition;
            blockViewTransform.position += blockPositionRelativeToAtom;
            blockInstance.Model.BlockCells = cells;
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
            if (CurrentSceneType is not SceneType.Gameplay) return;
            List<(BlockState beforeExplodeState, BlockColor blockType)> blocksToSave = 
                _blockOnGrid.Select(x => (x.BlockInstance.Model.BlockState.CurrentValue, x.BlockInstance.Model.BlockColor.CurrentValue)).ToList();
            
            await ClearGrid(true);
            //PlayerDataManager.Instance.SaveBlockDestroyed(FitType.FitMe, blocksToSave);
            RegenerateGrid();
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
            if (CurrentSceneType is SceneType.Gameplay)
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

        public async UniTask ClearGrid(bool destroyObstacle = true)
        {
            _audioManager.PlayAudioOneShot(_config.FitExplodeSfx, Vector3.zero);
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
        
        /// <summary>
        /// Reorder the rendering order of the blocks on the grid
        /// </summary>
        public void ReorderRenderingOrder()
        {
            for (var i = 0; i < _blockOnGrid.Count; i++)
            {
                var gridBlockData = _blockOnGrid[i];
                gridBlockData.BlockInstance.ViewModel.SetSortingOrderCommand.Execute(i);
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
            contactedBlocks.Add(blockInstance);
            foreach (var cell in blockInstance.Model.BlockCells)
            {
                var cellX = cell.Model.ArrayIndex.Value[0];
                var cellY = cell.Model.ArrayIndex.Value[1];
                var upCell = GetCellByArrayIndex(cellX - 1, cellY);
                var downCell = GetCellByArrayIndex(cellX + 1, cellY);
                var leftCell = GetCellByArrayIndex(cellX, cellY - 1);
                var rightCell = GetCellByArrayIndex(cellX, cellY + 1);
                var adjacentCells = new[] {upCell, downCell, leftCell, rightCell};
                foreach (var adjacentCell in adjacentCells)
                {
                    if (adjacentCell?.Model.CurrentAtom.Value == null) continue;
                    var adjacentBlock = adjacentCell.Model.CurrentAtom.Value.Model.ParentBlock.Value;
                    if (adjacentBlock.Model.BlockState.CurrentValue is BlockState.Infected or BlockState.Exploding or BlockState.Obstacle) continue;
                    if (adjacentBlock.Model.BlockColor.CurrentValue != currentColor) continue;
                    if (contactedBlocks.Contains(adjacentBlock)) continue;
                    CheckForContact(adjacentBlock, contactedBlocks);
                }
            }
            return contactedBlocks.Count >= _config.ComboThreshold;
        }

        private void AddContact(List<BlockInstance> contact, BlockInstance blockInstance)
        {
            //Remove contact with this block
            _allContacts.RemoveAll(x => x.Contains(blockInstance));
            _allContacts.Add(contact);
        }

        /// <summary>
        /// Create a schema of the vacant cells, 1 is vacant, 0 is occupied
        /// </summary>
        /// <returns>true if there are vacant cells, false otherwise</returns>
        public bool CreateVacantSchema(out int[,] vacantSchema, out int vacantCount)
        {
            vacantCount = 0;
            var row = CurrentGridSize.y;
            var column = CurrentGridSize.x;
            vacantSchema = new int[row, column];
            bool isVacant = false;
            for (int x = 0; x < row; x++)
            {
                for (int y = 0; y < column; y++)
                {
                    var cell = _cellArray[x, y];
                    if (cell == null) continue;
                    if (cell.Model.CurrentAtom.Value is not null &&
                        cell.Model.CurrentAtom.Value.Model.ParentBlock.Value.Model.BlockState.CurrentValue is not BlockState.Exploding) 
                        continue;
                    vacantSchema[x, y] = 1;
                    vacantCount++;
                    isVacant = true;
                }
            }
            //ArrayHelper.PrintSchema(_vacantSchema);
            return isVacant;
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
            availableBlocks = new List<BlockModel>();
            foreach (var block in blockToCheck)
            {
                // if we cannot rotate block, only check for current rotation
                /*if (CompareSchema(block, block.BlockView.Transform.rotation.eulerAngles.z))
                {
                    availableBlocks.Add(block);
                    continue;
                }*/
                
                // check for all rotations
                for (var i = 0; i < 4; i++)
                {
                    if (!CompareSchema(block, i)) continue;
                    availableBlocks.Add(block);
                    break;
                }
                //DebugUtils.Log("Block " + blockView.name + " cannot be placed");
            }
            if (availableBlocks.Count != 0) return true;
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
            if (schemaIndex >= 0 && schemaIndex < blockModel.BlockPreset.BlockSchemas.Count)
                return ArrayHelper.CanBFitInA(_vacantSchema, blockModel.BlockPreset.BlockSchemas[schemaIndex].schema,
                    out _);
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
        
        private static CellModel DrawCellArrayMatrix(Rect rect, CellModel cellModel)
        {
            if (cellModel == null) return null;
            EditorGUI.DrawRect(rect.Padding(1), cellModel.CurrentAtom.Value != null ? Color.green : Color.grey);
            return cellModel;
        }
        #endregion
#endif
    }
}
