using System;
using MessagePack;
using QuackUp.Save;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.GameData
{
    [MessagePackObject]
    [Serializable]
    public class EnergyManagerSaveData : IMessagePackSaveData
    {
        [Key("Version")] 
        [field: SerializeField] public string Version { get; set; } = string.Empty;
        
        [Key("CurrentEnergy")]
        [field: SerializeField] public int CurrentEnergy { get; set; } = 0;
        
        [Key("LastUpdateTime")]
        public DateTime LastEnergyUpdateTime { get; set; } = DateTime.MinValue;
        
        [IgnoreMember]
        [ShowInInspector] private string DebugLastEnergyUpdateTime => LastEnergyUpdateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }
}