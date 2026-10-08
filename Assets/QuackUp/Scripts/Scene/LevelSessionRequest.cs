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

        public LevelSessionRequest(GameMode gameMode, GridPreset gridPreset)
        {
            GameMode = gameMode;
            GridPreset = gridPreset;
            ShapeLevel = null;
        }

        public LevelSessionRequest(GameMode gameMode, GridPreset gridPreset, ShapeLevelDefinition shapeLevel)
        {
            GameMode = gameMode;
            ShapeLevel = gameMode is GameMode.LevelShape ? shapeLevel : null;
            GridPreset = ShapeLevel ? ShapeLevel.Board : gridPreset;
        }

        private LevelSessionRequest(ShapeLevelDefinition shapeLevel)
        {
            GameMode = GameMode.LevelShape;
            GridPreset = shapeLevel ? shapeLevel.Board : null;
            ShapeLevel = shapeLevel;
        }

        public static LevelSessionRequest ForShapeLevel(ShapeLevelDefinition shapeLevel) =>
            new(shapeLevel);

        public ShapeLevelRuntimeData CreateShapeRuntimeData() =>
            ShapeLevel ? ShapeLevel.ToRuntimeData() : null;
    }
}
