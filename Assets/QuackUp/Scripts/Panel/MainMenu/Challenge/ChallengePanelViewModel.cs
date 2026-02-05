using System.Collections.Generic;
using FitMe.Achievement;
using FitMe.GameData;
using QuackUp.Save;
using VContainer;

namespace FitMe.Panel
{
    public class ChallengePanelViewModel : PanelViewModel
    {
        public IReadOnlyDictionary<string, AchievementInstance> Achievements { get; private set; }
        public PlayerRecordSaveData PlayerRecordData { get; private set; }
        
        [Inject]
        public ChallengePanelViewModel(
            AchievementManager achievementManager,
            MessagePackSaveManager saveManager,
            PanelManager panelManager) : base(panelManager)
        {
            Achievements = achievementManager.Achievements;
            PlayerRecordData = saveManager.GetFirstSaveObjectOfType<PlayerRecordSaveObject>().GetSaveData<PlayerRecordSaveData>();
        }
    }
}