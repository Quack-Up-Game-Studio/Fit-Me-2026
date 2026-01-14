using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FMODUnity;
using MessagePipe;
using ObservableCollections;
using QuackUp.SceneManagement;
using R3;
using Redcode.Extensions;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Serialization;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;
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
    
    [ShowOdinSerializedPropertiesInInspector]
    public class GridManager : IDisposable
    {
        private readonly UnityEngine.Grid _grid;
        private readonly GridManagerConfig _config;
        private readonly CellFactory _cellFactory;
        private readonly ISubscriber<LoadSceneStageEvent> _sceneStageSubscriber;
        
        private IDisposable _subscriptions;
        
        #region Inspector
        
        [Title("Grid References")]
        [SerializeField] private List<GridPreset> gridPresets = new();
        
        [field: Title("Grid Debug")] 
        [field: SerializeField, DisableInPlayMode] [OnValueChanged(nameof(OnPresetChanged))]
        [field: InlineEditor]
        public GridPreset CurrentGridPreset { get; private set; }
        [Button("Refresh Grid Size")]
        private void OnPresetChanged()
        {
            currentGridSize = CurrentGridPreset ? CurrentGridPreset.GridSize : new Vector2Int(6, 8);
            UpdateGridOffset();
        }
        [SerializeField, Sirenix.OdinInspector.ReadOnly] [OnValueChanged(nameof(UpdateGridOffset))]
        [MinValue(1)]
        private Vector2Int currentGridSize = new(10, 10);
        [SerializeField, Sirenix.OdinInspector.ReadOnly] 
        private Vector2Int currentOffset = new(0, 0);
        #if UNITY_EDITOR
        [TableMatrix(SquareCells = true, HorizontalTitle = "Cell Array", IsReadOnly = true,
            DrawElementMethod = nameof(DrawCellArrayMatrix), Transpose = true)]
        #endif
        [SerializeField] private CellModel[,] _cellArray = {};
        #if UNITY_EDITOR
        [TableMatrix(SquareCells = true, HorizontalTitle = "Vacant Schema", IsReadOnly = true,
            DrawElementMethod = nameof(DrawVacantSchemaMatrix), Transpose = true)]
        #endif
        [SerializeField] private int[,] _vacantSchema = {};

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        private List<BlockModel> DebugBlockOnGrid => new(BlocksOnGrid);
        public ObservableList<BlockModel> BlocksOnGrid { get; private set; } = new();
        [SerializeField, ShowIf("@CurrentGridPreset && CurrentGridPreset.PresetGridType.HasFlag(GridType.Custom)")]
        private bool drawAllCustomGridCells = true;

        // [Title("Infected Debug")]
        // [ShowInInspector, DisplayAsString] public int TotalInfected => preInfectBlocks.Count + infectedBlocks.Count;
        
        [Button("Test Fit-me")]
        private void TestFitMe()
        {
            OnScoreAdded?.Invoke(ScoreTypes.FitMe, worldPosition: GetGridCenter());
            OnNextGameDifficulty?.Invoke();
        }
        // [field: SerializeField, Sirenix.OdinInspector.ReadOnly] public float RandomInfectedTime { get; private set; }
        // [SerializeField, Sirenix.OdinInspector.ReadOnly] private List<Block> preInfectBlocks = new();
        // [SerializeField, Sirenix.OdinInspector.ReadOnly] private List<Block> infectedBlocks = new();
        // [ShowInInspector, Sirenix.OdinInspector.ReadOnly] private Dictionary<GameDifficulty, List<GridPreset>> _difficultyGridPresets = new();
        #endregion

        #region Fields and Properties
        
        private List<CellModel> _previousValidationCells = new();
        public static event Action<BlockModel> OnBlockStateChanged;
        public static event Action<BlockModel> OnBlockPlaced;
        public static event Action<BlockModel> OnBlockDestroyed;
        public delegate void ScoreAdded(ScoreTypes scoreTypes, int contactCount = 0, Vector3 worldPosition = default);
        public static event ScoreAdded OnScoreAdded;
        public static event Action<FitType> OnFitCheck;
        public static event Action OnNextGameDifficulty;
        public UnityEngine.Grid Grid => _grid;
        // private IRequestHandler<GameStateRequest, GameState> _gameStateRequestHandler;
        // private IRequestHandler<GameDifficultyRequest, GameDifficulty> _gameDifficultyRequestHandler;
        // private IRequestHandler<BlockPresetRequest, List<BlockPreset>> _blockPresetRequestHandler;
        // private IPublisher<StartSpawnEvent> _startSpawnPublisher;
        private int _currentPresetIndex;
        private SceneType _currentSceneType;
        #endregion
        

        [Inject]
        public GridManager(
            UnityEngine.Grid grid,
            GridManagerConfig config,
            CellFactory cellFactory,
            ISubscriber<LoadSceneStageEvent> sceneStageSubscriber)
        {
            _grid = grid;
            _config = config;
            _cellFactory = cellFactory;
            _sceneStageSubscriber = sceneStageSubscriber;
            Subscribe();
        }

        private void Subscribe()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _sceneStageSubscriber
                .AsObservable().ToObservable()
                .Where(x => x.Stage == LoadSceneStage.FinishLoading)
                .Select(x => x.NextSceneType)
                .Subscribe(OnFinishedLoading)
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        #region Events

        private void OnFinishedLoading(SceneType sceneType)
        {
            _currentSceneType = sceneType;
            switch (sceneType)
            {
                case SceneType.MainMenu:
                    OnMainMenuSceneActivated();
                    break;
                case SceneType.Gameplay:
                    OnGameplaySceneActivated();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sceneType), sceneType, null);
            }
        }
        
        private void OnGameplaySceneActivated()
        {
            SetUpGameplayGridPreset();
            CreateCells();
        }

        private void OnMainMenuSceneActivated()
        {
            // if (_blockPresetRequestHandler == null) return;
            // var blockPresets = _blockPresetRequestHandler.Invoke(new BlockPresetRequest());
            // if (blockPresets.Count == 0) return;
            // var randomPreset = blockPresets.GetRandomElement();
            // SetUpMainMenuGridPreset(randomPreset);
            // CreateCells();
            // _startSpawnPublisher.Publish(new StartSpawnEvent(randomPreset));
        }
        #endregion
        
        // #region Initialization
        // protected void Awake()
        // {
        //     if (!_grid.cellSize.x.Equals(_grid.cellSize.y))
        //     {
        //         Debug.LogError("Grid cell size must be the same in both axes!");
        //     }
        //     // var infectionConfig = CurrentGridPreset.InfectionSettings;
        //     // RandomInfectedTime = Random.Range(infectionConfig.InfectionCountRange.x, infectionConfig.InfectionCountRange.y);
        // }
        //
        // private static Dictionary<GameDifficulty, List<GridPreset>> BucketByFlags(
        //     IEnumerable<GridPreset> source)
        // {
        //     // build the empty buckets first (one per defined flag)
        //     var flags = Enum.GetValues(typeof(GameDifficulty))
        //         .Cast<GameDifficulty>()
        //         .Where(f => f != 0 && (f & (f - 1)) == 0)  // only single-bit values
        //         .ToList();
        //     var lookup = flags.ToDictionary(f => f, _ => new List<GridPreset>());
        //     // fill the buckets
        //     foreach (var item in source)
        //     {
        //         foreach (var flag in flags)
        //         {
        //             if (item.GameDifficulty.HasFlag(flag))
        //                 lookup[flag].Add(item);
        //         }
        //     }
        //     return lookup;
        // }
        // #endregion
        
        #region Grid Generation
        private void SetUpGameplayGridPreset()
        {
            var currentEndlessType = _config.EndlessType;
            if (_config.EndlessType is EndlessType.All)
            {
                currentEndlessType = Random.Range(0, 2) == 0 ? EndlessType.Preset : EndlessType.Generated;
            }
            if (currentEndlessType is EndlessType.Preset && gridPresets.Count > 0)
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
                CurrentGridPreset = gridPresets.GetRandomElement();
            }
            else
            {
                CurrentGridPreset = gridPresets[_currentPresetIndex];
                _currentPresetIndex++;
                if (_currentPresetIndex >= gridPresets.Count)
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
                Debug.LogWarning("No valid ranges found, creating a full bridge.");
                for (var i = 0; i < columnCount; i++)
                {
                    bridgeIndices[i] = 1;
                }
                return bridgeIndices;
            }
            foreach (var range in validRanges)
            {
                Debug.Log($"Adding bridge from {range.start} to {range.end}");
                for (var i = range.start; i <= range.end; i++)
                {
                    bridgeIndices[i] = 1;
                }
            }
            return bridgeIndices;
        }
        public void RegenerateGrid()
        {
            Debug.Log("Regenerating grid...");
            ResetPreviousValidationCells();
            foreach (var cell in _cellArray)
            {
                cell?.Dispose();
            }
            _cellArray = new CellModel[0, 0];
            _vacantSchema = new int[0, 0];
            SetUpGameplayGridPreset();
            CreateCells();
        }
        
        private void UpdateGridOffset()
        {
            switch (_config.GridHorizontalOffsetType)
            {
                case GridOffsetType.Automatic:
                    currentOffset.x = -Mathf.FloorToInt(currentGridSize.x / 2f) + _config.CustomOffsetX;
                    if (currentGridSize.x % 2 != 0)
                    {
                        _grid.transform.SetPositionX(-_grid.cellSize.x / 2f);
                    }
                    else
                    {
                        _grid.transform.SetPositionX(0f);
                    }
                    break;
                case GridOffsetType.Custom:
                    currentOffset.x = _config.CustomOffsetX;
                    break;
            }
            
            switch (_config.GridVerticalOffsetType)
            {
                case GridOffsetType.Automatic:
                    currentOffset.y = Mathf.FloorToInt(currentGridSize.y / 2f) + _config.CustomOffsetY;
                    break;
                case GridOffsetType.Custom:
                    currentOffset.y = _config.CustomOffsetY;
                    break;
            }
        }
        
        /// <summary>
        /// Create the cells
        /// </summary>
        private void CreateCells()
        {
            currentGridSize = CurrentGridPreset.GridSize;
            UpdateGridOffset();
            var row = currentGridSize.y;
            var column = currentGridSize.x;
            var cellSize = _grid.cellSize.x;
            _cellArray = new CellModel[row, column];
            for (int x = 0; x < row; x++)
            {
                for (int y = 0; y < column; y++)
                {
                    if (CurrentGridPreset.PresetGridType is GridType.Custom && CurrentGridPreset.customGrid[x, y] == 0) continue; 
                    var halfSize = cellSize / 2;
                    var spawnPosition =
                        (Vector3)(new Vector2(halfSize, halfSize) +
                                  new Vector2(y + currentOffset.x, currentOffset.y - x) * cellSize) +
                        _grid.transform.position;
                    var cell = _cellFactory.Create(spawnPosition, Quaternion.identity, out var cellGameObject);
                    cellGameObject.transform.localScale = Vector3.one * cellSize;
                    cellGameObject.name = $"Cell {x}_{y}";
                    cell.ArrayIndex.Value = new Vector2Int(x, y);
                    cell.GridIndex.Value = ArrayToGridIndex(new Vector2Int(x, y));
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
            List<CellModel> cells = new List<CellModel>();
            foreach (var atom in blockModel.Atoms)
            {
                Vector3 atomPosition = atom.transform.position;
                Vector3 cellPosition = new Vector3(atomPosition.x, atomPosition.y, 0);
                CellModel cellModel = GetCellByPosition(cellPosition);
                if (cellModel == null || cellModel.CurrentAtom.Value)
                {
                    continue;
                }
                cells.Add(cellModel);
            }
            if (_previousValidationCells.Count > 0)
            {
                ResetPreviousValidationCells();
            }
            _previousValidationCells = cells;
            if (cells.Count < blockModel.Atoms.Count)
            {
                cells.ForEach(cell => cell.State.Value = CellState.CannotBePlaced);
                return false;
            }
            cells.ForEach(cell => cell.State.Value = CellState.CanBePlaced);
            return true;
        }
        
        /// <summary>
        /// Place the block in the grid
        /// </summary>
        /// <param name="blockModel">Block to place</param>
        /// <returns>true if the placement is valid, false otherwise</returns>
        public bool PlaceBlock(BlockModel blockModel)
        {
            var cellSize = _grid.cellSize.x;
            blockModel.transform.localScale = Vector3.one * cellSize;
            var atomPositionBeforePlacement = blockModel.Atoms[0].transform.position;
            var cells = new List<CellModel>();
            foreach (var atom in blockModel.Atoms)
            {
                var atomPosition = atom.transform.position;
                var cellPosition = new Vector3(atomPosition.x, atomPosition.y, 0);
                var cellModel = GetCellByPosition(cellPosition);
                if (cellModel == null || cellModel.CurrentAtom.Value)
                {
                    return false;
                }
                cells.Add(cellModel);
            }
            for (var i = 0; i < blockModel.Atoms.Count; i++) 
            {
                var atom = blockModel.Atoms[i];
                cells[i].CurrentAtom.Value = atom;
            }
            var atomPositionAfterPlacement = blockModel.Atoms[0].transform.position;
            var blockPositionRelativeToAtom = atomPositionAfterPlacement - atomPositionBeforePlacement;
            blockModel.transform.position += blockPositionRelativeToAtom;
            blockModel.transform.SetParent(_grid.transform);
            blockModel.BlockCells = cells;
            BlocksOnGrid.Add(blockModel);
            blockModel.ResetSortingLayer();
            ReorderRenderingOrder();
            OnScoreAdded?.Invoke(ScoreTypes.Placement, worldPosition: blockModel.transform.position);
            ResetPreviousValidationCells();
            var blockView = blockModel.BlockView;
            if (blockView) blockView.Place();
            var fit = UpdateBlockOnGrid(blockModel);
            OnFitCheck?.Invoke(fit);
            if (_currentSceneType == SceneType.Gameplay)
            {
                if (fit is FitType.FitMe)
                {
                    BlockManager.Instance.FreeSpawnPoint(blockModel.SpawnIndex);
                    BlockManager.Instance.ResetSpawnPoint();
                    BlockManager.Instance.SpawnRandomBlock();
                }
                else
                {
                    BlockManager.Instance.FreeSpawnPoint(blockModel.SpawnIndex);
                    BlockManager.Instance.ResetSpawnPoint();
                    BlockManager.Instance.SpawnRandomBlock();
                }
                if (fit is FitType.None)
                {
                    BlockManager.Instance.GameOverCheck().Forget();
                }
            }
            OnBlockPlaced?.Invoke(blockModel);
            return true;
        }
        /// <summary>
        /// Update the block on the grid, check for contacts and validate placement
        /// </summary>
        /// <param name="blockModel"></param>
        /// <returns>FitType indicating the result of the update</returns>
        public FitType UpdateBlockOnGrid(BlockModel blockModel)
        {
            if (!CreateVacantSchema(out var vacantSchema, out _)) //Fit Me!
            {
                _vacantSchema = vacantSchema;
                FitMe().Forget();
                return FitType.FitMe;
            }
            var contacts = new List<BlockModel>();
            if (!CheckForContact(blockModel, contacts))
            {
                return FitType.None;
            }
            Combo(contacts).Forget();
            return FitType.Combo;
        }

        private async UniTask FitMe()
        {
            if (_currentSceneType is not SceneType.Gameplay) return;
            List<(BlockState beforeExplodeState, BlockTypes blockType)> blocksToSave = 
                BlocksOnGrid.Select(block => (block.BlockState, block.BlockType)).ToList();
            await ClearGrid();
            //PlayerDataManager.Instance.SaveBlockDestroyed(FitType.FitMe, blocksToSave);
            OnScoreAdded?.Invoke(ScoreTypes.FitMe, worldPosition:GetGridCenter());
            OnNextGameDifficulty?.Invoke();
            RegenerateGrid();
        }

        private async UniTask Combo(List<BlockModel> contacts)
        {
            var middleOfBlocks = contacts.Select(block => block.transform.position)
                .Aggregate(Vector3.zero, (current, position) => current + position) / contacts.Count;
            List<(BlockState beforeExplodeState, BlockTypes blockType)> blocksToSave = 
                contacts.Select(block => (block.BlockState, block.BlockType)).ToList();
            //AudioManager.Instance.PlayAudioOneShot(stackExplodeSfx, transform.position);
            await UniTask.WhenAll(contacts.Select(block => RemoveBlock(block, FitType.Combo, true)));
            //PlayerDataManager.Instance.SaveBlockDestroyed(FitType.Combo, blocksToSave);
            OnScoreAdded?.Invoke(ScoreTypes.Combo, contacts.Count, middleOfBlocks);
            OnScoreAdded?.Invoke(ScoreTypes.Bomb, contacts.Count, middleOfBlocks);
            BlockManager.Instance.GameOverCheck().Forget();
        }
        
        /// <summary>
        /// Remove the block from the grid
        /// </summary>
        /// <param name="blockModel">Block to remove</param>
        /// <param name="destroy">Destroy the block, false by default</param>
        public async UniTask RemoveBlock(BlockModel blockModel, FitType fitType, bool destroy = false)
        {
            //DisinfectBlock(block);
            BlocksOnGrid.Remove(blockModel);
            // infectedBlocks.Remove(block);
            // preInfectBlocks.Remove(block);
            OnBlockDestroyed?.Invoke(blockModel);
            var atoms = new List<AtomView>(blockModel.Atoms);
            if (_currentSceneType is SceneType.Gameplay) 
                await blockModel.Explode(fitType, destroy);
            foreach (var atom in atoms)
            {
                var cellModel = GetCellByPosition(atom.transform.position);
                if (cellModel == null || cellModel.CurrentAtom.Value != atom)
                {
                    continue;
                }
                cellModel.CurrentAtom.Value = null;
            }
        }

        public async UniTask ClearGrid()
        {
            //AudioManager.Instance.PlayAudioOneShot(fitMeExplodeSfx, transform.position);
            await RemoveAllBlocks(true);
        }
    
        /// <summary>
        /// Remove all blocks from the grid
        /// </summary>
        /// <param name="destroy">Destroy the blocks, false by default</param>
        public async UniTask RemoveAllBlocks(bool destroy = false)
        {
            List<BlockModel> blocksToRemove = new List<BlockModel>(BlocksOnGrid);
            await UniTask.WhenAll(blocksToRemove.Select(block => RemoveBlock(block, FitType.FitMe, destroy)));
        }
        
        /// <summary>
        /// Reset the color of the previous validation cells
        /// </summary>
        public void ResetPreviousValidationCells()
        {
            if (_previousValidationCells.Count == 0) return;
            _previousValidationCells.ForEach(cell => cell.State.Value = CellState.None);
            _previousValidationCells.Clear();
        }
        
        /// <summary>
        /// Reorder the rendering order of the blocks on the grid
        /// </summary>
        public void ReorderRenderingOrder()
        {
            for (var i = 0; i < BlocksOnGrid.Count; i++)
            {
                var block = BlocksOnGrid[i];
                block.SetSortingOrder(i);
            }
        }
        #endregion
    
        #region Contacts
        /// <summary>
        /// Check for contact with other blocks
        /// </summary>
        /// <param name="blockModel">Current block</param>
        /// <param name="contactedBlocks">List of contacted blocks</param>
        /// <returns>true if the contacted blocks count is greater than or equal to the destroy threshold, false otherwise</returns>
        private bool CheckForContact(BlockModel blockModel, List<BlockModel> contactedBlocks)
        {
            BlockTypes currentType = blockModel.BlockType;
            contactedBlocks.Add(blockModel);
            foreach (var cell in blockModel.BlockCells)
            {
                var upCell = GetCellByArrayIndex(cell.ArrayIndex.Value[0] - 1, cell.ArrayIndex.Value[1]);
                var downCell = GetCellByArrayIndex(cell.ArrayIndex.Value[0] + 1, cell.ArrayIndex.Value[1]);
                var leftCell = GetCellByArrayIndex(cell.ArrayIndex.Value[0], cell.ArrayIndex.Value[1] - 1);
                var rightCell = GetCellByArrayIndex(cell.ArrayIndex.Value[0], cell.ArrayIndex.Value[1] + 1);
                var adjacentCells = new List<CellModel> {upCell, downCell, leftCell, rightCell};
                foreach (var adjacentCell in adjacentCells)
                {
                    if (adjacentCell == null || !adjacentCell.CurrentAtom.Value) continue;
                    var adjacentBlock = adjacentCell.CurrentAtom.Value.ParentBlockModel;
                    if (adjacentBlock.BlockState is BlockState.Infected or BlockState.Exploding) continue;
                    if (adjacentBlock.BlockType != currentType) continue;
                    if (contactedBlocks.Contains(adjacentBlock)) continue;
                    CheckForContact(adjacentBlock, contactedBlocks);
                }
            }
            return contactedBlocks.Count >= _config.DestroyThreshold;
        }

        /// <summary>
        /// Create a schema of the vacant cells, 1 is vacant, 0 is occupied
        /// </summary>
        /// <returns>true if there are vacant cells, false otherwise</returns>
        public bool CreateVacantSchema(out int[,] vacantSchema, out int vacantCount)
        {
            vacantCount = 0;
            var row = currentGridSize.y;
            var column = currentGridSize.x;
            vacantSchema = new int[row, column];
            bool isVacant = false;
            for (int x = 0; x < row; x++)
            {
                for (int y = 0; y < column; y++)
                {
                    var cell = _cellArray[x, y];
                    if (cell == null) continue;
                    if (cell.CurrentAtom.Value
                        && cell.CurrentAtom.Value.ParentBlockModel 
                        && cell.CurrentAtom.Value.ParentBlockModel.BlockState is not BlockState.Exploding) continue;
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
                if (CompareSchema(block, block.transform.eulerAngles.z))
                {
                    availableBlocks.Add(block);
                    continue;
                }
                Debug.Log("Block " + block.name + " cannot be placed");
            }
            if (availableBlocks.Count != 0) return true;
            Debug.Log("No blocks can be placed");
            return false;
        }

        /// <summary>
        /// Compare the schema of the block with the vacant schema
        /// </summary>
        /// <param name="blockModel">Block to compare</param>
        /// <param name="currentZEulerAngle">current Z euler angle of the block</param>
        /// <returns>true if the block can be placed, false otherwise</returns>
        private bool CompareSchema(BlockModel blockModel, float currentZEulerAngle)
        {
            var index = (int)currentZEulerAngle / 90;
            Debug.Log("Block " + blockModel.name + " angle: " + currentZEulerAngle + ", index: " + index);
            if (ArrayHelper.CanBFitInA(_vacantSchema, blockModel.BlockPreset.BlockSchemas[index].schema, out _))
            {
                Debug.Log("Block " + blockModel.name + " can be placed");
                return true;
            }
            return false;
        }
        
        public bool CompareSchema(BlockModel blockModel, int schemaIndex)
        {
            if (schemaIndex < 0 || schemaIndex >= blockModel.BlockPreset.BlockSchemas.Count)
            {
                Debug.LogError("Invalid schema index: " + schemaIndex);
                return false;
            }
            return CompareSchema(blockModel, schemaIndex * 90f);
        }
        #endregion
        
        /*#region Infection
        
        public void StopAllPreInfectFlash()
        {
            foreach (var block in preInfectBlocks)
            {
                block.StopAllFlash();
            }
        }

        /// <summary>
        /// Check if more blocks can be infected
        /// </summary>
        /// <returns>>true if more blocks can be infected, false otherwise</returns>
        private bool CanInfectMore()
        {
            var infectionConfig = CurrentGridPreset.InfectionSettings;
            return TotalInfected < infectionConfig.InfectionCountRange.y;
        }

        public void InfectBlock(Block block)
        {
            if (_gameStateRequestHandler == null) return;
            var currentGameState = _gameStateRequestHandler.Invoke(new GameStateRequest());
            if (currentGameState is not (GameState.PlaceBlock or GameState.UseItem)) return;
            preInfectBlocks.Remove(block);
            infectedBlocks.Add(block);
            block.Infect();
            OnBlockStateChanged?.Invoke(block);
            var infectionConfig = CurrentGridPreset.InfectionSettings;
            RandomInfectedTime = Random.Range(infectionConfig.InfectionTimeRange.x, infectionConfig.InfectionTimeRange.y);
        }

        public void DisinfectBlock(Block block, bool updateGrid = false)
        {
            if (_gameStateRequestHandler == null) return;
            var currentGameState = _gameStateRequestHandler.Invoke(new GameStateRequest());
            if (currentGameState is not (GameState.PlaceBlock or GameState.UseItem)) return;
            block.Disinfect();
            OnBlockStateChanged?.Invoke(block);
            infectedBlocks.Remove(block);
            if (updateGrid) UpdateBlockOnGrid(block);
        }
        
        public bool InfectRandomBlock(out Block block)
        {
            block = null;
            if (preInfectBlocks.Count >= 1) return false;
            if (!CanInfectMore()) return false;
            if (BlocksOnGrid.Count == 0) return false;
            
            var infectableBlocks = BlocksOnGrid.Where(block => block.BlockState == BlockState.Normal).ToList();
            if (infectableBlocks.Count == 0) return false;
            var infectBlock = infectableBlocks.GetRandomElement();
            infectBlock.PreInfect().Forget();
            OnBlockStateChanged?.Invoke(infectBlock);
            preInfectBlocks.Add(infectBlock);
            block = infectBlock;
            return true;
        }

        public void InfectAdjacentBlocks(Block sourceBlock)
        {
            if (!CanInfectMore()) return;
            if (preInfectBlocks.Count >= 1) return;
            if (!sourceBlock || sourceBlock.BlockState is not (BlockState.Infected or BlockState.PreInfected)) return;

            var candidatesForInfection = new List<Block>();

            foreach (var atom in sourceBlock.Atoms)
            {
                Cell cell = GetCellByPosition(atom.transform.position);
                if (!cell) continue;

                int x = cell.ArrayIndex[0];
                int y = cell.ArrayIndex[1];
                
                Cell[] adjacentCells =
                {
                    GetCellByArrayIndex(x - 1, y),
                    GetCellByArrayIndex(x + 1, y),
                    GetCellByArrayIndex(x, y - 1),
                    GetCellByArrayIndex(x, y + 1)
                };

                foreach (var adjacentCell in adjacentCells)
                {
                    if (!adjacentCell || !adjacentCell.CurrentAtom) continue;
                    Block adjacentBlock = adjacentCell.CurrentAtom.ParentBlock;
            
                    if (adjacentBlock && adjacentBlock.BlockState == BlockState.Normal)
                    {
                        candidatesForInfection.Add(adjacentBlock);
                    }
                }
            }
            
            if (candidatesForInfection.Count > 0)
            {
                var blockToInfect = candidatesForInfection.GetRandomElement(); 
                blockToInfect.PreInfect().Forget();
                OnBlockStateChanged?.Invoke(blockToInfect);
                preInfectBlocks.Add(blockToInfect);
            }
        }
        #endregion*/
        
        #region Utils
        /// <summary>
        /// Get the cell by array index
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>A Cell if it exists, null otherwise</returns>
        public CellModel GetCellByArrayIndex(int x, int y)
        {
            if (x < 0 || x >= currentGridSize.y || y < 0 || y >= currentGridSize.x)
            {
                return null;
            }
            return _cellArray[x, y];
        }
        
        public CellModel GetCellByArrayIndex(Vector2Int index)
        {
            return GetCellByArrayIndex(index.x, index.y);
        }
        
        public CellModel GetCellByGridIndex(int x, int y)
        {
            return GetCellByGridIndex(new Vector2Int(x, y));
        }
        
        public CellModel GetCellByGridIndex(Vector2Int gridIndex)
        {
            return GetCellByArrayIndex(GridToArrayIndex(gridIndex));
        }
        
        public Vector2Int ArrayToGridIndex(Vector2Int arrayIndex)
        {
            return new Vector2Int(arrayIndex.y + currentOffset.x, currentOffset.y - arrayIndex.x);
        }
        
        public Vector2Int GridToArrayIndex(Vector2Int gridIndex)
        {
            return new Vector2Int(currentOffset.y - gridIndex.y, gridIndex.x - currentOffset.x);
        }
    
        /// <summary>
        /// Get the cell by position, it will be rounded to the nearest cell
        /// </summary>
        /// <param name="position">Position to try to get a cell</param>
        /// <returns>A Cell if it exists, null otherwise</returns>
        public CellModel GetCellByPosition(Vector3 position)
        {
            var worldToCell = _grid.WorldToCell(position);
            int x = worldToCell.x;
            int y = worldToCell.y;
            return GetCellByGridIndex(x, y);
        }
        
        public Bounds GetCellBounds(CellModel cellModel)
        {
            var index = cellModel.GridIndex.Value;
            return GetCellBounds(index);
        }

        public Bounds GetCellBounds(Vector2Int gridIndex)
        {
            var cellBounds = _grid.GetBoundsLocal((Vector3Int)gridIndex);
            var centerWorld = _grid.GetCellCenterWorld((Vector3Int)gridIndex);
            cellBounds.center = centerWorld;
            return cellBounds;
        }

        public Vector2 GetGridCenter()
        {
            var column = currentGridSize.x;
            var row = currentGridSize.y;
            List<int> centerRows = column % 2 == 0
                ? new List<int> { column / 2 - 1, column / 2 }
                : new List<int> { Mathf.FloorToInt(column / 2f) };
            List<int> centerColumn = row % 2 == 0
                ? new List<int> { row / 2 - 1, row / 2 }
                : new List<int> { Mathf.FloorToInt(row / 2f) };
            List<Vector2Int> centerCells = (from x in centerRows from y in centerColumn 
                select ArrayToGridIndex(new Vector2Int(y, x))).ToList();
            Debug.Log("Center Cells: " + string.Join(", ", centerCells.Select(c => c.ToString())));
            List<Bounds> centerCellBounds = centerCells.Select(GetCellBounds).ToList();
            var center = centerCellBounds.Aggregate(Vector3.zero, (current, bounds) => current + bounds.center) 
                         / centerCellBounds.Count;
            return center;
        }
        #endregion
        
        #region Editor
        #if UNITY_EDITOR
        public void OnSceneGUI()
        {
            DrawGrid();
        }
        private void DrawGrid()
        {
            if (!CurrentGridPreset) return;
            var row = currentGridSize.y;
            var column = currentGridSize.x;
            for (int x = 0; x < row; x++)
            {
                for (int y = 0; y < column; y++)
                {
                    var textColor = Color.green;
                    var handleColor = Color.green;
                    if (CurrentGridPreset.PresetGridType is GridType.Custom && CurrentGridPreset.customGrid[x, y] == 0)
                    {
                        if (!drawAllCustomGridCells) continue;
                        handleColor = Color.red;
                        textColor = Color.red;
                    }
                    Handles.color = handleColor;
                    var arrayIndex = new Vector2Int(x, y);
                    var gridIndex = ArrayToGridIndex(arrayIndex);
                    var bounds = GetCellBounds(gridIndex);
                    Handles.DrawWireCube(bounds.center, bounds.size);
                    Handles.Label(bounds.center, arrayIndex.ToString(), style: new GUIStyle()
                    {
                        fontSize = 10,
                        normal = new GUIStyleState()
                        {
                            textColor = textColor
                        },
                        alignment = TextAnchor.MiddleCenter
                    });
                }
            }
        }
        #endif
        #endregion
        
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
            EditorGUI.DrawRect(rect.Padding(1), cellModel.CurrentAtom.Value ? Color.green : Color.grey);
            return cellModel;
        }
        #endregion
        #endif
    }

    // #if UNITY_EDITOR
    // [CustomEditor(typeof(GridManager), true)]
    // public class GridManagerEditor : OdinEditor
    // {
    //     public void OnSceneGUI()
    //     {
    //         if (target is not GridManager proceduralGrid) return;
    //         proceduralGrid.OnSceneGUI();
    //     }
    // }
    // #endif
}
