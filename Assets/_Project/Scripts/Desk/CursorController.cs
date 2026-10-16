using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Fare modu:
    ///  - Masa görünümü (Desk / HoldingStamp): fare kilitli ve gizli, ekran ortasında nokta (crosshair),
    ///    fare hareketi kamerayı döndürür (DeskLook), tıklama noktanın gösterdiği objeye gider.
    ///  - Yakın bakış (harita, klasör, dosya, monitör): fare serbest ama Windows imleci gizli; aynı nokta fareyi izler.
    ///  - Menüler, gün sonu ekranları: normal imleç.
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
            if (outline == null && crosshair != null)
            {
                var o = crosshair.Find("Outline");
                if (o != null) outline = o.GetComponent<Image>();
            }
        }

        enum Mode { Free, Aim, Inspect }

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

                default: // menü, gün sonu ekranları: normal imleç
                    if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
            }

            wantedLast = mode == Mode.Aim;
            IsAiming = mode == Mode.Aim && Cursor.lockState == CursorLockMode.Locked;
            UpdateCrosshair(mode);
        }

        static Mode WantedMode()
        {
            var gm = GameManager.Instance;
            var fc = FocusController.Instance;
            if (gm == null || fc == null || !gm.DeskInputEnabled) return Mode.Free;
            if (!fc.IsTransitioning && (fc.State == FocusState.Desk || fc.State == FocusState.HoldingStamp)) return Mode.Aim;
            return Mode.Inspect; // yakın bakış ya da geçiş sırasında
        }

        void UpdateCrosshair(Mode mode)
        {
            if (crosshair == null) return;
            bool show = mode != Mode.Free;
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
