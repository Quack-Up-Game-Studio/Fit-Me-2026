using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MessagePack;
using QuackUp.Save;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using DateTimeFormatter = MessagePack.Formatters.DateTimeFormatter;

namespace FitMe.GameData
{
    [Serializable]
    [MessagePackObject(AllowPrivate = true)]
    public partial class PlayerRecordSaveData : IMessagePackSaveData
    {
        [Key("Version")]
        [field: SerializeField] public string Version { get; set; } = string.Empty;

        [Key("PlayerID")]
        [field: SerializeField] public string PlayerID { get; set; } = string.Empty;
        
        [Key("IsFirstTimePlayer")]
        [field: SerializeField] public bool IsFirstTimePlayer { get; set; } = true;
        
        [Key("CompletedTutorial")]
        [field: SerializeField] public bool CompletedTutorial { get; set; }
        
        [Key("TotalPlayTime")]
        [field: OdinSerialize] public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;
        
        [IgnoreMember]
        [ShowInInspector, DisplayAsString] private string DebugTotalPlayTime => TotalPlayTime.ToString(@"hh\:mm\:ss");
        
        [Serializable]
        [MessagePackObject(AllowPrivate = true)]
        public partial record RunData
        {
            [Key("DateTime")]
            [MessagePackFormatter(typeof(DateTimeFormatter))]
            [field: OdinSerialize] public DateTime dateTime;
            [Key("Score")]
            [field: SerializeField] public float score;
            [Key("FitMe")]
            [field: SerializeField] public int fitMe;
            [IgnoreMember]
            [ShowInInspector, DisplayAsString] private string DebugDateTime => dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }
        
        [Key("HighScore")]
        [field: SerializeField] public RunData highScore = new();
        [Key("MostFitMe")]
        [field: SerializeField] public RunData mostFitMe = new();
        [Key("RunDataList")]
        [field: SerializeField] private List<RunData> runDataList = new();
        /// <remarks>
        /// Use <see cref="AddRunData"/> to add new run data while maintaining the maximum count.
        /// </remarks>
        [IgnoreMember]
        public IReadOnlyList<RunData> RunDataList => runDataList;
        [Key("CumulativeScore")]
        [field: SerializeField] public float cumulativeScore;
        [Key("CumulativeFitMe")]
        [field: SerializeField] public int cumulativeFitMe;
        
        [IgnoreMember][NonSerialized]
        private int _maxRunDataCount = 3;

        internal void Initialize(int maxRunDataCount)
        {
            _maxRunDataCount = maxRunDataCount;
        }

        public void AddRunData(RunData runData)
        {
            runDataList.Add(runData);
            if (runData.score > highScore.score)
            {
                highScore = runData;
            }
            if (runData.fitMe > mostFitMe.fitMe)
            {
                mostFitMe = runData;
            }
            Debug.Log($"run data list count: {runDataList.Count}, max count: {_maxRunDataCount}");
            if (runDataList.Count > _maxRunDataCount)
            {
                //sort by date time descending and take the latest _maxRunDataCount entries
                runDataList = runDataList.OrderByDescending(x => x.dateTime)
                    .Take(_maxRunDataCount)
                    .ToList();
            }
        }
    }
}