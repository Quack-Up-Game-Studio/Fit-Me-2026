namespace FitMe.Shared
{
    public enum MainMenuStartAction
    {
        WaitForSave,
        Tutorial,
        OutOfEnergy,
        ModeSelect,
    }

    public static class MainMenuStartRules
    {
        public static MainMenuStartAction Resolve(bool saveReady, bool completedTutorial, bool hasEnoughEnergy)
        {
            if (!saveReady) return MainMenuStartAction.WaitForSave;
            if (!completedTutorial) return MainMenuStartAction.Tutorial;
            return hasEnoughEnergy ? MainMenuStartAction.ModeSelect : MainMenuStartAction.OutOfEnergy;
        }
    }
}
