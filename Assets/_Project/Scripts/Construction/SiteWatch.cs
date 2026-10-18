using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Monitörde şantiyeyi izlerken (minigame yokken) kaytaran işçiyi uyarma:
    ///  - Kaytaran işçinin üstünde sarı ünlem (WorkerView)
    ///  - İmleç o işçinin üstüne gelince yanında [sol tık] "İşçiyi uyar"
    ///  - Tıklayınca işçi irkilir ("tamam şefim"), işine döner; hata kusura yazılmaz
    /// GameManager bu bileşeni kendisi ekler.
    public class SiteWatch : MonoBehaviour
    {
        GameObject tip;
        RectTransform tipRt;
        TextMeshProUGUI tipText;
        RectTransform canvasRt;
        float flashUntil;

        void Update()
        {
            var worker = HoveredSlacker();
            var mouse = Mouse.current;
            if (worker != null && mouse != null && mouse.leftButton.wasPressedThisFrame && Time.frameCount != CursorController.LockClickFrame)
            {
                var cm = ConstructionManager.Instance;
                var feed = MonitorFeed.Instance;
                if (cm != null && feed != null && cm.ClickWorker(feed.Shown, worker.index))
                {
                    worker.Warned();
                    flashUntil = Time.unscaledTime + 0.9f;
                }
            }
            else if (worker == null && mouse != null && mouse.leftButton.wasPressedThisFrame && Time.frameCount != CursorController.LockClickFrame
                     && OnMonitor() && MonitorFeed.Instance != null && MonitorFeed.Instance.Shown != null
                     && MinigameDirector.Instance != null && MinigameDirector.Instance.PointerOnScreen(out _))
                Sfx.Play("SFX_Worker_WrongClick"); // şantiyede boşa / düzgün işçiye tıklandı

            bool flash = Time.unscaledTime < flashUntil && OnMonitor();
            bool show = worker != null || flash;
            if (show && tip == null) BuildTip();
            if (tip == null) return;
            if (tip.activeSelf != show) tip.SetActive(show);
            if (!show) return;
            tipText.text = flash ? "<color=#7CD67C>Uyarıldı</color>" : "İşçiyi uyar";
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, CursorController.PointerScreen(), null, out var local);
            tipRt.anchoredPosition = local + new Vector2(22f, -14f);
        }

        static bool OnMonitor()
        {
            var fc = FocusController.Instance;
            var gm = GameManager.Instance;
            return fc != null && fc.State == FocusState.Monitor && !fc.IsTransitioning && !MinigameDirector.OnScreen
                   && (gm == null || gm.DeskInputEnabled);
        }

        /// İmlecin altındaki kaytaran işçi (monitör ekranı -> şantiye kamerası ışını)
        static WorkerView HoveredSlacker()
        {
            if (!OnMonitor()) return null;
            var feed = MonitorFeed.Instance;
            var mg = MinigameDirector.Instance;
            if (feed == null || feed.feedCamera == null || mg == null || feed.Shown == null) return null;
            if (!mg.PointerOnScreen(out var px)) return null;
            var ray = feed.feedCamera.ViewportPointToRay(new Vector3(px.x / 1280f, 1f - px.y / 720f, 0f));
            var hits = Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide);
            WorkerView best = null;
            float bestD = float.MaxValue;
            foreach (var h in hits)
            {
                var w = h.collider.GetComponentInParent<WorkerView>();
                if (w == null || w.FaultType == 0) continue;
                if (h.distance < bestD) { bestD = h.distance; best = w; }
            }
            if (best != null) return best;

            // Küçük figürler: ışına yakın kaytaran işçi de sayılır (tıklaması kolay olsun)
            foreach (var w in feed.Workers)
            {
                if (w == null || w.FaultType == 0) continue;
                var p = w.transform.position + Vector3.up * 0.15f;
                var v = feed.feedCamera.WorldToViewportPoint(p);
                var d = new Vector2((v.x - px.x / 1280f) * 1280f, (v.y - (1f - px.y / 720f)) * 720f);
                if (d.magnitude < 34f && d.magnitude < bestD) { bestD = d.magnitude; best = w; }
            }
            return best;
        }

        void BuildTip()
        {
            var go = new GameObject("SiteWatchUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var cv = go.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 70;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            canvasRt = (RectTransform)go.transform;

            tip = new GameObject("Tip", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            tipRt = (RectTransform)tip.transform;
            tipRt.SetParent(canvasRt, false);
            tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 0.5f);
            tipRt.pivot = new Vector2(0f, 1f);
            tip.GetComponent<Image>().color = new Color(0.05f, 0.055f, 0.06f, 0.9f);
            tip.GetComponent<Image>().raycastTarget = false;
            var hl = tip.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(10, 14, 6, 6);
            hl.spacing = 8f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var fit = tip.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            KeyIcons.Add(tipRt, "LMB", null);
            var t = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            t.transform.SetParent(tipRt, false);
            tipText = t.GetComponent<TextMeshProUGUI>();
            tipText.fontSize = 24f;
            tipText.color = Color.white;
            tipText.textWrappingMode = TextWrappingModes.NoWrap;
            tipText.raycastTarget = false;
            tip.SetActive(false);
        }
    }
}
