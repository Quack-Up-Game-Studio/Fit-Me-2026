using System.Globalization;
using FitMe.GameData;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace FitMe.Panel
{
    public class RecordBlock : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text fitMeText;
        
        public void SetData(PlayerRecordSaveData.RunData runData)
        {
            //var currentUICulture = CultureInfo.CurrentUICulture;
            dateText.text = runData.dateTime.ToLocalTime().ToString("dd/MM/yy - HH:mm", CultureInfo.InvariantCulture);
            scoreText.text = runData.score.ToString("N0");
            fitMeText.text = runData.fitMe.ToString("N0");
        }
    }
}