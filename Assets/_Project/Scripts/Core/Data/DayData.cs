using UnityEngine;

namespace LoadBearing
{
    [CreateAssetMenu(menuName = "Load Bearing/Day", fileName = "Day1")]
    public class DayData : ScriptableObject
    {
        public int dayNumber = 1;
        public CustomerData[] customers;
        public RuleData[] newRulePages;
        public DayEndEvent endEvent = DayEndEvent.Rain;
        public string lightingPreset = "Day1";
        [TextArea] public string openingHeadline;
    }
}
