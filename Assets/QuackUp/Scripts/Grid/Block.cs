using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace FitMe.Grid
{
    #region Enums
    public enum BlockState
    {
        Normal,
        PreInfected,
        Infected,
        Protected,
        Exploding
    }
    
    public enum FlashState
    {
        None,
        Flashing,
        PreInfectFlash
    }
    
    public enum BlockTypes
    {
        Red,
        Yellow,
        Green,
        Purple,
        Blue
    }
    #endregion

    [ShowOdinSerializedPropertiesInInspector]
    public class Block : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, ISerializationCallbackReceiver, ISupportsPrefabSerialization
    {
        #region Inspectors
        [Title("Block References")]
        [SerializeField] private Atom atomPrefab;
        [SerializeField] private Transform atomParent;

        [Title("Block Settings")]
        [SerializeField] private bool useAtomSprite;
        [SerializeField, SortingLayer] private int originalSortingLayer;
        [SerializeField, SortingLayer] private int pickUpSortingLayer;
        [SerializeField] private Color originalAtomColor = Color.white;
        [SerializeField] private Color infectColor = Color.gray;
        [SerializeField] private float flashDuration = 0.2f;
        [field: SerializeField] public bool AllowPickUpAfterPlacement { get; private set; }
        
        [Title("Audios")] 
        [SerializeField] private EventReference placeSucceedSfx;
        [SerializeField] private EventReference placeFailSfx;
        [SerializeField] private EventReference preInfectSfx;
        [SerializeField] private EventReference infectSfx;
        
        [field: Title("Block Debug")]
        [field: SerializeField, DisplayAsString] public BlockTypes BlockType { get; private set; }
        [field: SerializeField, DisplayAsString] public string BlockFace { get; private set; }
        [field: SerializeField, ReadOnly] public List<Atom> Atoms { get; private set; } = new();
        [field: SerializeField, ReadOnly] public BlockPreset BlockPreset { get; private set; }
        [SerializeField, DisplayAsString] private FlashState flashState;
        [field: SerializeField, DisplayAsString] public BlockState BlockState { get; private set; } = BlockState.Normal;
        [field: SerializeField, DisplayAsString] public bool IsPlaced { get; private set; }
        [field: SerializeField, ReadOnly] public List<CellModel> BlockCells { get; set; }
        [field: SerializeField, ReadOnly] public BlockView BlockView { get; private set; }
        public int SpawnIndex { get; set; }
        public BlockState beforeExplodeState = BlockState.Normal;
        #endregion
        
        #region Fields and Properties
        private Color _infectColor;
        private Vector3 _originalPosition;
        private Vector3 _originalRotation;
        private Vector3 _originalScale;
        private Color _beforeFlashColor;
        private Vector3 _mousePositionDifference;
        private Tween _transformTween;
        private Tween _flashTween;
        private Tween _preInfectTween;
        private bool _isDragging;
        private IDisposable _infectionSubscription;
        private float _protectedTime;
        [Inject] private IObjectResolver _objectResolver;
        private IRequestHandler<GameStateRequest, GameState> _gameStateRequest;
        public event Action OnBlockBeingDrag;
        public event Action<bool> OnBlockEndDrag;
        #endregion

        #region Initialization
        public void Initialize()
        {
            _objectResolver.TryResolve(out _gameStateRequest);
            _originalPosition = transform.position;
            _originalRotation = transform.eulerAngles;
            _originalScale = transform.localScale;
            Atoms.ForEach(a => a.ParentBlock = this);
            _infectColor = infectColor;
        }

        private void StartInfectTimer()
        {
            _infectionSubscription?.Dispose();
            if (BlockState is not BlockState.Infected) return;
            _infectionSubscription = Observable
                .Interval(TimeSpan.FromSeconds(GridManager.Instance.RandomInfectedTime))
                .Subscribe(_ => GridManager.Instance.InfectAdjacentBlocks(this));
        }
        #endregion

        #region Events
        void OnDestroy()
        {
            _infectionSubscription?.Dispose();
        }
        #endregion

        #region Schema
        public void GenerateAtom(string blockFace, BlockPreset preset)
        {
            var row = preset.BlockSize.y;
            var column = preset.BlockSize.x;
            BlockFace = blockFace;
            BlockPreset = preset;
            for (var x = 0; x < row; x++)
            {
                for (var y = 0; y < column; y++)
                {
                    if (preset.BlockSchema.schema[x, y] == 0)
                    {
                        continue;
                    }
                    float spawnPosX = -column / 2f + 0.5f + y;
                    float spawnPosY = row / 2f - 0.5f - x;
                    Vector3 spawnPosition = new Vector3(spawnPosX, spawnPosY, 0);
                    var atom = Instantiate(atomPrefab, spawnPosition, Quaternion.identity, atomParent);
                    atom.ParentBlock = this;
                    var hasTop = HasElement(x - 1, y);
                    var hasBottom = HasElement(x + 1, y);
                    var hasLeft = HasElement(x, y - 1);
                    var hasRight = HasElement(x, y + 1);
                    // atom.SpriteOutlineController.outlineTop = !hasTop;
                    // atom.SpriteOutlineController.outlineBottom = !hasBottom;
                    // atom.SpriteOutlineController.outlineLeft = !hasLeft;
                    // atom.SpriteOutlineController.outlineRight = !hasRight;
                    // atom.SpriteOutlineController.UpdateOutline();
                    Atoms.Add(atom);
                }
            }
            // var spritePositionX = -column / 2f + 0.5f;
            // var spritePositionY = row / 2f - 0.5f;
            // spriteRenderer.transform.localPosition = new Vector3(spritePositionX, spritePositionY, 0);
            if (useAtomSprite && BlockView)
            {
                BlockView.gameObject.SetActive(false);
            }
            else
            {
                Atoms.ForEach(a => a.SpriteRenderer.enabled = false);
            }
            BlockPreset.GenerateSchema();
            
            bool HasElement(int x, int y)
            {
                if (x < 0 || x >= row || y < 0 || y >= column)
                    return false;
                return preset.BlockSchema.schema[x, y] == 1;
            }
        }
        #endregion

        #region Infection

        /// <summary>
        /// Change state of the block to Protected state.
        /// In a Protected state, the block cannot be PreInfect and Infect.
        /// </summary>
        public async UniTask Protected()
        {
            BlockState = BlockState.Protected;
            
            await UniTask.WaitForSeconds(_protectedTime,
                cancellationToken: destroyCancellationToken);
            BlockState = BlockState.Normal;
        }
        
        public async UniTask PreInfect()
        {
            var infectionConfig = GridManager.Instance.CurrentGridPreset.InfectionSettings;
            BlockState = BlockState.PreInfected;
            if (BlockView) BlockView.PreInfect();
            StartFlashing(FlashState.PreInfectFlash);
            beforeExplodeState = BlockState.PreInfected;
            AudioManager.Instance.PlayAudioOneShot(preInfectSfx, transform.position);
            await UniTask.WaitForSeconds(infectionConfig.PreInfectTime,
                cancellationToken: destroyCancellationToken);
            if (BlockState is BlockState.Exploding) return;
            GridManager.Instance.InfectBlock(this);
        }
        
        public void Infect()
        {
            BlockState = BlockState.Infected;
            beforeExplodeState = BlockState.Infected;
            if (BlockView) BlockView.Infect();
            StopFlashing();
            StartInfectTimer();
            AudioManager.Instance.PlayAudioOneShot(infectSfx, transform.position);
        }
        
        public void Disinfect()
        {
            SetColor(originalAtomColor);
            BlockState = BlockState.Normal;
            _infectionSubscription?.Dispose();
        }
        #endregion
        
        #region Interactions
        /// <summary>
        /// Handle rotation of the block
        /// </summary>
        private void HandleBlockManipulation()
        {
            
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button is not PointerEventData.InputButton.Left) return;
            if (_gameStateRequest != null)
            {
                var gameState = _gameStateRequest.Invoke(new GameStateRequest());
                if (gameState is GameState.GameOver or GameState.GameClear)
                {
                    OnEndDrag(eventData);
                    return;
                }
                if (gameState is not GameState.PlaceBlock) return;
            }
            if (IsPlaced && !AllowPickUpAfterPlacement) return;
            var position = transform.position;
            var mousePosition = PointerManager.Instance.MouseWorldPosition;
            _mousePositionDifference = new Vector3(mousePosition.x - position.x,
                mousePosition.y - position.y, 0);
            //ChangeSortingOrder(1);
            AudioManager.Instance.PlayAudioOneShot(BlockPreset.PickupSfx, transform.position);
            SetSortingLayer(pickUpSortingLayer);
            OnBlockBeingDrag?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button is not PointerEventData.InputButton.Left) return;
            if (_gameStateRequest != null)
            {
                var gameState = _gameStateRequest.Invoke(new GameStateRequest());
                if (gameState is GameState.GameOver or GameState.GameClear)
                {
                    OnEndDrag(eventData);
                    return;
                }
                if (gameState is not GameState.PlaceBlock) return;
            }
            if (IsPlaced && !AllowPickUpAfterPlacement) return;
            HandleBlockManipulation();
            GridManager.Instance.ValidatePlacement(this);
            var mousePosition = PointerManager.Instance.MouseWorldPosition;
            transform.position = mousePosition - _mousePositionDifference;
            if (_isDragging) return; //Prevent unnecessary calculations
            PickUpBlock();
            //GridManager.Instance.RemoveBlock(this);
            _isDragging = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button is not PointerEventData.InputButton.Left) return;
            if (_gameStateRequest != null)
            {
                var gameState = _gameStateRequest.Invoke(new GameStateRequest());
                if (gameState is GameState.CountOff or GameState.Pause) return;
            }
            if (!_isDragging) return;
            var placed = GridManager.Instance.PlaceBlock(this);
            if (placed)
            {
                IsPlaced = true;
                AudioManager.Instance.PlayAudioOneShot(placeSucceedSfx, transform.position);
                _mousePositionDifference = Vector3.zero;
            }
            else
            {
                AudioManager.Instance.PlayAudioOneShot(placeFailSfx, transform.position);
                ReturnToOriginal();
                IsPlaced = false;
            }
            _isDragging = false;
            OnBlockEndDrag?.Invoke(placed);
        }
        #endregion
        
        #region Utils
        public void ChangeType(BlockTypes type, bool updateGrid = true)
        {
            BlockType = type;
            if (!useAtomSprite)
            {
                if (!GetBlockView(out var blockViewPrefab))
                {
                    Debug.LogWarning($"BlockView for block type {BlockType} not found. Fall back to atom sprite.");
                    useAtomSprite = true;
                    Atoms.ForEach(atom => atom.SpriteRenderer.enabled = true);
                    ChangeType(type, updateGrid);
                    return;
                }
                if (BlockView)
                {
                    Destroy(BlockView.gameObject);
                }
                BlockView = Instantiate(blockViewPrefab, transform.position, Quaternion.identity, transform);
                BlockView.SetType(type);
            }
            else
            {
                if (!BlockManager.Instance.AtomColorDictionary.TryGetValue(type, out var color))
                {
                    Debug.LogError($"Color for block type {type} not found.");
                    return;
                }
                originalAtomColor = color;
                SetColor(color);
            }
            if (!updateGrid) return;
            GridManager.Instance.UpdateBlockOnGrid(this);
        }

        private bool GetBlockView(out BlockView blockView)
        {
            blockView = null;
            if (!BlockManager.Instance.BlockViewDictionary.TryGetValue(BlockFace, out blockView))
            {
                Debug.LogWarning($"BlockView for block type {BlockType} not found in the database.");
                return false;
            }
            if (!blockView)
            {
                Debug.LogWarning($"BlockView for block type {BlockType} and face {BlockFace} is null.");
                return false;
            }
            return true;
        }
        
        public void StartFlashing(FlashState flashState)
        {
            switch (flashState)
            {
                case FlashState.Flashing:
                    if(_flashTween.isAlive) return;
                    
                    if (_preInfectTween.isAlive)
                    { _preInfectTween.Complete(); }
                    SetColor(originalAtomColor);
                    _flashTween = Tween.Custom(originalAtomColor, Color.red, flashDuration, cycles: -1, cycleMode: CycleMode.Yoyo,
                        onValueChange: SetColor);
                    break;
                
                case FlashState.PreInfectFlash:
                    if (BlockState != BlockState.PreInfected) return;
                    if(_preInfectTween.isAlive) return;
                    SetColor(originalAtomColor);
                    _preInfectTween = Tween.Custom(originalAtomColor, _infectColor, flashDuration, cycles: -1, cycleMode: CycleMode.Yoyo,
                        onValueChange: SetColor);
                    break;
                
                case FlashState.None:
                    if (BlockState is BlockState.PreInfected)
                        StartFlashing(FlashState.PreInfectFlash);
                    else if (BlockState is BlockState.Infected or BlockState.Normal)
                        StopFlashing();
                    break;
            }
        }
        
        public void StopFlashing()
        {
            if (_flashTween.isAlive)
            {
                _flashTween.Complete();
                _flashTween = default;
                SetColor(originalAtomColor);
            }

            switch (BlockState)
            {
                case BlockState.Infected or BlockState.PreInfected:
                    StopPreInfectFlash();
                    break;
                
                case BlockState.Normal:
                    flashState = FlashState.None;
                    break;
            }
        }
        
        public void StopPreInfectFlash()
        {
            if (_preInfectTween.isAlive)
            {
                _preInfectTween.Complete();
                _preInfectTween = default;
            }
            
            switch (BlockState)
            {
                case BlockState.PreInfected:
                    StartFlashing(FlashState.PreInfectFlash);
                    break;
                
                case BlockState.Infected:
                    flashState = FlashState.None;
                    SetColor(_infectColor);
                    break;
            }
        }
        
        public void StopAllFlash()
        {
            if (_flashTween.isAlive)
            { _flashTween.Stop(); }
            
            if (_preInfectTween.isAlive)
            { _preInfectTween.Stop(); }
            
            SetColor(originalAtomColor);
        }
        
        public void PickUpBlock()
        {
            //Tween the block to (1, 1, 1) scale
            if (_transformTween.isAlive)
            {
                _transformTween.Stop();
            }
            var gridSize = GridManager.Instance.Grid.cellSize;
            Tween.Scale(transform, gridSize, 0.2f);
            if (BlockView) BlockView.PickUp();
        }

        /// <summary>
        /// Return the block to its original position, rotation and scale
        /// </summary>
        public void ReturnToOriginal()
        {
            if (_transformTween.isAlive)
            {
                _transformTween.Stop();
            }
            SetSortingLayer(originalSortingLayer);
            _transformTween = Tween.Position(transform, _originalPosition, 0.2f);
            Tween.Rotation(transform, _originalRotation, 0.2f);
            Tween.Scale(transform, _originalScale, 0.2f);
            if (BlockView) BlockView.Place();
            GridManager.Instance.ResetPreviousValidationCells();
        }
        
        public void ResetSortingLayer()
        {
            if (useAtomSprite)
            {
                Atoms.ForEach(atom => atom.SpriteRenderer.sortingLayerID = originalSortingLayer);
                return;
            }
            BlockView.SetSortingLayer(originalSortingLayer);
        }

        public void SetSortingLayer(int layer)
        {
            if (useAtomSprite)
            {
                Atoms.ForEach(atom => atom.SpriteRenderer.sortingLayerID = layer);
                return;
            }
            BlockView.SetSortingLayer(layer);
        }

        /// <summary>
        /// Set the sorting order of atoms
        /// </summary>
        /// <param name="order">Order to render</param>
        public void SetSortingOrder(int order)
        {
            if (useAtomSprite)
            {
                Atoms.ForEach(atom => atom.SpriteRenderer.sortingOrder = order);
                return;
            }
            BlockView.SetSortingOrder(order);
        }
        
        public void ChangeSortingOrder(int change)
        {
            if (useAtomSprite)
            {
                Atoms.ForEach(atom => atom.SpriteRenderer.sortingOrder += change);
                return;
            }
            BlockView.ChangeSortingOrder(change);
        }

        public void SetColor(Color color)
        {
            if (useAtomSprite)
            {
                Atoms.ForEach(atom => atom.SpriteRenderer.color = color);
                return;
            }
            BlockView.SetColor(color);
        }

        public async UniTask Explode(FitType fitType, bool destroy = false)
        {
            BlockState = BlockState.Exploding;
            StopPreInfectFlash();
            SetColor(originalAtomColor);
            if (BlockView)
            {
                await BlockView.Explode(fitType);
            }
            Debug.Log($"Block {BlockType} exploded at position {transform.position}");
            if (destroy) Destroy(gameObject);
        }
        #endregion
        
        #region Serialization
        public void OnBeforeSerialize()
        {
            UnitySerializationUtility.SerializeUnityObject(this, ref serializationData);
        }

        public void OnAfterDeserialize()
        {
            UnitySerializationUtility.DeserializeUnityObject(this, ref serializationData);
        }

        [SerializeField, HideInInspector]
        private SerializationData serializationData;
        public SerializationData SerializationData 
        { 
            get => serializationData;
            set => serializationData = value;
        }
        #endregion
    }
}