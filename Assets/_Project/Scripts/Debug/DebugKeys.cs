using UnityEngine;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Sadece Editor ve Development Build'de çalışır (Debug.isDebugBuild) (Geliştirme dokümanı bölüm 8).
    /// Yeni sistem eklendikçe ilgili tuş buraya bağlanır.
    public class DebugKeys : MonoBehaviour
    {
        bool showOverlay = true;
        CustomerData lastDebugCustomer;

        // Editor'da ve Development Build'de açık, normal (release) build'de kendini kapatır
        void Awake()
        {
            if (!Debug.isDebugBuild) enabled = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var gm = GameManager.Instance;
            if (kb == null || gm == null) return;

            if (kb.f1Key.wasPressedThisFrame && StampController.Instance != null) StampController.Instance.DebugAutoStamp();
            // F2: hata başlat           -> D-11'de bağlanacak
            if (kb.f3Key.wasPressedThisFrame) { gm.Run.balance += 1000; Debug.Log("[Debug] +1000 TL"); }
            // F4: gün sonuna geç        -> D-08'de bağlanacak
            // F5: depremi tetikle       -> D-18'de bağlanacak
            // F6: sabrı dondur          -> D-08'de bağlanacak
            // F7: inşaatı 10x hızlandır -> D-11'de bağlanacak
            if (kb.f8Key.wasPressedThisFrame) gm.DebugAdvance();          // akışı bir adım ilerlet
            if (kb.f9Key.wasPressedThisFrame) gm.DebugJumpToDay(2);
            if (kb.f10Key.wasPressedThisFrame) showOverlay = !showOverlay;

            // GEÇİCİ (Adım 7'de müşteri sırası gelince gerek kalmaz):
            // Shift + 1..7 = C1..C7'nin dosyasını masaya getir, Shift + 0 = dosyayı kaldır
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (shift)
            {
                for (int i = 0; i < 7; i++)
                {
                    if (!kb[Key.Digit1 + i].wasPressedThisFrame) continue;
                    var c = gm.FindCustomer($"C{i + 1}");
                    if (c != null) { lastDebugCustomer = c; GameEvents.RaiseCustomerArrived(c); }
                }
                if (kb.digit0Key.wasPressedThisFrame && lastDebugCustomer != null)
                {
                    GameEvents.RaiseCustomerLeft(lastDebugCustomer, false);
                    lastDebugCustomer = null;
                }
            }
        }

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (!showOverlay || gm == null || gm.Run == null) return;
            var r = gm.Run;
            var focus = FocusController.Instance != null ? FocusController.Instance.State.ToString() : "-";
            string last = "-";
            if (r.decisions.Count > 0)
            {
                var d = r.decisions[r.decisions.Count - 1];
                last = $"{d.customerId} {(d.approved ? "ONAY" : "RET")} ({(d.approved == d.valid ? "doğru" : "YANLIŞ")})";
            }
            GUI.Box(new Rect(10, 10, 340, 205),
                $"DURUM: {gm.State}   GÜN: {r.day}\n" +
                $"ODAK: {focus}\n" +
                $"Bakiye: {r.balance} TL\n" +
                $"Ceza: {r.penalties}   Haksız ret: {r.wrongfulRejects}\n" +
                $"Rüşvet: {(r.anyBribe ? "alındı" : "yok")}   Kovulma bekliyor: {r.pendingFired}\n" +
                $"Son karar: {last}   Bugün ücret: {r.ledger.fees} TL\n" +
                $"\nF1 doğru mühür · F8 akış · F9 2. gün · F10 gizle\nShift+1..7 dosya getir · Shift+0 kaldır");
        }
    }
}
