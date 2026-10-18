using UnityEngine;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Masadaki kâğıt harita. Haritaya yakından bakarken (kamera tam tepeden bakar):
    ///  - Tekerlek: 3 kademe yakınlaştır / uzaklaştır, imlecin olduğu noktaya doğru
    ///  - Yakınken kamera fareyi takip eder (panMode: EdgeScroll / FollowMouse)
    ///  - Boşluk: ASETAT (fay hattı + taşkın alanı katmanı) aç / kapat
    /// Harita yüzeyi: tasarımcının SM_Map modeli (mapRenderer). Yoksa eski greybox canvas (mapCanvas) kullanılır.
    /// PixelToWorld: harita pikseli (sol üst 0,0) -> dünyadaki nokta (iğneler için), mesh UV'leri üzerinden.
    public class MapView : MonoBehaviour
    {
        public enum PanMode { EdgeScroll, FollowMouse }

        [Header("Harita")]
        [Tooltip("Tasarımcının harita modeli (M_Map_Karaova materyalli renderer)")]
        public Renderer mapRenderer;
        [Tooltip("Eski greybox harita (mapRenderer yoksa)")]
        public RectTransform mapCanvas;
        public Vector2 mapPixelSize = new Vector2(4800f, 2640f);
        public Vector2 mapWorldSize = new Vector2(0.6f, 0.4f);

        [Header("Asetat")]
        public GameObject overlay;
        public bool overlayStartsVisible = false;

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

        public bool OverlayVisible => overlay != null && overlayOn;

        [Header("Asetat animasyonu")]
        [Tooltip("Asetatın üstten (kuzeyden) kayarak gelip haritaya oturma süresi")]
        public float overlayAnimTime = 0.35f;
        bool overlayOn;
        Coroutine overlayAnim;
        Vector3 overlayHome;
        bool overlayHomeSaved;
        MaterialPropertyBlock overlayMpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Transform cam;
        Camera camComp;
        Vector3 basePos;
        float baseDist;
        bool active;
        int zoom;
        float curZoom = 1f;
        Vector2 pan, curPan;
        float nextWheelTime;

        // UV -> dünya için mesh önbelleği
        Vector3[] verts;
        Vector2[] uvs;
        int[] tris;

        Vector3 SurfaceCenter
        {
            get
            {
                if (mapRenderer != null) return mapRenderer.bounds.center;
                if (mapCanvas != null) return mapCanvas.position;
                return transform.position;
            }
        }

        void Start()
        {
            SetOverlay(overlayStartsVisible, false);

            if (mapRenderer != null)
            {
                var b = mapRenderer.bounds;
                mapWorldSize = new Vector2(b.size.x, b.size.z);
                CacheMesh();
            }

            // Yakın bakış noktasını haritanın tam tepesine al
            var it = GetComponent<Interactable>();
            if (it != null && it.focusPoint != null)
            {
                var center = SurfaceCenter;
                float dist = Vector3.Distance(it.focusPoint.position, center);
                // Haritanın tamamı sığsın (16:9, dikey FOV ~70)
                float fit = Mathf.Max(mapWorldSize.y, mapWorldSize.x / 1.7f) * 0.5f / Mathf.Tan(35f * Mathf.Deg2Rad) * 1.08f;
                dist = Mathf.Max(fit, 0.2f);
                it.focusPoint.position = center + Vector3.up * dist;
                it.focusPoint.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // ekranın üstü = kuzey
            }
        }

        void OnEnable()
        {
            GameEvents.FocusChanged += OnFocusChanged;
            GameEvents.FocusTransitionStarted += OnTransition;
            GameEvents.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.FocusChanged -= OnFocusChanged;
            GameEvents.FocusTransitionStarted -= OnTransition;
            GameEvents.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(GameState s)
        {
            // Yeni oyunda asetat başlangıç durumuna döner
            if (s == GameState.MainMenu && overlay != null) SetOverlay(overlayStartsVisible, false);
        }

        void OnFocusChanged(FocusState f)
        {
            if (f != FocusState.Map) return;
            camComp = Camera.main;
            if (camComp == null) return;
            cam = camComp.transform;
            basePos = cam.position;
            float planeY = SurfaceCenter.y;
            float down = Mathf.Max(0.05f, -cam.forward.y);
            baseDist = (basePos.y - planeY) / down;
            zoom = 0; curZoom = 1f; pan = curPan = Vector2.zero;
            active = true;
        }

        void OnTransition(FocusState from, FocusState to)
        {
            if (from == FocusState.Map)
            {
                active = false;
                if (overlayOn) SetOverlay(false, true); // haritadan çıkınca tehlikeli bölge katmanı geri kalkar
            }
        }

        public void ToggleOverlay()
        {
            if (overlay == null) return;
            if (!overlayOn) Sfx.Play("SFX_Acetate_Slide"); // şeffaf katman haritanın üstüne
            SetOverlay(!overlayOn, true);
        }

        /// Asetat: açılırken haritanın üst kenarından biraz yukarıdan kayıp kâğıdın üstüne yatar, kapanırken geri kalkar
        void SetOverlay(bool on, bool animate)
        {
            if (overlay == null) return;
            if (!overlayHomeSaved) { overlayHome = overlay.transform.localPosition; overlayHomeSaved = true; }
            overlayOn = on;
            if (overlayAnim != null) { StopCoroutine(overlayAnim); overlayAnim = null; }
            if (!animate || !isActiveAndEnabled)
            {
                overlay.transform.localPosition = overlayHome;
                SetOverlayAlpha(1f);
                overlay.SetActive(on);
                return;
            }
            overlayAnim = StartCoroutine(AnimateOverlay(on));
        }

        System.Collections.IEnumerator AnimateOverlay(bool on)
        {
            var t = overlay.transform;
            // Dünyada: 4 cm yukarı + 12 cm kuzeye (haritanın üst kenarı) -> yerel uzaya çevir
            Vector3 lift = Vector3.up * 0.04f + Vector3.forward * 0.12f;
            Vector3 off = t.parent != null ? t.parent.InverseTransformVector(lift) : lift;
            Vector3 away = overlayHome + off;
            overlay.SetActive(true);
            float d = Mathf.Max(0.05f, overlayAnimTime);
            for (float e = 0f; e < d; e += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(e / d);
                float s = on ? 1f - (1f - k) * (1f - k) : k * k; // açılış: yavaşlayarak oturur, kapanış: hızlanarak kalkar
                float p = on ? s : 1f - s;                        // 1 = yerinde
                t.localPosition = Vector3.Lerp(away, overlayHome, p);
                SetOverlayAlpha(p);
                yield return null;
            }
            t.localPosition = overlayHome;
            SetOverlayAlpha(1f);
            overlay.SetActive(on);
            overlayAnim = null;
        }

        void SetOverlayAlpha(float a)
        {
            if (overlay == null) return;
            overlayMpb ??= new MaterialPropertyBlock();
            foreach (var r in overlay.GetComponentsInChildren<Renderer>(true))
            {
                r.GetPropertyBlock(overlayMpb);
                overlayMpb.SetColor(BaseColorId, new Color(1f, 1f, 1f, a));
                r.SetPropertyBlock(overlayMpb);
            }
        }

        void LateUpdate()
        {
            if (!active || cam == null) return;
            var fc = FocusController.Instance;
            if (fc == null || fc.State != FocusState.Map || fc.IsTransitioning) return;

            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) ToggleOverlay();

            var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;

            if (mouse != null)
            {
                Vector2 m = mouse.position.ReadValue();
                float h = Mathf.Max(1, Screen.height);

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

        // ------------------------------------------------------------------ piksel -> dünya

        void CacheMesh()
        {
            var mf = mapRenderer.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) return;
            if (!mesh.isReadable) { Debug.LogWarning("[MapView] Harita mesh'i okunamıyor (Read/Write kapalı). Menü 15 bunu açar."); return; }

            int sub = 0;
            var mats = mapRenderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.Contains("Map_Karaova")) { sub = i; break; }
            verts = mesh.vertices;
            uvs = mesh.uv;
            tris = mesh.GetTriangles(Mathf.Min(sub, mesh.subMeshCount - 1));
        }

        /// Harita pikseli (sol üst 0,0) -> dünya konumu (harita yüzeyinde)
        public Vector3 PixelToWorld(Vector2 px)
        {
            var uv = new Vector2(px.x / mapPixelSize.x, 1f - px.y / mapPixelSize.y);
            if (TryUVToWorld(uv, out var w)) return w;

            // Yedek: eski canvas ya da yüzey sınırları
            if (mapCanvas != null)
                return mapCanvas.TransformPoint(new Vector3(px.x - mapPixelSize.x * 0.5f, mapPixelSize.y * 0.5f - px.y, 0f));
            var c = SurfaceCenter;
            return c + new Vector3((uv.x - 0.5f) * mapWorldSize.x, 0f, (uv.y - 0.5f) * mapWorldSize.y);
        }

        bool TryUVToWorld(Vector2 uv, out Vector3 world)
        {
            world = Vector3.zero;
            if (tris == null || uvs == null || uvs.Length == 0) return false;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector2 a = uvs[tris[i]], b = uvs[tris[i + 1]], c = uvs[tris[i + 2]];
                Vector2 v0 = b - a, v1 = c - a, v2 = uv - a;
                float d00 = Vector2.Dot(v0, v0), d01 = Vector2.Dot(v0, v1), d11 = Vector2.Dot(v1, v1);
                float d20 = Vector2.Dot(v2, v0), d21 = Vector2.Dot(v2, v1);
                float den = d00 * d11 - d01 * d01;
                if (Mathf.Abs(den) < 1e-12f) continue;
                float v = (d11 * d20 - d01 * d21) / den;
                float w = (d00 * d21 - d01 * d20) / den;
                float u = 1f - v - w;
                if (u < -1e-4f || v < -1e-4f || w < -1e-4f) continue;
                var p = verts[tris[i]] * u + verts[tris[i + 1]] * v + verts[tris[i + 2]] * w;
                world = mapRenderer.transform.TransformPoint(p);
                return true;
            }
            return false;
        }
    }
}
