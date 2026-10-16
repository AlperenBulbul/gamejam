using UnityEngine;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Sadece Editor ve Development Build'de çalışır (Geliştirme dokümanı bölüm 8).
    /// Yeni sistem eklendikçe ilgili tuş buraya bağlanır.
    public class DebugKeys : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool showOverlay = true;

        void Update()
        {
            var kb = Keyboard.current;
            var gm = GameManager.Instance;
            if (kb == null || gm == null) return;

            // F1: doğru mühür           -> D-07'de bağlanacak
            // F2: hata başlat           -> D-11'de bağlanacak
            if (kb.f3Key.wasPressedThisFrame) { gm.Run.balance += 1000; Debug.Log("[Debug] +1000 TL"); }
            // F4: gün sonuna geç        -> D-08'de bağlanacak
            // F5: depremi tetikle       -> D-18'de bağlanacak
            // F6: sabrı dondur          -> D-08'de bağlanacak
            // F7: inşaatı 10x hızlandır -> D-11'de bağlanacak
            if (kb.f8Key.wasPressedThisFrame) gm.DebugAdvance();          // akışı bir adım ilerlet
            if (kb.f9Key.wasPressedThisFrame) gm.DebugJumpToDay(2);
            if (kb.f10Key.wasPressedThisFrame) showOverlay = !showOverlay;
        }

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (!showOverlay || gm == null || gm.Run == null) return;
            var r = gm.Run;
            var focus = FocusController.Instance != null ? FocusController.Instance.State.ToString() : "-";
            GUI.Box(new Rect(10, 10, 300, 150),
                $"DURUM: {gm.State}   GÜN: {r.day}\n" +
                $"ODAK: {focus}\n" +
                $"Bakiye: {r.balance} TL\n" +
                $"Ceza: {r.penalties}   Haksız ret: {r.wrongfulRejects}\n" +
                $"Rüşvet: {(r.anyBribe ? "alındı" : "yok")}   Kovulma bekliyor: {r.pendingFired}\n" +
                $"\nF8 akışı ilerlet · F9 2. gün · F10 gizle");
        }
#endif
    }
}
