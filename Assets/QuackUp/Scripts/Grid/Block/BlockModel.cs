using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
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
        Exploding,
        Obstacle
    }

    public enum BlockInteractionState
    {
        PlacedOnSpawn,
        PickUp,
        PlacedOnGrid
    }
    #endregion

    [Serializable]
    public class BlockModel
    {
        private readonly BlockConfig _config;
        private readonly AtomFactory _atomFactory;
        private readonly List<BlockSchema> _blockSchemas = new();
        
        [Inject]
        public BlockModel(
            BlockConfig config,
            AtomFactory atomFactory)
        {
            _config = config;
            _atomFactory = atomFactory;
        }
        
        #region Inspectors
        [field: Title("Block Debug")]
        public Guid Id { get; set; } = Guid.NewGuid();
        /// <remarks>
        /// Use <see cref="ChangeColor"/> to change the block type.
        /// </remarks>
        public ReadOnlyReactiveProperty<BlockColor> BlockColor => _blockColor.ToReadOnlyReactiveProperty();
        public BlockConfig Config => _config;
        public BlockShape BlockShape { get; private set; }
        public List<AtomInstance> Atoms { get; private set; } = new(); 
        public BlockPreset BlockPreset { get; private set; }
        public IReadOnlyList<BlockSchema> BlockSchemas
        {
            get
            {
                if (_blockSchemas.Count > 0) return _blockSchemas;
                return BlockPreset ? BlockPreset.BlockSchemas : Array.Empty<BlockSchema>();
            }
        }
        public ReactiveProperty<BlockState> BlockState { get; set; } = new(Grid.BlockState.Normal);
        public List<CellInstance> BlockCells { get; set; }
        public int SpawnIndex { get; set; }
        public int RotationalIndex { get; set; }
        
        public ReactiveCommand UpdateGridCommand { get; } = new();
        
        private ReactiveProperty<BlockColor> _blockColor = new(Shared.BlockColor.Red);
        private int _originalSortingOrder;
        #endregion

        #region Schema
        public void GenerateAtom(BlockShape blockShape, BlockPreset preset, BlockInstance instance, int[,] schemaOverride = null)
        {
            var schema = schemaOverride ?? preset.BlockSchema.schema;
            var row = schema.GetLength(0);
            var column = schema.GetLength(1);
            BlockShape = blockShape;
            BlockPreset = preset;
            if (schemaOverride == null)
            {
                preset.GenerateSchema();
                _blockSchemas.Clear();
                _blockSchemas.AddRange(preset.BlockSchemas);
            }
            else
            {
                SetShapeSchema(schemaOverride);
            }

            for (var x = 0; x < row; x++)
            {
                for (var y = 0; y < column; y++)
                {
                    if (schema[x, y] == 0)
                    {
                        continue;
                    }
                    var spawnPosX = -column / 2f + 0.5f + y; //0
                    var spawnPosY = row / 2f - 0.5f - x;
                    var spawnPosition = new Vector3(spawnPosX, spawnPosY, 0);
                    //DebugUtils.Log("Spawning atom at: " + spawnPosition);
                    var atom = _atomFactory.Create(spawnPosition, Quaternion.identity, new InstantiateParameters
                    {
                        worldSpace = false,
                        parent = instance.GameObject.transform
                    });
                    atom.Model.ArrayIndex.Value = new Vector2Int(x, y);
                    //atom.AtomView?.SetParent(BlockView);
                    atom.Model.ParentBlock.Value = instance;
                    var hasTop = HasElement(x - 1, y);
                    var hasBottom = HasElement(x + 1, y);
                    var hasLeft = HasElement(x, y - 1);
                    var hasRight = HasElement(x, y + 1);
                    var settings = new SpriteOutlineSettings
                    {
                        OutlineTop = !hasTop,
                        OutlineBottom = !hasBottom,
                        OutlineLeft = !hasLeft,
                        OutlineRight = !hasRight
                    };
                    atom.ViewModel.SetOutlineCommand.Execute(settings);
                    Atoms.Add(atom);
                }
            }
            // var spritePositionX = -column / 2f + 0.5f;
            // var spritePositionY = row / 2f - 0.5f;
            // spriteRenderer.transform.localPosition = new Vector3(spritePositionX, spritePositionY, 0);
            // if (_config.UseAtomSprite && BlockView)
            // {
            //     BlockView.gameObject.SetActive(false);
            // }
            // else
            // {
            //     Atoms.ForEach(a => a.SpriteRenderer.enabled = false);
            // }
            return;

            bool HasElement(int x, int y)
            {
                if (x < 0 || x >= row || y < 0 || y >= column)
                    return false;
                return schema[x, y] == 1;
            }
        }

        public void SetShapeSchema(int[,] schema)
        {
            if (schema == null || schema.GetLength(0) == 0 || schema.GetLength(1) == 0)
                throw new ArgumentException("Block schema must have non-zero dimensions.", nameof(schema));

            _blockSchemas.Clear();
            _blockSchemas.Add(new BlockSchema(schema, 0));
            _blockSchemas.Add(new BlockSchema(ArrayHelper.Rotate270(schema), 1));
            _blockSchemas.Add(new BlockSchema(ArrayHelper.Rotate180(schema), 2));
            _blockSchemas.Add(new BlockSchema(ArrayHelper.Rotate90(schema), 3));
        }
        #endregion
        
        #region Utils
        public void ChangeColor(BlockColor color, bool updateGrid = true)
        {
            _blockColor.OnNext(color); //Force push the new value
            if (!updateGrid) return;
            UpdateGridCommand.Execute(Unit.Default);
        }
        #endregion
    }
}