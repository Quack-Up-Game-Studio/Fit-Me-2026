using System;
using QuackUp.Utils;
using R3;
using VContainer;

namespace FitMe.Grid
{
    public class AtomModel : IDisposable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ReactiveProperty<BlockModel> ParentBlockModel { get; set; } = new();
        public TransformData TransformData { get; set; } = new();
        public ReactiveCommand<SpriteOutlineSettings> SetOutlineCommand { get; } = new();
        public IAtomView AtomView { get; set; }

        public void Dispose()
        {
            TransformData?.Dispose();
        }
    }
}