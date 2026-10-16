using UnityEngine;

namespace LoadBearing
{
    [CreateAssetMenu(menuName = "Load Bearing/Shop Item", fileName = "Item_")]
    public class ShopItemData : ScriptableObject
    {
        [Range(1, 6)] public int slot = 1;
        public string displayName;
        public int price;
        public GameObject prefab;
        public Sprite icon;
    }
}
