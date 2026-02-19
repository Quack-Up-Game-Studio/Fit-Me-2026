using MessagePack;
using MessagePack.Resolvers;
using QuackUp.Save;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.GameData
{
    [CreateAssetMenu(fileName = "EnergyManagerSaveObject", menuName = "FitMe/GameData/Energy/EnergyManagerSaveObject")]
    [ShowOdinSerializedPropertiesInInspector]
    public class EnergyManagerSaveObject : MessagePackSaveObject<EnergyManagerSaveData>
    {
        // public override MessagePackSerializerOptions DefaultSerializerOptions
        // {
        //     get
        //     {
        //         var resolver = CompositeResolver.Create(
        //             // enable extension packages first
        //             ReactivePropertyValueResolver.Instance,
        //             MessagePack.Unity.Extension.UnityBlitResolver.Instance,
        //             MessagePack.Unity.UnityResolver.Instance,
        //
        //             // finally use standard (default) resolver
        //             ContractlessStandardResolver.Instance
        //         );
        //         return ContractlessStandardResolver.Options.WithResolver(resolver);
        //     }
        // }
    }
}