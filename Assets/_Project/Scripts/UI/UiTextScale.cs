using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Oyundaki ekran yazıları bir tık büyük: ekran üstü (overlay) kanvasların referans çözünürlüğü %8 küçültülür,
    /// böylece yazılar ve kutular birlikte büyür. Sonradan açılan kanvaslar (minigame tanıtımı, duraklatma...) da yakalanır.
    /// Dünya kanvasları (belge, monitör) etkilenmez. GameManager bu bileşeni kendisi ekler.
    public class UiTextScale : MonoBehaviour
    {
        public float scale = 1.08f;
        readonly HashSet<CanvasScaler> done = new HashSet<CanvasScaler>();
        float next;

        void LateUpdate()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.1f;
            foreach (var cs in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cs == null || !done.Add(cs)) continue;
                var cv = cs.GetComponent<Canvas>();
                if (cv == null || cv.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (cs.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) cs.referenceResolution /= scale;
                else if (cs.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize) cs.scaleFactor *= scale;
            }
        }
    }
}
