using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/5 - Başvuru formunu kur"
    /// Desk/FolderSpot altına dosya + üstünde okunabilir başvuru formu kurar (tekrar çalıştırılabilir).
    /// Form, dünya uzayında bir Canvas: 1 birim = 1 mm (ölçek 0.001), A4'e yakın 220 x 300.
    public static class FolderSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";

        static readonly Color Paper = new Color(0.96f, 0.95f, 0.91f);
        static readonly Color Ink = new Color(0.12f, 0.12f, 0.13f);
        static readonly Color Muted = new Color(0.42f, 0.42f, 0.42f);
        static readonly Color BlueInk = new Color(0.12f, 0.24f, 0.55f);
        static readonly Color Line = new Color(0.25f, 0.25f, 0.27f);

        [MenuItem("Load Bearing/5 - Başvuru formunu kur", priority = 5)]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var spotGo = GameObject.Find("Desk/FolderSpot");
            if (spotGo == null) { Debug.LogError("Desk/FolderSpot bulunamadı. Önce menü 2 ile sahneyi kur."); return; }
            var spot = spotGo.transform;

            // Eski kurulumu temizle ama greybox 'Visual' kutusunu koru
            var oldContent = spot.Find("Content");
            var visual = spot.Find("Visual") ?? (oldContent != null ? oldContent.Find("Visual") : null);
            if (visual != null) visual.SetParent(spot, true);
            if (oldContent != null) Object.DestroyImmediate(oldContent.gameObject);
            var oldView = spotGo.GetComponent<FolderView>();
            if (oldView != null) Object.DestroyImmediate(oldView);

            // Content: dosyanın tamamı (müşteri yokken gizli)
            var content = new GameObject("Content").transform;
            content.SetParent(spot, false);
            if (visual != null) visual.SetParent(content, true);

            // Form canvas (masaya yatık, yazının üstü gişeye bakar)
            var formGo = new GameObject("Form", typeof(RectTransform), typeof(Canvas));
            var form = (RectTransform)formGo.transform;
            form.SetParent(content, false);
            form.localPosition = new Vector3(0f, 0.0156f, 0f);
            form.localRotation = Quaternion.Euler(90f, 0f, 0f);
            form.localScale = Vector3.one * 0.001f;
            form.sizeDelta = new Vector2(220f, 300f);
            formGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var paper = Rect("Paper", form); Stretch(paper);
            paper.gameObject.AddComponent<Image>().color = Paper;

            var view = spotGo.AddComponent<FolderView>();
            view.content = content.gameObject;
            view.interactable = spotGo.GetComponent<Interactable>();
            if (view.interactable != null) view.interactable.prompt = "İncele";

            // Başlık
            TopText("Org", form, "KARAOVA BELEDİYESİ · YAPI DENETİM", 6.5f, Muted, 12, -12, 150, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            view.appNo = TopText("AppNo", form, "No: 2026/C0", 6.5f, Muted, -12, -12, 80, FontStyles.Normal, TextAlignmentOptions.TopRight, rightAnchored: true);
            TopText("Title", form, "YAPI RUHSATI BAŞVURU FORMU", 11f, Ink, 12, -24, 196, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            HLine(form, -42f, 0.9f);

            // Satırlar
            string[] labels = { "Mal sahibi", "Ada / Parsel", "Arsa alanı", "İstenen kat", "Kullanım", "Mühendis kaşesi", "Zemin etüdü" };
            var values = new TMP_Text[labels.Length];
            float y = -52f;
            for (int i = 0; i < labels.Length; i++)
            {
                TopText($"L_{i}", form, labels[i].ToUpper(new System.Globalization.CultureInfo("tr-TR")), 6.2f, Muted, 12, y - 3f, 78, FontStyles.Bold, TextAlignmentOptions.TopLeft);
                var v = TopText($"V_{i}", form, "-", 11f, Ink, 92, y, 116, FontStyles.Bold, TextAlignmentOptions.TopLeft);
                v.enableAutoSizing = true; v.fontSizeMin = 6.5f; v.fontSizeMax = 11f;
                values[i] = v;
                HLine(form, y - 19f, 0.35f);
                y -= 23f;
            }
            view.owner = values[0]; view.adaParsel = values[1]; view.area = values[2]; view.floors = values[3];
            view.use = values[4]; view.license = values[5]; view.soil = values[6];

            // Mühendis kaşesi mavi mürekkep gibi
            view.license.color = BlueInk;
            view.license.fontStyle = FontStyles.Bold | FontStyles.Italic;

            // KARAR kutusu (Adım 6'da mühür buraya basılacak)
            var box = Rect("StampArea", form);
            box.anchorMin = new Vector2(0, 0); box.anchorMax = new Vector2(1, 0); box.pivot = new Vector2(0.5f, 0);
            box.offsetMin = new Vector2(12, 12); box.offsetMax = new Vector2(-12, 82);
            Border(box, 0.9f);
            var lbl = Text("Label", box, "KARAR", 6.2f, Muted, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            lbl.rectTransform.anchorMin = new Vector2(0, 1); lbl.rectTransform.anchorMax = new Vector2(0, 1);
            lbl.rectTransform.pivot = new Vector2(0, 1);
            lbl.rectTransform.anchoredPosition = new Vector2(5, -4); lbl.rectTransform.sizeDelta = new Vector2(60, 10);

            // Yakın bakış: dosyanın neredeyse tam üstünden (yazı düz okunsun)
            var fp = spot.Find("FocusPoint_Folder");
            if (fp != null)
            {
                var center = spot.position + Vector3.up * 0.016f;
                fp.position = center + new Vector3(0f, 0.33f, -0.05f);
                fp.rotation = Quaternion.LookRotation(center - fp.position, Vector3.up);
            }

            EditorUtility.SetDirty(view);
            if (view.interactable != null) EditorUtility.SetDirty(view.interactable);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=lime>[Load Bearing]</color> Başvuru formu kuruldu. Play > BAŞLA > ✓ > gazete > Shift+1..7 ile dosya getir.");
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

        static TextMeshProUGUI Text(string name, Transform parent, string s, float size, Color c, FontStyles style, TextAlignmentOptions align)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.color = c; t.fontStyle = style; t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        /// Formun üst kenarına göre yerleşen yazı. x: soldan (rightAnchored ise sağdan, negatif), y: üstten (negatif)
        static TextMeshProUGUI TopText(string name, Transform parent, string s, float size, Color c,
                                       float x, float y, float width, FontStyles style, TextAlignmentOptions align,
                                       bool rightAnchored = false)
        {
            var t = Text(name, parent, s, size, c, style, align);
            var rt = t.rectTransform;
            float ax = rightAnchored ? 1f : 0f;
            rt.anchorMin = rt.anchorMax = new Vector2(ax, 1f);
            rt.pivot = new Vector2(ax, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, size * 1.6f);
            return t;
        }

        static void HLine(RectTransform parent, float y, float thickness)
        {
            var l = Rect("Line", parent);
            l.anchorMin = new Vector2(0, 1); l.anchorMax = new Vector2(1, 1); l.pivot = new Vector2(0.5f, 1);
            l.offsetMin = new Vector2(12, y - thickness); l.offsetMax = new Vector2(-12, y);
            var img = l.gameObject.AddComponent<Image>();
            img.color = Line; img.raycastTarget = false;
        }

        static void Border(RectTransform box, float t)
        {
            void Side(string n, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
            {
                var s = Rect(n, box);
                s.anchorMin = aMin; s.anchorMax = aMax; s.offsetMin = oMin; s.offsetMax = oMax;
                var img = s.gameObject.AddComponent<Image>(); img.color = Line; img.raycastTarget = false;
            }
            Side("Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -t), Vector2.zero);
            Side("Bottom", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, t));
            Side("Left", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(t, 0));
            Side("Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-t, 0), Vector2.zero);
        }
    }
}
