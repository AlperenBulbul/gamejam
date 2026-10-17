using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// 1. gün minigame'i: Mikser bandı.
    /// Bant malzemeleri mikserin hunisine taşır. İrsaliyede olanlar (çimento, dere kumu, çakıl, şebeke suyu) girmeli;
    /// müteahhidin araya kattığı deniz kumu (kırmızı etiketli çuval) tıklanıp banttan atılmalı.
    ///  - Tuzak huniye girerse: KUSUR +1
    ///  - Doğru malzeme atılırsa (eksik malzeme): KUSUR +1
    /// Yerleşim tasarımcının OKU_BENI_Santiye.md tablosundan (1280 × 720, sol üst köşeler).
    public class MixerGame : MgGame
    {
        const float BeltY = 308f;        // eşyaların altı (bant yüzeyi)
        const float EndX = 927f;         // "geri dönüş yok" çizgisi
        const float HopperX = 887f, HopperY = 268f;
        const int Count = 14, Traps = 5;

        class Spec
        {
            public string tex, label;
            public bool trap, liquid;
        }

        static readonly Spec[] Good =
        {
            new Spec { tex = "SPR_Item_Cement", label = "Çimento" },
            new Spec { tex = "SPR_Item_RiverSand", label = "Dere kumu" },
            new Spec { tex = "SPR_Item_Gravel", label = "Çakıl" },
            new Spec { tex = "SPR_Item_CityWater", label = "Su", liquid = true },
        };
        static readonly Spec[] Bad =
        {
            new Spec { tex = "SPR_Item_SeaSand", label = "Deniz kumu", trap = true }, // tek tuzak: deniz kumu
        };

        enum ItemState { Belt, Tossed, Falling }

        class Item
        {
            public Spec spec;
            public RawImage img;
            public RectTransform rt;
            public float x, w, h, t, vx, vy, rot;
            public ItemState state;
            public Vector2 pos; // sol üst (atılırken / düşerken)
        }

        readonly List<Spec> queue = new List<Spec>();
        readonly List<Item> items = new List<Item>();
        RectTransform itemLayer;
        RawImage belt;
        float beltOffset, speed = 140f, nextGap, sinceSpawn;
        int spawned, trapsIn, goodTossed, trapsCaught;

        public override string Title => "MİKSER KONTROLÜ";
        public override string Instructions =>
            "Bu çuvalı görünce\n<b><color=#F2B233>TIKLA</color></b>\n\n<size=75%><color=#B8C0C8>Diğerlerine dokunma</color></size>";
        public override string CardImage => "SPR_Item_SeaSand";
        public override string IntroTitle => "MÜTEAHHİT MALZEMEDEN ÇALIYOR!";
        public override string IntroBefore => "Bantta";
        public override string IntroAfter => "görünce çıkart";
        public override string CardImageCaption => "<color=#FF5A4A><b>DENİZ KUMU = YASAK</b></color>";
        public override string Strip => $"{SiteLabel}   {(site != null ? site.StepLabel.ToUpper(new System.Globalization.CultureInfo("tr-TR")) : "")}   BETON KARIŞIMI";
        public override string Summary =>
            $"Ayıklanan deniz kumu: <b>{trapsCaught} / {Traps}</b>\n" +
            $"Miksere kaçan: <b>{trapsIn}</b>\n" +
            $"Yanlış atılan: <b>{goodTossed}</b>";
        public override (string key, string action)[] Hints => new[] { ("Sol tık", "Deniz kumunu banttan at"), ("Sağ tık / Esc", "Geri") };

        protected override void Build()
        {
            MgUI.Raw(root, MgUI.Tex("UI_MG_Mixer_BG"), 0f, 0f, MgUI.W, MgUI.H);

            // Bant: tek uzun parça, doku kaydırılarak hareket eder (son parça huniye kadar)
            var beltTex = MgUI.Tex("SPR_Belt_Tile");
            belt = MgUI.Raw(root, beltTex, 0f, 298f, 856f + 128f, 48f);
            belt.uvRect = new Rect(0f, 0f, (856f + 128f) / 128f, 1f);

            MgUI.Raw(root, MgUI.Tex("SPR_Mixer_Body"), 900f, 344f);
            MgUI.Raw(root, MgUI.Tex("SPR_Mixer_Mouth_Back"), HopperX, HopperY);
            itemLayer = MgUI.Group(root, "Items");
            MgUI.Raw(root, MgUI.Tex("SPR_Mixer_Mouth_Front"), HopperX, HopperY);

            // İrsaliye
            var wb = MgUI.Raw(root, MgUI.Tex("UI_Waybill"), 862f, 62f).transform;
            var ink = new Color(0.17f, 0.15f, 0.13f);
            var title = MgUI.Text(wb, "İRSALİYE", 22f, 6f, 200f, 34f, 26f, ink);
            title.fontStyle = FontStyles.Bold; title.characterSpacing = 6f;
            MgUI.Text(wb, "No 0417", 230f, 8f, 110f, 30f, 16f, ink, TextAlignmentOptions.MidlineRight);
            float[] rows = { 66f, 92f, 118f, 144f };
            for (int i = 0; i < Good.Length; i++)
            {
                MgUI.Text(wb, "· " + Good[i].label, 26f, rows[i] - 24f, 220f, 24f, 19f, ink).fontStyle = FontStyles.Bold;
                MgUI.Text(wb, "1 ad.", 250f, rows[i] - 24f, 90f, 24f, 18f, ink, TextAlignmentOptions.MidlineRight);
            }

            // Yönerge paneli
            MgUI.Rect(root, new Color(0f, 0f, 0f, 0.7f), 300f, 600f, 680f, 64f);
            MgUI.Text(root, "DENİZ KUMUNU TIKLA · BANTTAN AT", 300f, 600f, 680f, 64f, 28f, new Color(0.87f, 0.9f, 0.93f), TextAlignmentOptions.Center)
                .characterSpacing = 2f;

            // Sıra: ilki doğru malzeme, tuzaklar arka arkaya en fazla 2
            var list = new List<Spec>();
            for (int i = 0; i < Traps; i++) list.Add(Bad[i % Bad.Length]);
            for (int i = 0; i < Count - Traps; i++) list.Add(Good[i % Good.Length]);
            for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
            if (list[0].trap) { int g = list.FindIndex(s => !s.trap); (list[0], list[g]) = (list[g], list[0]); }
            queue.AddRange(list);
            nextGap = 0f;
        }

        public override void Tick(float dt, Vector2? click, bool action)
        {
            if (Finished) return;
            // Hız: başta yavaş, sona doğru hızlanır
            speed = Mathf.Lerp(140f, 230f, spawned / (float)Count);
            beltOffset = Mathf.Repeat(beltOffset - speed * dt / 128f, 1f);
            belt.uvRect = new Rect(beltOffset, 0f, belt.uvRect.width, 1f);

            // Yeni malzeme: bant belli bir mesafe ilerleyince
            sinceSpawn += speed * dt;
            if (spawned < queue.Count && sinceSpawn >= nextGap)
            {
                Spawn(queue[spawned++]);
                sinceSpawn = 0f;
                nextGap = Random.Range(230f, 330f);
            }

            // Tıklama: bantta, çizgiyi geçmemiş eşya
            if (click.HasValue)
            {
                var c = click.Value;
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    var it = items[i];
                    if (it.state != ItemState.Belt || it.x + it.w * 0.5f >= EndX) continue;
                    float top = BeltY - it.h;
                    if (c.x >= it.x - 6f && c.x <= it.x + it.w + 6f && c.y >= top - 6f && c.y <= BeltY + 6f) { Toss(it); break; }
                }
            }

            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                switch (it.state)
                {
                    case ItemState.Belt:
                        it.x += speed * dt;
                        MgUI.Place(it.rt, it.x, BeltY - it.h, it.w, it.h, new Vector2(0.5f, 0.5f));
                        if (it.x + it.w * 0.5f >= EndX) Fall(it);
                        break;

                    case ItemState.Tossed: // yukarı fırlar, döner, kaybolur
                        it.t += dt;
                        it.vy += 1400f * dt;
                        it.pos += new Vector2(it.vx, it.vy) * dt;
                        it.rot += -260f * dt;
                        MgUI.Place(it.rt, it.pos.x, it.pos.y, it.w, it.h, new Vector2(0.5f, 0.5f));
                        it.rt.localEulerAngles = new Vector3(0f, 0f, it.rot);
                        it.img.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((it.t - 0.35f) / 0.35f));
                        if (it.t > 0.7f) Remove(i);
                        break;

                    case ItemState.Falling: // hunin içine düşer (ön duvarın arkasında kalır)
                        it.t += dt;
                        it.pos += new Vector2(70f, 520f * it.t) * dt;
                        it.rot = Mathf.Lerp(0f, -22f, it.t / 0.45f);
                        MgUI.Place(it.rt, it.pos.x, it.pos.y, it.w, it.h, new Vector2(0.5f, 0.5f));
                        it.rt.localEulerAngles = new Vector3(0f, 0f, it.rot);
                        if (it.t > 0.45f) Remove(i);
                        break;
                }
            }

            TickFx(dt);
            TickPopups(dt);
            if (spawned >= queue.Count && items.Count == 0) Finished = true;
        }

        void Spawn(Spec s)
        {
            var tex = MgUI.Tex(s.tex);
            float w = tex != null ? tex.width : 128f, h = tex != null ? tex.height : 104f;
            var img = MgUI.Raw(itemLayer, tex, -140f, BeltY - h, w, h, new Vector2(0.5f, 0.5f));
            items.Add(new Item { spec = s, img = img, rt = img.rectTransform, x = -140f, w = w, h = h, state = ItemState.Belt });
        }

        void Toss(Item it)
        {
            it.state = ItemState.Tossed;
            it.t = 0f;
            it.pos = new Vector2(it.x, BeltY - it.h);
            it.vx = Random.Range(-60f, 60f);
            it.vy = -620f;
            var at = new Vector2(it.x + it.w * 0.5f, BeltY - it.h - 20f);
            if (it.spec.trap)
            {
                trapsCaught++;
                Popup("AYIKLANDI!", at, new Color(0.45f, 0.95f, 0.45f));
            }
            else
            {
                goodTossed++;
                Defects++;
                Popup($"EKSİK {it.spec.label.ToUpper(new System.Globalization.CultureInfo("tr-TR"))}! KUSUR +1", at, new Color(1f, 0.35f, 0.3f), 28f);
            }
        }

        void Fall(Item it)
        {
            it.state = ItemState.Falling;
            it.t = 0f;
            it.pos = new Vector2(it.x, BeltY - it.h);
            PlayFx(it.spec.liquid ? "SPR_FX_Mixer_Splash" : "SPR_FX_Mixer_Dust", 3, HopperX + 116f, HopperY + 62f);
            if (it.spec.trap)
            {
                trapsIn++;
                Defects++;
                Popup($"{it.spec.label.ToUpper(new System.Globalization.CultureInfo("tr-TR"))} GİRDİ! KUSUR +1", new Vector2(760f, 230f), new Color(1f, 0.35f, 0.3f), 30f);
            }
        }

        void Remove(int i)
        {
            if (items[i].img != null) Object.Destroy(items[i].img.gameObject);
            items.RemoveAt(i);
        }
    }
}
