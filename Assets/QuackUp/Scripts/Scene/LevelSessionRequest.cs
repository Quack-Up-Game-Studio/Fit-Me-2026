using FitMe.GameData;
using FitMe.Grid;
using FitMe.Shared;

namespace FitMe.Scene
{
    public readonly struct LevelSessionRequest
    {
        public GameMode GameMode { get; }
        public GridPreset GridPreset { get; }
        public ShapeLevelDefinition ShapeLevel { get; }
        public string ShapeLevelId { get; }
        public string ShapeWorldKey { get; }

        public LevelSessionRequest(GameMode gameMode, GridPreset gridPreset)
        {
            GameMode = gameMode;
            GridPreset = gridPreset;
            ShapeLevel = null;
            ShapeLevelId = null;
            ShapeWorldKey = null;
        }

        public LevelSessionRequest(GameMode gameMode, GridPreset gridPreset, ShapeLevelDefinition shapeLevel)
        {
            GameMode = gameMode;
            ShapeLevel = gameMode is GameMode.LevelShape ? shapeLevel : null;
            GridPreset = ShapeLevel ? ShapeLevel.Board : gridPreset;
            ShapeLevelId = null;
            ShapeWorldKey = null;
        }

        public LevelSessionRequest(
            GameMode gameMode,
            GridPreset gridPreset,
            ShapeLevelDefinition shapeLevel,
            string shapeLevelId)
        {
            GameMode = gameMode;
            ShapeLevel = gameMode is GameMode.LevelShape ? shapeLevel : null;
            GridPreset = ShapeLevel ? ShapeLevel.Board : gridPreset;
            ShapeLevelId = ShapeLevel ? shapeLevelId : null;
            ShapeWorldKey = null;
        }

        private LevelSessionRequest(string shapeLevelId, ShapeLevelDefinition shapeLevel, string shapeWorldKey)
        {
            GameMode = GameMode.LevelShape;
            GridPreset = shapeLevel ? shapeLevel.Board : null;
            ShapeLevel = shapeLevel;
            ShapeLevelId = shapeLevel ? shapeLevelId : null;
            ShapeWorldKey = shapeLevel ? shapeWorldKey : null;
        }

        public static LevelSessionRequest ForShapeLevel(ShapeLevelDefinition shapeLevel) =>
            new(null, shapeLevel, null);

        public static LevelSessionRequest ForShapeLevel(string shapeLevelId, ShapeLevelDefinition shapeLevel) =>
            new(shapeLevelId, shapeLevel, null);

        public static LevelSessionRequest ForShapeLevel(string worldKey, string shapeLevelId, ShapeLevelDefinition shapeLevel) =>
            new(shapeLevelId, shapeLevel, worldKey);

        public ShapeLevelRuntimeData CreateShapeRuntimeData() =>
            ShapeLevel ? ShapeLevel.ToRuntimeData() : null;
    }
}
