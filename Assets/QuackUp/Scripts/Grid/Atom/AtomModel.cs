using System;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Grid
{
    public class AtomModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ReactiveProperty<BlockModel> ParentBlockModel { get; set; } = new();
        public IAtomView AtomView { get; private set; }
        
        [Inject]
        public AtomModel(IAtomView atomView)
        {
            AtomView = atomView;
        }
    }
}