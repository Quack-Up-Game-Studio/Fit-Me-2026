using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Grid
{
    public class AtomViewModel
    {
        public ReadOnlyReactiveProperty<BlockModel> ParentBlockModel => _model.ParentBlockModel;
        public TransformData TransformData => _model.TransformData;
        
        private readonly AtomModel _model;
        
        [Inject]
        public AtomViewModel(AtomModel model)
        {
            _model = model;
        }
    }
}