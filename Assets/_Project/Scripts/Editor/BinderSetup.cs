using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/6 - Kural klasörünü kur"
    /// Desk/RuleBinder'ın üstüne okunabilir bir sayfa (dünya uzayı Canvas, 1 birim = 1 mm) kurar.
    public static class BinderSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";

        static readonly Color Paper = new Color(0.97f, 0.96f, 0.92f);
        static readonly Color Ink = new Color(0.12f, 0.12f, 0.13f);
        static readonly Color Muted = new Color(0.42f, 0.42f, 0.42f);
        static readonly Color Line = new Color(0.25f, 0.25f, 0.27f);
        static readonly Color Hole = new Color(0.55f, 0.55f, 0.58f);
        static readonly Color Orange = new Color(0.94f, 0.48f, 0.10f);

        [MenuItem("Load Bearing/6 - Kural klasörünü kur", priority = 6)]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var binderGo = GameObject.Find("Desk/RuleBinder");
            if (binderGo == null) { Debug.LogError("Desk/RuleBinder bulunamadı. Önce menü 2 ile sahneyi kur."); return; }
            var binder = binderGo.transform;

            // Temizle (tekrar çalıştırılabilir)
            var old = binder.Find("Page");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var oldView = binderGo.GetComponent<BinderView>();
            if (oldView != null) Object.DestroyImmediate(oldView);
            foreach (var n in new[] { "CoverHinge", "AlertFrame", "AlertLight" })
            {
                var o = binder.Find(n);
                if (o != null) Object.DestroyImmediate(o.gameObject);
            }

            // Biraz sağa (ve haritaya binmesin diye biraz öne) al
            binder.position = new Vector3(-0.42f, 0.75f, -0.24f);

            // Sayfa canvas'ı: klasör kutusunun üst yüzü 0.05 m yükseklikte
            var pageGo = new GameObject("Page", typeof(RectTransform), typeof(Canvas));
            var page = (RectTransform)pageGo.transform;
            page.SetParent(binder, false);
            page.localPosition = new Vector3(0f, 0.0506f, 0f);
            page.localRotation = Quaternion.Euler(90f, 0f, 0f);
            page.localScale = Vector3.one * 0.001f;
            page.sizeDelta = new Vector2(205f, 285f);
            pageGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            // Sayfa çevirirken bu döner (sol kenardan, halka tarafından)
            var pageRoot = Rect("PageRoot", page);
            Stretch(pageRoot);
            pageRoot.pivot = new Vector2(0f, 0.5f);
            pageRoot.anchoredPosition = new Vector2(-102.5f, 0f);
            pageRoot.offsetMin = Vector2.zero; pageRoot.offsetMax = Vector2.zero;
            Img(pageRoot, Paper);

            // Halka delikleri (sol kenar)
            for (int i = 0; i < 3; i++)
            {
                var h = Rect($"Hole_{i}", pageRoot);
                h.anchorMin = h.anchorMax = new Vector2(0f, 0.5f);
                h.pivot = new Vector2(0.5f, 0.5f);
                h.anchoredPosition = new Vector2(9f, (i - 1) * 80f);
                h.sizeDelta = new Vector2(7f, 7f);
                var img = Img(h, Hole);
                img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            }

            var view = binderGo.AddComponent<BinderView>();
            view.pageRoot = pageRoot;

            TopText("Header", pageRoot, "KARAOVA YAPI DENETİM · KURALLAR", 6.2f, Muted, 22, -12, 160, FontStyles.Bold);
            view.title = TopText("Title", pageRoot, "Kural", 13f, Ink, 22, -24, 165, FontStyles.Bold);
            view.title.enableAutoSizing = true; view.title.fontSizeMin = 9f; view.title.fontSizeMax = 13f;
            HLine(pageRoot, -46f);

            var body = Text("Body", pageRoot, "", 8.5f, Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            var brt = body.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(22, 26); brt.offsetMax = new Vector2(-14, -54);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            body.enableAutoSizing = true; body.fontSizeMin = 6f; body.fontSizeMax = 8.5f;
            body.lineSpacing = 8f;
            view.body = body;

            var pageNo = Text("PageNo", pageRoot, "Sayfa 1 / 2", 6.5f, Muted, FontStyles.Bold, TextAlignmentOptions.Bottom);
            var prt = pageNo.rectTransform;
            prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0); prt.pivot = new Vector2(0.5f, 0);
            prt.offsetMin = new Vector2(22, 8); prt.offsetMax = new Vector2(-14, 20);
            view.pageNo = pageNo;

            // YENİ etiketi (turuncu, hafif eğik)
            var sticker = Rect("NewSticker", pageRoot);
            sticker.anchorMin = sticker.anchorMax = new Vector2(1f, 1f);
            sticker.pivot = new Vector2(1f, 1f);
            sticker.anchoredPosition = new Vector2(-6f, -6f);
            sticker.sizeDelta = new Vector2(34f, 13f);
            sticker.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Img(sticker, Orange);
            var st = Text("Label", sticker, "YENİ", 7.5f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(st.rectTransform);
            view.newSticker = sticker.gameObject;
            sticker.gameObject.SetActive(false);

            // ---- Kapak: sol kenardaki menteşeden açılır
            var hinge = new GameObject("CoverHinge").transform;
            hinge.SetParent(binder, false);
            hinge.localPosition = new Vector3(-0.113f, 0.054f, 0f);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "Cover";
            cover.transform.SetParent(hinge, false);
            cover.transform.localPosition = new Vector3(0.113f, 0f, 0f);
            cover.transform.localScale = new Vector3(0.226f, 0.006f, 0.306f);
            var binderMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Greybox/M_GB_Binder.mat");
            if (binderMat != null) cover.GetComponent<Renderer>().sharedMaterial = binderMat;

            // Kapak etiketi
            var labelGo = new GameObject("CoverLabel", typeof(RectTransform), typeof(Canvas));
            var label = (RectTransform)labelGo.transform;
            label.SetParent(hinge, false);
            label.localPosition = new Vector3(0.113f, 0.0032f, 0f);
            label.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label.localScale = Vector3.one * 0.001f;
            label.sizeDelta = new Vector2(205f, 285f);
            labelGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var plate = Rect("Plate", label);
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0.62f);
            plate.sizeDelta = new Vector2(130f, 52f);
            Img(plate, Paper);
            var plateText = Text("Title", plate, "KURALLAR", 17f, Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(plateText.rectTransform);
            plateText.characterSpacing = 6f;
            var sub = Text("Sub", label, "KARAOVA YAPI DENETİM OFİSİ", 6.5f, new Color(0.85f, 0.87f, 0.92f), FontStyles.Bold, TextAlignmentOptions.Center);
            sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0.5f, 0.48f);
            sub.rectTransform.sizeDelta = new Vector2(180f, 12f);
            sub.characterSpacing = 3f;

            // ---- Yeni kural uyarısı: klasörün etrafında ince beyaz çerçeve (BinderView parlatır)
            var glowMat = GlowMaterial();
            var frame = new GameObject("AlertFrame").transform;
            frame.SetParent(binder, false);
            const float frameH = 0.064f, frameT = 0.004f;
            Bar("Left", frame, new Vector3(-0.118f, frameH * 0.5f, 0f), new Vector3(frameT, frameH, 0.320f), glowMat);
            Bar("Right", frame, new Vector3(0.118f, frameH * 0.5f, 0f), new Vector3(frameT, frameH, 0.320f), glowMat);
            Bar("Front", frame, new Vector3(0f, frameH * 0.5f, -0.158f), new Vector3(0.240f, frameH, frameT), glowMat);
            Bar("Back", frame, new Vector3(0f, frameH * 0.5f, 0.158f), new Vector3(0.240f, frameH, frameT), glowMat);
            frame.gameObject.SetActive(false);

            view.coverHinge = hinge;
            view.alertFrame = frame.gameObject;

            // Yakın bakış: tam üstten
            var fp = binder.Find("FocusPoint_Binder");
            if (fp != null)
            {
                var center = binder.position + Vector3.up * 0.05f;
                fp.position = center + new Vector3(0f, 0.32f, -0.05f);
                fp.rotation = Quaternion.LookRotation(center - fp.position, Vector3.up);
            }

            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=lime>[Load Bearing]</color> Kural klasörü kuruldu. Klasöre bak: tekerlek / sol-sağ ok ile sayfa çevir.");
        }

        // ---------------- yardımcılar ----------------

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static Image Img(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string s, float size, Color c, FontStyles style, TextAlignmentOptions align)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.color = c; t.fontStyle = style; t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        static TextMeshProUGUI TopText(string name, Transform parent, string s, float size, Color c,
                                       float x, float y, float width, FontStyles style)
        {
            var t = Text(name, parent, s, size, c, style, TextAlignmentOptions.TopLeft);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, size * 1.6f);
            return t;
        }

        static void Bar(string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            Object.DestroyImmediate(b.GetComponent<Collider>()); // tıklamayı engellemesin
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos;
            b.transform.localScale = scale;
            var r = b.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Material GlowMaterial()
        {
            const string path = "Assets/_Project/Materials/Greybox/M_GB_AlertGlow.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            m = new Material(shader);
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.6f));
            m.SetFloat("_Surface", 1f);   // Transparent
            m.SetFloat("_Blend", 0f);     // Alpha
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void HLine(RectTransform parent, float y)
        {
            var l = Rect("Line", parent);
            l.anchorMin = new Vector2(0, 1); l.anchorMax = new Vector2(1, 1); l.pivot = new Vector2(0.5f, 1);
            l.offsetMin = new Vector2(22, y - 0.9f); l.offsetMax = new Vector2(-14, y);
            Img(l, Line);
        }
    }
}
