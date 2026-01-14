using UnityEngine;

namespace FitMe.Shared
{
    public enum GameState
    {
        CountOff,
        PlaceBlock,
        GameClear,
        GameOver,
        Pause,
    }
    public static class GameStatic
    {
        public static GameState CurrentGameState = GameState.CountOff;
    }
}
