using UnityEngine;

namespace LoadBearing
{
    [CreateAssetMenu(menuName = "Load Bearing/Rule Page", fileName = "Rule_")]
    public class RuleData : ScriptableObject
    {
        public string id;
        public string title;
        [TextArea(4, 12)] public string body;
        public int dayIntroduced = 1;
    }
}
