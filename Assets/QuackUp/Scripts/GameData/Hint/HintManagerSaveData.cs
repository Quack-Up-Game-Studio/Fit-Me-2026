using System;
using MessagePack;
using QuackUp.Save;
using UnityEngine;

namespace FitMe.GameData
{
    [MessagePackObject]
    [Serializable]
    public class HintManagerSaveData : IMessagePackSaveData
    {
        [Key("Version")]
        [field: SerializeField] public string Version { get; set; } = string.Empty;

        [Key("CurrentHints")]
        [field: SerializeField] public int CurrentHints { get; set; }

        [Key("LastDailyHintCheckDate")]
        [field: SerializeField] public string LastDailyHintCheckDate { get; set; } = string.Empty;
    }
}
