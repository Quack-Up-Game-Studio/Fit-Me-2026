using Sirenix.OdinInspector;
using UnityEngine;

namespace QuackUp.GPGS
{
    [CreateAssetMenu(fileName = "GPGSAuthenticationManagerConfig", menuName = "QuackUp/GPGS/AuthenticationManagerConfig")]
    public class GPGSAuthenticationManagerConfig : SerializedScriptableObject
    {
        [field: SerializeField] public bool AutoAuthenticateOnStart { get; private set; }
    }
}