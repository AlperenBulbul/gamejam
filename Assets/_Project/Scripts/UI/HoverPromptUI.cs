using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Tıklanabilir bir objenin üstüne gelince imlecin yanında
    /// [fare simgesi, sol tuş vurgulu] + "Aç" gibi kısa bir yazı gösterir.
    /// Yazı objenin Interactable.prompt alanından gelir.
    public class HoverPromptUI : MonoBehaviour
    {
        public RectTransform panel;
        public TMP_Text label;
        [Tooltip("İmlece göre kayma (piksel, 1920x1080 referansında)")]
        public Vector2 offset = new Vector2(26f, -18f);

        RectTransform canvasRect;
        DeskInteractor interactor;

        void Awake()
        {
            var canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            panel.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (interactor == null && Camera.main != null) interactor = Camera.main.GetComponent<DeskInteractor>();

            var gm = GameManager.Instance;
            var hovered = interactor != null ? interactor.Hovered : null;
            var mouse = Mouse.current;
            bool show = hovered != null && mouse != null && (gm == null || gm.DeskInputEnabled)
                        && !string.IsNullOrEmpty(hovered.prompt);

            if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
            if (!show || canvasRect == null) return;

            var fc = FocusController.Instance;
            string text = fc != null && fc.State == FocusState.HoldingStamp ? "Mühür bas" : hovered.prompt;
            if (label.text != text) label.text = text;

            // Ekran pikselini canvas koordinatına çevir (Screen Space Overlay -> kamera null)
            // Nişan alınıyorsa crosshair'in (ekran ortası) yanında, değilse imlecin yanında
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, CursorController.PointerScreen(), null, out var local);
            var pos = local + offset;

            // Ekranın sağına / altına taşmasın
            var half = canvasRect.rect.size * 0.5f;
            var size = panel.rect.size;
            if (pos.x + size.x > half.x) pos.x = local.x - offset.x - size.x;
            if (pos.y - size.y < -half.y) pos.y = local.y - offset.y + size.y;

            panel.anchoredPosition = pos;
        }
    }
}
