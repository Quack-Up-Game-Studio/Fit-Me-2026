using VContainer;

namespace FitMe.Grid
{
    public class AtomViewModel
    {
        private readonly AtomModel _model;
        
        [Inject]
        public AtomViewModel(AtomModel model)
        {
            _model = model;
        }
    }
}