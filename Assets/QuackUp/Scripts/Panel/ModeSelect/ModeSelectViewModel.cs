using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Grid;
using QuackUp.SceneManagement;
using R3;
using UnityEngine.SceneManagement;
using VContainer;

namespace FitMe.Panel
{
    public class ModeSelectViewModel : PanelViewModel
    {
        private readonly LoadSceneManager _loadSceneManager;
        
        public readonly Subject<int> ModeSelected = new Subject<int>();

        [Inject]
        public ModeSelectViewModel(PanelManager panelManager,
            LoadSceneManager loadSceneManager) : base(panelManager)
        {
            _loadSceneManager = loadSceneManager;
        }

        public async UniTask OnGameMode(int mode)
        {
            ModeSelected?.OnNext(mode);
            await _loadSceneManager.LoadScene(SceneType.Gameplay, LoadSceneMode.Single, false);
        }
    }
}