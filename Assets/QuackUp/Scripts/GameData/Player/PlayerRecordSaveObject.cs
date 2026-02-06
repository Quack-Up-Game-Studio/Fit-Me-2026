using System;
using System.Collections.Generic;
using System.Dynamic;
using MessagePack;
using MessagePack.Resolvers;
using QuackUp.Save;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "PlayerRecordSaveObject", menuName = "FitMe/GameData/PlayerRecordSaveObject", order = 0)]
    public class PlayerRecordSaveObject : MessagePackSaveObject<PlayerRecordSaveData>
    {
        [field: SerializeField] public int MaxRunDataCount { get; private set; } = 3;

        public override MessagePackSerializerOptions DefaultSerializerOptions =>
            ContractlessStandardResolverAllowPrivate.Options;

        public override void OnInitialize()
        {
            base.OnInitialize();
            saveData.Initialize(MaxRunDataCount);
        }
        
        public override void Reset()
        {
            base.Reset();
            saveData = new PlayerRecordSaveData();
        }
    }
}