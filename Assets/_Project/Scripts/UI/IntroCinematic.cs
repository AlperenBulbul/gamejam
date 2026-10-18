using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

namespace LoadBearing
{
    /// İmar Sinan açılış sinematiği (tasarımcı, 18 Ekim): Resources/Intro/ImarSinan_Acilis_Sinematigi.mp4
    /// Sıra: Oyuna başla -> Nasıl Oynanır -> (BAŞLA) sinematik -> masa (1. gün).
    /// Tam ekran oynar (ses videonun içinde), Boşluk / Enter / Esc / sol tık ile geçilir (ilk 1 sn kilitli).
    /// Video siyahla biter; oyun siyahın arkasında başlar, perde açılınca masa görünür.
    /// Oyun açık kaldığı sürece bir kez oynar (yeniden oynayınca doğrudan masa). Video yoksa / açılamazsa doğrudan oyun başlar.
    /// GameManager bu bileşeni kendisi ekler, sahnede kurulum gerekmez.
    public class IntroCinematic : MonoBehaviour
    {
        const string ClipPath = "Intro/ImarSinan_Acilis_Sinematigi";
        const float SkipLock = 1.0f;

        /// Sinematik ekranda: menü girdileri kapalı
        public static bool Playing { get; private set; }
        public static IntroCinematic Instance { get; private set; }
        static bool played;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Playing = false; played = false; Instance = null; }

        GameObject root;
        CanvasGroup group;
        RenderTexture rtex;
        System.Action onFinished;

        void Awake() => Instance = this;

        /// Sinematiği oynat; bitince (ya da geçilince, siyah ekranın arkasında) onDone çağrılır.
        /// Daha önce oynadıysa / video yoksa onDone hemen çağrılır.
        public static void Play(System.Action onDone)
        {
            var clip = !played && Instance != null ? Resources.Load<VideoClip>(ClipPath) : null;
            if (clip == null)
            {
                if (!played && Instance != null) Debug.LogWarning("[Sinematik] Resources/" + ClipPath + " bulunamadı, doğrudan oyun");
                onDone?.Invoke();
                return;
            }
            played = true;
            Playing = true;
            Instance.onFinished = onDone;
            Instance.Build();
            Instance.StartCoroutine(Instance.Run(clip));
        }

        void OnDestroy()
        {
            if (rtex != null) { rtex.Release(); Destroy(rtex); }
            if (Playing) Playing = false;
        }

        IEnumerator Run(VideoClip clip)
        {
            var img = root.transform.Find("Video").GetComponent<RawImage>();
            var hint = root.transform.Find("Hint").GetComponent<TextMeshProUGUI>();

            rtex = new RenderTexture((int)Mathf.Max(16, clip.width), (int)Mathf.Max(16, clip.height), 0);
            var vp = img.gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.isLooping = false;
            vp.waitForFirstFrame = true;
            vp.skipOnDrop = true;
            vp.clip = clip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rtex;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            bool ended = false;
            vp.loopPointReached += v => ended = true;
            vp.Prepare();
            for (float w = 0f; !vp.isPrepared && w < 8f; w += Time.unscaledDeltaTime) yield return null;

            if (vp.isPrepared)
            {
                float vol = GameSettings.Master / 100f; // Direct ses AudioListener'dan geçmez: genel sesi elle uygula
                for (ushort i = 0; i < vp.audioTrackCount; i++) vp.SetDirectAudioVolume(i, vol);
                img.texture = rtex;
                img.color = Color.white;
                var fit = img.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = clip.width > 0 && clip.height > 0 ? (float)clip.width / clip.height : 16f / 9f;
                vp.Play();

                double length = clip.length;
                float t = 0f;
                while (true)
                {
                    t += Time.unscaledDeltaTime;
                    if (ended || (t > 1f && !vp.isPlaying) || t > length + 3.0) break;
                    hint.alpha = Mathf.MoveTowards(hint.alpha, t > SkipLock ? 0.6f : 0f, Time.unscaledDeltaTime * 2f);
                    if (t > SkipLock && SkipPressed())
                    {
                        // Geçildi: sesi kısa sürede kıs, görüntüyü siyaha indir
                        for (float k = 0f; k < 0.35f; k += Time.unscaledDeltaTime)
                        {
                            float a = 1f - k / 0.35f;
                            for (ushort i = 0; i < vp.audioTrackCount; i++) vp.SetDirectAudioVolume(i, vol * a);
                            img.color = new Color(a, a, a, 1f);
                            hint.alpha = Mathf.Min(hint.alpha, a * 0.6f);
                            yield return null;
                        }
                        break;
                    }
                    yield return null;
                }
                vp.Stop();
            }
            else Debug.LogWarning("[Sinematik] Video hazırlanamadı, doğrudan menü");

            // Siyahın arkasında oyun başlar, sonra perde açılır (geçiş tıklaması masaya geçmesin)
            img.color = Color.black;
            hint.alpha = 0f;
            yield return null;
            var done = onFinished; onFinished = null;
            done?.Invoke();
            yield return null;
            Playing = false;
            for (float k = 0f; k < 0.5f; k += Time.unscaledDeltaTime) { group.alpha = 1f - k / 0.5f; yield return null; }
            Destroy(root);
            if (rtex != null) { rtex.Release(); Destroy(rtex); rtex = null; }
        }

        static bool SkipPressed()
        {
            var k = Keyboard.current; var m = Mouse.current;
            return (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)) ||
                   (m != null && m.leftButton.wasPressedThisFrame);
        }

        void Build()
        {
            root = new GameObject("IntroCinematic", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700; // her şeyin üstünde (menü 400, gün kartı 450, mola 500)
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            group = root.GetComponent<CanvasGroup>();
            var rt = (RectTransform)root.transform;

            // Siyah zemin (tıklamaları da tutar: alttaki menüye geçmez)
            var bg = new GameObject("Black", typeof(RectTransform), typeof(Image));
            var brt = (RectTransform)bg.transform;
            brt.SetParent(rt, false);
            Stretch(brt);
            bg.GetComponent<Image>().color = Color.black;

            var vid = new GameObject("Video", typeof(RectTransform), typeof(RawImage));
            var vrt = (RectTransform)vid.transform;
            vrt.SetParent(rt, false);
            Stretch(vrt);
            var ri = vid.GetComponent<RawImage>();
            ri.color = Color.black;
            ri.raycastTarget = false;

            // Sağ altta "Geç" ipucu (1 sn sonra yumuşakça belirir)
            var hint = new GameObject("Hint", typeof(RectTransform), typeof(TextMeshProUGUI));
            var hrt = (RectTransform)hint.transform;
            hrt.SetParent(rt, false);
            hrt.anchorMin = hrt.anchorMax = new Vector2(1f, 0f);
            hrt.pivot = new Vector2(1f, 0f);
            hrt.anchoredPosition = new Vector2(-56f, 44f);
            hrt.sizeDelta = new Vector2(500f, 40f);
            var t = hint.GetComponent<TextMeshProUGUI>();
            t.text = "GEÇ  ·  BOŞLUK";
            t.fontSize = 22f;
            t.characterSpacing = 12f;
            t.alignment = TextAlignmentOptions.BottomRight;
            t.color = new Color32(0xF1, 0xEA, 0xDC, 0xFF);
            t.raycastTarget = false;
            var f = UiFonts.SemiBold;
            if (f != null) t.font = f;
            t.alpha = 0f;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
