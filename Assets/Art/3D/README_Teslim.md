# Load Bearing · 3D Teslim Notları (Unity 6 URP)

Kaynak: `Blender/LoadBearing_Office.blend` · Son güncelleme: 17.10.2026

Tüm FBX'ler: 1 birim = 1 metre, Y yukarı, dönüş 0, ölçek 1 (Blender'dan "Apply Transform" ile çıkarıldı ve geri yüklenerek doğrulandı).
Pivot her objede **alt orta**, şu bilinçli istisnalar dışında:

| Obje | Pivot | Neden |
|---|---|---|
| SM_Door | Menteşe kenarı | Çarpma animasyonu |
| SM_Office_Lamp | Tavandaki bağlantı noktası | Depremde sallanma |
| SM_MapPin | İğnenin ucu | Haritaya batırma |
| SM_MapOverlay | SM_Map ile aynı nokta (4 mm yukarıda) | Haritayla birebir hizalı kalsın |

Unity'de FBX içe aktarırken: **Bake Axis Conversion açık**, Scale Factor 1. Dokular FBX'e gömülü değil, aşağıdaki tabloya göre elle atanmalı.

## 1. Materyaller

| Materyal | Ayar | Doku |
|---|---|---|
| M_Palette | URP/Lit, Smoothness 0.25 | `Textures/T_Palette.png` (Filter: **Point**, Mipmap kapalı) |
| M_Palette_Metal | URP/Lit, Metallic 0.45, Smoothness 0.6 | `Textures/T_Palette.png` (aynı ayar) |
| M_Map_Karaova | URP/Lit, Smoothness 0.15 | `UI/Desk/T_Map_Karaova_Base.png` |
| M_MapOverlay | URP/Lit **Transparent**, Alpha = doku alfası, Smoothness 0.55 | `UI/Desk/T_Map_Overlay_Asetat.png` |
| M_Glass | URP/Lit **Transparent**, Base alpha ~0.08, Smoothness 0.95 | — |
| M_GlassStreak | URP/Unlit **Transparent**, beyaz, alpha ~0.07 | — |
| M_MonitorScreen | URP/Lit, Emission açık | Geliştirici: şantiye Render Texture'ı (örnek: `UI/Monitor/UI_Monitor_Sample.png`) |
| M_NewspaperFront | URP/Lit | Geliştirici basar (örnek: `UI/Desk/T_NewspaperFront_Sample.png`) |
| M_FormPage_Front | URP/Lit | Geliştirici basar (örnek: `UI/Desk/T_Form_Sample_C1.png`) |
| M_RulePage_Front | URP/Lit | Geliştirici basar (örnek: `UI/Desk/T_RulePage_03_Sample.png`) |
| M_LedOff / Green / Yellow / Grey / Red | URP/Lit, Emission | Renk koddan değişir |
| M_LampEmit | URP/Lit, Emission (sıcak beyaz) | — |
| M_Sky | URP/Unlit | — (2. gün turuncuya çekilebilir) |

Mühür izleri (decal, dosya kapağına basılır): `UI/Desk/T_StampMark_Approve.png`, `UI/Desk/T_StampMark_Reject.png` (512×256, şeffaf). Kapakta ~12×6 cm basılması uygun.

## 2. Durum değiştiren objeler

| Obje | Ne zaman |
|---|---|
| SM_Folder → SM_Folder_Open | Dosyaya yakın bakışta kapalı gizlenir, açık gösterilir |
| SM_RuleBinder → SM_RuleBinder_Open | Klasöre yakın bakışta aynı şekilde |
| SM_RuleBinder_NewTab | **2. gün** başında görünür (turuncu YENİ sekmesi, klasörün çocuğu) |
| SM_Envelope | Varsayılan gizli; sadece rüşvetli müşteride (C7) dosyanın üstünde |
| SM_MapOverlay | Tuşla aç/kapat (asetat); SM_Map'in çocuğu, zoom/kaydırmada onunla hareket eder |
| SM_Monitor_LED_A–G | Her biri ayrı nesne; rengini kod değiştirir |
| SM_Monitor_Button_Prev / _Next | Ayrı nesne; basılma animasyonu verilebilir |
| SM_Radio_Light | Ayrı nesne; uyarıda yanıp söner |
| SM_CounterGlass_Glass | Ayrı nesne; cama vurma efekti |
| SM_Office_Lamp | Ayrı nesne; depremde sallanır |
| SM_WallClock_HourHand / MinuteHand | Ayrı nesne (süs) |

## 3. Kameralar

`Office/LB_FocusCameras.fbx` (Cinemachine hedefi olarak kullanılabilir):

| Kamera | Görünüm |
|---|---|
| CAM_Player | Masa görünümü (oturan oyuncu, göz ~1.27 m) |
| CAM_Focus_Map | Harita yakın bakışı |
| CAM_Focus_Form | Açık dosya / form |
| CAM_Focus_Binder | Açık kural klasörü |
| CAM_Focus_Monitor | Güvenlik monitörü |
| CAM_Focus_Newspaper | Gazete |

## 4. Harita: arsa koordinatları

Doku: `T_Map_Karaova_Base.png` (4800 × 2640). Asetat aynı boyutta ve piksel piksel hizalı.
Piksel = sol üst köşe (0,0). UV = Unity `RaycastHit.textureCoord` (sol alt 0,0).

| Müşteri | Ada / Parsel | Piksel (x, y) | UV (u, v) | Asetat kontrolü |
|---|---|---|---|---|
| C1 | Ada 12 / P4 | 744, 760 | 0.155, 0.712 | Güvenli ✓ (iğne A) |
| C2 | Ada 7 / P19 | 2876, 640 | 0.599, 0.758 | Taşkın alanının hemen dışında, net boşluk var ✓ (iğne B) |
| C3 | Ada 3 / P8 | 2028, 248 | 0.423, 0.906 | Taşkın alanının içinde ✓ → RET |
| C4 | Ada 15 / P2 | 3200, 2184 | 0.667, 0.173 | Güvenli ✓ |
| C5 | Ada 9 / P11 | 1240, 1664 | 0.258, 0.370 | Güvenli ✓ |
| C6 | Ada 21 / P6 | 4508, 1552 | 0.939, 0.412 | Güvenli, geniş arsa ✓ |
| C7 | Ada 1 / P1 | 4180, 540 | 0.871, 0.795 | Kuzey Fayı adanın köşesini kesiyor ✓ → RET |

GDD 6.1'deki yedi şartın hepsi bu haritada sağlanıyor.

## 5. Işık referansı (Blender'daki değerler, URP'de hedef görüntü için)

| | 1. gün öğle | 2. gün ikindi |
|---|---|---|
| Güneş rengi | #FFF9E9 | #FFC78F |
| Güneş açısı | ~34° yükseklik, sağ duvardaki pencereden | ~21°, masayı çapraz keser, uzun gölge |
| Floresan | Tam güç, nötr sıcak | Biraz kısık, daha sıcak |

Render'lar: `Blender/render_1a_Masa.png`, `Blender/render_2gun_Ikindi.png`, `Blender/yakin_bakis_*.png`.

## 6. Eksik / başkasında

- Müşteri karakterleri ve animasyonlar: karakter ekibi (sahnedeki CH_C1_Hasan_Placeholder sadece ölçü referansı, FBX'e girmez).
- Jam eşyaları: şimdilik rafta (`LB_JamItems_Parked`).
