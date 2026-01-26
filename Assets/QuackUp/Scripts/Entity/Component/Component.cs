namespace FitMe.Entity
{
    public interface IComponent
    {
        void SetActive(bool isActive);
    }
    public abstract class Component : IComponent
    {
        protected ComponentPreset Preset { get; private set; }
        private Entity _parentEntity;
        private bool _isActive = true;
        
        protected Component(ComponentPreset preset)
        {
            Preset = preset;
        }
        
        public virtual void SetActive(bool isActive)
        {
            _isActive = isActive;
        }
    }
}