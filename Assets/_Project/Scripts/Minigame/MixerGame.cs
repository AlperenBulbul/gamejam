using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// 1. gün minigame'i: Mikser bandı — betonu doğru karıştır.
    /// Tarif (sağ üstte simge + sayı): 1 çimento, 2 dere kumu, 3 çakıl, 1 su. Bant bunları ve fazlasını getirir.
    /// Oyuncu tarifte olanları miksere bırakır, gerisini (deniz kumu ve fazla malzeme) tıklayıp banttan atar.
    ///  - Deniz kumu ya da fazla malzeme miksere girerse: HATA +1
    ///  - Bant bitince tarifte eksik kalan her malzeme: HATA +1
    /// Yerleşim tasarımcının OKU_BENI_Santiye.md tablosundan (1280 × 720, sol üst köşeler).
    public class MixerGame : MgGame
    {
        const float BeltY = 308f;        // eşyaların altı (bant yüzeyi)
        const float EndX = 927f;         // "geri dönüş yok" çizgisi
        const float HopperX = 887f, HopperY = 268f;
        const int Traps = 3;

        class Spec
        {
            public string tex, label;
            public bool trap, liquid;
            public int need;          // tarifte kaç tane (0 = tarifte yok)
        }

        // Beton tarifi (1 çimento : 2 kum : 3 çakıl + su)
        static readonly Spec[] Good =
        {
            new Spec { tex = "SPR_Item_Cement", label = "Çimento", need = 1 },
            new Spec { tex = "SPR_Item_RiverSand", label = "Dere kumu", need = 2 },
            new Spec { tex = "SPR_Item_Gravel", label = "Çakıl", need = 3 },
            new Spec { tex = "SPR_Item_CityWater", label = "Su", liquid = true, need = 1 },
        };
        // Tarifin fazlası (banttan atılmalı)
        static readonly int[] Extra = { 1, 0, 1, 1 };
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
        float beltOffset, speed = 170f, nextGap, sinceSpawn;
        int spawned, trapsIn, goodTossed, trapsCaught, extraIn, missing;
        readonly int[] have = new int[4];
        readonly int[] tossedByType = new int[4];
        readonly TextMeshProUGUI[] haveText = new TextMeshProUGUI[4];
        readonly RawImage[] haveIcon = new RawImage[4];
        int Needed { get { int n = 0; foreach (var g in Good) n += g.need; return n; } }
        int Correct { get { int n = 0; for (int i = 0; i < Good.Length; i++) n += Mathf.Min(have[i], Good[i].need); return n; } }

        public override string Title => "MİKSER KONTROLÜ";
        public override string Instructions =>
            "Tarifteki malzemeyi\n<b><color=#F2B233>TOPLA</color></b>\n\n<size=75%><color=#B8C0C8>Gerisini tıklayıp at</color></size>";
        public override string CardImage => "SPR_Item_SeaSand";
        public override string IntroTitle => "Betonu doğru karıştır";
        public override string IntroBefore => "Topla:";
        public override string IntroAfter => "gerisini banttan at";
        public override Vector2 IntroVisualSize => new Vector2(4 * 128f, 110f);

        /// Tarif: her malzemenin simgesi ve altında sayısı
        public override void BuildIntroVisual(RectTransform parent)
        {
            for (int i = 0; i < Good.Length; i++)
            {
                float x = -IntroVisualSize.x * 0.5f + 64f + i * 128f;
                var tex = MgUI.Tex(Good[i].tex);
                var ic = new GameObject("I", typeof(RectTransform), typeof(RawImage));
                var irt = (RectTransform)ic.transform; irt.SetParent(parent, false);
                float k = tex != null ? Mathf.Min(96f / tex.width, 74f / tex.height) : 1f;
                irt.sizeDelta = tex != null ? new Vector2(tex.width * k, tex.height * k) : new Vector2(80f, 64f);
                irt.anchoredPosition = new Vector2(x, 16f);
                var ri = ic.GetComponent<RawImage>(); ri.texture = tex; ri.raycastTarget = false;
                var t = new GameObject("N", typeof(RectTransform), typeof(TextMeshProUGUI));
                var trt = (RectTransform)t.transform; trt.SetParent(parent, false);
                trt.sizeDelta = new Vector2(120f, 44f);
                trt.anchoredPosition = new Vector2(x, -44f);
                var tm = t.GetComponent<TextMeshProUGUI>();
                tm.text = Good[i].need.ToString(); tm.fontSize = 40f; tm.fontStyle = FontStyles.Bold;
                tm.color = new Color(0.95f, 0.70f, 0.20f); tm.alignment = TextAlignmentOptions.Center; tm.raycastTarget = false;
            }
        }
        public override string CardImageCaption => "<color=#FF5A4A><b>DENİZ KUMU = YASAK</b></color>";
        public override string Strip => $"{SiteLabel}   {(site != null ? site.StepLabel.ToUpper(new System.Globalization.CultureInfo("tr-TR")) : "")}   BETON KARIŞIMI";
        public override string Summary =>
            $"Doğru karışım: <b>{Correct} / {Needed}</b>\n" +
            $"Ayıklanan deniz kumu: <b>{trapsCaught} / {Traps}</b>\n" +
            $"Miksere giren fazlalık: <b>{trapsIn + extraIn}</b>";
        /// Banttan geçen her malzeme bir deneme. Doğru: deniz kumunu atmak, tarifteki malzemeyi miksere sokmak,
        /// tarifin fazlasını atmak (o malzemeden fazlası kadar).
        public override int Attempts => spawned;
        public override int Hits
        {
            get
            {
                int n = trapsCaught;
                for (int i = 0; i < Good.Length; i++) n += have[i] + Mathf.Min(tossedByType[i], Extra[i]);
                return n;
            }
        }
        public override (string key, string action)[] Hints => new[] { ("Sol tık", "Fazla malzemeyi banttan at"), ("Sağ tık / Esc", "Geri") };

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

            // Tarif kartı (irsaliye): simge + toplanan / gereken
            var wb = MgUI.Raw(root, MgUI.Tex("UI_Waybill"), 862f, 62f).transform;
            var ink = new Color(0.17f, 0.15f, 0.13f);
            var title = MgUI.Text(wb, "TARİF", 22f, 6f, 200f, 34f, 26f, ink);
            title.fontStyle = FontStyles.Bold; title.characterSpacing = 6f;
            for (int i = 0; i < Good.Length; i++)
            {
                var tex = MgUI.Tex(Good[i].tex);
                float k = tex != null ? Mathf.Min(70f / tex.width, 58f / tex.height) : 1f;
                float w = tex != null ? tex.width * k : 64f, h = tex != null ? tex.height * k : 52f;
                float cx = 52f + i * 84f;
                haveIcon[i] = MgUI.Raw(wb, tex, cx - w * 0.5f, 98f - h, w, h);
                haveText[i] = MgUI.Text(wb, "", cx - 42f, 100f, 84f, 40f, 28f, ink, TextAlignmentOptions.Center);
                haveText[i].fontStyle = FontStyles.Bold;
            }
            RefreshRecipe();

            // Yönerge paneli
            MgUI.Rect(root, new Color(0f, 0f, 0f, 0.7f), 300f, 600f, 680f, 64f);
            MgUI.Text(root, "TARİFTEKİNİ TOPLA · GERİSİNİ TIKLA, AT", 300f, 600f, 680f, 64f, 28f, new Color(0.87f, 0.9f, 0.93f), TextAlignmentOptions.Center)
                .characterSpacing = 2f;

            // Sıra: tarif + fazlası + deniz kumu, karışık (ilki tarifte olan)
            var list = new List<Spec>();
            for (int i = 0; i < Traps; i++) list.Add(Bad[i % Bad.Length]);
            for (int g = 0; g < Good.Length; g++)
                for (int n = 0; n < Good[g].need + Extra[g]; n++) list.Add(Good[g]);
            for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
            if (list[0].trap) { int g = list.FindIndex(s => !s.trap); (list[0], list[g]) = (list[g], list[0]); }
            queue.AddRange(list);
            nextGap = 0f;
        }

        public override void Tick(float dt, Vector2? click, bool action)
        {
            if (Finished) return;
            // Hız: başta yavaş, sona doğru hızlanır
            speed = Mathf.Lerp(170f, 250f, spawned / (float)Mathf.Max(1, queue.Count));
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
            if (spawned >= queue.Count && items.Count == 0 && !Finished)
            {
                // Bant bitti: tarifte eksik kalan her malzeme HATA
                for (int i = 0; i < Good.Length; i++)
                {
                    int miss = Mathf.Max(0, Good[i].need - have[i]);
                    if (miss <= 0) continue;
                    missing += miss; Defects += miss;
                }
                Finished = true;
            }
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
                goodTossed++; // tarif eksik kalırsa bant sonunda hata yazılır
                int gi = System.Array.IndexOf(Good, it.spec);
                if (gi >= 0) tossedByType[gi]++;
                Popup("ATILDI", at, new Color(0.85f, 0.88f, 0.9f), 26f);
            }
        }

        void Fall(Item it)
        {
            it.state = ItemState.Falling;
            it.t = 0f;
            it.pos = new Vector2(it.x, BeltY - it.h);
            PlayFx(it.spec.liquid ? "SPR_FX_Mixer_Splash" : "SPR_FX_Mixer_Dust", 3, HopperX + 116f, HopperY + 62f);
            var tr = new System.Globalization.CultureInfo("tr-TR");
            if (it.spec.trap)
            {
                trapsIn++;
                Defects++;
                Popup($"{it.spec.label.ToUpper(tr)} GİRDİ! HATA +1", new Vector2(760f, 230f), new Color(1f, 0.35f, 0.3f), 30f);
                return;
            }
            int gi = System.Array.IndexOf(Good, it.spec);
            if (gi >= 0 && have[gi] < Good[gi].need)
            {
                have[gi]++;
                RefreshRecipe();
                Popup("+1", new Vector2(760f, 230f), new Color(0.45f, 0.95f, 0.45f), 34f);
            }
            else
            {
                extraIn++;
                Defects++;
                Popup($"FAZLA {it.spec.label.ToUpper(tr)}! HATA +1", new Vector2(760f, 230f), new Color(1f, 0.35f, 0.3f), 30f);
            }
        }

        void RefreshRecipe()
        {
            for (int i = 0; i < Good.Length; i++)
            {
                if (haveText[i] == null) continue;
                bool done = have[i] >= Good[i].need;
                haveText[i].text = $"{have[i]}/{Good[i].need}";
                haveText[i].color = done ? new Color(0.16f, 0.55f, 0.22f) : new Color(0.17f, 0.15f, 0.13f);
                if (haveIcon[i] != null) haveIcon[i].color = done ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            }
        }

        void Remove(int i)
        {
            if (items[i].img != null) Object.Destroy(items[i].img.gameObject);
            items.RemoveAt(i);
        }
    }
}
