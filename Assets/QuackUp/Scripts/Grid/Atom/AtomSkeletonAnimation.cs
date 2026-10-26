using Spine.Unity;

namespace FitMe.Grid
{
    public sealed class AtomSkeletonAnimation : SkeletonAnimation
    {
        public bool PhysicsSimulationEnabled { get; private set; } = true;

        private bool _resetPhysicsOnNextUpdate;

        public void SetPhysicsSimulationEnabled(bool enabled)
        {
            if (PhysicsSimulationEnabled == enabled) return;
            PhysicsSimulationEnabled = enabled;
            if (enabled) _resetPhysicsOnNextUpdate = true;
        }

        protected override void UpdateWorldTransform(Spine.Skeleton.Physics physics)
        {
            if (!PhysicsSimulationEnabled)
            {
                base.UpdateWorldTransform(Spine.Skeleton.Physics.None);
                return;
            }

            if (_resetPhysicsOnNextUpdate)
            {
                _resetPhysicsOnNextUpdate = false;
                base.UpdateWorldTransform(Spine.Skeleton.Physics.Reset);
                return;
            }

            base.UpdateWorldTransform(physics);
        }
    }
}