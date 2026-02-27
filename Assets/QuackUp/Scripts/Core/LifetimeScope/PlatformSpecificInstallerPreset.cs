using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer.Unity;

namespace QuackUp.Core
{
    [CreateAssetMenu(fileName = "PlatformSpecificInstallerPreset", menuName = "QuackUp/Core/PlatformSpecificInstallerPreset")]
    [ShowOdinSerializedPropertiesInInspector]
    public class PlatformSpecificInstallerPreset : SerializedScriptableObject
    {
        [OdinSerialize] private List<IInstaller> platformSpecificInstallers = new();
        public IReadOnlyList<IInstaller> PlatformSpecificInstallers => platformSpecificInstallers;
    }
}
