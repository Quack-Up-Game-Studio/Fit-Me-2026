using System;
using QuackUp.Utils;

namespace FitMe.Grid
{
    public class AtomModel : IDisposable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public BlockModel ParentBlockModel { get; set; }
        public TransformData TransformData { get; set; } = new();

        public void Dispose()
        {
            TransformData?.Dispose();
        }
    }
}