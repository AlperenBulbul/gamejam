using UnityEngine;

namespace LoadBearing
{
    /// Tüm müşterilerin ortak sabır replikleri (GDD bölüm 4)
    [CreateAssetMenu(menuName = "Load Bearing/Dialogue Set", fileName = "CommonDialogue")]
    public class DialogueSet : ScriptableObject
    {
        public string[] impatient;
        public string[] angry;
        public string[] rage;
        public string[] leave;

        public string[] For(PatienceStage stage) => stage switch
        {
            PatienceStage.Impatient => impatient,
            PatienceStage.Angry => angry,
            PatienceStage.Rage => rage,
            PatienceStage.Gone => leave,
            _ => null,
        };

        public string RandomFor(PatienceStage stage)
        {
            var arr = For(stage);
            return (arr == null || arr.Length == 0) ? null : arr[Random.Range(0, arr.Length)];
        }
    }
}
