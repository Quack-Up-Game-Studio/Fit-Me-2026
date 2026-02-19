using R3;
using VContainer;

namespace FitMe.Panel
{
    public class PopUpScoreViewModel
    {
        public ReadOnlyReactiveProperty<string> PopUpScoreText { get; }

        [Inject]
        public PopUpScoreViewModel(int score)
        {
            PopUpScoreText = new ReactiveProperty<string>(score.ToString("N0"));
        }
        
        public void Dispose()
        {
            PopUpScoreText?.Dispose();
        }
    }
}