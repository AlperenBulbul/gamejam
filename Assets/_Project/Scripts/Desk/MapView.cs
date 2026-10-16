using UnityEngine;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Masadaki kâğıt harita. Haritaya yakından bakarken (kamera tam tepeden bakar):
    ///  - Tekerlek: 3 kademe yakınlaştır / uzaklaştır, imlecin olduğu noktaya doğru
    ///    (imlecin altındaki yer zoom sırasında yerinde kalır)
    ///  - Yakınken kamera fareyi takip eder (panMode):
    ///      EdgeScroll  = imleç ekran kenarına gidince o yöne kayar (masada etrafa bakmak gibi)
    ///      FollowMouse = imlecin ekrandaki yeri haritanın o bölgesine karşılık gelir (büyüteç gibi)
    ///  - Görüş harita kenarından dışarı çıkmaz.
    /// Harita, piksel uzayında (4096 x 2730, sol üst = 0,0) çizilmiş dünya-uzayı bir Canvas'tır.
    public class MapView : MonoBehaviour
    {
        public enum PanMode { EdgeScroll, FollowMouse }

        [Header("Harita")]
        public RectTransform mapCanvas;
        public Vector2 mapPixelSize = new Vector2(4096f, 2730f);
        public Vector2 mapWorldSize = new Vector2(0.6f, 0.4f);

        [Header("Yakınlaştırma")]
        [Tooltip("Kamera mesafesi çarpanı: 1 = normal, küçüldükçe yakın")]
        public float[] zoomLevels = { 1f, 0.55f, 0.3f };
        public float smooth = 14f;
        public float wheelCooldown = 0.12f;

        [Header("Yakınken kamera hareketi")]
        public PanMode panMode = PanMode.EdgeScroll;
        [Range(0.05f, 0.45f)] public float edgeSize = 0.18f;
        [Tooltip("Kenarın en dibinde saniyede kaç 'ekran yüksekliği' kayar")]
        public float edgeSpeed = 1.1f;

        Transform cam;
        Camera camComp;
        Vector3 basePos;
        float baseDist;
        bool active;
        int zoom;
        float curZoom = 1f;
        Vector2 pan, curPan;          // dünya x / z kayması
        float nextWheelTime;

        void Start()
        {
            // Yakın bakış noktasını haritanın tam tepesine al (eğik bakış zoom'da kaymaya yol açıyordu)
            var it = GetComponent<Interactable>();
            if (it != null && it.focusPoint != null && mapCanvas != null)
            {
                var center = mapCanvas.position;
                float dist = Vector3.Distance(it.focusPoint.position, center);
                it.focusPoint.position = center + Vector3.up * dist;
                it.focusPoint.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // ekranın üstü = kuzey
            }
        }

        void OnEnable()
        {
            GameEvents.FocusChanged += OnFocusChanged;
            GameEvents.FocusTransitionStarted += OnTransition;
        }

        void OnDisable()
        {
            GameEvents.FocusChanged -= OnFocusChanged;
            GameEvents.FocusTransitionStarted -= OnTransition;
        }

        void OnFocusChanged(FocusState f)
        {
            if (f != FocusState.Map) return;
            camComp = Camera.main;
            if (camComp == null) return;
            cam = camComp.transform;
            basePos = cam.position;
            float planeY = mapCanvas != null ? mapCanvas.position.y : transform.position.y;
            float down = Mathf.Max(0.05f, -cam.forward.y);
            baseDist = (basePos.y - planeY) / down;
            zoom = 0; curZoom = 1f; pan = curPan = Vector2.zero;
            active = true;
        }

        void OnTransition(FocusState from, FocusState to)
        {
            if (from == FocusState.Map) active = false; // FocusController kamerayı masaya geri götürür
        }

        void LateUpdate()
        {
            if (!active || cam == null) return;
            var fc = FocusController.Instance;
            if (fc == null || fc.State != FocusState.Map || fc.IsTransitioning) return;

            var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;

            if (mouse != null)
            {
                Vector2 m = mouse.position.ReadValue();
                float h = Mathf.Max(1, Screen.height);

                // Tekerlek: imleç noktasına zoom
                float wheel = mouse.scroll.ReadValue().y;
                if (Time.unscaledTime >= nextWheelTime && Mathf.Abs(wheel) > 0.01f)
                {
                    int next = Mathf.Clamp(zoom + (wheel > 0 ? 1 : -1), 0, zoomLevels.Length - 1);
                    if (next != zoom)
                    {
                        Vector2 o = new Vector2((m.x - Screen.width * 0.5f) / h, (m.y - Screen.height * 0.5f) / h);
                        pan += o * (ViewHeight(zoomLevels[zoom]) - ViewHeight(zoomLevels[next]));
                        zoom = next;
                    }
                    nextWheelTime = Time.unscaledTime + wheelCooldown;
                }

                // Yakınken kamera fareyi takip eder
                if (zoom > 0 && Application.isFocused)
                {
                    float nx = Mathf.Clamp01(m.x / Mathf.Max(1, Screen.width));
                    float ny = Mathf.Clamp01(m.y / h);
                    if (panMode == PanMode.EdgeScroll)
                    {
                        float vh = ViewHeight(zoomLevels[zoom]);
                        pan.x += EdgeAxis(nx, edgeSize) * edgeSpeed * vh * dt;
                        pan.y += EdgeAxis(ny, edgeSize) * edgeSpeed * vh * dt;
                    }
                    else
                    {
                        var max = MaxPan(zoomLevels[zoom]);
                        pan = new Vector2((nx * 2f - 1f) * max.x, (ny * 2f - 1f) * max.y);
                    }
                }
            }

            ClampPan(zoomLevels[zoom]);

            float k = 1f - Mathf.Exp(-smooth * dt);
            curZoom = Mathf.Lerp(curZoom, zoomLevels[zoom], k);
            curPan = Vector2.Lerp(curPan, pan, k);

            cam.position = basePos + new Vector3(curPan.x, 0f, curPan.y) + cam.forward * baseDist * (1f - curZoom);
        }

        static float EdgeAxis(float n, float edge)
        {
            if (n < edge) return -(edge - n) / edge;
            if (n > 1f - edge) return (n - (1f - edge)) / edge;
            return 0f;
        }

        float ViewHeight(float zoomFactor)
        {
            float fov = camComp != null ? camComp.fieldOfView : 60f;
            return 2f * baseDist * zoomFactor * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        }

        Vector2 MaxPan(float zoomFactor)
        {
            float vh = ViewHeight(zoomFactor);
            float vw = vh * (camComp != null ? camComp.aspect : 16f / 9f);
            return new Vector2(Mathf.Max(0f, (mapWorldSize.x - vw) * 0.5f), Mathf.Max(0f, (mapWorldSize.y - vh) * 0.5f));
        }

        void ClampPan(float zoomFactor)
        {
            var max = MaxPan(zoomFactor);
            pan.x = Mathf.Clamp(pan.x, -max.x, max.x);
            pan.y = Mathf.Clamp(pan.y, -max.y, max.y);
        }

        /// Harita pikseli (sol üst 0,0) -> dünya konumu (harita yüzeyinde)
        public Vector3 PixelToWorld(Vector2 px)
        {
            var local = new Vector3(px.x - mapPixelSize.x * 0.5f, mapPixelSize.y * 0.5f - px.y, 0f);
            return mapCanvas.TransformPoint(local);
        }
    }
}
