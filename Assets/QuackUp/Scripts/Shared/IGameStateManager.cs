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
    
    public class GameStateManagerMock : IGameStateManager
    {
        public ReadOnlyReactiveProperty<GameState> GameState => _currentGameState.ToReadOnlyReactiveProperty();
        private readonly ReactiveProperty<GameState> _currentGameState = new(Shared.GameState.PlaceBlock);
        public GameStateManagerMock(GameState initialState)
        {
            _currentGameState.Value = initialState;
        }
        public void SetGameState(GameState newState){}
        public void Pause(){}
        public void Unpause(){}
    }
}
