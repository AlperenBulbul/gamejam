using UnityEngine;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Masa görünümünde imleç ekranın dış kenarına (%20) gelince bakış o yöne döner.
    /// Yatay ±lookYaw, dikey ±lookPitch (GameConfig). Yakın bakışta FocusController bunu kapatır.
    public class DeskLook : MonoBehaviour
    {
        [Tooltip("Kenarın en dibinde saniyede kaç derece döner")]
        [SerializeField] float maxTurnSpeed = 75f;
        [SerializeField] float smoothTime = 0.12f;

        float baseYaw, basePitch;
        float yaw, pitch, targetYaw, targetPitch, yawVel, pitchVel;

        /// Masa görünümünün o anki dönüşü (FocusController geri dönerken kullanır)
        public Quaternion DeskRotation => Quaternion.Euler(basePitch + pitch, baseYaw + yaw, 0f);

        void Awake()
        {
            var e = transform.eulerAngles;
            baseYaw = e.y;
            basePitch = e.x > 180f ? e.x - 360f : e.x;
        }

        void Update()
        {
            var fc = FocusController.Instance;
            if (fc != null && (fc.IsTransitioning || !(fc.State == FocusState.Desk || fc.State == FocusState.HoldingStamp)))
                return;

            var cfg = GameConfig.Current;
            float edge = cfg ? cfg.lookEdge : 0.2f;
            float maxYaw = cfg ? cfg.lookYaw : 60f;
            float maxPitch = cfg ? cfg.lookPitch : 25f;

            var mouse = Mouse.current;
            if (mouse != null && Application.isFocused)
            {
                Vector2 p = mouse.position.ReadValue();
                float nx = Mathf.Clamp01(p.x / Screen.width);
                float ny = Mathf.Clamp01(p.y / Screen.height);
                float dx = EdgeAxis(nx, edge);
                float dy = EdgeAxis(ny, edge);

                targetYaw = Mathf.Clamp(targetYaw + dx * maxTurnSpeed * Time.deltaTime, -maxYaw, maxYaw);
                targetPitch = Mathf.Clamp(targetPitch - dy * maxTurnSpeed * Time.deltaTime, -maxPitch, maxPitch);
            }

            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVel, smoothTime);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVel, smoothTime);
            transform.rotation = DeskRotation;
        }

        /// Kenar bölgesinde -1..1, ortada 0
        static float EdgeAxis(float n, float edge)
        {
            if (n < edge) return -(edge - n) / edge;
            if (n > 1f - edge) return (n - (1f - edge)) / edge;
            return 0f;
        }

        /// Deprem sarsıntısı vb. için bakışı sıfırla
        public void ResetLook()
        {
            yaw = pitch = targetYaw = targetPitch = yawVel = pitchVel = 0f;
            transform.rotation = DeskRotation;
        }
    }
}
