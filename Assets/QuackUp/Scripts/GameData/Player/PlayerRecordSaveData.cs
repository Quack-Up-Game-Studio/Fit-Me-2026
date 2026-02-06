using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using QuackUp.Save;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.GameData
{
    [Serializable]
    [MessagePackObject(AllowPrivate = true)]
    public partial class PlayerRecordSaveData : IMessagePackSaveData
    {
        [Key("Version")]
        [field: SerializeField] public string Version { get; set; }
        
        [Serializable]
        [MessagePackObject(AllowPrivate = true)]
        public partial record RunData
        {
            [Key("DateTime")]
            [field: OdinSerialize] public DateTime dateTime;
            [Key("Score")]
            [field: SerializeField] public float score;
            [Key("FitMe")]
            [field: SerializeField] public int fitMe;
            [IgnoreMember]
            [ShowInInspector, DisplayAsString] private string DebugDateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss");
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