using System;
using System.Collections.Generic;
using MessagePack;
using QuackUp.Save;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Achievement
{
    [Serializable]
    [MessagePackObject]
    public class AchievementSaveData : IMessagePackSaveData
    {
        [Key("Version")]
        [field: SerializeField] public string Version { get; set; }
        
        [Key("Achievements")]
        [OdinSerialize] public Dictionary<string, AchievementData> Achievements { get; set; } = new();
    }
}