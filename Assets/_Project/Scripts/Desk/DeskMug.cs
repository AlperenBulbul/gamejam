using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LoadBearing
{
    /// Masadaki ODTÜ kupası (tasarımcı: SM_JamItem_1_Mug, 9,5 cm). Gazetenin sağına konur:
    ///  - Masadaki diğer nesnelerin (gizli olanlar da: müşteri gelince çıkan dosya vb.) kutularıyla çakışmayan
    ///    ilk boş yer aranır; hiçbir şeyin içine girmez.
    ///  - Kulp oyuncunun sağına, logo oyuncuya bakar.
    ///  - Masadayken üstüne gelip sol tuşa basılı tutunca sürüklenir, bırakınca masaya konur
    ///    (tutarken 12 cm havada, nesnelerin üstünden geçer; masanın dışına çıkmaz, bir nesnenin üstüne bırakılırsa
    ///    en son boş yere iner).
    /// GameManager bu bileşeni kendisi ekler.
    public class DeskMug : MonoBehaviour
    {
        const float Height = 0.095f;   // gerçek ölçü (m)
        const float Gap = 0.025f;      // gazeteyle / diğer nesnelerle arasındaki boşluk

        Transform mug;

        IEnumerator Start()
        {
            yield return null; // masadaki nesneler yerine otursun
            yield return null;
            Place();
        }

        void Place()
        {
            var prefab = Resources.Load<GameObject>("Props/SM_JamItem_1_Mug");
            var paperView = FindAnyObjectByType<NewspaperView>();
            if (prefab == null || paperView == null) return;
            var paper = Largest(paperView.transform);
            if (paper == null) return;

            // Kupa: ölçü + materyal (sırlı porselen)
            var go = Instantiate(prefab);
            go.name = "Desk_Mug";
            mug = go.transform;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            var mat = MugMaterial();
            foreach (var r in rends)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // gölge yok
                r.receiveShadows = false;
                if (mat != null)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var b0 = Bounds(rends);
            if (b0.size.y > 1e-4f) mug.localScale *= Height / b0.size.y;
            mugRends = rends;

            // Yönler: oyuncunun sağı ve oyuncudan uzağa
            var cam = Camera.main;
            Vector3 right = cam != null ? Flat(cam.transform.right) : Vector3.right;
            Vector3 away = cam != null ? Flat(cam.transform.forward) : Vector3.forward;

            // Kulp oyuncunun sağına: mesh'in kulp tarafı (kutunun merkezi kulpa doğru kayık)
            var mf = go.GetComponentInChildren<MeshFilter>();
            Vector3 handleWorld = right;
            if (mf != null && mf.sharedMesh != null)
            {
                var c = mf.sharedMesh.bounds.center;
                var h = Flat(mf.transform.TransformDirection(new Vector3(c.x, 0f, c.z)));
                if (h.sqrMagnitude > 1e-6f) handleWorld = h;
            }
            mug.rotation = Quaternion.FromToRotation(handleWorld, right) * mug.rotation;

            // Kupanın masadaki kapladığı alan (dönmüş hâli)
            mug.position = Vector3.zero;
            var mb = Bounds(rends);
            Vector3 pivotToCenter = mb.center - mug.position;
            float deskY = paper.bounds.min.y;

            var blockers = Blockers(paper, mug);
            var desk = DeskTop(paper, out bool hasDesk);
            float baseOffset = mb.min.y - mug.position.y; // pivot ile modelin tabanı arası
            // Sürükleme için sakla
            paperR = paper; deskTopY = deskY;
            deskBounds = DragDesk(paper, out deskFound);
            if (!deskFound) { deskBounds = desk; deskFound = hasDesk; }
            mugSize = mb.size; mugPivotToCenter = pivotToCenter; mugBaseOffset = baseOffset;

            // Aday yerler (sırayla): mühürlerin solu, gazetenin solu, gazetenin sağı
            var anchors = new List<(Bounds b, Vector3 dir)>();
            var sc = StampController.Instance;
            if (sc != null)
            {
                var sb = new List<Renderer>();
                if (sc.approveStamp != null) sb.AddRange(sc.approveStamp.GetComponentsInChildren<Renderer>());
                if (sc.rejectStamp != null) sb.AddRange(sc.rejectStamp.GetComponentsInChildren<Renderer>());
                if (sb.Count > 0) anchors.Add((Bounds(sb.ToArray()), -right));
            }
            anchors.Add((paper.bounds, -right));
            anchors.Add((paper.bounds, right));

            // Monitörün derinliği (oyuncudan uzaklığı): kupa onun hizasına gider
            float? monitorDepth = null;
            var mon = FindAnyObjectByType<MonitorView>();
            if (mon != null)
            {
                var mr = Largest(mon.transform);
                if (mr != null) monitorDepth = Vector3.Dot(mr.bounds.center, away);
            }

            float halfW = Extent(mb, right);
            float[] depths = { 0f, -0.05f, 0.05f, -0.10f, 0.10f, -0.15f, 0.15f };
            foreach (var (ab, dir) in anchors)
                foreach (float dz in depths)
                    for (float dx = 0f; dx <= 0.45f; dx += 0.015f)
                    {
                        float edge = Vector3.Dot(ab.center, dir) + Extent(ab, dir);
                        float along = edge + Gap + halfW + dx;
                        Vector3 center = ab.center + dir * (along - Vector3.Dot(ab.center, dir)) + away * dz;
                        center.y = deskY + mb.extents.y + 0.001f;
                        var test = new Bounds(center, mb.size + new Vector3(Gap, 0f, Gap));
                        if (Overlaps(test, blockers)) continue;
                        if (!OnDesk(test, desk, hasDesk, deskY)) continue; // masanın kenarından taşmasın / havada kalmasın
                        // Monitör hizasına kadar arkaya (oyuncudan uzağa) kaydır: boş ve masada kaldığı kadar
                        float want = monitorDepth.HasValue ? monitorDepth.Value - Vector3.Dot(center, away) : 0.15f;
                        for (float back = want; back > 0.005f; back -= 0.02f)
                        {
                            var t2 = test; t2.center += away * back;
                            if (Overlaps(t2, blockers) || !OnDesk(t2, desk, hasDesk, deskY)) continue;
                            center += away * back;
                            break;
                        }
                        mug.position = new Vector3(center.x - pivotToCenter.x, deskY - baseOffset + 0.0005f, center.z - pivotToCenter.z);
                        Debug.Log($"[Kupa] Masaya kondu: {mug.position} (masa bulundu: {hasDesk})");
                        MakeDraggable();
                        return;
                    }
            // Boş yer bulunamadıysa: mühürlerin hemen soluna koy (en sık boş kalan yer)
            var fb = anchors[0].b;
            Vector3 fc = fb.center - right * (Extent(fb, right) + Gap + halfW);
            if (monitorDepth.HasValue) fc += away * Mathf.Max(0f, monitorDepth.Value - Vector3.Dot(fc, away));
            mug.position = new Vector3(fc.x - pivotToCenter.x, deskY - baseOffset + 0.0005f, fc.z - pivotToCenter.z);
            Debug.LogWarning($"[Kupa] Kontrollü boş yer bulunamadı, mühürlerin soluna kondu: {mug.position}");
            MakeDraggable();
        }

        // ------------------------------------------------------------------ sürükle-bırak

        const float Lift = 0.12f;         // tutarken masadan bu kadar kalkar: diğer nesnelerin üstünden rahatça geçer
        Renderer[] mugRends;
        Renderer paperR;
        Bounds deskBounds;
        bool deskFound;
        float deskTopY, mugBaseOffset;
        Vector3 mugSize, mugPivotToCenter, grabOffset, lastFree;
        Interactable handle;
        DeskInteractor interactor;
        List<Bounds> dragBlockers;
        bool dragging;
        Vector3 homeCenter;

        void OnEnable() => GameEvents.CustomerArrived += OnCustomerArrived;
        void OnDisable() => GameEvents.CustomerArrived -= OnCustomerArrived;

        /// Müşterinin dosyası masaya gelince kupa dosyanın yerindeyse kenara (ilk konduğu yere) çekilir
        void OnCustomerArrived(CustomerData c) { if (mug != null && handle != null) StartCoroutine(ClearFolderSpot()); }

        IEnumerator ClearFolderSpot()
        {
            yield return new WaitForSeconds(0.6f); // dosya kayıp yerine otursun
            var fv = FolderView.Instance;
            if (fv == null || dragging) yield break;
            var rs = fv.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) yield break;
            var fb = Bounds(rs);
            var mine = new Bounds(CurrentCenter(), mugSize);
            bool hit = mine.min.x < fb.max.x && mine.max.x > fb.min.x && mine.min.z < fb.max.z && mine.max.z > fb.min.z;
            if (hit) SetCenter(homeCenter, 0f);
        }

        void MakeDraggable()
        {
            if (mug == null) return;
            homeCenter = CurrentCenter();
            // Tıklama için kutu collider (modelin kendi mesh'ine oturur)
            var mf = mug.GetComponentInChildren<MeshFilter>();
            var host = mf != null ? mf.gameObject : mug.gameObject;
            if (host.GetComponent<Collider>() == null) host.AddComponent<BoxCollider>();
            handle = mug.gameObject.AddComponent<Interactable>();
            handle.clickableIn = new[] { FocusState.Desk };
            handle.prompt = "Basılı tut: taşı";
            handle.highlightBoost = 1.12f;
            handle.CacheRenderers();
        }

        void Update()
        {
            if (mug == null || handle == null) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var fc = FocusController.Instance;
            var gm = GameManager.Instance;
            bool canUse = mouse != null && fc != null && fc.State == FocusState.Desk && !fc.IsTransitioning
                          && (gm == null || gm.DeskInputEnabled) && !PauseMenu.IsPaused && !GuidedTour.Running;

            if (!dragging)
            {
                if (!canUse || !mouse.leftButton.wasPressedThisFrame) return;
                if (interactor == null && Camera.main != null) interactor = Camera.main.GetComponent<DeskInteractor>();
                if (interactor == null || interactor.Hovered != handle) return;
                if (!PointOnDesk(out var p)) return;
                var c = CurrentCenter();
                grabOffset = new Vector3(c.x - p.x, 0f, c.z - p.z);
                dragBlockers = DragBlockers(); // sadece o an masada görünen nesneler
                dragging = true;
                lastFree = c;
                SetCenter(c, Lift);
                return;
            }

            if (!canUse || !mouse.leftButton.isPressed)
            {
                // Bırak: altı boşsa oraya, bir nesnenin üstündeyse en son boş yere iner
                var drop = CurrentCenter();
                if (Overlaps(new Bounds(drop, mugSize + new Vector3(0.01f, 0f, 0.01f)), dragBlockers)) drop = lastFree;
                SetCenter(drop, 0f);
                dragging = false;
                return;
            }
            if (!PointOnDesk(out var hit)) return;
            var want = hit + grabOffset;
            want.y = deskTopY + mugSize.y * 0.5f + 0.001f;
            if (deskFound) ClampToSurface(ref want);
            // Havadayken serbestçe gezer (nesnelerin üstünden geçer); bırakılabilecek son boş yer hatırlanır
            var test = new Bounds(want, mugSize + new Vector3(0.01f, 0f, 0.01f));
            if (!Overlaps(test, dragBlockers)) lastFree = want;
            SetCenter(want, Lift);
        }

        /// Masanın düz üst yüzü: modelin kutusundan kenar payları çıkarılır. Oyuncu tarafındaki ön kenarda
        /// yükseltilmiş pervaz var (kupa oraya konunca altı pervazın içinde kalıyordu), o yüzden orada pay daha büyük.
        const float EdgeMargin = 0.06f, FrontMargin = 0.11f;

        void ClampToSurface(ref Vector3 want)
        {
            float minX = deskBounds.min.x + EdgeMargin, maxX = deskBounds.max.x - EdgeMargin;
            float minZ = deskBounds.min.z + EdgeMargin, maxZ = deskBounds.max.z - EdgeMargin;
            var cam = Camera.main;
            if (cam != null)
            {
                var d = cam.transform.position - deskBounds.center;
                if (Mathf.Abs(d.x) > Mathf.Abs(d.z)) { if (d.x > 0f) maxX = deskBounds.max.x - FrontMargin; else minX = deskBounds.min.x + FrontMargin; }
                else { if (d.z > 0f) maxZ = deskBounds.max.z - FrontMargin; else minZ = deskBounds.min.z + FrontMargin; }
            }
            float hx = mugSize.x * 0.5f, hz = mugSize.z * 0.5f;
            if (maxX - minX > 2f * hx) want.x = Mathf.Clamp(want.x, minX + hx, maxX - hx);
            if (maxZ - minZ > 2f * hz) want.z = Mathf.Clamp(want.z, minZ + hz, maxZ - hz);
        }

        /// Nişan / imleç ışınının masa yüzeyiyle kesiştiği nokta
        bool PointOnDesk(out Vector3 p)
        {
            p = default;
            var cam = Camera.main;
            if (cam == null) return false;
            var ray = CursorController.PointerRay(cam);
            var plane = new Plane(Vector3.up, new Vector3(0f, deskTopY + mugSize.y * 0.5f + Lift, 0f)); // havadaki kupanın yüksekliği: nişanın altında kalır
            if (!plane.Raycast(ray, out float d) || d > 6f) return false;
            p = ray.GetPoint(d);
            return true;
        }

        Vector3 CurrentCenter()
        {
            var c = mug.position + mugPivotToCenter;
            c.y = deskTopY + mugSize.y * 0.5f + 0.001f;
            return c;
        }

        void SetCenter(Vector3 center, float lift)
        {
            mug.position = new Vector3(center.x - mugPivotToCenter.x, deskTopY - mugBaseOffset + 0.0005f + lift, center.z - mugPivotToCenter.z);
        }

        /// O an masada görünen nesneler: kupa onların içine bırakılamaz (gizli dosyanın yeri boş sayılır; dosya gelince kupa kenara çekilir)
        List<Bounds> DragBlockers()
        {
            var list = new List<Bounds>();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || r.transform.IsChildOf(mug)) continue; // görünmeyenler (gizli dosya, kapatılan cam) sayılmaz
                var b = WorldBounds(r);
                if (b.size.x > 1.2f || b.size.z > 1.2f) continue;                       // oda, masa, duvar
                if (b.max.y < deskTopY + 0.002f || b.min.y > deskTopY + 0.6f) continue; // masanın altında / çok yukarıda
                if (b.size.sqrMagnitude < 1e-8f) continue;
                if (r.name == "Back" || r.name == "Halo") continue;                       // parıltı çerçevesinin kopyaları
                list.Add(b);
            }
            return list;
        }

        /// Gizli nesnelerde de çalışan dünya kutusu (mesh kutusunun 8 köşesi)
        static Bounds WorldBounds(MeshRenderer r)
        {
            if (r.gameObject.activeInHierarchy) return r.bounds;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return new Bounds(r.transform.position, Vector3.zero);
            var lb = mf.sharedMesh.bounds;
            var m = r.transform.localToWorldMatrix;
            var b = new Bounds(m.MultiplyPoint3x4(lb.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                b.Encapsulate(m.MultiplyPoint3x4(corner));
            }
            return b;
        }

        /// Masanın üst yüzü: gazetenin altındaki büyük nesne (masa modeli)
        static Bounds DeskTop(Renderer paper, out bool found)
        {
            found = false;
            float deskY = paper.bounds.min.y;
            var pc = paper.bounds.center;
            Bounds best = default;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled) continue;
                var b = r.bounds;
                if (b.size.x < 0.6f && b.size.z < 0.6f) continue;
                if (b.max.y < deskY - 0.03f || b.max.y > deskY + 0.015f) continue;      // üst yüzü gazetenin hizasında
                if (pc.x < b.min.x || pc.x > b.max.x || pc.z < b.min.z || pc.z > b.max.z) continue;
                if (!found || b.size.x * b.size.z < best.size.x * best.size.z) { best = b; found = true; } // en dar olan (oda zemini değil)
            }
            return best;
        }

        /// Sürükleme alanı: masa modelinin kendisi (adında "Desk" geçen, üst yüzü gazetenin hizasında en büyük nesne)
        static Bounds DragDesk(Renderer paper, out bool found)
        {
            found = false;
            float deskY = paper.bounds.min.y;
            var pc = paper.bounds.center;
            Bounds best = default, any = default;
            bool anyFound = false;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled) continue;
                var b = r.bounds;
                if (b.size.x < 0.6f && b.size.z < 0.6f) continue;
                if (b.size.x > 3.5f || b.size.z > 3.5f) continue;                         // oda zemini / duvar
                if (b.max.y < deskY - 0.04f || b.max.y > deskY + 0.02f) continue;
                if (pc.x < b.min.x || pc.x > b.max.x || pc.z < b.min.z || pc.z > b.max.z) continue;
                float area = b.size.x * b.size.z;
                string n = r.name.ToLowerInvariant();
                if (n.Contains("desk") && !n.Contains("mug") && (!found || area > best.size.x * best.size.z)) { best = b; found = true; }
                if (!anyFound || area > any.size.x * any.size.z) { any = b; anyFound = true; }
            }
            if (!found && anyFound) { best = any; found = true; }
            if (found) Debug.Log($"[Kupa] Sürükleme alanı: {best.size.x:0.00} x {best.size.z:0.00} m");
            return best;
        }

        /// Kupanın tabanı tamamen masanın üstünde mi (4 köşe + orta: aşağı ışın da masaya değmeli)
        static bool OnDesk(Bounds test, Bounds desk, bool hasDesk, float deskY)
        {
            const float margin = 0.03f;
            if (hasDesk && (test.min.x < desk.min.x + margin || test.max.x > desk.max.x - margin ||
                            test.min.z < desk.min.z + margin || test.max.z > desk.max.z - margin)) return false;
            return true;
        }

        /// Masadaki nesneler (gizliler dahil). Oda, masa gibi büyük şeyler ve kupanın kendisi hariç.
        static List<Bounds> Blockers(Renderer paper, Transform self)
        {
            var list = new List<Bounds>();
            float deskY = paper.bounds.min.y;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || r.transform.IsChildOf(self) || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.x > 1.2f || b.size.z > 1.2f) continue;            // oda, masa, duvar
                if (b.max.y < deskY + 0.002f || b.min.y > deskY + 0.6f) continue; // masanın altında / çok yukarıda
                if (b.size.sqrMagnitude < 1e-8f) continue;
                list.Add(b);
            }
            return list;
        }

        static bool Overlaps(Bounds a, List<Bounds> others)
        {
            foreach (var b in others)
                if (a.min.x < b.max.x && a.max.x > b.min.x && a.min.z < b.max.z && a.max.z > b.min.z && a.min.y < b.max.y && a.max.y > b.min.y)
                    return true;
            return false;
        }

        static float Extent(Bounds b, Vector3 dir) =>
            Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.y) * b.extents.y + Mathf.Abs(dir.z) * b.extents.z;

        static Bounds Bounds(Renderer[] rends)
        {
            var b = new Bounds();
            bool any = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static Renderer Largest(Transform root)
        {
            Renderer best = null;
            float area = 0f;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                var s = r.bounds.size;
                if (s.x * s.z > area) { area = s.x * s.z; best = r; }
            }
            return best;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        static Material MugMaterial()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return null;
            var m = new Material(sh) { name = "M_JamItem_1_Mug (runtime)" };
            var tex = Resources.Load<Texture2D>("Props/T_JamItem_1_Mug");
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            m.SetColor("_BaseColor", Color.white);
            // Parlaklık orta: çok parlakken alt kısmı karanlık ortam yansımasıyla siyah görünüyordu
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.4f);
            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            // Hafif kendi ışığı: alt kısım masanın gölgesinde kararmasın (porselen beyaz kalsın)
            if (tex != null && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", new Color(0.32f, 0.32f, 0.32f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }
    }
}
