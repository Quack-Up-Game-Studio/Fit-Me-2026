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
        public ScoreViewModel(IScoreManager scoreManager)
        {
            ScoreText = scoreManager.Score
                .Select(score => score.ToString("N0")) 
                .ToReadOnlyReactiveProperty();
            FitText = scoreManager.FitMe
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
