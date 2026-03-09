using Sirenix.OdinInspector;
using UnityEngine;

namespace QuackUp.GPGS
{
    [CreateAssetMenu(fileName = "GPGSSavedGamesConfig", menuName = "QuackUp/GPGS/SavedGames")]
    public class GPGSSavedGamesConfig : SerializedScriptableObject
    {
        [field: SerializeField] public bool LoadAutomaticallyAfterAuthentication { get; private set; } = true;
        [field: SerializeField] public Sprite DefaultSavedImage { get; private set; }
        [field: SerializeField] public SaveUIConfig SaveUIConfig { get; private set; }
        [field: SerializeField] public SaveUIConfig LoadUIConfig { get; private set; }
    }
}