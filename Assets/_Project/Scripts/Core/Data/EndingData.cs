using UnityEngine;

namespace LoadBearing
{
    [CreateAssetMenu(menuName = "Load Bearing/Ending", fileName = "End_")]
    public class EndingData : ScriptableObject
    {
        public string id;
        [Tooltip("1 = ilk kontrol edilen (GDD bölüm 8)")]
        public int priority;
        public string title;
        [TextArea(3, 8)] public string body;
        public Sprite image;
        public AudioClip stinger;
    }
}
