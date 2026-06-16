namespace QuackUp.Notification
{
    public interface IEnergyProvider
    {
        int CurrentEnergy { get; }
        int MaxEnergy { get; }
        float SecondsPerEnergy { get; }
    }
}
