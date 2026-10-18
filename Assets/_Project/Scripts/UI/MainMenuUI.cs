using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Başlangıç menüsü (tasarımcı "Ana menü + Ayarlar" paketi, 18 Ekim). Görseller Resources/Menu.
    ///  01 Oyuna başla -> Nasıl Oynanır açılır (oyun orada ✓ ile başlar) · 02 Ayarlar · 03 Oyundan çık
    ///  Fare üstüne gelince seçim kayar, ↑/↓ (W/S) ile de seçilir, Enter / tık = seç. Ayarlar'da Esc / Geri = ana menü.
    /// Menü kodla kurulur (kendi Canvas'ı). Sahnedeki eski menü paneli ("root") gizlenir.
    /// Bu bileşen her zaman aktif kalan bir objede durur.
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("Eski (sahnede kurulu) menü paneli: yeni menü açılınca gizlenir")]
        public GameObject root;
        public Button startButton;
        public Button quitButton;
        public HowToPlayUI howToPlay;

        // Renkler (tasarım)
        static readonly Color Cream = new Color32(0xF1, 0xEA, 0xDC, 0xFF);
        static readonly Color CreamSoft = new Color32(0xF1, 0xEA, 0xDC, 0x8C);   // %55
        static readonly Color CreamNum = new Color32(0xF1, 0xEA, 0xDC, 0x59);    // %35
        static readonly Color Gold = new Color32(0xC9, 0xA5, 0x5A, 0xFF);
        static readonly Color GoldDim = new Color32(0xC9, 0xA5, 0x5A, 0x99);     // %60
        static readonly Color Red = new Color32(0xD6, 0x78, 0x68, 0xFF);
        static readonly Color RedSoft = new Color32(0xD6, 0x78, 0x68, 0xB3);     // %70
        static readonly Color LineCol = new Color32(0xF1, 0xEA, 0xDC, 0x2E);     // %18

        GameObject menu;                 // yeni menünün kökü (Canvas)
        RectTransform shakeRoot;         // açılış sarsıntısı
        GameObject mainPage, settingsPage;
        RectTransform titleBottom;
        RawImage seismo;

        // Ana menü satırları
        TextMeshProUGUI[] itemText = new TextMeshProUGUI[3], itemNum = new TextMeshProUGUI[3];
        RectTransform[] itemLine = new RectTransform[3];
        GameObject[] itemRow = new GameObject[3];
        int sel;

        // Ayarlar satırları: 0 Genel ses, 1 Müzik, 2 Efektler, 3 Fare hızı, 4 Ekran, 5 Geri
        TextMeshProUGUI[] setLabel = new TextMeshProUGUI[5], setValue = new TextMeshProUGUI[4];
        RectTransform[] setTrack = new RectTransform[4], setFill = new RectTransform[4], setHandle = new RectTransform[4];
        Image[] setFillImg = new Image[4], setHandleImg = new Image[4];
        TextMeshProUGUI fullText, windowText, backText;
        RectTransform screenLine;
        Image backChip;
        TextMeshProUGUI backChipText;
        Sprite handleNormal, handleSelected;
        int setSel;
        bool inSettings;
        int lastInputFrame = -10;

        void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        void Start()
        {
            // Eski düğmeler yedek olarak bağlı kalsın (yeni menü kurulamazsa)
            if (startButton != null) startButton.onClick.AddListener(StartClicked);
            if (quitButton != null) quitButton.onClick.AddListener(QuitClicked);
#if UNITY_WEBGL && !UNITY_EDITOR
            if (quitButton != null) quitButton.gameObject.SetActive(false); // tarayıcıda çıkış olmaz
#endif
            try { Build(); }
            catch (System.Exception e) { Debug.LogError("[Menü] Yeni ana menü kurulamadı, eski menü kullanılıyor: " + e); if (menu != null) Destroy(menu); menu = null; }

            var gm = GameManager.Instance;
            OnStateChanged(gm != null ? gm.State : GameState.MainMenu);
        }

        void OnStateChanged(GameState s)
        {
            bool inMenu = s == GameState.MainMenu;
            if (!inMenu && howToPlay != null) howToPlay.Close();
            bool show = inMenu && (howToPlay == null || !howToPlay.IsOpen);
            if (menu == null) { if (root != null) root.SetActive(show); return; }
            if (root != null) root.SetActive(false);
            bool was = menu.activeSelf;
            menu.SetActive(show);
            if (show && !was) { ShowMain(); StartCoroutine(Intro()); }
        }

        /// Nasıl Oynanır'dan Esc / MENÜ ile dönüş
        public void ShowMenu()
        {
            if (menu == null) { if (root != null) root.SetActive(true); return; }
            menu.SetActive(true);
            ShowMain();
            lastInputFrame = Time.frameCount; // aynı karedeki Esc / Enter menüye geçmesin
        }

        void StartClicked()
        {
            if (menu != null) menu.SetActive(false);
            if (root != null) root.SetActive(false);
            if (howToPlay != null) howToPlay.Open();
            else GameManager.Instance.StartGame();
        }

        void QuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ giriş

        void Update()
        {
            if (menu == null || !menu.activeInHierarchy || Time.frameCount == lastInputFrame) return;
            if (seismo != null) { var r = seismo.uvRect; r.x = (r.x + Time.unscaledDeltaTime * 0.02f) % 1f; seismo.uvRect = r; } // çok yavaş kayar

            var kb = Keyboard.current;
            if (kb == null) return;
            bool up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            bool down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;

            if (!inSettings)
            {
                if (up) Select(Prev(sel));
                else if (down) Select(Next(sel));
                else if (enter) Activate(sel);
                return;
            }

            bool left = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
            bool right = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            if (kb.escapeKey.wasPressedThisFrame) { ShowMain(); return; }
            if (up) SelectSetting((setSel + 5) % 6);
            else if (down) SelectSetting((setSel + 1) % 6);
            else if (left || right)
            {
                int d = right ? 5 : -5;
                if (setSel < 4) SetValue(setSel, GetValue(setSel) + d);
                else if (setSel == 4) GameSettings.SetFullscreen(!GameSettings.Fullscreen);
                RefreshSettings();
            }
            else if (enter)
            {
                if (setSel == 4) { GameSettings.SetFullscreen(!GameSettings.Fullscreen); RefreshSettings(); }
                else if (setSel == 5) ShowMain();
            }
        }

        bool QuitVisible
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
                return true;
#endif
            }
        }

        int Next(int i) { int n = QuitVisible ? 3 : 2; return (i + 1) % n; }
        int Prev(int i) { int n = QuitVisible ? 3 : 2; return (i + n - 1) % n; }

        void Activate(int i)
        {
            lastInputFrame = Time.frameCount;
            switch (i)
            {
                case 0: StartClicked(); break;
                case 1: ShowSettings(); break;
                case 2: QuitClicked(); break;
            }
        }

        // ------------------------------------------------------------------ sayfalar

        void ShowMain()
        {
            inSettings = false;
            if (mainPage == null) return;
            GameSettings.Save();
            mainPage.SetActive(true);
            settingsPage.SetActive(false);
            Select(0);
        }

        void ShowSettings()
        {
            inSettings = true;
            mainPage.SetActive(false);
            settingsPage.SetActive(true);
            SelectSetting(0);
        }

        void Select(int i)
        {
            sel = i;
            for (int k = 0; k < 3; k++)
            {
                if (itemText[k] == null) continue;
                bool on = k == i;
                bool quit = k == 2;
                itemText[k].color = quit ? (on ? Red : RedSoft) : (on ? Cream : CreamSoft);
                itemNum[k].color = on ? Gold : CreamNum;
                itemLine[k].gameObject.SetActive(on);
                if (on)
                {
                    itemText[k].ForceMeshUpdate();
                    float w = itemText[k].preferredWidth;
                    itemLine[k].anchoredPosition = new Vector2(214f + w + 36f, itemLine[k].anchoredPosition.y);
                }
            }
        }

        void SelectSetting(int i)
        {
            setSel = i;
            RefreshSettings();
        }

        static int GetValue(int i) => i switch
        {
            0 => GameSettings.Master,
            1 => GameSettings.Music,
            2 => GameSettings.Sfx,
            _ => GameSettings.Mouse,
        };

        static void SetValue(int i, int v)
        {
            switch (i)
            {
                case 0: GameSettings.SetMaster(v); break;
                case 1: GameSettings.SetMusic(v); break;
                case 2: GameSettings.SetSfx(v); break;
                default: GameSettings.SetMouse(v); break;
            }
        }

        void RefreshSettings()
        {
            if (settingsPage == null) return;
            for (int i = 0; i < 5; i++) setLabel[i].color = i == setSel ? Cream : CreamSoft;
            for (int i = 0; i < 4; i++)
            {
                bool on = i == setSel;
                int v = GetValue(i);
                float k = v / 100f;
                setValue[i].text = v.ToString();
                setValue[i].color = on ? Cream : CreamSoft;
                setFill[i].sizeDelta = new Vector2(520f * k, 4f);
                setFillImg[i].color = on ? Gold : GoldDim;
                setHandle[i].anchoredPosition = new Vector2(520f * k, 0f);
                if (handleSelected != null) setHandleImg[i].sprite = on ? handleSelected : handleNormal;
                else setHandleImg[i].color = on ? Cream : CreamSoft;
            }
            bool full = GameSettings.Fullscreen;
            fullText.color = full ? Cream : CreamNum;
            windowText.color = full ? CreamNum : Cream;
            var target = full ? fullText : windowText;
            target.ForceMeshUpdate();
            screenLine.anchoredPosition = new Vector2(target.rectTransform.anchoredPosition.x, screenLine.anchoredPosition.y);
            screenLine.sizeDelta = new Vector2(target.preferredWidth, 4f);
            backText.color = setSel == 5 ? Cream : CreamSoft;
            backChipText.color = new Color(Cream.r, Cream.g, Cream.b, setSel == 5 ? 0.9f : 0.6f);
        }

        // ------------------------------------------------------------------ açılış animasyonu

        IEnumerator Intro()
        {
            if (titleBottom == null) yield break;
            titleBottom.anchoredPosition = TitlePos;
            titleBottom.localRotation = Quaternion.identity;
            if (shakeRoot != null) shakeRoot.anchoredPosition = Vector2.zero;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) { if (!menu.activeInHierarchy) yield break; yield return null; }
            // Başlık çatlar: alt yarı (+9, +15) kayar, 0,5° döner; ekran 0,2 sn sarsılır
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.15f);
                titleBottom.anchoredPosition = TitlePos + new Vector2(9f, -15f) * k;
                titleBottom.localRotation = Quaternion.Euler(0f, 0f, -0.5f * k);
                if (shakeRoot != null) shakeRoot.anchoredPosition = Random.insideUnitCircle * 6f * (1f - t / 0.2f);
                yield return null;
            }
            titleBottom.anchoredPosition = TitlePos + new Vector2(9f, -15f);
            titleBottom.localRotation = Quaternion.Euler(0f, 0f, -0.5f);
            if (shakeRoot != null) shakeRoot.anchoredPosition = Vector2.zero;
        }

        static readonly Vector2 TitlePos = new Vector2(138f + 850f, -(160f + 170f)); // sol üst (138,160), 1700 × 340, pivot ortada

        // ------------------------------------------------------------------ kurulum

        void Build()
        {
            menu = new GameObject("MainMenu_Imar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = menu.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400; // gün kartı / bitiş (450) ve mola (500) altında
            var scaler = menu.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var croot = (RectTransform)menu.transform;

            // Arka plan: masa sahnesi (karartma içinde), ekranı kaplar
            var bgBase = NewRect("BgBase", croot); Stretch(bgBase);
            var bgBaseImg = bgBase.gameObject.AddComponent<Image>(); bgBaseImg.color = new Color32(0x1E, 0x1B, 0x18, 0xFF);
            var bgTex = Resources.Load<Texture2D>("Menu/T_Menu_Background");
            if (bgTex != null)
            {
                var bg = NewRect("Background", croot); Stretch(bg);
                var ri = bg.gameObject.AddComponent<RawImage>(); ri.texture = bgTex; ri.raycastTarget = false;
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = (float)bgTex.width / bgTex.height;
            }
            else Debug.LogWarning("[Menü] Resources/Menu/T_Menu_Background bulunamadı");

            // 1920 × 1080 tasarım alanı (ortada, oranı korur)
            var area = NewRect("Area", croot);
            area.anchorMin = area.anchorMax = new Vector2(0.5f, 0.5f);
            area.sizeDelta = new Vector2(1920f, 1080f);
            area.gameObject.AddComponent<AspectFit>();
            shakeRoot = NewRect("Shake", area); Stretch(shakeRoot);

            handleNormal = Sprite2x("Menu/UI_Settings_Handle_Normal");
            handleSelected = Sprite2x("Menu/UI_Settings_Handle_Selected");

            BuildMain(shakeRoot);
            BuildSettings(shakeRoot);
            menu.SetActive(false);
        }

        void BuildMain(RectTransform parent)
        {
            mainPage = NewRect("Main", parent).gameObject; Stretch((RectTransform)mainPage.transform);
            var p = (RectTransform)mainPage.transform;

            var tag = Txt(p, "KARAOVA YAPI DENETİM", 22f, Gold, UiFonts.SemiBold, 150f, 150f, 900f, 30f);
            tag.characterSpacing = 32f;

            // Çatlak başlık: üst + alt yarı üst üste (alt yarı açılışta kayar)
            var top = Img(p, "TitleTop", "Menu/UI_Menu_Title_Top", 138f, 160f, 1700f, 340f);
            titleBottom = Img(p, "TitleBottom", "Menu/UI_Menu_Title_Bottom", 138f, 160f, 1700f, 340f);
            if (top == null || titleBottom == null)
            {
                if (top != null) Destroy(top.gameObject);
                if (titleBottom != null) Destroy(titleBottom.gameObject);
                titleBottom = null;
                if (Img(p, "Title", "Menu/UI_Menu_Title_Full", 138f, 160f, 1700f, 340f) == null)
                {
                    var t = Txt(p, "İMAR SİNAN", 220f, Cream, UiFonts.Heavy, 138f, 160f, 1700f, 340f);
                    t.alignment = TextAlignmentOptions.MidlineLeft;
                }
            }
            else
            {
                // Pivot ortada (dönüş için)
                titleBottom.pivot = new Vector2(0.5f, 0.5f);
                titleBottom.anchoredPosition = TitlePos;
            }

            Bar(p, 150f, 560f, 1620f, 2f, LineCol);

            string[] labels = { "Oyuna başla", "Ayarlar", "Oyundan çık" };
            for (int i = 0; i < 3; i++)
            {
                float y = 640f + 96f * i;
                var row = NewRect("Row" + (i + 1), p);
                TopLeft(row, 150f, y, 760f, 96f);
                var hit = row.gameObject.AddComponent<Image>(); hit.color = new Color(0f, 0f, 0f, 0f); // fare alanı
                int idx = i;
                var h = row.gameObject.AddComponent<MenuHit>();
                h.enter = () => { if (sel != idx) Select(idx); };
                h.click = () => Activate(idx);
                itemRow[i] = row.gameObject;

                var num = Txt(row, $"0{i + 1}", 20f, CreamNum, UiFonts.SemiBold, 0f, 8f, 60f, 96f);
                num.characterSpacing = 14f;
                var lab = Txt(row, labels[i], 52f, CreamSoft, UiFonts.SemiBold, 64f, 0f, 680f, 96f);
                itemNum[i] = num; itemText[i] = lab;

                var line = Bar(p, 0f, y + 46f, 70f, 4f, Gold);
                line.anchoredPosition = new Vector2(214f + 300f, line.anchoredPosition.y);
                itemLine[i] = line;
                line.gameObject.SetActive(false);
            }
            if (!QuitVisible) itemRow[2].SetActive(false);

            // Sismograf (dekor), soldan sağa çok yavaş kayar
            var stex = Resources.Load<Texture2D>("Menu/UI_Menu_Seismograph");
            if (stex != null)
            {
                stex.wrapMode = TextureWrapMode.Repeat;
                var s = NewRect("Seismograph", p);
                TopLeft(s, 1210f, 640f, 560f, 120f);
                seismo = s.gameObject.AddComponent<RawImage>();
                seismo.texture = stex;
                seismo.raycastTarget = false;
            }
        }

        void BuildSettings(RectTransform parent)
        {
            settingsPage = NewRect("Settings", parent).gameObject; Stretch((RectTransform)settingsPage.transform);
            var p = (RectTransform)settingsPage.transform;

            var tag = Txt(p, "İMAR SİNAN", 22f, Gold, UiFonts.SemiBold, 150f, 150f, 900f, 30f);
            tag.characterSpacing = 32f;
            var title = Txt(p, "Ayarlar", 150f, Cream, UiFonts.Heavy, 142f, 190f, 1200f, 200f);
            title.alignment = TextAlignmentOptions.TopLeft;
            Bar(p, 150f, 420f, 1620f, 2f, LineCol);

            string[] labels = { "Genel ses", "Müzik", "Efektler", "Fare hızı", "Ekran" };
            for (int i = 0; i < 5; i++)
            {
                float y = 470f + 84f * i;
                var row = NewRect("Row_" + labels[i], p);
                TopLeft(row, 150f, y, 1100f, 84f);
                var hit = row.gameObject.AddComponent<Image>(); hit.color = new Color(0f, 0f, 0f, 0f);
                int idx = i;
                var h = row.gameObject.AddComponent<MenuHit>();
                h.enter = () => { if (setSel != idx) SelectSetting(idx); };
                setLabel[i] = Txt(row, labels[i], 38f, CreamSoft, UiFonts.SemiBold, 0f, 0f, 420f, 84f);

                if (i < 4)
                {
                    // Slider: zemin 520 × 4, dolgu, elmas tutamaç
                    var track = NewRect("Track", row);
                    track.anchorMin = track.anchorMax = new Vector2(0f, 0.5f);
                    track.pivot = new Vector2(0f, 0.5f);
                    track.sizeDelta = new Vector2(520f, 4f);
                    track.anchoredPosition = new Vector2(590f - 150f, 0f);
                    track.gameObject.AddComponent<Image>().color = LineCol;
                    setTrack[i] = track;

                    var fill = NewRect("Fill", track);
                    fill.anchorMin = fill.anchorMax = new Vector2(0f, 0.5f);
                    fill.pivot = new Vector2(0f, 0.5f);
                    fill.anchoredPosition = Vector2.zero;
                    setFillImg[i] = fill.gameObject.AddComponent<Image>();
                    setFillImg[i].raycastTarget = false;
                    setFill[i] = fill;

                    var handle = NewRect("Handle", track);
                    handle.anchorMin = handle.anchorMax = new Vector2(0f, 0.5f);
                    handle.pivot = new Vector2(0.5f, 0.5f);
                    handle.sizeDelta = new Vector2(32f, 32f);
                    setHandleImg[i] = handle.gameObject.AddComponent<Image>();
                    setHandleImg[i].raycastTarget = false;
                    if (handleNormal != null) setHandleImg[i].sprite = handleNormal;
                    else handle.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    setHandle[i] = handle;

                    // Sürükleme alanı: slider boyunca, satır yüksekliğinde
                    var drag = NewRect("Drag", row);
                    drag.anchorMin = drag.anchorMax = new Vector2(0f, 0.5f);
                    drag.pivot = new Vector2(0f, 0.5f);
                    drag.sizeDelta = new Vector2(560f, 60f);
                    drag.anchoredPosition = new Vector2(590f - 150f - 20f, 0f);
                    drag.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                    var sd = drag.gameObject.AddComponent<MenuHit>();
                    var trackRef = track;
                    sd.enter = () => { if (setSel != idx) SelectSetting(idx); };
                    sd.drag = pos =>
                    {
                        if (setSel != idx) setSel = idx;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(trackRef, pos, null, out var local);
                        SetValue(idx, Mathf.RoundToInt(Mathf.Clamp01(local.x / 520f) * 100f));
                        RefreshSettings();
                    };

                    var val = Txt(row, "0", 26f, CreamSoft, UiFonts.SemiBold, 1230f - 150f - 120f, 0f, 120f, 84f);
                    val.alignment = TextAlignmentOptions.MidlineRight;
                    setValue[i] = val;
                }
                else
                {
                    // Ekran: Tam ekran / Pencere
                    fullText = Txt(row, "Tam ekran", 30f, Cream, UiFonts.SemiBold, 590f - 150f, 0f, 200f, 84f);
                    fullText.ForceMeshUpdate();
                    float fw = fullText.preferredWidth;
                    fullText.rectTransform.sizeDelta = new Vector2(fw + 2f, 84f);
                    windowText = Txt(row, "Pencere", 30f, CreamNum, UiFonts.SemiBold, 590f - 150f + fw + 34f, 0f, 200f, 84f);
                    windowText.ForceMeshUpdate();
                    windowText.rectTransform.sizeDelta = new Vector2(windowText.preferredWidth + 2f, 84f);
                    var fh = fullText.gameObject.AddComponent<MenuHit>();
                    fullText.raycastTarget = true;
                    fh.click = () => { setSel = 4; GameSettings.SetFullscreen(true); RefreshSettings(); };
                    var wh = windowText.gameObject.AddComponent<MenuHit>();
                    windowText.raycastTarget = true;
                    wh.click = () => { setSel = 4; GameSettings.SetFullscreen(false); RefreshSettings(); };

                    screenLine = Bar(row, 590f - 150f, 84f / 2f + 22f, fw, 4f, Gold);
                }
            }

            // ← Geri  [ESC]
            var back = NewRect("Back", p);
            TopLeft(back, 150f, 930f, 360f, 60f);
            back.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var bh = back.gameObject.AddComponent<MenuHit>();
            bh.enter = () => { if (setSel != 5) SelectSetting(5); };
            bh.click = ShowMain;
            backText = Txt(back, "←  Geri", 38f, CreamSoft, UiFonts.SemiBold, 0f, 0f, 200f, 60f);
            backText.ForceMeshUpdate();
            float bw = backText.preferredWidth;
            var chip = NewRect("EscChip", back);
            TopLeft(chip, bw + 24f, (60f - 35f) / 2f, 66f, 35f);
            backChip = chip.gameObject.AddComponent<Image>();
            backChip.raycastTarget = false;
            var chipSp = Sprite2x("Menu/UI_Settings_EscChip");
            if (chipSp != null) backChip.sprite = chipSp;
            else backChip.color = new Color(1f, 1f, 1f, 0.12f);
            backChipText = Txt(chip, "ESC", 18f, Cream, UiFonts.SemiBold, 0f, 0f, 66f, 35f);
            backChipText.alignment = TextAlignmentOptions.Center;
            backChipText.characterSpacing = 4f;

            settingsPage.SetActive(false);
        }

        // ------------------------------------------------------------------ yardımcılar

        static RectTransform NewRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// Ebeveynin sol üstünden (x, y) konumunda, w × h kutu (tasarım koordinatları: y aşağı doğru)
        static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        static TextMeshProUGUI Txt(RectTransform parent, string text, float size, Color color, TMP_FontAsset font,
                                    float x, float y, float w, float h)
        {
            var rt = NewRect("Text", parent);
            TopLeft(rt, x, y, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            if (font != null) t.font = font; else t.fontStyle = FontStyles.Bold;
            return t;
        }

        /// Düz renk çizgi: sol üst (x, y), w × h
        static RectTransform Bar(RectTransform parent, float x, float y, float w, float h, Color c)
        {
            var rt = NewRect("Line", parent);
            TopLeft(rt, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return rt;
        }

        static RectTransform Img(RectTransform parent, string name, string path, float x, float y, float w, float h)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) { Debug.LogWarning("[Menü] Resources/" + path + " bulunamadı"); return null; }
            var rt = NewRect(name, parent);
            TopLeft(rt, x, y, w, h);
            var ri = rt.gameObject.AddComponent<RawImage>();
            ri.texture = tex;
            ri.raycastTarget = false;
            return rt;
        }

        /// 2x çözünürlüklü tasarım görseli -> sprite, isteğe bağlı 9 dilim kenarı (piksel)
        static Sprite Sprite2x(string path, float border = 0f)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 200f, 0,
                                 SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }
    }

    /// 1920 × 1080 tasarım alanını ekrana sığdırır (oran korunur, ortalanır)
    public class AspectFit : MonoBehaviour
    {
        void LateUpdate()
        {
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            var size = parent.rect.size;
            if (size.x < 1f || size.y < 1f) return;
            float k = Mathf.Min(size.x / 1920f, size.y / 1080f);
            rt.localScale = new Vector3(k, k, 1f);
        }
    }

    /// Menü satırı: fare üstüne gelince / tıklayınca / sürükleyince haber verir
    public class MenuHit : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IPointerDownHandler, IDragHandler
    {
        public System.Action enter, click;
        public System.Action<Vector2> drag;

        public void OnPointerEnter(PointerEventData e) => enter?.Invoke();
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) click?.Invoke(); }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) drag?.Invoke(e.position); }
        public void OnDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) drag?.Invoke(e.position); }
    }
}
