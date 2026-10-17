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

        int skipFrames = 3;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.DeskInputEnabled) { skipFrames = 3; return; } // menü vb. açıkken kamera dönmez

            var fc = FocusController.Instance;
            if (fc != null && (fc.IsTransitioning || !(fc.State == FocusState.Desk || fc.State == FocusState.HoldingStamp)))
            { skipFrames = 3; return; }

            var cfg = GameConfig.Current;
            float maxYaw = cfg ? cfg.lookYaw : 60f;
            float maxPitchUp = cfg ? cfg.lookPitch : 25f;
            float maxPitchDown = cfg ? cfg.lookPitchDown : 45f;

            var mouse = Mouse.current;
            // İmleç kilitlendiği ilk karelerde fare hareketi sıçrar (imleç ortaya ışınlanır): yok say
            if (!CursorController.IsAiming) skipFrames = 3;
            else if (skipFrames > 0) { skipFrames--; if (mouse != null) mouse.delta.ReadValue(); }
            else if (mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue();
                targetYaw = Mathf.Clamp(targetYaw + d.x * sensitivity, -maxYaw, maxYaw);
                targetPitch = Mathf.Clamp(targetPitch - d.y * sensitivity, -maxPitchUp, maxPitchDown); // + = aşağı
            }

            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVel, smoothTime);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVel, smoothTime);
            transform.rotation = DeskRotation;
        }

        /// Masa bakışını verilen yöne çevir (sınırlar içinde). Yakın bakıştan çıkarken imlecin gösterdiği yer
        /// nişan noktasının altında kalsın diye kullanılır.
        public Quaternion LookToward(Vector3 dir)
        {
            var cfg = GameConfig.Current;
            float maxYaw = cfg ? cfg.lookYaw : 60f;
            float maxPitchUp = cfg ? cfg.lookPitch : 25f;
            float maxPitchDown = cfg ? cfg.lookPitchDown : 45f;
            var e = Quaternion.LookRotation(dir, Vector3.up).eulerAngles;
            targetYaw = yaw = Mathf.Clamp(Mathf.DeltaAngle(baseYaw, e.y), -maxYaw, maxYaw);
            targetPitch = pitch = Mathf.Clamp(Mathf.DeltaAngle(basePitch, e.x), -maxPitchUp, maxPitchDown);
            yawVel = pitchVel = 0f;
            return DeskRotation;
        }

        /// Bakışı başlangıç açısına sıfırla (gün başı vb.)
        public void ResetLook()
        {
            yaw = pitch = targetYaw = targetPitch = yawVel = pitchVel = 0f;
            transform.rotation = DeskRotation;
        }
    }
}
