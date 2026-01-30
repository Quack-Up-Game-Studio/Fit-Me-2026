using R3;
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
    
    public interface IGameStateManager
    {
        ReadOnlyReactiveProperty<GameState> GameState { get; }
        void SetGameState(GameState newState);
        void Pause();
        void Unpause();
    }
}
