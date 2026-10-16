using System;
using UnityEngine;
using UnityEngine.Events;

namespace LoadBearing
{
    /// Tıklanabilir her masa nesnesine eklenir. Collider bu nesnede ya da çocuklarında olmalı.
    /// focusPoint doluysa tıklayınca kamera oraya yakın bakışa geçer.
    public class Interactable : MonoBehaviour
    {
        [Tooltip("Bu nesneye hangi odak durumlarında tıklanabilir")]
        public FocusState[] clickableIn = { FocusState.Desk };

        [Header("Yakın bakış (boşsa sadece olay çalışır)")]
        public Transform focusPoint;
        public FocusState focusState = FocusState.Desk;

        [Header("İmleç ipucu")]
        [Tooltip("Üzerine gelince imlecin yanında 'Sol tık' simgesiyle yazan kelime")]
        public string prompt = "Aç";

        [Header("Olay")]
        public UnityEvent onClick;
        public event Action<Interactable> Clicked;

        [Header("Vurgu")]
        [Tooltip("Üzerine gelince renk ne kadar açılsın")]
        public float highlightBoost = 1.35f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Renderer[] rends;
        Color[] baseColors;
        MaterialPropertyBlock mpb;
        bool highlighted;

        void Awake() => CacheRenderers();

        /// Görsel değişirse (asset gelince) tekrar çağrılabilir.
        public void CacheRenderers()
        {
            rends = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[rends.Length];
            mpb ??= new MaterialPropertyBlock();
            for (int i = 0; i < rends.Length; i++)
            {
                var m = rends[i].sharedMaterial;
                baseColors[i] = (m != null && m.HasProperty(BaseColorId)) ? m.GetColor(BaseColorId) : Color.white;
            }
        }

        public bool IsClickableIn(FocusState s)
        {
            foreach (var c in clickableIn) if (c == s) return true;
            return false;
        }

        public void SetHighlight(bool on)
        {
            if (on == highlighted || rends == null) return;
            highlighted = on;
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                var c = baseColors[i];
                if (on) { var a = c.a; c *= highlightBoost; c.a = a; }
                rends[i].GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                rends[i].SetPropertyBlock(mpb);
            }
        }

        public void Click()
        {
            onClick?.Invoke();
            Clicked?.Invoke(this);
            if (focusPoint != null && FocusController.Instance != null)
                FocusController.Instance.FocusOn(focusPoint, focusState);
        }

        void OnDisable() => SetHighlight(false);
    }
}
