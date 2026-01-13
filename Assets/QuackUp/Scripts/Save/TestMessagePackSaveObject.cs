using System;
using System.Collections.Generic;
using System.Dynamic;
using MessagePack;
using Sirenix.OdinInspector;
using UnityEngine;

namespace QuackUp.Save
{
    [MessagePackObject]
    [Serializable]
    public class TestMessagePackSaveData : IMessagePackSaveData
    {
        [Key("Version")]
        [field: SerializeField] public string Version { get; set; } = string.Empty;
        
        [Key("TestInt")]
        public int testInt;
    }
    
    [Serializable]
    public class TestMessagePackMigrationResolver : ISaveMigrationResolver<TestMessagePackSaveData>
    {
        [field: HideReferenceObjectPicker, 
                SerializeField] public string SourceVersion { get; private set; }
        [field: HideReferenceObjectPicker, 
                SerializeField] public string TargetVersion { get; private set; }

        [SerializeField] private int additionalTestInt;

        public ExpandoObject Migrate(ExpandoObject expando)
        {
            IDictionary<string, object> dict = expando;
            dict["Version"] = TargetVersion;
            dict["TestInt"] = Convert.ToInt32(dict["TestInt"]) + additionalTestInt;
            return expando;
        }

        public TestMessagePackSaveData Finalize(ExpandoObject expando)
        {
            IDictionary<string, object> dict = expando;
            var final = new TestMessagePackSaveData
            {
                Version = TargetVersion,
                testInt = Convert.ToInt32(dict["TestInt"])
            };
            return final;
        }
    }
    
    [CreateAssetMenu(fileName = "TestMessagePackSaveObject", menuName = "QuackUp/Save/TestMessagePackSaveObject", order = 0)]
    public class TestMessagePackSaveObject : MessagePackSaveObject<TestMessagePackSaveData>
    {
        public override void Reset()
        {
            base.Reset();
            saveData = new TestMessagePackSaveData
            {
                Version = string.Empty
            };
        }
    }
}