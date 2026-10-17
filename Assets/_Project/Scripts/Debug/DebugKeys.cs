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
            var cm = ConstructionManager.Instance;
            if (kb.f2Key.wasPressedThisFrame && cm != null) cm.DebugStartFault();
            if (kb.f3Key.wasPressedThisFrame) { gm.Run.balance += 1000; Debug.Log("[Debug] +1000 TL"); }
            if (kb.f4Key.wasPressedThisFrame && CustomerQueue.Instance != null) CustomerQueue.Instance.DebugSkipDay();
            // F5: depremi tetikle       -> D-18'de bağlanacak
            if (kb.f6Key.wasPressedThisFrame)
            {
                CustomerController.FreezePatience = !CustomerController.FreezePatience;
                Debug.Log($"[Debug] Sabır {(CustomerController.FreezePatience ? "DONDURULDU" : "normal")}");
            }
            if (kb.f7Key.wasPressedThisFrame && cm != null) cm.DebugToggleSpeed();
            if (kb.f11Key.wasPressedThisFrame && cm != null) cm.DebugClick(kb.shiftKey.isPressed); // monitör gelene kadar
            if (kb.f8Key.wasPressedThisFrame) gm.DebugAdvance();          // akışı bir adım ilerlet
            if (kb.f9Key.wasPressedThisFrame) gm.DebugJumpToDay(2);
            if (kb.f10Key.wasPressedThisFrame) showOverlay = !showOverlay;
            if (kb.f12Key.wasPressedThisFrame && EnvironmentController.Instance != null) EnvironmentController.Instance.ToggleRain(); // yağmur testi

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
            var cm = ConstructionManager.Instance;
            string sites = cm != null ? cm.DebugSummary() : "-";
            int siteLines = sites.Split('\n').Length;
            GUI.Box(new Rect(10, 10, 380, 305 + siteLines * 15),
                $"DURUM: {gm.State}   GÜN: {r.day}\n" +
                $"ODAK: {focus}\n" +
                $"Bakiye: {r.balance} TL\n" +
                $"Ceza: {r.penalties}   Haksız ret: {r.wrongfulRejects}\n" +
                $"Rüşvet: {(r.anyBribe ? $"alındı ({r.ledger.bribe} TL)" : "yok")}{(r.bribeOnApproved != null && r.bribeOnApproved != "" ? $" · onaylandı: {r.bribeOnApproved}" : "")}   Kovulma bekliyor: {r.pendingFired}\n" +
                $"Son karar: {last}   Bugün ücret: {r.ledger.fees} TL\n" +
                $"Müşteri: {CustomerInfo()}\n" +
                $"Bugün: {r.ledger.stepsCompleted} adım · {r.ledger.fixes} düzeltme\n" +
                $"ŞANTİYELER:\n{sites}\n" +
                $"\nF1 doğru mühür · F4 günü bitir · F6 sabrı dondur\nF2 hata başlat · F7 inşaat x10 · F11 hatayı düzelt\nShift+F11 düzgün işçiye tıkla · F12 yağmur\nF8 akış · F9 2. gün · F10 gizle");
        }
    }
}
