using UnityEngine;

namespace LoadBearing
{
    /// İlk tur göstergeleri (1. gün): oyuncuya sırayla ne yapacağını turuncu yanıp sönen çerçevelerle gösterir.
    ///  1) Gazete parlar (NewspaperView)                      -> güne başla
    ///  1b) İlk müşteri dosyasını bırakınca dosya yanıp söner (açılana kadar)
    ///  1c) Kural klasörüne ve haritaya baktıktan sonra dosya tekrar yanar; açılınca (ilk karar verilene kadar) mühürlerde halka yanıp söner
    ///  2) İlk müşterinin belgesi okunup kapatılınca: kural klasörü ve harita yanıp söner (açılana kadar)
    ///  3) İlk onaydan sonra (şantiye açıldı): monitör yanıp söner (açılana kadar)
    /// Her gösterge oyun başına bir kez çıkar. GameManager bu bileşeni Systems'e kendisi ekler.
    public class TutorialHints : MonoBehaviour
    {
        HintGlow binderGlow, mapGlow, monitorGlow, folderGlow, approveGlow, rejectGlow;
        Transform parcelTarget;
        bool built;
        bool rulesShown, monitorShown, folderShown, folderAgainShown, firstStampDone;
        FolderView folderView;
        bool openedBinder, openedMap, openedMonitor;

        void OnEnable()
        {
            GameEvents.FocusTransitionStarted += OnTransition;
            GameEvents.Stamped += OnStamped;
            GameEvents.StateChanged += OnState;
            GameEvents.CustomerArrived += OnArrived;
            GameEvents.CustomerLeaving += OnLeaving;
        }

        void OnDisable()
        {
            GameEvents.FocusTransitionStarted -= OnTransition;
            GameEvents.Stamped -= OnStamped;
            GameEvents.StateChanged -= OnState;
            GameEvents.CustomerArrived -= OnArrived;
            GameEvents.CustomerLeaving -= OnLeaving;
        }

        void Start() => Build();

        void Build()
        {
            if (built) return;
            built = true;

            var binder = FindAnyObjectByType<BinderView>();
            if (binder != null) binderGlow = HintGlow.Create(transform, Largest(binder.closedModel != null ? binder.closedModel.transform : binder.transform), "Hint_Binder");
            var map = FindAnyObjectByType<MapView>();
            if (map != null) mapGlow = HintGlow.Create(transform, map.mapRenderer != null ? map.mapRenderer : Largest(map.transform), "Hint_Map");
            var monitor = FindAnyObjectByType<MonitorView>();
            if (monitor != null) monitorGlow = HintGlow.Create(transform, MonitorCase(monitor.transform), "Hint_Monitor");
            var folder = FindAnyObjectByType<FolderView>();
            folderView = folder;
            if (folder != null) folderGlow = HintGlow.Create(transform, FolderModel(folder), "Hint_Folder");
            var sc = StampController.Instance;
            if (sc != null)
            {
                if (sc.approveStamp != null) approveGlow = HintGlow.Create(transform, LowestPart(sc.approveStamp.transform), "Hint_StampApprove", true);
                if (sc.rejectStamp != null) rejectGlow = HintGlow.Create(transform, LowestPart(sc.rejectStamp.transform), "Hint_StampReject", true);
            }
        }

        // Mühür göstergesi: dosya açık, karar verilmemiş, mühür elde değilken (sadece ilk karara kadar)
        void Update()
        {
            if (approveGlow == null && rejectGlow == null) return;
            var fc = FocusController.Instance;
            var sc = StampController.Instance;
            bool show = !firstStampDone && FirstDay && !GuidedTour.ActiveThisRun && openedBinder && openedMap && fc != null // önce kurallara ve haritaya baksın && fc.State == FocusState.Folder && !fc.IsTransitioning
                        && folderView != null && folderView.Current != null && !folderView.Decided && !folderView.Leaving
                        && (sc == null || !sc.IsHolding);
            if (approveGlow != null && approveGlow.IsOn != show) approveGlow.Show(show);
            if (rejectGlow != null && rejectGlow.IsOn != show) rejectGlow.Show(show);
        }

        /// Kapalı dosya modeli (müşteri gelene kadar gizli olduğu için isimle bulunur)
        internal static Renderer FolderModel(FolderView f)
        {
            var root = f.closedModel != null ? f.closedModel.transform : f.transform;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name == "SM_Folder") return r;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.GetComponent<MeshFilter>() != null) return r;
            return null;
        }

        void OnArrived(CustomerData c)
        {
            // İlk müşteri dosyasını bıraktı: önce dosyayı açması gerektiğini göster
            if (folderShown || !FirstDay || GuidedTour.ActiveThisRun) return; // hazır tur varsa onun göstergeleri
            folderShown = true;
            Show(folderGlow, true);
        }

        void OnLeaving(CustomerData c) => Show(folderGlow, false);

        static Renderer MonitorCase(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.enabled && r.name == "SM_Monitor") return r;
            return Largest(root);
        }

        /// En büyük görünen model (tasarım modeli; gizli greybox değil)
        /// Mührün masaya değen parçası (taban): halka onun çevresine
        internal static Renderer LowestPart(Transform root)
        {
            Renderer best = null;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.GetComponent<MeshFilter>() == null) continue;
                if (best == null || r.bounds.min.y < best.bounds.min.y - 0.002f ||
                    (Mathf.Abs(r.bounds.min.y - best.bounds.min.y) <= 0.002f && r.bounds.size.x * r.bounds.size.z > best.bounds.size.x * best.bounds.size.z))
                    best = r;
            }
            return best;
        }

        static Renderer Largest(Transform root)
        {
            Renderer best = null;
            float vol = 0f;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.GetComponent<MeshFilter>() == null) continue;
                var s = r.bounds.size;
                float v = s.x * s.z + s.y * 0.01f;
                if (v > vol) { vol = v; best = r; }
            }
            return best;
        }

        static bool FirstDay => GameManager.Instance != null && GameManager.Instance.Run != null && GameManager.Instance.Run.day == 1;

        void OnState(GameState s)
        {
            if (s == GameState.MainMenu)
            {
                rulesShown = monitorShown = folderShown = folderAgainShown = firstStampDone = openedBinder = openedMap = openedMonitor = false;
                HideAll();
            }
            else if (s != GameState.Working && s != GameState.DayIntro && s != GameState.FinalShift)
                HideAll(); // fiş, dükkân, olay ekranlarında gösterge yok
        }

        void OnTransition(FocusState from, FocusState to)
        {
            // İlk müşterinin belgesi açıkken haritaya bakınca: belgedeki parsel haritada yanıp söner
            if (to == FocusState.Map) ShowParcel();
            if (from == FocusState.Map) HideParcel();

            if (GuidedTour.ActiveThisRun) return; // hazır tur: başka hiçbir şey yanmasın (arsa parıltısı yukarıda)

            switch (to)
            {
                case FocusState.Binder: openedBinder = true; Show(binderGlow, false); break;
                case FocusState.Map: openedMap = true; Show(mapGlow, false); break;
                case FocusState.Monitor: openedMonitor = true; Show(monitorGlow, false); break;
                case FocusState.Folder: Show(folderGlow, false); break;
            }

            // Kurallara ve haritaya baktı, masaya döndü: belgeye geri dönüp karar vermesi için dosya tekrar yanar
            if (to == FocusState.Desk && openedBinder && openedMap && !folderAgainShown && !firstStampDone && FirstDay
                && folderView != null && folderView.Current != null && !folderView.Decided)
            {
                folderAgainShown = true;
                Show(folderGlow, true);
            }

            // Belge okundu (dosyadan çıkıldı): kurallara ve haritaya bakması gerektiğini göster
            if (from == FocusState.Folder && !rulesShown && FirstDay)
            {
                rulesShown = true;
                if (!openedBinder) Show(binderGlow, true);
                if (!openedMap) Show(mapGlow, true);
            }
        }

        void ShowParcel()
        {
            var gm = GameManager.Instance;
            var c = folderView != null ? folderView.Current : null;
            if (!FirstDay || c == null || folderView.Decided || gm.Days == null || gm.Days.Length == 0) return;
            var first = gm.Days[0] != null && gm.Days[0].customers != null && gm.Days[0].customers.Length > 0 ? gm.Days[0].customers[0] : null;
            if (c != first) return; // sadece ilk müşteri (öğretici)
            var map = FindAnyObjectByType<MapView>();
            if (map == null) return;

            if (parcelTarget == null)
            {
                // Arsanın üstünde yarı saydam turuncu yama: sadece o arsa hafifçe parlar (çerçeve yok)
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "ParcelGlow";
                Destroy(go.GetComponent<Collider>());
                parcelTarget = go.transform;
                parcelTarget.SetParent(transform, false);
                parcelTarget.localScale = new Vector3(0.026f, 0.022f, 1f);
                parcelRend = go.GetComponent<Renderer>();
                var mat = HintGlow.GlowMaterial();
                if (mat != null) parcelRend.sharedMaterial = mat;
                parcelRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                parcelRend.receiveShadows = false;
            }
            parcelTarget.position = map.PixelToWorld(c.mapPixel) + Vector3.up * 0.003f;
            parcelTarget.rotation = Quaternion.Euler(90f, 0f, 0f); // yüzü yukarı
            parcelTarget.gameObject.SetActive(true);
            parcelOn = true;
        }

        Renderer parcelRend;
        bool parcelOn;
        MaterialPropertyBlock parcelMpb;

        void HideParcel()
        {
            parcelOn = false;
            if (parcelTarget != null) parcelTarget.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!parcelOn || parcelRend == null) return;
            float k = HintColors.Steady ? 0.75f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.4f * 2f * Mathf.PI);
            var col = HintColors.Orange; col.a = Mathf.Lerp(0.12f, 0.55f, k);
            parcelMpb ??= new MaterialPropertyBlock();
            parcelRend.GetPropertyBlock(parcelMpb);
            parcelMpb.SetColor(Shader.PropertyToID("_BaseColor"), col);
            parcelRend.SetPropertyBlock(parcelMpb);
        }

        void OnStamped(CustomerData c, bool approved)
        {
            HideParcel();
            firstStampDone = true;
            // İlk onay: inşaat başladı, monitöre bakması gerektiğini göster
            if (!approved || monitorShown || !FirstDay || GuidedTour.ActiveThisRun) return;
            monitorShown = true;
            if (!openedMonitor) Show(monitorGlow, true);
        }

        void HideAll()
        {
            Show(binderGlow, false);
            Show(mapGlow, false);
            Show(monitorGlow, false);
            Show(folderGlow, false);
            HideParcel();
        }

        static void Show(HintGlow g, bool on)
        {
            if (g != null) g.Show(on);
        }
    }
}
