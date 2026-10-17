using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Ekranın altında tek satır yönlendirme ("Gazeteye git", "Monitöre git").
    /// İlk turdaki yönerge çubuğuyla aynı görünüm. Kendini kendisi kurar, sahnede bir şey gerekmez.
    ///   PromptBar.Set("sahip", "yazı")  -> gösterir
    ///   PromptBar.Set("sahip", null)    -> o sahibin yazısını kaldırır
    /// Birden fazla yazı varsa en son gelen görünür.
    public class PromptBar : MonoBehaviour
    {
        static PromptBar inst;
        readonly List<(string owner, string text)> items = new List<(string, string)>();

        GameObject bar;
        RectTransform barRt;
        TextMeshProUGUI label;
        string shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => inst = null;

        public static void Set(string owner, string text)
        {
            if (inst == null)
            {
                if (string.IsNullOrEmpty(text)) return;
                inst = new GameObject("PromptBar").AddComponent<PromptBar>();
            }
            var list = inst.items;
            int i = list.FindIndex(x => x.owner == owner);
            if (string.IsNullOrEmpty(text)) { if (i >= 0) list.RemoveAt(i); return; }
            if (i >= 0) { if (list[i].text == text) return; list.RemoveAt(i); }
            list.Add((owner, text));
        }

        void OnDestroy() { if (inst == this) inst = null; }

        void LateUpdate()
        {
            bool blocked = GuidedTour.Running || PauseMenu.IsPaused || EndScreenUI.Blocking || MinigameDirector.IntroOpen;
            string text = !blocked && items.Count > 0 ? items[items.Count - 1].text : null;
            if (text == shown) return;
            shown = text;
            if (bar == null) Build();
            if (string.IsNullOrEmpty(text)) { bar.SetActive(false); return; }
            label.text = text;
            label.ForceMeshUpdate();
            barRt.sizeDelta = new Vector2(Mathf.Clamp(label.preferredWidth + 56f, 260f, 1500f), 60f);
            bar.SetActive(true);
        }

        void Build()
        {
            var go = new GameObject("PromptBarUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 54;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            barRt = (RectTransform)bar.transform;
            barRt.SetParent(go.transform, false);
            barRt.anchorMin = barRt.anchorMax = new Vector2(0.5f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = new Vector2(0f, 36f);
            var img = bar.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.78f);
            img.raycastTarget = false;

            // Solda ince turuncu şerit: masadaki parlayan nesneyle aynı renk (nereye bakacağını bağlar)
            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            var art = (RectTransform)accent.transform;
            art.SetParent(barRt, false);
            art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(6f, 0f);
            var ai = accent.GetComponent<Image>();
            ai.color = HintColors.Orange;
            ai.raycastTarget = false;

            var t = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var trt = (RectTransform)t.transform;
            trt.SetParent(barRt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(30f, 0f); trt.offsetMax = new Vector2(-24f, 0f);
            label = t.GetComponent<TextMeshProUGUI>();
            label.fontSize = 30f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            bar.SetActive(false);
        }
    }
}
