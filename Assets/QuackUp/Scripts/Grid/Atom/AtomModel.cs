using System;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Grid
{
    public class AtomModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ReactiveProperty<BlockInstance> ParentBlock { get; set; } = new();
        
        [Inject]
        public AtomModel()
        {
        }
    }
}