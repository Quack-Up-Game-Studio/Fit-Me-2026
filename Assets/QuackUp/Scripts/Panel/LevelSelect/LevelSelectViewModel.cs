using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Grid;
using QuackUp.SceneManagement;
using R3;
using UnityEngine.SceneManagement;

namespace FitMe.Panel
{
    public class LevelSelectViewModel : PanelViewModel
    {
        public int PlayerMaxLevel { get; }
        private readonly int _levelsPerChunk = 10;
        
        private readonly LevelDatabase _levelDatabase;
        private readonly LoadSceneManager _loadSceneManager;
        
        public Subject<GridPreset> LevelSelected = new Subject<GridPreset>();
        
        public LevelSelectViewModel(int playerMaxLevel,
            PanelManager panelManager,
            LevelDatabase levelDatabase, 
            LoadSceneManager loadSceneManager) : base(panelManager)
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
                LevelSelected?.OnNext(preset);
                await _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false);
            }
        }
    }
}