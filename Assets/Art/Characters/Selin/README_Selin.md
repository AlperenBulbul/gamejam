# C3 Selin Kaya · Karakter Teslimi (Unity 6 URP)

Rig ve animasyonlar Meshy auto-rig ile yapıldı (Mixamo değil). Tüm dosyalar aynı iskeleti kullanır.

## Model
- `SK_Customer_Selin.fbx` — rigli model, 8.305 üçgen, boy 1,65 m, 24 kemik (parmak kemiği yok).
- Doku FBX'e gömülü. Ayrıca `T_Customer_Selin.png` olarak da var (gerekirse elle ata).
- Telefon modelde yok; gerekirse ayrı obje olarak RightHand kemiğine bağlanır.

## Unity içe aktarma
1. Model ve tüm animasyon FBX'leri: **Rig → Animation Type: Humanoid**.
2. Model için Avatar Definition: *Create From This Model*. Animasyonlarda: *Copy From Other Avatar* → SK_Customer_Selin avatarı.
3. Scale Factor 1, Convert Units açık (FBX cm biriminde; Unity'de 1,65 m gelir).
4. Animasyon FBX'lerinde mesh de var; sadece klip kullanılacak.

## Animasyonlar (30 fps, hepsi yerinde — kök hareketi yok, yürüyüşü kod taşır)

| Dosya | Süre | Döngü | Doküman karşılığı / not |
|---|---|---|---|
| A_Customer_Selin_Walk | 1,07 sn | Evet | WalkIn ve WalkOut. WalkOutAngry için hızı ~1,3x yapın |
| A_Customer_Selin_IdleCalm | 4,0 sn | Evet | IdleCalm |
| A_Customer_Selin_IdleImpatient | 5,0 sn | Evet | IdleImpatient (elini beline koyar) |
| A_Customer_Selin_IdleAngry | 8,0 sn | Evet | IdleAngry (ayak vurur) |
| A_Customer_Selin_Rage | 7,2 sn | Evet | Rage — dokümanda 2 sn; uygun 2 sn'lik bölümü kırpın, vuruş event'ini oraya koyun |
| A_Customer_Selin_Talk | 4,0 sn | Evet | Talk (üst gövde maskesiyle bindirilebilir) |
| A_Customer_Selin_ReactHappy | 1,5 sn | Hayır | ReactHappy |
| A_Customer_Selin_ReactSad | 4,9 sn | Hayır | ReactSad — gerekirse 1,5 sn'ye kırpın |
