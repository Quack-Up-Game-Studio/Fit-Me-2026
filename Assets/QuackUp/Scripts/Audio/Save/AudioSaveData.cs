using System;
using System.Collections.Generic;
using QuackUp.Save;
using MessagePack;
using MessagePack.Formatters;
using Sirenix.Serialization;
using UnityEngine;

namespace QuackUp.Audio
{
    [Serializable]
    [MessagePackObject]
    public class AudioSaveData : IMessagePackSaveData
    {
        [Key("Version")]
        [field: SerializeField] public string Version { get; set; } = string.Empty;
        
        [Key("BusData")]
        [field: OdinSerialize] public Dictionary<BusType, BusSaveData> BusSaveData { get; set; } = new();
    }
    
    [Serializable]
    [MessagePackObject]
    public partial class BusSaveData
    {
        [Key("LinearVolume")]
        [field: SerializeField] public float LinearVolume { get; set; }
        
        [Key("IsMuted")]
        [field: SerializeField] public bool IsMuted { get; set; }
    }
}


