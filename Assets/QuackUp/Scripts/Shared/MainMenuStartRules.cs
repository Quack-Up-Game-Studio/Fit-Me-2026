namespace FitMe.Shared
{
    public enum MainMenuStartAction
    {
        WaitForSave,
        OutOfEnergy,
        ModeSelect,
    }

    public static class MainMenuStartRules
    {
        public static MainMenuStartAction Resolve(bool saveReady, bool hasEnoughEnergy)
        {
            if (!saveReady) return MainMenuStartAction.WaitForSave;
            return hasEnoughEnergy ? MainMenuStartAction.ModeSelect : MainMenuStartAction.OutOfEnergy;
        }
    }
}
