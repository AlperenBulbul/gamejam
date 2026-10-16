using System.Collections;
using UnityEngine;

namespace LoadBearing
{
    /// Kamerayı nesnelerin FocusPoint'ine taşır (yakın bakış) ve geri getirir.
    /// Geçiş süresince tıklama kapalıdır (IsTransitioning).
    public class FocusController : MonoBehaviour
    {
        public static FocusController Instance { get; private set; }

        [SerializeField] Transform cameraTransform;
        [SerializeField] DeskLook deskLook;

        public FocusState State { get; private set; } = FocusState.Desk;
        public bool IsTransitioning { get; private set; }
        public bool IsFocused => State == FocusState.Folder || State == FocusState.Map ||
                                 State == FocusState.Binder || State == FocusState.Monitor;
        /// Konuşma sistemi: masa görünümünde balon, yakın bakışta alt yazı
        public bool IsDeskView => !IsFocused;

        Vector3 deskPosition;

        void Awake()
        {
            Instance = this;
            if (cameraTransform == null) cameraTransform = transform;
            if (deskLook == null) deskLook = cameraTransform.GetComponent<DeskLook>();
            deskPosition = cameraTransform.position;
        }

        float FocusTime => GameConfig.Current ? GameConfig.Current.focusTime : 0.35f;

        public void FocusOn(Transform point, FocusState target)
        {
            if (IsTransitioning || IsFocused || point == null) return;
            if (State == FocusState.HoldingStamp) return; // mühür elindeyken yakın bakış yok
            StartCoroutine(Move(point.position, point.rotation, target));
        }

        /// Sağ tık / Esc
        public void Back()
        {
            if (IsTransitioning) return;
            if (State == FocusState.HoldingStamp) { SetState(FocusState.Desk); return; }
            if (!IsFocused) return;
            var rot = deskLook != null ? deskLook.DeskRotation : cameraTransform.rotation;
            StartCoroutine(Move(deskPosition, rot, FocusState.Desk));
        }

        public void SetHoldingStamp(bool holding)
        {
            if (IsTransitioning || IsFocused) return;
            SetState(holding ? FocusState.HoldingStamp : FocusState.Desk);
        }

        IEnumerator Move(Vector3 pos, Quaternion rot, FocusState target)
        {
            IsTransitioning = true;
            if (deskLook != null) deskLook.enabled = false;

            Vector3 p0 = cameraTransform.position;
            Quaternion r0 = cameraTransform.rotation;
            float d = Mathf.Max(0.01f, FocusTime);
            for (float t = 0f; t < d; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / d);
                cameraTransform.SetPositionAndRotation(Vector3.Lerp(p0, pos, k), Quaternion.Slerp(r0, rot, k));
                yield return null;
            }
            cameraTransform.SetPositionAndRotation(pos, rot);

            IsTransitioning = false;
            SetState(target);
            if (target == FocusState.Desk && deskLook != null) deskLook.enabled = true;
        }

        void SetState(FocusState s)
        {
            if (State == s) return;
            State = s;
            GameEvents.RaiseFocusChanged(s);
        }
    }
}
