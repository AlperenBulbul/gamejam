using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// 2. gün minigame'i: Kolon (şakül).
    /// Kolon kalıbı sağa sola sallanır; alttaki şakül göstergesinde kabarcık yeşil bölgedeyken tıkla (ya da Boşluk):
    /// kolon dik dökülür. Yeşil dışında dökülen kolon eğri kalır: KUSUR +1.
    /// 2 kat × 3 kolon; her kolonda sallanma hızlanır. Sonunda kısa bir deprem testi (eğri kolonlar sallanır).
    /// Yerleşim tasarımcının dokümanından: kolon tabanları y = 520, x = 360 / 640 / 920, gösterge (340, 612).
    public class ColumnGame : MgGame
    {
        static readonly float[] BaseX = { 360f, 640f, 920f };
        const float BaseY = 520f, ColW = 72f, ColH = 300f;
        const float GaugeX = 340f, GaugeY = 612f;
        const float Amplitude = 14f;     // derece
        const float Tolerance = 2.4f;    // yeşil bölge (derece)
        const int Floors = 2;

        enum Phase { Swing, Settle, NextFloor, Quake, Done }

        RectTransform world;
        readonly RawImage[] cols = new RawImage[3];
        readonly float[] angle = new float[3];
        readonly bool[] bad = new bool[3];
        RawImage bubble;
        TextMeshProUGUI floorLabel;
        int floor, col, badTotal, goodTotal;
        float phaseT, swingPhase, wait;
        Phase phase = Phase.Swing;

        public override string Title => "KOLON DÖKÜMÜ";
        public override string Instructions =>
            "Kalıplar sallanıyor! Şakül göstergesindeki <b>kabarcık yeşil bölgedeyken</b> tıkla (ya da Boşluk): kolon dik dökülür.\n" +
            "Eğri dökülen kolon deprem testinde binayı yıkar.";
        public override string Strip => $"{SiteLabel}   KAT {floor + 1} / {Floors}   KOLON {Mathf.Min(col + 1, 3)} / 3";
        public override string Summary => $"Dik kolon: {goodTotal} / {Floors * 3}\nEğri kolon: {badTotal}";
        public override (string key, string action)[] Hints => new[] { ("Sol tık / Boşluk", "Kolonu dik dök"), ("Sağ tık / Esc", "Geri") };

        protected override void Build()
        {
            world = MgUI.Group(root, "World"); // deprem testinde sallanan kısım
            MgUI.Raw(world, MgUI.Tex("UI_MG_Column_BG"), 0f, 0f, MgUI.W, MgUI.H);
            for (int i = 0; i < 3; i++)
                cols[i] = MgUI.Raw(world, MgUI.Tex("SPR_Column_Slot"), BaseX[i] - ColW * 0.5f, BaseY - ColH, ColW, ColH, new Vector2(0.5f, 0f));

            // Gösterge paneli (arkası %70 koyu)
            MgUI.Rect(root, new Color(0f, 0f, 0f, 0.7f), 320f, 574f, 640f, 100f);
            MgUI.Text(root, "KABARCIK YEŞİLDEYKEN TIKLA · BOŞLUK", 320f, 578f, 640f, 34f, 20f, new Color(0.87f, 0.9f, 0.93f), TextAlignmentOptions.Center)
                .characterSpacing = 2f;
            MgUI.Raw(root, MgUI.Tex("UI_Plumb_Gauge"), GaugeX, GaugeY);
            float zone = Tolerance / Amplitude * 280f;
            MgUI.Rect(root, new Color(0.30f, 0.75f, 0.35f, 0.75f), GaugeX + 300f - zone, GaugeY + 12f, zone * 2f, 28f);
            MgUI.Rect(root, new Color(1f, 1f, 1f, 0.9f), GaugeX + 299f, GaugeY + 8f, 2f, 36f); // orta çizgi
            bubble = MgUI.Raw(root, MgUI.Tex("UI_Plumb_Bubble_White"), GaugeX + 300f - 18f, GaugeY + 8f, 36f, 36f);

            floorLabel = MgUI.Text(root, "", 0f, 300f, MgUI.W, 80f, 60f, Color.white, TextAlignmentOptions.Center);
            floorLabel.fontStyle = FontStyles.Bold;
            floorLabel.outlineWidth = 0.25f;
            floorLabel.outlineColor = new Color32(0, 0, 0, 220);
            StartColumn();
        }

        void StartColumn()
        {
            cols[col].texture = MgUI.Tex("SPR_Column_Form");
            swingPhase = Random.Range(0f, Mathf.PI * 2f);
            phase = Phase.Swing;
        }

        float Speed => 2.1f + (floor * 3 + col) * 0.45f; // rad/sn, her kolonda hızlanır

        public override void Tick(float dt, Vector2? click, bool action)
        {
            if (Finished) return;
            phaseT += dt;
            switch (phase)
            {
                case Phase.Swing:
                    swingPhase += Speed * dt;
                    angle[col] = Amplitude * Mathf.Sin(swingPhase);
                    cols[col].rectTransform.localEulerAngles = new Vector3(0f, 0f, angle[col]);
                    UpdateBubble(angle[col]);
                    if (action) Lock();
                    break;

                case Phase.Settle:
                    wait -= dt;
                    if (wait <= 0f)
                    {
                        col++;
                        if (col < 3) StartColumn();
                        else if (floor + 1 < Floors) { phase = Phase.NextFloor; wait = 1.2f; floorLabel.text = $"KAT {floor + 2}"; }
                        else { phase = Phase.Quake; wait = 2.6f; floorLabel.text = "DEPREM TESTİ"; floorLabel.color = new Color(1f, 0.8f, 0.3f); }
                    }
                    break;

                case Phase.NextFloor:
                    wait -= dt;
                    if (wait <= 0f)
                    {
                        floor++;
                        col = 0;
                        floorLabel.text = "";
                        for (int i = 0; i < 3; i++)
                        {
                            cols[i].texture = MgUI.Tex("SPR_Column_Slot");
                            cols[i].color = Color.white;
                            cols[i].rectTransform.localEulerAngles = Vector3.zero;
                            angle[i] = 0f; bad[i] = false;
                        }
                        StartColumn();
                    }
                    break;

                case Phase.Quake: // ekran sallanır, eğri kolonlar daha çok sallanır
                    wait -= dt;
                    float k = Mathf.Clamp01(wait / 2.6f);
                    float amp = (8f + badTotal * 4f) * k;
                    ((RectTransform)world).anchoredPosition = new Vector2(Random.Range(-amp, amp), Random.Range(-amp * 0.5f, amp * 0.5f));
                    for (int i = 0; i < 3; i++)
                    {
                        float wob = bad[i] ? Mathf.Sin(phaseT * 18f + i) * 4f * k : Mathf.Sin(phaseT * 22f + i) * 0.6f * k;
                        cols[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, angle[i] + wob);
                    }
                    if (wait <= 0f)
                    {
                        ((RectTransform)world).anchoredPosition = Vector2.zero;
                        floorLabel.text = badTotal == 0 ? "BİNA SAĞLAM!" : $"{badTotal} EĞRİ KOLON";
                        floorLabel.color = badTotal == 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.4f, 0.35f);
                        phase = Phase.Done;
                        wait = 1.4f;
                    }
                    break;

                case Phase.Done:
                    wait -= dt;
                    if (wait <= 0f) Finished = true;
                    break;
            }
            TickFx(dt);
            TickPopups(dt);
        }

        void UpdateBubble(float a)
        {
            float x = GaugeX + 300f + Mathf.Clamp(a / Amplitude, -1f, 1f) * 280f;
            MgUI.Place(bubble.rectTransform, x - 18f, GaugeY + 8f, 36f, 36f);
            bubble.color = Mathf.Abs(a) <= Tolerance ? new Color(0.45f, 1f, 0.45f) : new Color(1f, 0.85f, 0.25f);
        }

        void Lock()
        {
            bool ok = Mathf.Abs(angle[col]) <= Tolerance;
            if (ok) angle[col] = 0f;
            bad[col] = !ok;
            var c = cols[col];
            c.texture = MgUI.Tex("SPR_Column_Filled");
            c.color = ok ? new Color(0.75f, 1f, 0.75f) : new Color(1f, 0.62f, 0.6f);
            c.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle[col]);

            // Kolonun tepesi (eğikse yana kayık)
            float rad = angle[col] * Mathf.Deg2Rad;
            var top = new Vector2(BaseX[col] - Mathf.Sin(rad) * ColH, BaseY - Mathf.Cos(rad) * ColH);
            PlayFx("SPR_FX_Concrete_Pour", 4, top.x, top.y - 10f);
            if (ok) { goodTotal++; Popup("DİK!", top + new Vector2(0f, -50f), new Color(0.5f, 1f, 0.5f)); }
            else { badTotal++; Defects++; Popup("EĞRİ! KUSUR +1", top + new Vector2(0f, -50f), new Color(1f, 0.38f, 0.32f), 30f); }
            UpdateBubble(angle[col]);
            phase = Phase.Settle;
            wait = 0.6f;
        }
    }
}
