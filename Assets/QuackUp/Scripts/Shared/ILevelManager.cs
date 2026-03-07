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
        ClearingGrid,
        Pause,
    }
    
    public struct GameOverEvent
    {
        public bool IsOver;

        public GameOverEvent(bool isOver = false)
        {
            IsOver = isOver;
        }
    }
    
    public interface ILevelManager
    {
        ReadOnlyReactiveProperty<GameState> GameState { get; }
        ReactiveProperty<int> Score { get; }
        ReactiveProperty<int> FitMe { get; }
        int CurrentObstacleCount { get; }
        void SetGameState(GameState newState);
        void Pause();
        void Unpause();
    }
    
    public class LevelManagerMock : ILevelManager
    {
        public ReadOnlyReactiveProperty<GameState> GameState => _currentGameState.ToReadOnlyReactiveProperty();
        public ReactiveProperty<int> Score { get; } = new(0);
        public ReactiveProperty<int> FitMe { get; } = new(0);
        public int CurrentObstacleCount => 0;
        private readonly ReactiveProperty<GameState> _currentGameState = new(Shared.GameState.PlaceBlock);
        public LevelManagerMock(GameState initialState)
        {
            _currentGameState.Value = initialState;
        }
        public void SetGameState(GameState newState){}
        public void Pause(){}
        public void Unpause(){}
    }
}
