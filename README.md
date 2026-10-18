# İMAR SİNAN

**CIVIL GAME JAM 2026** (16–18 Ekim 2026, ODTÜ KALTEV) için 48 saatte geliştirildi. Tema: afete hazırlık ve farkındalık.

Karaova Belediyesi'nin yeni yapı denetçisisin. Gelen inşaat başvurularını incele, fay hatlarına ve dere yataklarına dikkat et, rüşvet zarflarına direnmeye çalış. Onayladığın her bina şantiyede yükselir ve sonunda sınanır.

---

## Oyunu çalıştırma (Windows)

1. [Releases](https://github.com/AlperenBulbul/gamejam/releases) sayfasından ya da teslim edilen ZIP'ten oyunu indirin.
2. ZIP'i bir klasöre çıkartın.
3. `İmar Sinan.exe` dosyasını çalıştırın.

**Gereksinimler:** Windows 10/11 (64 bit), DirectX 12 destekli ekran kartı, fare. Ek yazılım gerekmez.

## Nasıl oynanır

- Her gün önce masadaki **gazeteyi** oku, sonra **GÜNE BAŞLA**. 2. gün yeni kural gelir: önce **kural defterine** bak.
- Gişeye gelen vatandaşın **dosyasını** aç. Başvurudaki arsayı **haritada** bul, **kural defterindeki** kurallarla karşılaştır.
- Kurala uygunsa **ONAY**, değilse **RET** mührünü bas.
- Onayladığın binalar şantiyede yükselir. **Monitörden** şantiyeleri izle, hatalı çalışan işçileri yakala, beton / kolon minigame'lerini oyna.
- Rüşvet zarfını alma. Fay hattına, dere yatağına ya da kat sınırını aşan binaya verdiğin onay sonunda deprem ya da sel olarak geri döner.

### Kontroller

| Tuş | İşlev |
|---|---|
| Fare | Masada etrafa bak |
| Sol tık | Seç / incele, mührü al, mühür bas |
| Sağ tık / Esc | Geri (yakın bakıştan masaya dön), mührü bırak |
| Esc (masada) | Duraklat / ayarlar |
| Enter | Gazetede: güne başla |
| Fare tekerleği | Haritada yakınlaştır |
| Boşluk | Haritada tehlikeli bölgeleri (fay / dere) göster / gizle |
| Tab | Harita veya kural defterinden belgeye dön |
| A / D, 1–7 | Monitörde kanal değiştir / seç |

Ayarlar (ana menü ve duraklatma menüsü): genel ses, müzik, efektler, parlaklık, fare hızı, tam ekran / pencere.

---

## Kaynak projeyi açma (geliştiriciler için)

- **Unity 6000.6.5f1** (Universal Render Pipeline)
- Büyük dosyalar (görsel, ses, model) **Git LFS** ile tutulur. Klonladıktan sonra:
  ```
  git lfs install
  git lfs pull
  ```
- Sahne: `Assets/_Project/Scenes/Office.unity`
- Kod: `Assets/_Project/Scripts` (oyun), `Assets/_Project/Scripts/Editor` (kurulum menüleri: *Load Bearing/...*)
- Proje klasörü ve kod içindeki ad **LoadBearing** (oyunun çalışma adı); oyunun adı **İmar Sinan**.

---

## Hazır kaynak beyanı

| Kategori | Kaynak / sağlayıcı | Bağlantı | Lisans / kullanım izni | Oyundaki kullanımı |
|---|---|---|---|---|
| Görsel (animasyon) | Mixamo (Adobe) | https://www.mixamo.com/ | Mixamo kullanım şartları (oyun / proje içinde ücretsiz kullanım) | Müşteri karakterlerinin konuşma, bağırma, tartışma, dans, yürüme animasyonları. Modellerimize Unity'de Humanoid olarak uygulandı. |
| Ses (müzik) | Fuzzeke – "Plan A" | https://www.youtube.com/watch?v=TXgVIwSaKME | Sanatçının paylaştığı kullanım koşulları | Sadece tanıtım videosunun (trailer) müziği; oyunun içinde yok. |
| Metin (yazı tipi) | Inter – Rasmus Andersson | https://rsms.me/inter/ | SIL Open Font License 1.1 | Menü, rapor ve arayüz yazıları |
| Metin (yazı tipi) | Liberation Sans (TextMesh Pro ile gelen) | https://github.com/liberationfonts/liberation-fonts | SIL Open Font License 1.1 | Oyun içi yazılar (Türkçe karakterler eklendi) |
| Kod (motor / paket) | Unity Technologies: Universal RP, Input System, TextMesh Pro, Video | https://unity.com/ | Unity Companion License / Unity şartları | Motor ve standart paketler; oyun kodu ekibe ait |

Kullanılmadı: hazır 3D model paketi, hazır ses efekti paketi, hazır kod / script paketi.

## AI kullanım beyanı

| Kategori | Kullanıldı mı | Araç | Amaç ve oyundaki kullanım yeri |
|---|---|---|---|
| Görsel | Kullanıldı | Claude, Meshy.ai | Tüm görsel içerikler ve 3D modeller (ofis, masa eşyaları, karakterler, harita, gazete, menüler, sinematikler) AI ile üretildi; modeller Blender'da düzenlendi. |
| Ses | Kullanıldı | AI ses üretim araçları | Oyun içindeki ses efektleri ve ortam sesleri. Müşteri mırıltısı ve telsiz cızırtısı oyun kodunda üretilir. |
| Kod | Kullanıldı | Claude | Oyunun tüm kodu (oyun mantığı, arayüz, minigame'ler, kurulum araçları) Claude ile yazıldı; ekip tarafından Unity'de test edilip yönlendirildi. |
| Metin | Kullanıldı | Claude | Karakter diyalogları, gazete ve kural metinleri, açıklamalar Claude ile oluşturuldu; ekip tarafından düzenlendi. |
| Diğer | Kullanıldı | Claude | Minigame mekanikleri ve oyun kuralları AI tarafından önerildi; ekip tarafından seçilip düzenlenerek eklendi. |

---

## Lisans

Oyunun özgün içerikleri ve kodu ekip üyelerine aittir. CIVIL GAME JAM 2026 kural kitapçığı madde 9 uyarınca organizasyona ticari olmayan kullanım izni verilmiştir. Üçüncü taraf kaynaklar kendi lisanslarına tabidir.
