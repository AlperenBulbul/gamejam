using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LoadBearing
{
    /// Ayarlar > Parlaklık: sahneye URP Color Adjustments > Post Exposure olarak uygulanır.
    /// Parlaklık 50 (normal) iken hiçbir şey değişmez (kameranın post-processing'i açılmaz).
    /// GameManager bu bileşeni kendisi ekler, sahnede kurulum gerekmez.
    public class BrightnessFx : MonoBehaviour
    {
        Volume volume;
        ColorAdjustments adjust;

        void OnEnable() => GameSettings.BrightnessChanged += Apply;
        void OnDisable() => GameSettings.BrightnessChanged -= Apply;

        void Start() => Apply();

        void OnDestroy()
        {
            if (volume != null && volume.profile != null) Destroy(volume.profile);
        }

        public void Apply()
        {
            float ev = GameSettings.ExposureEV;
            bool needed = Mathf.Abs(ev) > 0.001f;
            if (volume == null)
            {
                if (!needed) return; // normal parlaklık: hiçbir şey kurma
                var go = new GameObject("BrightnessVolume");
                go.transform.SetParent(transform, false);
                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 100f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                adjust = profile.Add<ColorAdjustments>(true);
                volume.profile = profile;
            }
            adjust.postExposure.Override(ev);
            volume.weight = needed ? 1f : 0f;
            if (needed) EnablePostProcessing();
        }

        /// Sahnedeki kameraların post-processing'ini aç (pozlama ancak öyle görünür)
        static void EnablePostProcessing()
        {
            foreach (var cam in Camera.allCameras)
            {
                if (cam == null || cam.targetTexture != null) continue; // monitör vb. dokuya çizen kameralar hariç
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null && !data.renderPostProcessing) data.renderPostProcessing = true;
            }
        }

        void LateUpdate()
        {
            // Kamera sonradan açılırsa (odak değişimi vb.) onun da post-processing'i açık olsun
            if (volume != null && volume.weight > 0f && Time.frameCount % 30 == 0) EnablePostProcessing();
        }
    }
}
