using Cysharp.Threading.Tasks;
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
        public readonly bool IsOver;

        public GameOverEvent(bool isOver = false)
        {
            IsOver = isOver;
        }
    }
    
    public interface ILevelManager
    {
        int CurrentObstacleCount { get; }
        UniTask NextTutorialPreset(bool playSound);
    }
    
    public interface IScoreManager
    {
        ReactiveProperty<int> Score { get; }
        ReactiveProperty<int> FitMe { get; }
        void ChangeScore(int amount);
        void ChangeFitMe(int amount);
    }

    public interface IGameStateManager
    {
        ReadOnlyReactiveProperty<GameState> GameState { get; }
        void SetGameState(GameState newState);
        void Pause();
        void Unpause();
    }
    
    public class LevelManagerMock : ILevelManager, IGameStateManager, IScoreManager
    {
        public ReadOnlyReactiveProperty<GameState> GameState => _currentGameState.ToReadOnlyReactiveProperty();
        public ReactiveProperty<int> Score { get; } = new(0);
        public ReactiveProperty<int> FitMe { get; } = new(0);
        public void ChangeScore(int amount){}
        public void ChangeFitMe(int amount){}

        public int CurrentObstacleCount => 0;
        public UniTask NextTutorialPreset(bool playSound) => UniTask.CompletedTask;

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
