using FitMe.Shared;
using R3;
using VContainer;

namespace FitMe.Panel
{
    public class ScoreViewModel
    {
        public ReadOnlyReactiveProperty<string> ScoreText { get; }
        public ReadOnlyReactiveProperty<string> FitText { get; }

        [Inject]
        public ScoreViewModel(ILevelManager levelManager)
        {
            ScoreText = levelManager.Score
                .Select(score => score.ToString("N0")) 
                .ToReadOnlyReactiveProperty();
            FitText = levelManager.FitMe
                .Select(fit => fit.ToString("N0")) 
                .ToReadOnlyReactiveProperty();
        }
        
        public void Dispose()
        {
            ScoreText?.Dispose();
            FitText?.Dispose();
        }
    }
}
