using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/7 - Kâğıt haritayı kur"
    /// Desk/Map üstüne greybox Karaova imar paftası çizer (tekrar çalıştırılabilir):
    ///  - 24 ada (6 sütun x 4 satır), her ada 4 x 5 = 20 parsel, parseller soldan sağa / yukarıdan aşağı numaralı
    ///  - Kuzey Fayı (kırmızı çizgi), Kara Dere (mavi), taşkın alanı (mavi tarama)
    ///  - Müşterilerin CustomerData.mapPixel değerlerini kendi parsel merkezlerine yazar
    /// Koordinatlar harita PİKSELİ: 4096 x 2730, sol üst = (0,0). Tasarım ekibi gerçek harita görselini
    /// aynı ölçüde çizerse bu yerleşimi altlık olarak kullanabilir.
    public static class MapSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";
        const float W = 4096f, H = 2730f;

        // Ada ızgarası
        const int Cols = 6, Rows = 4;
        const float BlockX0 = 170f, BlockY0 = 320f, BlockW = 520f, BlockH = 450f, StepX = 650f, StepY = 580f;
        const int PCols = 4, PRows = 5;          // ada içi parsel ızgarası
        const float PW = BlockW / PCols, PH = BlockH / PRows;

        // Renkler
        static readonly Color Land = new Color(0.89f, 0.91f, 0.85f);
        static readonly Color BlockFill = new Color(0.95f, 0.93f, 0.86f);
        static readonly Color BlockEdge = new Color(0.33f, 0.31f, 0.28f);
        static readonly Color ParcelLine = new Color(0.62f, 0.59f, 0.53f);
        static readonly Color Ink = new Color(0.16f, 0.15f, 0.14f);
        static readonly Color Muted = new Color(0.42f, 0.40f, 0.37f);
        static readonly Color FaultRed = new Color(0.82f, 0.12f, 0.10f);
        static readonly Color River = new Color(0.20f, 0.42f, 0.78f);
        static readonly Color FloodFill = new Color(0.25f, 0.50f, 0.90f, 0.16f);
        static readonly Color FloodHatch = new Color(0.20f, 0.42f, 0.85f, 0.55f);

        // Tehlikeler (piksel)
        static readonly Vector2[] FaultLine =
        {
            new Vector2(0, 520), new Vector2(330, 330), new Vector2(700, 230), new Vector2(1500, 190),
            new Vector2(2400, 230), new Vector2(3300, 170), new Vector2(4096, 210),
        };
        const float RiverX = 2055f, RiverY = 1415f, RiverWidth = 44f;
        static readonly Rect FloodZone = new Rect(1850f, 0f, 410f, 820f); // x, y(üstten), w, h

        [MenuItem("Load Bearing/7 - Kâğıt haritayı kur", priority = 7)]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var mapGo = GameObject.Find("Desk/Map");
            if (mapGo == null) { Debug.LogError("Desk/Map bulunamadı. Önce menü 2 ile sahneyi kur."); return; }
            var map = mapGo.transform;

            var old = map.Find("MapCanvas");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var oldView = mapGo.GetComponent<MapView>();
            if (oldView != null) Object.DestroyImmediate(oldView);

            // Canvas: 4096 x 2730 piksel = 0.6 x 0.4 m
            var cgo = new GameObject("MapCanvas", typeof(RectTransform), typeof(Canvas));
            var canvas = (RectTransform)cgo.transform;
            canvas.SetParent(map, false);
            canvas.localPosition = new Vector3(0f, 0.0056f, 0f);
            canvas.localRotation = Quaternion.Euler(90f, 0f, 0f);
            canvas.localScale = Vector3.one * (0.6f / W);
            canvas.sizeDelta = new Vector2(W, H);
            cgo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            // Zemin
            Box("Land", canvas, 0, 0, W, H, Land);

            // Dere
            Box("River_N", canvas, RiverX - RiverWidth / 2, 0, RiverWidth, RiverY + RiverWidth / 2, River);
            Box("River_W", canvas, 0, RiverY - RiverWidth / 2, RiverX + RiverWidth / 2, RiverWidth, River);

            // Adalar
            var adas = new GameObject("Adas", typeof(RectTransform)).transform as RectTransform;
            adas.SetParent(canvas, false); Full(adas);
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    BuildAda(adas, r * Cols + c + 1, BlockX0 + c * StepX, BlockY0 + r * StepY);

            // Taşkın alanı: adaların ÜSTÜNE yarı saydam mavi + çizgili tarama (altındaki parseller okunur)
            BuildFloodZone(canvas);
            BuildHatch(canvas, "FloodZone_Hatch", FloodZone, FloodHatch);

            // Fay (en üstte)
            for (int i = 0; i < FaultLine.Length - 1; i++)
                Segment($"Fault_{i}", canvas, FaultLine[i], FaultLine[i + 1], 18f, FaultRed);

            // Yazılar
            Label("FaultLabel", canvas, "KUZEY FAYI", 1250, 70, 600, 70, 54f, FaultRed, FontStyles.Bold, 10f);
            Label("RiverLabel", canvas, "KARA DERE", 700, RiverY - 22, 500, 44, 34f, Color.white, FontStyles.Bold, 8f);
            Label("RiverLabelN", canvas, "KARA DERE", RiverX + 40, 860, 400, 50, 38f, River, FontStyles.Bold, 6f);
            Label("FloodLabel", canvas, "TAŞKIN ALANI", FloodZone.x + 20, FloodZone.y + 20, 380, 50, 38f, River, FontStyles.Bold, 4f);
            Label("Title", canvas, "KARAOVA İMAR PAFTASI", 170, 2560, 1400, 80, 64f, Ink, FontStyles.Bold, 8f);
            Label("Scale", canvas, "Ölçek 1:5000  ·  Yapı Denetim Ofisi", 170, 2645, 1400, 50, 34f, Muted, FontStyles.Normal, 2f);
            BuildLegend(canvas);

            // Bileşen
            var view = mapGo.AddComponent<MapView>();
            view.mapCanvas = canvas;

            // Yakın bakış: haritanın neredeyse tam üstünden, tamamı görünsün
            var fp = map.Find("FocusPoint_Map");
            if (fp != null)
            {
                var center = map.position + Vector3.up * 0.006f;
                fp.position = center + new Vector3(0f, 0.42f, -0.06f);
                fp.rotation = Quaternion.LookRotation(center - fp.position, Vector3.up);
            }

            WriteCustomerPixels();

            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=lime>[Load Bearing]</color> Harita kuruldu. Haritaya bak: tekerlek = yakınlaştır, sol tık sürükle = kaydır.");
        }

        // ------------------------------------------------------------------ parçalar

        static void BuildAda(RectTransform parent, int adaNo, float x, float y)
        {
            var root = new GameObject($"Ada_{adaNo}", typeof(RectTransform)).transform as RectTransform;
            root.SetParent(parent, false); Full(root);

            Box("Edge", root, x - 6, y - 6, BlockW + 12, BlockH + 12, BlockEdge);
            Box("Fill", root, x, y, BlockW, BlockH, BlockFill);
            for (int c = 1; c < PCols; c++) Box($"V{c}", root, x + c * PW - 1, y, 2, BlockH, ParcelLine);
            for (int r = 1; r < PRows; r++) Box($"H{r}", root, x, y + r * PH - 1, BlockW, 2, ParcelLine);

            // Parsel numaraları: tek bir yazı, <pos> ile sütunlara dizilmiş
            var sb = new StringBuilder($"<line-height={(int)PH}>");
            for (int r = 0; r < PRows; r++)
            {
                if (r > 0) sb.Append('\n');
                for (int c = 0; c < PCols; c++)
                    sb.Append($"<pos={(int)(c * PW + PW * 0.5f - 14f)}>{r * PCols + c + 1}");
            }
            var nums = Label("Parcels", root, sb.ToString(), x, y, BlockW, BlockH, 30f, Muted, FontStyles.Normal, 0f);
            nums.richText = true;
            nums.margin = new Vector4(0f, PH * 0.5f - 16f, 0f, 0f);
            nums.textWrappingMode = TextWrappingModes.NoWrap;

            Label("AdaTag", root, $"ADA {adaNo}", x - 4, y - 66, 320, 56, 44f, Ink, FontStyles.Bold, 4f);
        }

        static void BuildFloodZone(RectTransform canvas)
        {
            Box("FloodZone_Fill", canvas, FloodZone.x, FloodZone.y, FloodZone.width, FloodZone.height, FloodFill);
            // Kenar çizgisi
            var r = FloodZone; const float t = 6f;
            Box("FloodEdge_L", canvas, r.x, r.y, t, r.height, FloodHatch);
            Box("FloodEdge_R", canvas, r.xMax - t, r.y, t, r.height, FloodHatch);
            Box("FloodEdge_B", canvas, r.x, r.yMax - t, r.width, t, FloodHatch);
        }

        /// Dikdörtgen içine 45° çizgili tarama (RectMask2D ile kırpılır)
        static RectTransform BuildHatch(RectTransform canvas, string name, Rect zone, Color color)
        {
            var z = Box(name, canvas, zone.x, zone.y, zone.width, zone.height, new Color(0, 0, 0, 0));
            z.gameObject.AddComponent<RectMask2D>();
            float diag = zone.width + zone.height;
            const float spacing = 46f, thick = 7f;
            int i = 0;
            for (float cpos = -zone.height; cpos < zone.width; cpos += spacing, i++)
            {
                var bar = new GameObject($"Hatch_{i}", typeof(RectTransform), typeof(Image)).transform as RectTransform;
                bar.SetParent(z, false);
                bar.anchorMin = bar.anchorMax = Vector2.zero;
                bar.pivot = new Vector2(0.5f, 0.5f);
                bar.anchoredPosition = new Vector2(cpos + zone.height * 0.5f, zone.height * 0.5f);
                bar.sizeDelta = new Vector2(diag * 1.5f, thick);
                bar.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var img = bar.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
            }
            return z;
        }

        static void BuildLegend(RectTransform canvas)
        {
            float x = 3000f, y = 2540f;
            Box("Legend_BG", canvas, x - 20, y - 16, 1060, 176, new Color(1f, 1f, 1f, 0.55f));
            Box("Legend_Fault", canvas, x, y + 22, 120, 18, FaultRed);
            Label("Legend_FaultText", canvas, "Kuzey Fayı — yapı yasak", x + 150, y, 880, 60, 38f, Ink, FontStyles.Normal, 0f);
            Box("Legend_Flood", canvas, x, y + 86, 120, 50, new Color(0.25f, 0.50f, 0.90f, 0.35f));
            Label("Legend_FloodText", canvas, "Taşkın alanı — yapı yasak", x + 150, y + 82, 880, 60, 38f, Ink, FontStyles.Normal, 0f);
        }

        static void WriteCustomerPixels()
        {
            var guids = AssetDatabase.FindAssets("t:CustomerData", new[] { "Assets/_Project/Data/Customers" });
            foreach (var g in guids)
            {
                var c = AssetDatabase.LoadAssetAtPath<CustomerData>(AssetDatabase.GUIDToAssetPath(g));
                if (c == null || c.ada < 1 || c.ada > Rows * Cols || c.parsel < 1 || c.parsel > PRows * PCols) continue;
                int ai = c.ada - 1, pi = c.parsel - 1;
                float bx = BlockX0 + (ai % Cols) * StepX, by = BlockY0 + (ai / Cols) * StepY;
                c.mapPixel = new Vector2(bx + (pi % PCols) * PW + PW * 0.5f, by + (pi / PCols) * PH + PH * 0.5f);
                EditorUtility.SetDirty(c);
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ yardımcılar (piksel uzayı)

        static void Full(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// Sol üst köşesi (x,y) olan w x h kutu (y aşağı doğru)
        static RectTransform Box(string name, RectTransform parent, float x, float y, float w, float h, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = false;
            return rt;
        }

        static void Segment(string name, RectTransform parent, Vector2 a, Vector2 b, float thickness, Color c)
        {
            var pa = new Vector2(a.x, -a.y); var pb = new Vector2(b.x, -b.y);
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (pa + pb) * 0.5f;
            rt.sizeDelta = new Vector2(Vector2.Distance(pa, pb) + thickness, thickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg);
            var img = go.GetComponent<Image>(); img.color = c; img.raycastTarget = false;
        }

        static TextMeshProUGUI Label(string name, RectTransform parent, string s, float x, float y, float w, float h,
                                     float size, Color c, FontStyles style, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = s; t.fontSize = size; t.color = c; t.fontStyle = style;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.characterSpacing = spacing;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
