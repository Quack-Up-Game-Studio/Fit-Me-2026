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

        public LevelSessionRequest(GameMode gameMode, GridPreset gridPreset)
        {
            GameMode = gameMode;
            GridPreset = gridPreset;
            ShapeLevel = null;
            ShapeLevelId = null;
        }

        public LevelSessionRequest(GameMode gameMode, GridPreset gridPreset, ShapeLevelDefinition shapeLevel)
        {
            GameMode = gameMode;
            ShapeLevel = gameMode is GameMode.LevelShape ? shapeLevel : null;
            GridPreset = ShapeLevel ? ShapeLevel.Board : gridPreset;
            ShapeLevelId = null;
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
        }

        private LevelSessionRequest(string shapeLevelId, ShapeLevelDefinition shapeLevel)
        {
            GameMode = GameMode.LevelShape;
            GridPreset = shapeLevel ? shapeLevel.Board : null;
            ShapeLevel = shapeLevel;
            ShapeLevelId = shapeLevel ? shapeLevelId : null;
        }

        public static LevelSessionRequest ForShapeLevel(ShapeLevelDefinition shapeLevel) =>
            new(null, shapeLevel);

        public static LevelSessionRequest ForShapeLevel(string shapeLevelId, ShapeLevelDefinition shapeLevel) =>
            new(shapeLevelId, shapeLevel);

        public ShapeLevelRuntimeData CreateShapeRuntimeData() =>
            ShapeLevel ? ShapeLevel.ToRuntimeData() : null;
    }
}
