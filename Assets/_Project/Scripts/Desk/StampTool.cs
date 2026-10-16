using UnityEngine;

namespace LoadBearing
{
    /// Masadaki tek bir mühür (ONAY ya da RET). Mantık StampController'da.
    /// Objenin kökü mührün tabanıdır: mühür tam bu noktadan kâğıda değer.
    public class StampTool : MonoBehaviour
    {
        public StampType type = StampType.Approve;

        public Vector3 HomePos { get; private set; }
        public Quaternion HomeRot { get; private set; }

        void Awake()
        {
            HomePos = transform.position;
            HomeRot = transform.rotation;
        }
    }
}
