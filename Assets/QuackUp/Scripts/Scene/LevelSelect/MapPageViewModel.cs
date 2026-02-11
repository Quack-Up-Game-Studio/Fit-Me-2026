using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using QuackUp.SceneManagement;
using QuackUp.Utils; 
using R3;
using UnityEngine.SceneManagement;

namespace FitMe.Scene
{
    public class MapPageViewModel
    {
        public int PlayerMaxLevel { get; }
        private readonly int _levelsPerChunk = 10;
        
        private readonly LevelDatabase _levelDatabase;
        private readonly LoadSceneManager _loadSceneManager;
        
        public MapPageViewModel(int playerMaxLevel, LevelDatabase levelDatabase, LoadSceneManager loadSceneManager)
        {
            PlayerMaxLevel = playerMaxLevel;
            _levelDatabase = levelDatabase;
            _loadSceneManager = loadSceneManager;
        }

        public List<int> GetChunksToLoad()
        {
            int currentChunk = (PlayerMaxLevel - 1) / _levelsPerChunk;
            var chunks = new List<int>();
            if (currentChunk > 0) chunks.Add(currentChunk - 1);
            chunks.Add(currentChunk);
            chunks.Add(currentChunk + 1);
            return chunks;
        }

        public LevelButtonViewModel CreateButtonViewModel(int levelId)
        {
            return new LevelButtonViewModel(levelId, PlayerMaxLevel, this);
        }

        public async UniTask OnLevelSelected(int levelId)
        {
            var preset = _levelDatabase.GetPreset(levelId);

            if (preset != null)
            {
                LevelManager.GridPreset = preset;
                await _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false);
            }
        }
    }
}