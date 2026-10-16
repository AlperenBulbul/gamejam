using UnityEngine;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Sadece Editor ve Development Build'de çalışır (Debug.isDebugBuild) (Geliştirme dokümanı bölüm 8).
    /// Yeni sistem eklendikçe ilgili tuş buraya bağlanır.
    public class DebugKeys : MonoBehaviour
    {
        bool showOverlay = true;

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
            if (kb.f4Key.wasPressedThisFrame && CustomerQueue.Instance != null) CustomerQueue.Instance.DebugSkipDay();
            // F5: depremi tetikle       -> D-18'de bağlanacak
            if (kb.f6Key.wasPressedThisFrame)
            {
                CustomerController.FreezePatience = !CustomerController.FreezePatience;
                Debug.Log($"[Debug] Sabır {(CustomerController.FreezePatience ? "DONDURULDU" : "normal")}");
            }
            // F7: inşaatı 10x hızlandır -> D-11'de bağlanacak
            if (kb.f8Key.wasPressedThisFrame) gm.DebugAdvance();          // akışı bir adım ilerlet
            if (kb.f9Key.wasPressedThisFrame) gm.DebugJumpToDay(2);
            if (kb.f10Key.wasPressedThisFrame) showOverlay = !showOverlay;

        }

        static string CustomerInfo()
        {
            var q = CustomerQueue.Instance;
            var c = q != null ? q.Current : null;
            if (c == null || c.Data == null) return "-";
            string freeze = CustomerController.FreezePatience ? " (DONUK)" : "";
            return $"{c.Data.id} {c.Current} · sabır %{Mathf.RoundToInt(c.Patience * 100f)} {c.Stage}{freeze}";
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
            GUI.Box(new Rect(10, 10, 360, 225),
                $"DURUM: {gm.State}   GÜN: {r.day}\n" +
                $"ODAK: {focus}\n" +
                $"Bakiye: {r.balance} TL\n" +
                $"Ceza: {r.penalties}   Haksız ret: {r.wrongfulRejects}\n" +
                $"Rüşvet: {(r.anyBribe ? $"alındı ({r.ledger.bribe} TL)" : "yok")}{(r.bribeOnApproved != null && r.bribeOnApproved != "" ? $" · onaylandı: {r.bribeOnApproved}" : "")}   Kovulma bekliyor: {r.pendingFired}\n" +
                $"Son karar: {last}   Bugün ücret: {r.ledger.fees} TL\n" +
                $"Müşteri: {CustomerInfo()}\n" +
                $"\nF1 doğru mühür · F4 günü bitir · F6 sabrı dondur\nF8 akış · F9 2. gün · F10 gizle");
        }
    }
}
