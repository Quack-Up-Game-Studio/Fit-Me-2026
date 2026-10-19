using VContainer;
using VContainer.Unity;

namespace FitMe.Scene
{
    public sealed class ShapeLevelSelectSceneInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<ShapeLevelSelectionService>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<ShapeLevelSelectManager>().AsSelf();
            builder.RegisterBuildCallback(x => x.Resolve<ShapeLevelSelectManager>());
        }
    }
}
