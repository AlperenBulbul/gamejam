using UnityEngine;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Masa görünümünde fare hareketi kamerayı döndürür (crosshair ortada sabit).
    /// Yatay ±lookYaw, yukarı lookPitch, aşağı lookPitchDown (GameConfig). Sadece fare kilitliyken (CursorController.IsAiming).
    /// Yakın bakışta FocusController bunu kapatır.
    public class DeskLook : MonoBehaviour
    {
        [Tooltip("Fare hassasiyeti: piksel başına derece")]
        public float sensitivity = 0.08f;
        [Tooltip("Dönüş yumuşatma (0 = anında)")]
        public float smoothTime = 0.03f;

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
            var gm = GameManager.Instance;
            if (gm != null && !gm.DeskInputEnabled) return; // menü vb. açıkken kamera dönmez

            var fc = FocusController.Instance;
            if (fc != null && (fc.IsTransitioning || !(fc.State == FocusState.Desk || fc.State == FocusState.HoldingStamp)))
                return;

            var cfg = GameConfig.Current;
            float maxYaw = cfg ? cfg.lookYaw : 60f;
            float maxPitchUp = cfg ? cfg.lookPitch : 25f;
            float maxPitchDown = cfg ? cfg.lookPitchDown : 45f;

            var mouse = Mouse.current;
            if (mouse != null && CursorController.IsAiming)
            {
                Vector2 d = mouse.delta.ReadValue();
                targetYaw = Mathf.Clamp(targetYaw + d.x * sensitivity, -maxYaw, maxYaw);
                targetPitch = Mathf.Clamp(targetPitch - d.y * sensitivity, -maxPitchUp, maxPitchDown); // + = aşağı
            }

            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVel, smoothTime);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVel, smoothTime);
            transform.rotation = DeskRotation;
        }

        /// Bakışı başlangıç açısına sıfırla (gün başı vb.)
        public void ResetLook()
        {
            yaw = pitch = targetYaw = targetPitch = yawVel = pitchVel = 0f;
            transform.rotation = DeskRotation;
        }
    }
}
