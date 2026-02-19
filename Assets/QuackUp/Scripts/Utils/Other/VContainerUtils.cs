using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace QuackUp.Utils
{
    public class SerializedLifetimeScope : LifetimeScope, ISerializationCallbackReceiver, ISupportsPrefabSerialization
    {
        [SerializeField, HideInInspector]
        private SerializationData serializationData;

        SerializationData ISupportsPrefabSerialization.SerializationData
        {
            get => serializationData; 
            set => serializationData = value;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            UnitySerializationUtility.DeserializeUnityObject(this, ref serializationData);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            UnitySerializationUtility.SerializeUnityObject(this, ref serializationData);
        }
    }
    
    [Serializable]
    [ShowOdinSerializedPropertiesInInspector]
    public abstract class DebugableInstaller<T> : IInstaller where T : IDebugData
    {
        protected T DebugData;
        
#if UNITY_EDITOR
        [Button("Open Debug Window"), HideInEditorMode]
        private void OpenDebugWindow()
        {
            DebugEditorWindow.Inspect(DebugData, typeof(T).ToString());
        }
#endif
        public abstract void Install(IContainerBuilder builder);
    }
}
