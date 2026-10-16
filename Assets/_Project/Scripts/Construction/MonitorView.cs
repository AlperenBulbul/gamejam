using UnityEngine;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Masadaki güvenlik monitörü.
    /// - Yakın bakış: kamera ekranın tam karşısına geçer, ekran kadrajı doldurur
    /// - Monitöre bakarken: A / D ya da sol / sağ ok kanal değiştirir, 1-7 doğrudan kanal seçer
    /// (İşçi tıklama, düğmeler, LED ve telsiz adım 13'te.)
    public class MonitorView : MonoBehaviour
    {
        public Renderer screenRenderer;
        [Tooltip("Yakın bakışta ekranın etrafında kalan pay (1 = ekran kadrajı tam doldurur)")]
        public float focusMargin = 1.15f;

        void Start()
        {
            var it = GetComponent<Interactable>();
            if (it == null || it.focusPoint == null || screenRenderer == null) return;

            var cam = Camera.main;
            var b = screenRenderer.bounds;
            Vector3 center = b.center;

            // Ekranın yüzü: modelin ileri ekseninden hangisi kameraya bakıyorsa
            var t = screenRenderer.transform;
            Vector3 n = t.forward; n.y = 0f;
            if (cam != null && Vector3.Dot(n, cam.transform.position - center) < 0f) n = -n;
            n = n.sqrMagnitude > 1e-4f ? n.normalized : Vector3.back;

            // Ekran boyu (modelden): genişlik ve yükseklik
            var mf = screenRenderer.GetComponent<MeshFilter>();
            Vector2 size = new Vector2(0.28f, 0.21f);
            if (mf != null && mf.sharedMesh != null)
            {
                var lb = mf.sharedMesh.bounds;
                var s = Vector3.Scale(lb.size, t.lossyScale);
                size = new Vector2(Mathf.Max(Mathf.Abs(s.x), 0.05f), Mathf.Max(Mathf.Abs(s.y), 0.05f));
            }

            float fov = cam != null ? cam.fieldOfView : 60f;
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            float dist = Mathf.Max(size.y, size.x / aspect) * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * focusMargin;

            it.focusPoint.position = center + n * dist;
            it.focusPoint.rotation = Quaternion.LookRotation(-n, Vector3.up);
        }

        void Update()
        {
            var fc = FocusController.Instance;
            var cm = ConstructionManager.Instance;
            if (fc == null || cm == null || fc.State != FocusState.Monitor || fc.IsTransitioning) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) cm.SelectNext(-1);
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) cm.SelectNext(+1);

            var digits = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key };
            for (int i = 0; i < digits.Length; i++)
                if (digits[i].wasPressedThisFrame) cm.Select(i);
        }
    }
}
