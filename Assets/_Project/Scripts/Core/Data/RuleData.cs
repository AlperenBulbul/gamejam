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
        [Tooltip("Tasarlanmış sayfa görseli (T_RulePage_0n). Doluysa metin yerine bu gösterilir.")]
        public Texture2D pageTexture;
    }
}
