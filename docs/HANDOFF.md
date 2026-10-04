# Devir Teslim — Nerede Kaldık (2026-10-04)

> Başka bir bilgisayardan devam etmek için. Önce bu dosyayı, sonra
> [ARCHITECTURE.md](ARCHITECTURE.md) § 6 ve § 12'yi oku. Bu dosya oturum sonunda
> güncellenir; iş bitince silinebilir.

Branch: **`claude/busy-allen-o0x8n2`** (`feature/casual-simplify`'ın devamı; main'e henüz
merge edilmedi).

---

## 1. Ne yapılıyordu?

Oyun, güncel sort/block puzzle hitleri (Block Blast, Hexa Sort) gibi **basitleştiriliyor
ve görsel olarak güçlendiriliyor**. İş dört pakete bölündü; **dördü de kodda bitti.**

| Paket | İçerik | Durum | Commit |
|---|---|---|---|
| 0 | Level editörü 4 sekmeli UI Toolkit shell'e taşındı (Editor / Gallery / Produce / Solving) | ✅ | `bed37d0` |
| 1 | Üçlü top tepsisi, iniş hücresinde ortalı tek sayılı damgalar (1/3/5), tek-dokunuş girdi, gerçek şekilli top gövdeleri, pastel palet, 66° kamera, level1–5 yeniden üretildi | ✅ | `7169b97` |
| 2 | Yuvarlatılmış küpler, açık tahta/soket teması, gradient arka plan, bloom + vignette, dolu hücre emisyonu, "GREAT!" praise, renk bitince konfeti, hit-stop | ✅ | `d4c1c12` |
| 3 | Booster çubuğu (Rainbow / Recolor / Bomb), taş = delik, joker üretimi durdu, dead-end otomatik kayıp yerine uyarı | ✅ | `c7bb461` (WIP) + bu oturum |
| 4 | Ses paketi: atış, iniş, hücre tık (pentatonik), buz çatlağı, renk fanfarı, praise stinger, kazanma/kaybetme, booster, uyarı, undo, havai fişek | ✅ | bu oturum |

## 2. Bu oturumda yapılanlar

**Paket 3'ün kalanı:**
- `GameManager`: `IsDeadEnd → EndGame(DeadEnd)` kaldırıldı, yerine `CheckDeadEnd()` geldi.
  İlk `Impossible` satır, opsiyonel `_warningLabel` üzerinde renkli bir uyarı gösterir
  ("Navy can't be finished / Undo or use a booster"): 2.5 sn, unscaled zaman, Shake +
  Haptics + ses. Aynı dead-end tekrar uyarılmaz; çözülebilir olunca, undo'da ve
  `ResetScore`'da sıfırlanır. Kayıp yalnızca `OutOfBalls`'ta.
- `ResultScreenUI.Reason.DeadEnd` ve metinleri silindi (artık ulaşılmıyordu).
- `GameConstants.GetColorDisplayName`: Black→"Navy", White→"Yellow" (enum adları legacy).
- `GameplaySceneBuilder` (KURAL 3): `BoosterSystem` GO, canvas'ta `BoosterBarUI` + 3 buton
  (sol alt sütun, x 0.03–0.21, y 0.10'dan yukarı) + "×N" sayaçlar, `LevelLoader._boosters`,
  `WarningLabel` (üst-orta, y 0.70–0.77) → `GameManager._warningLabel`, kameraya `AudioListener`.
- Temizlik: kullanılmayan `GridRenderer.IsBlocking` silindi; `CoverageAnalyzer`
  yorumlarındaki taş gölgesi ifadeleri düzeltildi; `BoosterBarUI` kare başına string
  üretmeyi bıraktı.

**Paket 4 — ses:** `Shared/GameAudio.cs` (yeni), `GameFX` gibi kendini yaratan bir singleton,
sahne bağlantısı yok. Projede ses varlığı olmadığı için **her klip çalışma zamanında
sentezleniyor** (ton + filtrelenmiş gürültü + zarf, birkaç ms). Gerçek bir kayıtla
değiştirmek için `Assets/Resources/Audio/<Sfx adı>.wav` koymak yeterli. Bağlandığı yerler:
`BallLauncher` (atış, iniş, dalga tıkları, buz), `BallVisual` (havai fişek), `ScoreHUD`
(praise), `GameManager` (fanfar, uyarı, undo, kazan/kaybet, Keep Going), `BoosterSystem`.

**Yol üstünde düzeltilen:** `ScoreHUD.PraisePop` her kesintide dinlenme konumunu kaymış
yerden okuyordu; art arda gelen praise yazısı her seferinde biraz daha yukarı kayıyordu.

## 2b. Faz 4 — menüler ve ilerleme (2026-10-04, ikinci commit)

- **Yıldız sistemi yok** — istenmedi (BACKLOG F4-1 iptal).
- `Shared/LevelOrder.cs` (sayısal level sırası, editörün numaralamasıyla ortak) ve
  `Shared/PlayerProgress.cs` (kazanılan level'ler `Won_<ad>`; kilit türetilir: ilk level ya da
  öncekisi kazanılmış).
- `GameManager.BeginRun(levelName)` (`ResetScore`'un yerine, `LevelLoader` çağırır): in-place
  level değişiminde bitmiş oyun / Keep Going teklifi de sıfırlanır. Kazanınca `MarkWon`.
  `NextLevel()` + sonuç ekranında **Next Level** butonu. Menü sahnesi Build Settings'te yoksa
  Menu/Next uyarı loglar, istisna atmaz.
- `LevelLoader.LoadByName` adı `SelectLevel` ile de kaydeder → Retry, dev seçiciyle açılan
  level'i yeniden oynatır (önceden varsayılan level'e dönüyordu).
- `MainMenuUI` (Play → ilk kazanılmamış level, "3 / 5 levels") ve `LevelSelectUI` (koddan
  numaralı kutucuklar, prefab yok) yeniden yazıldı.
- `Editor/MenuSceneBuilder.cs` → `CatapultGames/Build Menu Scenes`: `MainMenu.unity` ve
  `LevelSelect.unity`'yi üretir, Build Settings'i MainMenu = index 0 olacak şekilde sıralar.
- `LevelPickerHUD` artık sayısal sırada listeliyor (level10 eskiden level2'den önce geliyordu).

## 3. Sıradaki adım: Unity'de doğrulama (zorunlu)

**Hiçbir paket Play modunda izlenmedi.** Bu oturum da Unity olmadan, yalnızca derleme
kontrolüyle (bkz. § 4) yapıldı.

1. Unity'yi aç, odaklanınca `GameAudio.cs.meta` ve UXML/USS `.meta`'ları üretilsin/eşleşsin.
2. `CatapultGames/Build Menu Scenes`, **sonra** `CatapultGames/Build Gameplay Scene` çalıştır.
   Bu yapılmazsa menüler, Next Level, booster'lar, uyarı etiketi, AudioListener ve paket 2'nin
   çoğu **görünmez**. Sonra Play'e `MainMenu` sahnesinden bas.
3. Gözle bakılacaklar:
   - **Booster sütunu** sol tepsi topuyla ya da tahtayla çakışıyor mu? Çakışıyorsa
     `GameplaySceneBuilder.MakeBoosterButton`'daki anchor'ları ayarla. Rainbow → tepsi topu
     gökkuşağı bloklarına dönmeli, nişanda tüm hedefler parlamalı. Recolor → en çok boş
     hücresi kalan renk. Bomb → 3×3 gökkuşağı. Sayaç ×1 → ×0, buton pasifleşir.
   - **Dead-end uyarısı:** bir rengi bitiremeyecek şekilde oyna. Uyarı bir kez belirmeli,
     oyun bitmemeli. Undo'dan sonra aynı hataya tekrar düşünce yeniden uyarmalı.
   - **Sesler:** dalga tıkları tırmanıyor mu, hit-stop sırasında ses kesiliyor mu (kesilmemeli),
     genel seviye (`GameAudio.Mix` tablosu). Beğenilmeyen sesi Resources/Audio ile değiştir.
   - **Menü akışı:** MainMenu → Play doğru level'i açıyor mu; LevelSelect'te yalnız ilk level
     açık, kazanınca sonraki açılıyor mu; Next Level son level'de gizli mi; LevelSelect
     kaydırması. Testte ilerlemeyi sıfırlamak için `PlayerPrefs` temizlenir
     (Edit → Clear All PlayerPrefs) — oyunda bir sıfırlama butonu yok.
   - Paket 1–2'den kalanlar: tepsi yuvaları (x = ±1.65), "+N" sayacı, 66° kadraj, bloom
     yoğunluğu (`GameplayPostFX.asset`), `RoundedCubeMesh` ince plakalarda.
4. Unity'nin kendi derlemesinde hata çıkarsa büyük ihtimalle Unity 6 / paket API farkıdır
   (§ 4'teki kontrol 2021.3 API'sine ve stub'lara karşı derliyor).
5. Her şey yolundaysa bu branch → `main` (PR ile).

## 4. Unity olmadan derleme kontrolü (bu repoda)

`tools/compile-check/` (Unity bu klasörü görmez):

```bash
dotnet build tools/compile-check/Runtime.Check.csproj   # runtime assembly'nin tamamı
dotnet build tools/compile-check/Editor.Check.csproj    # GameplaySceneBuilder + PlayoutBoard
```

- .NET 8 SDK ve nuget.org erişimi yeterli. UnityEngine `UnityEngine.Modules` 2021.3.33'ten,
  UnityEditor `Unity3D.UnityEditor` 2018.1'den geliyor.
- uGUI / TMP / Input System / URP tipleri `stubs/` altında **yalnızca biçim** olarak duruyor:
  sadece projenin kullandığı üyeler. Yeni bir üye kullanılırsa stub'a eklenmeli. Bu kontrolün
  geçmesi Unity derlemesinin yerini tutmaz, ama imza/tip hatalarını Unity açmadan yakalar.
- Editor assembly'sinin geri kalanı (UI Toolkit level editörü) 2018.1 editör API'sinde
  olmadığı için bu kontrole dahil değil.
- Unity açıkken eski yöntem de çalışır: Unity'nin ürettiği csproj'ların glob'lu kopyaları +
  `Library/ScriptAssemblies` DLL'leri (önceki HANDOFF'taki 5 adım; git geçmişinde `c7bb461`).

## 5. Paketlerden sonra açık kalanlar

`BACKLOG.md`'deki fazlardan başlanmamış olanlar (paketler bunları kapsamıyordu):

- ~~Faz 4~~ kodda bitti (bkz. § 2b); yıldızlar iptal. Sahneler Unity'de üretilmeli.
- **Faz 5 — öğretici:** ilk atış ipucu, level'e gömülü `hint`.
- **Faz 7 — büyüme:** fotoğraftan level, günlük bulmaca, telemetri.
- **Faz 1** (eski level verisi) 2026-09-15'teki yeniden üretimle büyük ölçüde geçersiz;
  yeni level1–5 Unity'de oynanarak doğrulanmalı.
- Ses için bir **ayar/sessize alma butonu** yok (`GameAudio.Muted` hazır).
- Solving sekmesi eşikleri (0.85/0.65/0.40/0.20) kalibre edilmedi; band botu `careless-15`.
  Solver botları booster ve undo kullanmıyor; ölçülen zorluk, oyuncunun gerçek
  zorluğundan biraz yüksek.
- Test assembly'si yok; `applicationIdentifier` şablon değerinde.
- `L` ve `Diagonal` şekilleri kodda duruyor ama üretilmiyor (enum append-only).
