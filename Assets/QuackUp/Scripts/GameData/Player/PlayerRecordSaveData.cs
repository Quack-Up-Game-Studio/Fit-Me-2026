using System;
using System.Collections.Generic;
using MessagePack;
using QuackUp.Save;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.GameData
{
    [Serializable]
    [MessagePackObject]
    public class PlayerRecordSaveData : IMessagePackSaveData
    {
        [Key("Version")]
        [field: SerializeField] public string Version { get; set; }
        
        [Serializable]
        [MessagePackObject]
        public record RunData
        {
            [Key("DateTime")]
            [field: OdinSerialize] public DateTime dateTime;
            [Key("Score")]
            [field: SerializeField] public float score;
            [Key("FitMe")]
            [field: SerializeField] public int fitMe;
            [ShowInInspector, DisplayAsString] private string DebugDateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        
        [Key("HighScore")]
        [field: SerializeField] public RunData highScore = new();
        [Key("MostFitMe")]
        [field: SerializeField] public RunData mostFitMe = new();
        [Key("RunDataList")]
        [field: SerializeField] public List<RunData> runData = new();
        [Key("CumulativeScore")]
        [field: SerializeField] public float cumulativeScore;
        [Key("CumulativeFitMe")]
        [field: SerializeField] public int cumulativeFitMe;
    }
}