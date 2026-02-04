using System;
using System.Collections.Generic;
using FitMe.Shared;
using MessagePack;
using QuackUp.Save;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.GameData
{
    [Serializable]
    [MessagePackObject]
    public class GameRecordSaveData : IMessagePackSaveData
    {
        [Key("Version")]
        [field: SerializeField] public string Version { get; set; }
        [Key("CumulativePreInfectBlockDestroyed")]
        [field: SerializeField] public int cumulativePreInfectBlockDestroyed;
        [Key("CumulativeBlockDestroyed")]
        [field: SerializeField] public int cumulativeBlockDestroyed;
        [Key("CumulativeColorBlastDictionary")]
        [OdinSerialize] public Dictionary<BlockColor, int> cumulativeColorBlastDictionary = new();
    }
}