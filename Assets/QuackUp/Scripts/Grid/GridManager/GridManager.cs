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
        Combo,
        Bomb,
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
        public readonly int ContactCount;
        public readonly Vector3 WorldPosition;
        
        public ScoreEvent(ScoreTypes scoreType, int contactCount = 0, Vector3 worldPosition = default)
        {
            ScoreType = scoreType;
            ContactCount = contactCount;
            WorldPosition = worldPosition;
        }
    }
    
    [ShowOdinSerializedPropertiesInInspector]
    public class GridManager : IDisposable
    {
        private readonly UnityEngine.Grid _grid;
        private readonly GridManagerConfig _config;
        private readonly CellFactory _cellFactory;
        private readonly IAudioManager _audioManager;
        private readonly IMessageHub _messageHub;
        
        private IDisposable _subscriptions;
        
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
            OnScoreAdded.OnNext(new
            (ScoreTypes.FitMe, 
                0, 
                _grid.GetGridCenter(CurrentGridSize, CurrentOffset)));
        }
        #endregion

        #region Fields and Properties
        
        private List<CellInstance> _previousValidationCells = new();
        private readonly ObservableList<GridBlockData> _blockOnGrid = new();
        public IReadOnlyObservableList<GridBlockData> BlocksOnGrid => _blockOnGrid;
        // public event Action<BlockModel> OnBlockStateChanged;
        public Subject<BlockInstance> OnBlockPlaced = new();
        // public event Action<BlockModel> OnBlockDestroyed;
        public Subject<ScoreEvent> OnScoreAdded = new();
        public Subject<FitTypeEvent> OnFitCheck = new();
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
                .Subscribe<StartSpawnEvent>(x => OnSpawnGridWithPreset(x.BlockPreset))
                .AddTo(ref disposableBuilder);
            _messageHub
                .Subscribe<ClearGridEvent>(_ => ClearGrid().Forget())
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            OnScoreAdded.Dispose();
            OnFitCheck.Dispose();
            _subscriptions.Dispose();
            _blockOnGrid.ForEach(x => x.Subscription?.Dispose());
        }

        #region Events

        private void OnFinishedLoading(SceneType sceneType)
        {
            DebugUtils.Log("GridManager: OnFinishedLoading " + sceneType);
            CurrentSceneType = sceneType;
            if (CurrentSceneType is SceneType.Gameplay) 
                OnGameplaySceneActivated();
        }
        
        private void OnGameplaySceneActivated()
        {
            SetUpGameplayGridPreset();
            CreateCells();
        }

        private void OnSpawnGridWithPreset(BlockPreset preset)
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
            SetUpGameplayGridPreset();
            CreateCells();
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
            for (int x = 0; x < row; x++)
            {
                for (int y = 0; y < column; y++)
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
            }
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
        /// <returns>true if the placement is valid, false otherwise</returns>
        public bool TryPlaceBlock(BlockInstance blockInstance)
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
            OnScoreAdded.OnNext(new(ScoreTypes.Placement, worldPosition: blockViewTransform.position));
            blockInstance.ViewModel.BlockInteractionState.Value = BlockInteractionState.PlacedOnGrid;
            blockInstance.GameObject.transform.SetParent(_grid.transform);
            //blockView.ResetSortingLayer();
            ReorderRenderingOrder();
            var fit = UpdateBlockOnGrid(blockInstance);
            OnFitCheck?.OnNext(new FitTypeEvent(fit, blockInstance));
            OnBlockPlaced?.OnNext(blockInstance);
            return true;
        }
        
        /// <summary>
        /// Update the block on the grid, check for contacts and validate placement
        /// </summary>
        /// <param name="blockModel"></param>
        /// <returns>FitType indicating the result of the update</returns>
        public FitType UpdateBlockOnGrid(BlockInstance blockModel)
        {
            if (!CreateVacantSchema(out var vacantSchema, out _)) //Fit Me!
            {
                _vacantSchema = vacantSchema;
                FitMe().Forget();
                return FitType.FitMe;
            }
            var contacts = new List<BlockInstance>();
            if (!CheckForContact(blockModel, contacts))
            {
                return FitType.None;
            }
            Combo(contacts).Forget();
            return FitType.Combo;
        }

        private async UniTask FitMe()
        {
            if (CurrentSceneType is not SceneType.Gameplay) return;
            List<(BlockState beforeExplodeState, BlockColor blockType)> blocksToSave = 
                _blockOnGrid.Select(x => (x.BlockInstance.Model.BlockState, x.BlockInstance.Model.BlockColor.CurrentValue)).ToList();
            await ClearGrid();
            //PlayerDataManager.Instance.SaveBlockDestroyed(FitType.FitMe, blocksToSave);
            OnScoreAdded.OnNext(new(ScoreTypes.FitMe, worldPosition:_grid.GetGridCenter(CurrentGridSize, CurrentOffset)));
            RegenerateGrid();
        }

        private async UniTask Combo(List<BlockInstance> contacts)
        {
            var middleOfBlocks = contacts.Select(block => block.GameObject.transform.position)
                .Aggregate(Vector3.zero, (current, position) => current + position) / contacts.Count;
            List<(BlockState beforeExplodeState, BlockColor blockType)> blocksToSave = 
                _blockOnGrid.Select(x => (x.BlockInstance.Model.BlockState, x.BlockInstance.Model.BlockColor.CurrentValue)).ToList();
            //AudioManager.Instance.PlayAudioOneShot(stackExplodeSfx, transform.position);
            var gridBlockData = _blockOnGrid.Where(x => contacts.Contains(x.BlockInstance)).ToList();
            //await UniTask.WhenAll(gridBlockData.Select(block => RemoveBlock(block, FitType.Combo, true)));
            //PlayerDataManager.Instance.SaveBlockDestroyed(FitType.Combo, blocksToSave);
            OnScoreAdded.OnNext(new(ScoreTypes.Combo, contacts.Count, middleOfBlocks));
            OnScoreAdded.OnNext(new(ScoreTypes.Bomb, contacts.Count, middleOfBlocks));
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
            //OnBlockDestroyed?.Invoke(gridBlockData.Block);
            var atoms = new List<AtomInstance>(gridBlockData.BlockInstance.Model.Atoms);
            if (CurrentSceneType is SceneType.Gameplay)
            {
                gridBlockData.BlockInstance.Model.BlockState = BlockState.Exploding;
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

        public async UniTask ClearGrid()
        {
            _audioManager.PlayAudioOneShot(_config.FitExplodeSfx, Vector3.zero);
            await RemoveAllBlocks(true);
        }
    
        /// <summary>
        /// Remove all blocks from the grid
        /// </summary>
        /// <param name="destroy">Destroy the blocks, false by default</param>
        public async UniTask RemoveAllBlocks(bool destroy = false)
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
            BlockColor currentColor = blockInstance.Model.BlockColor.CurrentValue;
            contactedBlocks.Add(blockInstance);
            foreach (var cell in blockInstance.Model.BlockCells)
            {
                var upCell = GetCellByArrayIndex(cell.Model.ArrayIndex.Value[0] - 1, cell.Model.ArrayIndex.Value[1]);
                var downCell = GetCellByArrayIndex(cell.Model.ArrayIndex.Value[0] + 1, cell.Model.ArrayIndex.Value[1]);
                var leftCell = GetCellByArrayIndex(cell.Model.ArrayIndex.Value[0], cell.Model.ArrayIndex.Value[1] - 1);
                var rightCell = GetCellByArrayIndex(cell.Model.ArrayIndex.Value[0], cell.Model.ArrayIndex.Value[1] + 1);
                var adjacentCells = new List<CellInstance> {upCell, downCell, leftCell, rightCell};
                foreach (var adjacentCell in adjacentCells)
                {
                    if (adjacentCell?.Model.CurrentAtom.Value == null) continue;
                    var adjacentBlock = adjacentCell.Model.CurrentAtom.Value.Model.ParentBlock.Value;
                    if (adjacentBlock.Model.BlockState is BlockState.Infected or BlockState.Exploding) continue;
                    if (adjacentBlock.Model.BlockColor.CurrentValue != currentColor) continue;
                    if (contactedBlocks.Contains(adjacentBlock)) continue;
                    CheckForContact(adjacentBlock, contactedBlocks);
                }
            }
            return contactedBlocks.Count >= _config.ComboThreshold;
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
                        cell.Model.CurrentAtom.Value.Model.ParentBlock.Value.Model.BlockState is not BlockState.Exploding) 
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
