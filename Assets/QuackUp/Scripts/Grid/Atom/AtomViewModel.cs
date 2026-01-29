using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Grid
{
    public class AtomViewModel
    {
        public ReadOnlyReactiveProperty<BlockInstance> ParentBlock => _model.ParentBlock;
        public ReactiveCommand<SpriteOutlineSettings> SetOutlineCommand { get; } = new();
        
        private readonly AtomModel _model;
        
        [Inject]
        public AtomViewModel(AtomModel model)
        {
            _model = model;
        }
    }
}