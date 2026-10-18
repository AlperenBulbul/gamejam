using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Fare modu:
    ///  - Masa görünümü (Desk / HoldingStamp): fare kilitli ve gizli, ekran ortasında nokta (crosshair),
    ///    fare hareketi kamerayı döndürür (DeskLook), tıklama noktanın gösterdiği objeye gider.
    ///  - Yakın bakış (harita, klasör, dosya, monitör): fare serbest ama Windows imleci gizli; aynı nokta fareyi izler.
    ///  - Menüler, gün sonu ekranları: Windows imleci yine gizli; oyunun noktası en üst katmanda fareyi izler.
    ///  - Sinematik oynarken (açılış, son videoları): hiçbir imleç görünmez.
    /// Fare bir sebeple serbest kaldıysa (Editor'da Esc) ilk tıklama sadece tekrar kilitler.
    [DefaultExecutionOrder(-50)] // DeskInteractor / StampController'dan önce
    public class CursorController : MonoBehaviour
    {
        /// Şu an crosshair ile mi nişan alınıyor (fare kilitli ve masa görünümü)
        public static bool IsAiming { get; private set; }
        /// Fareyi yeniden kilitlemek için harcanan tıklamanın karesi (o tıklama objeye gitmez)
        public static int LockClickFrame { get; private set; } = -1;

        [Header("Crosshair")]
        public RectTransform crosshair;
        public Image dot;
        [Tooltip("Noktanın arkasındaki koyu kenar (boşsa 'Outline' adlı çocuk bulunur)")]
        public Image outline;
        [Header("Boyutlar (piksel)")]
        public float aimDotSize = 6f;
        public float aimOutlineSize = 10f;
        [Tooltip("İncelemede (harita, klasör, dosya) nokta daha büyük")]
        public float inspectDotSize = 11f;
        public float inspectOutlineSize = 18f;
        public Color idleColor = Color.white;
        public Color hoverColor = new Color(0.95f, 0.70f, 0.20f);
        public float hoverScale = 1.8f;

        DeskInteractor interactor;
        bool wantedLast;

        // Menü imleci: crosshair'in kopyası, her ekranın (menü 400, mola 500, sinematik 700) üstünde
        RectTransform menuCursor;
        Image menuDot, menuOutline;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        PointerEventData uiPointer;
        EventSystem uiEs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { IsAiming = false; LockClickFrame = -1; }

        /// Etkileşim ışını: nişan alınıyorsa ekran ortası, değilse imleç
        public static Ray PointerRay(Camera cam)
        {
            return cam.ScreenPointToRay(PointerScreen());
        }

        public static Vector2 PointerScreen()
        {
            var m = Mouse.current;
            if (IsAiming || m == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return m.position.ReadValue();
        }

        void Awake()
        {
            interactor = GetComponent<DeskInteractor>();
            Cursor.visible = false; // oyun açılır açılmaz Windows imleci görünmesin
            if (outline == null && crosshair != null)
            {
                var o = crosshair.Find("Outline");
                if (o != null) outline = o.GetComponent<Image>();
            }
        }

        enum Mode { Free, Aim, Inspect, Hidden }

        void Update()
        {
            var mode = WantedMode();
            var mouse = Mouse.current;

            switch (mode)
            {
                case Mode.Aim: // masa: kilitli, gizli, nokta ortada
                    if (Cursor.lockState != CursorLockMode.Locked)
                    {
                        bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
                        if (!wantedLast || clicked)
                        {
                            Cursor.lockState = CursorLockMode.Locked;
                            if (clicked) LockClickFrame = Time.frameCount;
                        }
                    }
                    Cursor.visible = false;
                    break;

                case Mode.Inspect: // yakın bakış: serbest ama pencereye hapsedilmiş, Windows imleci gizli, nokta fareyi izler
                    if (Cursor.lockState != CursorLockMode.Confined) Cursor.lockState = CursorLockMode.Confined;
                    Cursor.visible = false;
                    break;

                default: // menü, gün sonu ekranları, sinematik: imleç serbest, Windows imleci gizli
                    if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = false;
                    break;
            }

            wantedLast = mode == Mode.Aim;
            IsAiming = mode == Mode.Aim && Cursor.lockState == CursorLockMode.Locked;
            UpdateCrosshair(mode);
            UpdateMenuCursor(mode);
        }

        static bool CinematicPlaying => IntroCinematic.Playing || EndScreenUI.VideoPlaying;

        static Mode WantedMode()
        {
            if (CinematicPlaying) return Mode.Hidden;
            var gm = GameManager.Instance;
            var fc = FocusController.Instance;
            if (gm == null || fc == null || !gm.DeskInputEnabled) return Mode.Free;
            if (!fc.IsTransitioning && (fc.State == FocusState.Desk || fc.State == FocusState.HoldingStamp)) return Mode.Aim;
            return Mode.Inspect; // yakın bakış ya da geçiş sırasında
        }

        void UpdateCrosshair(Mode mode)
        {
            if (crosshair == null) return;
            bool show = mode == Mode.Aim || mode == Mode.Inspect;
            if (crosshair.gameObject.activeSelf != show) crosshair.gameObject.SetActive(show);
            if (!show) return;

            // Konum: nişan alırken ekran ortası, incelemede farenin olduğu yer
            var parent = crosshair.parent as RectTransform;
            if (parent != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, PointerScreen(), null, out var local);
                crosshair.anchoredPosition = local;
            }

            bool hover = interactor != null && interactor.Hovered != null;
            float k = 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime);

            // Mod'a göre boyut: incelemede daha büyük nokta ve kalın kenar
            bool inspect = mode == Mode.Inspect;
            SetSize(dot, inspect ? inspectDotSize : aimDotSize, k);
            SetSize(outline, inspect ? inspectOutlineSize : aimOutlineSize, k);
            float target = hover ? hoverScale : 1f;
            float s = Mathf.Lerp(crosshair.localScale.x, target, k);
            crosshair.localScale = new Vector3(s, s, 1f);
            if (dot != null) dot.color = Color.Lerp(dot.color, hover ? hoverColor : idleColor, k);
        }

        /// Menü / gün sonu ekranlarında oyunun noktası, en üst katmanda farenin olduğu yerde
        void UpdateMenuCursor(Mode mode)
        {
            bool show = mode == Mode.Free;
            if (show) EnsureMenuCursor();
            if (menuCursor == null) return;
            if (menuCursor.gameObject.activeSelf != show) menuCursor.gameObject.SetActive(show);
            if (!show) return;

            var parent = menuCursor.parent as RectTransform;
            var pos = PointerScreen();
            if (parent != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pos, null, out var local);
                menuCursor.anchoredPosition = local;
            }

            bool hover = OverClickableUI(pos);
            float k = 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime);
            SetSize(menuDot, inspectDotSize, k);
            SetSize(menuOutline, inspectOutlineSize, k);
            float target = hover ? hoverScale : 1f;
            float s = Mathf.Lerp(menuCursor.localScale.x, target, k);
            menuCursor.localScale = new Vector3(s, s, 1f);
            if (menuDot != null) menuDot.color = Color.Lerp(menuDot.color, hover ? hoverColor : idleColor, k);
        }

        void EnsureMenuCursor()
        {
            if (menuCursor != null || crosshair == null) return;
            var go = new GameObject("MenuCursor_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var cv = go.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 32000; // her şeyin üstünde; GraphicRaycaster yok, tıklamayı engellemez
            var scaler = go.GetComponent<CanvasScaler>();
            var src = crosshair.GetComponentInParent<Canvas>();
            var srcScaler = src != null ? src.rootCanvas.GetComponent<CanvasScaler>() : null;
            if (srcScaler != null)
            {
                scaler.uiScaleMode = srcScaler.uiScaleMode;
                scaler.referenceResolution = srcScaler.referenceResolution;
                scaler.screenMatchMode = srcScaler.screenMatchMode;
                scaler.matchWidthOrHeight = srcScaler.matchWidthOrHeight;
                scaler.scaleFactor = srcScaler.scaleFactor;
            }

            var clone = Instantiate(crosshair.gameObject, go.transform, false);
            clone.name = "MenuCursor";
            clone.SetActive(false);
            menuCursor = (RectTransform)clone.transform;
            menuCursor.anchorMin = menuCursor.anchorMax = new Vector2(0.5f, 0.5f);
            menuCursor.localScale = Vector3.one;
            foreach (var g in clone.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            var d = menuCursor.Find("Dot");
            var o = menuCursor.Find("Outline");
            menuDot = d != null ? d.GetComponent<Image>() : null;
            menuOutline = o != null ? o.GetComponent<Image>() : null;
            if (menuDot != null) menuDot.color = idleColor;
        }

        /// Fare tıklanabilir bir arayüz öğesinin (düğme, kaydırıcı, menü satırı) üstünde mi
        bool OverClickableUI(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (uiPointer == null || uiEs != es) { uiPointer = new PointerEventData(es); uiEs = es; }
            uiPointer.position = pos;
            uiHits.Clear();
            es.RaycastAll(uiPointer, uiHits);
            if (uiHits.Count == 0) return false;
            var go = uiHits[0].gameObject;
            if (go == null) return false;
            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) != null) return true;
            if (ExecuteEvents.GetEventHandler<IPointerDownHandler>(go) != null) return true;
            return false;
        }

        static void SetSize(Image img, float size, float k)
        {
            if (img == null) return;
            var rt = img.rectTransform;
            float s = Mathf.Lerp(rt.sizeDelta.x, size, k);
            rt.sizeDelta = new Vector2(s, s);
        }

        void OnDisable()
        {
            IsAiming = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
