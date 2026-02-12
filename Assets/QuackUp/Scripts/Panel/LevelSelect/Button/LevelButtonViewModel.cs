using R3;

namespace FitMe.Panel
{
    public class LevelButtonViewModel
    {
        public int LevelID { get; }
        public ReadOnlyReactiveProperty<bool> IsLocked { get; }
        public ReadOnlyReactiveProperty<bool> IsCurrent { get; }
        
        public ReactiveCommand OnClickCommand { get; } = new();

        private readonly LevelSelectViewModel _parentMapVM;

        public LevelButtonViewModel(int levelId, int playerMaxLevel, LevelSelectViewModel parentVM)
        {
            LevelID = levelId;
            _parentMapVM = parentVM;

            IsLocked = Observable.Return(levelId > playerMaxLevel).ToReadOnlyReactiveProperty();
            IsCurrent = Observable.Return(levelId == playerMaxLevel).ToReadOnlyReactiveProperty();

            OnClickCommand.Subscribe(_ => 
            {
                if (levelId > playerMaxLevel) return;

                _parentMapVM.OnLevelSelected(LevelID);
            });
        }
    }
}