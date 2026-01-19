using System;
using QuackUp.Utils;
using R3;

namespace FitMe.Grid
{
    public class AtomModel : IDisposable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ReactiveProperty<BlockModel> ParentBlockModel { get; set; }
        public TransformData TransformData { get; set; } = new();
        public ReactiveCommand<SpriteOutlineSettings> SetOutlineCommand { get; } = new();

        public void Dispose()
        {
            TransformData?.Dispose();
        }
    }
}