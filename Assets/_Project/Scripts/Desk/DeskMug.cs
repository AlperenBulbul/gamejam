using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LoadBearing
{
    /// Masadaki ODTÜ kupası (tasarımcı: SM_JamItem_1_Mug, 9,5 cm). Gazetenin sağına konur:
    ///  - Masadaki diğer nesnelerin (gizli olanlar da: müşteri gelince çıkan dosya vb.) kutularıyla çakışmayan
    ///    ilk boş yer aranır; hiçbir şeyin içine girmez.
    ///  - Kulp oyuncunun sağına, logo oyuncuya bakar.
    ///  - Collider yok: tıklamaları engellemez, sadece süs.
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
                        return;
                    }
            // Boş yer bulunamadıysa: mühürlerin hemen soluna koy (en sık boş kalan yer)
            var fb = anchors[0].b;
            Vector3 fc = fb.center - right * (Extent(fb, right) + Gap + halfW);
            if (monitorDepth.HasValue) fc += away * Mathf.Max(0f, monitorDepth.Value - Vector3.Dot(fc, away));
            mug.position = new Vector3(fc.x - pivotToCenter.x, deskY - baseOffset + 0.0005f, fc.z - pivotToCenter.z);
            Debug.LogWarning($"[Kupa] Kontrollü boş yer bulunamadı, mühürlerin soluna kondu: {mug.position}");
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
