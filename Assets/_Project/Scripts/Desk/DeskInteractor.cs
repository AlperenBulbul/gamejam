using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Main Camera üzerinde. Her karede imleçten ışın atar, Interactable'ı vurgular, tıklamada çağırır.
    /// Sağ tık / Esc: yakın bakıştan masaya dön.
    [RequireComponent(typeof(Camera))]
    public class DeskInteractor : MonoBehaviour
    {
        [SerializeField] LayerMask mask = ~0;
        [SerializeField] float maxDistance = 6f;

        Camera cam;
        Interactable hovered;

        /// Son ışının çarptığı yer (monitörde UV okumak için D-13'te kullanılacak)
        public RaycastHit LastHit { get; private set; }
        public bool HasHit { get; private set; }
        public Interactable Hovered => hovered;
        public Camera Cam => cam;

        void Awake() => cam = GetComponent<Camera>();

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var gm = GameManager.Instance;
            if (gm != null && !gm.DeskInputEnabled)
            {
                // Menü / fiş / dükkân ekranı açık: masa tamamen kapalı
                if (hovered != null) { hovered.SetHighlight(false); hovered = null; }
                HasHit = false;
                return;
            }

            var fc = FocusController.Instance;
            var focus = fc != null ? fc.State : FocusState.Desk;
            bool transitioning = fc != null && fc.IsTransitioning;
            bool deskView = focus == FocusState.Desk || focus == FocusState.HoldingStamp;
            // Masa görünümünde sadece crosshair ile (fare kilitliyken) etkileşim olur
            bool canPoint = !deskView || CursorController.IsAiming;
            bool overUI = !CursorController.IsAiming && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            Interactable target = null;
            HasHit = false;

            if (!transitioning && !overUI && canPoint)
            {
                Ray ray = CursorController.PointerRay(cam); // nişan alınıyorsa ekran ortası, değilse imleç
                if (Physics.Raycast(ray, out var hit, maxDistance, mask, QueryTriggerInteraction.Collide))
                {
                    LastHit = hit;
                    HasHit = true;
                    var it = hit.collider.GetComponentInParent<Interactable>();
                    if (it != null && it.isActiveAndEnabled && it.IsClickableIn(focus)) target = it;
                }
            }

            if (target != hovered)
            {
                if (hovered != null) hovered.SetHighlight(false);
                hovered = target;
                if (hovered != null) hovered.SetHighlight(true);
            }

            bool click = mouse.leftButton.wasPressedThisFrame && Time.frameCount != CursorController.LockClickFrame;
            if (hovered != null && click)
                hovered.Click();

            bool back = mouse.rightButton.wasPressedThisFrame ||
                        (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
            if (back && fc != null)
            {
                // Açık dosyada mühür elindeyse sağ tık önce mührü bırakır
                var sc = StampController.Instance;
                if (sc == null || !sc.TryDropInFocus()) fc.Back();
            }
        }
    }
}
