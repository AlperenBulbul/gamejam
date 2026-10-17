using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/4 - Menü + Nasıl Oynanır + Tuş ipuçları kur"
    /// Office sahnesindeki UI objesini baştan kurar (tekrar çalıştırılabilir).
    public static class UISetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";

        static readonly Color Ink = new Color(0.07f, 0.075f, 0.08f, 0.92f);   // koyu zemin
        static readonly Color Paper = new Color(0.945f, 0.925f, 0.875f);       // kâğıt
        static readonly Color PaperInk = new Color(0.12f, 0.12f, 0.13f);       // kâğıt üstü yazı
        static readonly Color OffWhite = new Color(0.93f, 0.91f, 0.86f);
        static readonly Color Amber = new Color(0.95f, 0.70f, 0.20f);          // ana vurgu (inşaat sarısı)
        static readonly Color Slate = new Color(0.17f, 0.18f, 0.20f);
        static readonly Color Muted = new Color(0.55f, 0.55f, 0.55f);
        static readonly Color StampGreen = new Color(0.18f, 0.55f, 0.30f);

        [MenuItem("Load Bearing/4 - Menü + Nasıl Oynanır + Tuş ipuçları kur", priority = 4)]
        public static void Build()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                EditorUtility.DisplayDialog("TextMeshPro eksik",
                    "Önce: Window > TextMeshPro > Import TMP Essential Resources\nSonra bu menüyü tekrar çalıştır.", "Tamam");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // Eskileri temizle
            var oldUI = GameObject.Find("UI");
            if (oldUI != null) Object.DestroyImmediate(oldUI);
            var oldES = Object.FindAnyObjectByType<EventSystem>();
            if (oldES != null) Object.DestroyImmediate(oldES.gameObject);

            // EventSystem (yeni Input System ile)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // Canvas
            var ui = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var keyHints = BuildKeyHints(ui.transform);
            var howTo = BuildHowToPlay(ui.transform);
            var menu = BuildMainMenu(ui.transform, howTo);
            SpeechSetup.BuildSubtitle(ui.transform);     // altyazı (menü 10 ile aynı)
            CursorSetup.BuildCrosshair(ui.transform);    // crosshair (menü 11 ile aynı)
            var hover = BuildHoverPrompt(ui.transform); // en üstte çizilsin diye en son
            EditorUtility.SetDirty(hover);
            howTo.mainMenu = menu;
            EditorUtility.SetDirty(howTo);
            EditorUtility.SetDirty(menu);
            EditorUtility.SetDirty(keyHints);

            WireNewspaper();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=lime>[Load Bearing]</color> UI kuruldu: Ana menü, Nasıl Oynanır, tuş ipuçları. Gazete tıklaması güne başlatıyor.");
        }

        // ------------------------------------------------------------------ Ana menü

        static MainMenuUI BuildMainMenu(Transform parent, HowToPlayUI howTo)
        {
            var host = Rect("MainMenu", parent); Stretch(host);
            var comp = host.gameObject.AddComponent<MainMenuUI>();

            var panel = Rect("Panel", host); Stretch(panel);
            Img(panel, Ink);

            // ince sarı şerit (inşaat bandı hissi)
            var band = Rect("Band", panel);
            Place(band, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(140, 10));
            Img(band, Amber);

            var title = Text("Title", panel, "LOAD BEARING", 128, OffWhite, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1400, 160));
            title.characterSpacing = 6;

            var sub = Text("Subtitle", panel, "Karaova Yapı Denetim Ofisi", 34, Amber, TextAlignmentOptions.Center);
            Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1000, 60));
            sub.characterSpacing = 4;

            comp.startButton = Btn("StartButton", panel, "BAŞLA", Amber, PaperInk, 38, new Vector2(0, -90), new Vector2(380, 88));
            comp.quitButton = Btn("QuitButton", panel, "ÇIKIŞ", Slate, OffWhite, 28, new Vector2(0, -200), new Vector2(380, 70));

            var footer = Text("Footer", panel, "CIVIL GAME JAM 2026", 20, Muted, TextAlignmentOptions.Center);
            Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 36), new Vector2(800, 40));
            footer.characterSpacing = 8;

            comp.root = panel.gameObject;
            comp.howToPlay = howTo;
            return comp;
        }

        // ------------------------------------------------------------------ Nasıl Oynanır

        static HowToPlayUI BuildHowToPlay(Transform parent)
        {
            var host = Rect("HowToPlay", parent); Stretch(host);
            var comp = host.gameObject.AddComponent<HowToPlayUI>();

            var panel = Rect("Panel", host); Stretch(panel);
            Img(panel, Ink);

            var card = Rect("Card", panel);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1160, 720));
            Img(card, Paper);

            var tag = Text("Header", card, "NASIL OYNANIR", 24, new Color(0.62f, 0.42f, 0.05f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(tag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -44), new Vector2(600, 40));
            tag.characterSpacing = 8;

            comp.menuButton = Btn("MenuButton", card, "MENÜ  (Esc)", new Color(0, 0, 0, 0.06f), PaperInk, 20, Vector2.zero, new Vector2(170, 44));
            Place((RectTransform)comp.menuButton.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -36), new Vector2(170, 44));

            comp.titleText = Text("PageTitle", card, "Sayfa 1", 52, PaperInk, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Place(comp.titleText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -92), new Vector2(1050, 70));

            var rule = Rect("Rule", card);
            Place(rule, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -168), new Vector2(1048, 3));
            Img(rule, PaperInk);

            var imgRt = Rect("PageImage", card);
            imgRt.anchorMin = new Vector2(0, 1); imgRt.anchorMax = new Vector2(1, 1); imgRt.pivot = new Vector2(0.5f, 1);
            imgRt.offsetMin = new Vector2(56, -470); imgRt.offsetMax = new Vector2(-56, -190);
            comp.pageImage = Img(imgRt, Color.white);
            comp.pageImage.preserveAspect = true;
            imgRt.gameObject.SetActive(false);

            comp.bodyText = Text("PageBody", card, "", 30, PaperInk, TextAlignmentOptions.TopLeft);
            var bodyRt = comp.bodyText.rectTransform;
            bodyRt.anchorMin = Vector2.zero; bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(56, 120); bodyRt.offsetMax = new Vector2(-56, -190);
            comp.bodyText.textWrappingMode = TextWrappingModes.Normal;

            comp.prevButton = Btn("PrevButton", card, "< GERİ", Slate, OffWhite, 24, Vector2.zero, new Vector2(200, 64));
            Place((RectTransform)comp.prevButton.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(56, 40), new Vector2(200, 64));

            comp.pageCounter = Text("PageCounter", card, "1 / 4", 26, Muted, TextAlignmentOptions.Center, FontStyles.Bold);
            Place(comp.pageCounter.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(200, 40));

            comp.nextButton = Btn("NextButton", card, "İLERİ >", Slate, OffWhite, 24, Vector2.zero, new Vector2(200, 64));
            Place((RectTransform)comp.nextButton.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56, 40), new Vector2(200, 64));

            // ✓ BAŞLA (son sayfa) — tik işareti Unity'nin hazır Checkmark görseli (fontta ✓ olmayabilir)
            comp.doneButton = Btn("DoneButton", card, "BAŞLA", StampGreen, Color.white, 26, Vector2.zero, new Vector2(240, 64));
            var doneRt = (RectTransform)comp.doneButton.transform;
            Place(doneRt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56, 40), new Vector2(240, 64));
            var label = doneRt.GetComponentInChildren<TextMeshProUGUI>();
            label.rectTransform.offsetMin = new Vector2(56, 0);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var tick = Rect("Tick", doneRt);
            Place(tick, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(36, 36));
            var tickImg = Img(tick, Color.white);
            tickImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
            tickImg.raycastTarget = false;

            var hint = Text("KeysHint", panel, "A / D  sayfa      Enter  başla      Esc  menü", 20, Muted, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(900, 36));

            comp.root = panel.gameObject;
            return comp;
        }

        // ------------------------------------------------------------------ Tuş ipuçları

        static KeyHintsUI BuildKeyHints(Transform parent)
        {
            var host = Rect("KeyHints", parent); Stretch(host);
            var comp = host.gameObject.AddComponent<KeyHintsUI>();

            var panel = Rect("Panel", host);
            panel.anchorMin = panel.anchorMax = new Vector2(1, 0);
            panel.pivot = new Vector2(1, 0);
            panel.anchoredPosition = new Vector2(-24, 24);
            var bg = Img(panel, new Color(0, 0, 0, 0.55f));
            bg.raycastTarget = false;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 12, 12);
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            comp.text = Text("Text", panel, "<b>Sol tık</b>  Seç", 22, OffWhite, TextAlignmentOptions.TopLeft);
            comp.text.lineSpacing = 12;
            comp.text.textWrappingMode = TextWrappingModes.NoWrap;

            comp.root = panel.gameObject;
            comp.keyColor = Amber;
            return comp;
        }

        // ------------------------------------------------------------------ İmleç ipucu (fare simgesi + "Aç")

        static HoverPromptUI BuildHoverPrompt(Transform parent)
        {
            var host = Rect("HoverPrompt", parent); Stretch(host);
            var comp = host.gameObject.AddComponent<HoverPromptUI>();
            var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var panel = Rect("Panel", host);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0, 1); // sol üst köşesi imlecin yanında
            var bg = Img(panel, new Color(0.05f, 0.05f, 0.06f, 0.82f));
            bg.sprite = rounded; bg.type = Image.Type.Sliced; bg.raycastTarget = false;

            var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 16, 7, 7);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Fare simgesi: gövde + vurgulu sol tuş + çizgiler
            var mouse = Rect("MouseIcon", panel);
            var le = mouse.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = 22; le.minHeight = le.preferredHeight = 32;
            var body = Img(mouse, OffWhite);
            body.sprite = rounded; body.type = Image.Type.Sliced; body.raycastTarget = false;

            var left = Rect("LeftButton", mouse);
            Place(left, new Vector2(0, 1), new Vector2(0, 1), new Vector2(2, -2), new Vector2(9, 12));
            var leftImg = Img(left, Amber); leftImg.raycastTarget = false;

            var hLine = Rect("Line", mouse);
            hLine.anchorMin = new Vector2(0, 1); hLine.anchorMax = new Vector2(1, 1); hLine.pivot = new Vector2(0.5f, 1);
            hLine.offsetMin = new Vector2(1, -16); hLine.offsetMax = new Vector2(-1, -14);
            Img(hLine, Slate).raycastTarget = false;

            var vLine = Rect("Split", mouse);
            Place(vLine, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -1), new Vector2(2, 14));
            Img(vLine, Slate).raycastTarget = false;

            comp.label = Text("Label", panel, "Aç", 24, OffWhite, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            comp.label.textWrappingMode = TextWrappingModes.NoWrap;

            comp.panel = panel;
            return comp;
        }

        // ------------------------------------------------------------------ Gazete -> güne başla

        static void WireNewspaper()
        {
            var paper = GameObject.Find("Desk/Newspaper");
            var gm = Object.FindAnyObjectByType<GameManager>();
            if (paper == null || gm == null) { Debug.LogWarning("Gazete ya da GameManager bulunamadı; gazete bağlanmadı."); return; }
            var it = paper.GetComponent<Interactable>();
            if (it == null) it = paper.AddComponent<Interactable>();
            for (int i = it.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(it.onClick, i);
            // Gazeteye tıklayınca yakın bakış açılır, "GÜNE BAŞLA" düğmesi mesaiyi başlatır (NewspaperView)
            if (paper.GetComponent<NewspaperView>() == null) paper.AddComponent<NewspaperView>();
            EditorUtility.SetDirty(it);
        }

        // ------------------------------------------------------------------ yardımcılar

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static Image Img(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string s, float size, Color c,
                                    TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            return t;
        }

        static Button Btn(string name, Transform parent, string label, Color bg, Color fg, float fontSize, Vector2 pos, Vector2 size)
        {
            var rt = Rect(name, parent);
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = Img(rt, bg);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.fadeDuration = 0.08f;
            b.colors = colors;

            var t = Text("Label", rt, label, fontSize, fg, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(t.rectTransform);
            t.characterSpacing = 3;
            return b;
        }
    }
}
