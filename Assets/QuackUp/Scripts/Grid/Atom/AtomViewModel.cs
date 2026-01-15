using QuackUp.Utils;
using VContainer;

namespace FitMe.Grid
{
    public class AtomViewModel
    {
        private readonly AtomModel _model;
        
        public TransformData TransformData => _model.TransformData;
        
        [Inject]
        public AtomViewModel(AtomModel model)
        {
            _model = model;
        }
    }
}