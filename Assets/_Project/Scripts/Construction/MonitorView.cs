using UnityEngine;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Masadaki güvenlik monitörü.
    /// - Yakın bakış: kamera monitörün tam karşısına geçer; ekran + altındaki düğmeler ve ışıklar kadrajı doldurur
    /// - Monitöre bakarken: ekranın altındaki ◀ / ▶ düğmelerine tıklamak (ya da A / D, sol / sağ ok) kanal değiştirir,
    ///   1-7 doğrudan kanal seçer
    /// - A–G ışıkları: şantiye yok = sönük, çalışıyor = yeşil, düzeltilmemiş hata = sarı yanıp söner (2 Hz),
    ///   bitti = sabit yeşil, su altında / yıkıldı = gri
    /// (Ekranda işçiye tıklama ve telsiz adım 13'te.)
    public class MonitorView : MonoBehaviour
    {
        public Renderer screenRenderer;
        [Tooltip("Yakın bakışta monitörün etrafında kalan pay (1 = ön yüz kadrajı tam doldurur)")]
        public float focusMargin = 1.06f;

        [Header("Işık materyalleri (menü 21 bağlar)")]
        public Material ledOff, ledGreen, ledYellow, ledGrey;

        [Header("Düğme")]
        public float pressDepth = 0.004f;
        public float pressTime = 0.12f;

        Transform prevButton, nextButton;
        Renderer[] leds = new Renderer[7];
        Material[] ledCurrent = new Material[7];
        Vector3 prevHome, nextHome;
        float prevPressT = -1f, nextPressT = -1f;
        Vector3 normal = Vector3.back;
        Camera cam;
        MaterialPropertyBlock mpb;
        Renderer hoverRend;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Start()
        {
            cam = Camera.main;
            mpb = new MaterialPropertyBlock();
            prevButton = FindDeep(transform, "SM_Monitor_Button_Prev");
            nextButton = FindDeep(transform, "SM_Monitor_Button_Next");
            foreach (var b in new[] { prevButton, nextButton })
                if (b != null && b.GetComponent<Collider>() == null) AddBox(b);
            if (prevButton != null) prevHome = prevButton.localPosition;
            if (nextButton != null) nextHome = nextButton.localPosition;
            for (int i = 0; i < 7; i++)
            {
                var t = FindDeep(transform, $"SM_Monitor_LED_{(char)('A' + i)}");
                leds[i] = t != null ? t.GetComponent<Renderer>() : null;
            }
            SetupFocus();
        }

        static void AddBox(Transform t)
        {
            var mf = t.GetComponent<MeshFilter>();
            var box = t.gameObject.AddComponent<BoxCollider>();
            if (mf != null && mf.sharedMesh != null)
            {
                box.center = mf.sharedMesh.bounds.center;
                box.size = mf.sharedMesh.bounds.size + new Vector3(0.004f, 0.004f, 0.004f);
            }
        }

        // ------------------------------------------------------------------ yakın bakış

        void SetupFocus()
        {
            var it = GetComponent<Interactable>();
            if (it == null || it.focusPoint == null || screenRenderer == null) return;

            var t = screenRenderer.transform;
            Vector3 center = screenRenderer.bounds.center;

            // Ekranın yüzü: modelin ileri ekseninden hangisi kameraya bakıyorsa
            Vector3 n = t.forward; n.y = 0f;
            if (cam != null && Vector3.Dot(n, cam.transform.position - center) < 0f) n = -n;
            normal = n.sqrMagnitude > 1e-4f ? n.normalized : Vector3.back;

            // Ön yüzün tamamı (kasa + ekran + düğmeler + ışıklar): ekran düzleminde sınırlar
            var right = Vector3.Cross(Vector3.up, -normal).normalized; // kameradan bakınca sağ
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var p = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var d = p - center;
                    float x = Vector3.Dot(d, right), y = d.y;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            }
            if (minX > maxX) { minX = -0.2f; maxX = 0.2f; minY = -0.15f; maxY = 0.12f; }
            // Kasa alt kenarı masaya kadar iniyor; ayağı değil ön yüzü çerçevele
            float w = maxX - minX, h = maxY - minY;
            var frameCenter = center + right * ((minX + maxX) * 0.5f) + Vector3.up * ((minY + maxY) * 0.5f);

            float fov = cam != null ? cam.fieldOfView : 60f;
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            float dist = Mathf.Max(h, w / aspect) * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * focusMargin;

            it.focusPoint.position = frameCenter + normal * dist;
            it.focusPoint.rotation = Quaternion.LookRotation(-normal, Vector3.up);
        }

        // ------------------------------------------------------------------ kanal

        void Update()
        {
            UpdateLeds();
            AnimateButton(prevButton, prevHome, ref prevPressT);
            AnimateButton(nextButton, nextHome, ref nextPressT);

            var fc = FocusController.Instance;
            var cm = ConstructionManager.Instance;
            bool active = fc != null && cm != null && fc.State == FocusState.Monitor && !fc.IsTransitioning;
            Hover(active ? ButtonUnderPointer() : null);
            if (!active) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Press(-1);
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Press(+1);
                var digits = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key };
                for (int i = 0; i < digits.Length; i++)
                    if (digits[i].wasPressedThisFrame) cm.Select(i);
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                var b = ButtonUnderPointer();
                if (b == prevButton && b != null) Press(-1);
                else if (b == nextButton && b != null) Press(+1);
            }
        }

        void Press(int dir)
        {
            var cm = ConstructionManager.Instance;
            if (cm != null) cm.SelectNext(dir);
            if (dir < 0) prevPressT = 0f; else nextPressT = 0f;
        }

        Transform ButtonUnderPointer()
        {
            if (cam == null) return null;
            var ray = CursorController.PointerRay(cam);
            if (!Physics.Raycast(ray, out var hit, 3f, ~0, QueryTriggerInteraction.Collide)) return null;
            var t = hit.collider.transform;
            if (prevButton != null && (t == prevButton || t.IsChildOf(prevButton))) return prevButton;
            if (nextButton != null && (t == nextButton || t.IsChildOf(nextButton))) return nextButton;
            return null;
        }

        void Hover(Transform b)
        {
            var r = b != null ? b.GetComponent<Renderer>() : null;
            if (r == hoverRend) return;
            if (hoverRend != null) { hoverRend.SetPropertyBlock(null); }
            hoverRend = r;
            if (hoverRend != null)
            {
                hoverRend.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, new Color(1.5f, 1.5f, 1.5f, 1f)); // üstüne gelince parlar
                hoverRend.SetPropertyBlock(mpb);
            }
        }

        void AnimateButton(Transform b, Vector3 home, ref float t)
        {
            if (b == null || t < 0f) return;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / pressTime);
            float depth = Mathf.Sin(k * Mathf.PI) * pressDepth;
            // Düğme ekranın içine doğru basılır (yerel uzayda normalin tersine)
            var localIn = b.parent != null ? b.parent.InverseTransformDirection(-normal) : -normal;
            b.localPosition = home + localIn * depth;
            if (k >= 1f) { b.localPosition = home; t = -1f; }
        }

        // ------------------------------------------------------------------ ışıklar

        void UpdateLeds()
        {
            var cm = ConstructionManager.Instance;
            bool blinkOn = Mathf.Repeat(Time.time * 2f, 1f) < 0.5f;
            for (int i = 0; i < leds.Length; i++)
            {
                if (leds[i] == null) continue;
                Material m = ledOff;
                var site = cm != null && i < cm.Sites.Count ? cm.Sites[i] : null;
                if (site != null)
                {
                    switch (site.state)
                    {
                        case SiteState.Building: m = site.HasFaults ? (blinkOn ? ledYellow : ledOff) : ledGreen; break;
                        case SiteState.Done: m = ledGreen; break;
                        default: m = ledGrey; break;
                    }
                }
                if (m != null && m != ledCurrent[i]) { leds[i].sharedMaterial = m; ledCurrent[i] = m; }
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
