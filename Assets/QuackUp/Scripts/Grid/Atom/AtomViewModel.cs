using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Grid
{
    public class AtomViewModel
    {
        public ReadOnlyReactiveProperty<BlockModel> ParentBlockModel => _model.ParentBlockModel;
        
        private readonly AtomModel _model;
        
        [Inject]
        public AtomViewModel(AtomModel model)
        {
            _model = model;
        }
    }
}