using FitMe.Grid;
using FitMe.Shared;

namespace FitMe.Scene
{
    public readonly struct LevelSessionRequest
    {
        public GameMode GameMode { get; }
        public GridPreset GridPreset { get; }

        public LevelSessionRequest(GameMode gameMode, GridPreset gridPreset)
        {
            GameMode = gameMode;
            GridPreset = gridPreset;
        }
    }
}
