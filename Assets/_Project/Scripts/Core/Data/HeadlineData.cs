using UnityEngine;

namespace LoadBearing
{
    [CreateAssetMenu(menuName = "Load Bearing/Headline", fileName = "HL_")]
    public class HeadlineData : ScriptableObject
    {
        public string key;
        [TextArea] public string text;
        public Sprite image;
        [Tooltip("Büyük olan manşete çıkar: çöküş 50 > sel 40 > hepsi ayakta 35 > haksız ret 30 > müşteri gitti 20 > doğru karar 10")]
        public int priority;
    }
}
