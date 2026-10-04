# CatapultGames — Kalan İşler

> **Geçici dosya.** İşler bitince silinecek. Kalıcı bilgi `docs/ARCHITECTURE.md`'ye yazılıyor.
>
> Son güncelleme: 2026-08-12 · Kaynak plan: 8 faz / 28 kalem

---

## 1. Nerede kaldık

| Faz | Durum |
|---|---|
| **Faz 0** — Ölçüm zemini | ✅ Bitti (F0-1, F0-2, F0-3) |
| **Faz 1** — Adalet | ⏭️ **Atlandı** — bilinçli karar, ama borç olarak duruyor |
| **Faz 2** — Merhamet | ✅ Bitti (F2-1, F2-2, F2-3) + kurtarma topu seçimi elden geçirildi |
| **Faz 3** — Okunabilirlik | ✅ Bitti (F3-1, F3-2, F3-3, F3-4) |
| **Faz 4** — İlerleme ve meta | ✅ Kodda bitti (2026-10-04) — F4-1 yıldızlar **iptal**, F4-2/3/4 yapıldı; sahneler Unity'de üretilmeli |
| **Faz 5** — Öğretici | ⬜ Başlanmadı |
| **Faz 6** — Derinlik | ✅ Bitti (F6-1, F6-2, F6-3, F6-4) |
| **Faz 7** — Büyüme | ⬜ Başlanmadı |

Yazılan kodun tamamı derleniyor (0 hata). F6-3'ün kapsama matematiği ayrıca `dotnet`
altında sayısal olarak doğrulandı (38 kontrol, hepsi geçti — buz vuruşları, taş gölgesi,
joker kovası, geri uyumluluk, skor eşikleri). **Görsel hiçbir şey Unity'de doğrulanmadı** —
aşağıdaki kontrol listesi bunun için.

---

## 2. YARIN İLK İŞ — doğrulama

### 2.1 Sahneyi yeniden üret (zorunlu)

`CatapultGames/Build Gameplay Scene`

Bu adım atlanırsa şunlar **sessizce çalışmaz**:

- `TapLaunchController._queue` ve `._queueView` → kuyruktan top seçme (F6-2)
- `ResultScreenUI._extraBallsButton` → "Keep Going" teklifi (F2-3)
- `UndoButtonUI` + `UndoBtn` → geri alma (F2-2)
- `ScoreHUD` + `ScoreLabel`/`ComboLabel` → skor ve kombo (F6-4) — sahne üretilmezse
  HUD hiç oluşmaz
- `LevelLoader._gameManager` → level değişiminde skor sıfırlama (F6-4)

### 2.2 Gözle bakılacaklar

| # | Ne | Beklenen | Bozuksa nereye bak |
|---|---|---|---|
| 1 | Level Editor'de `level1` | Siyah satır **kırmızı**, `18 < 24` diyor | `LevelValidator.cs` |
| 2 | Aynı ekranda `(1,1)` uyarısı | "Isolated cell" sarı uyarı | `LevelValidator.CollectStrayCellWarnings` |
| 3 | Bozuk level kaydetme | "Save anyway / Cancel" dialogu | `LevelEditorWindow.ConfirmSaveIfUnsound` |
| 4 | ProgressHUD yerleşimi | Toplam üstte, altında renk çubukları, çakışma yok | `ProgressHUD.BarsTop` (şu an `-170`) |
| 5 | Nişan alırken damga | Boyanacaklar hayalet, kalan damga soluk mavi çerçeve | `CellView.FootprintTint` / `Refresh()` |
| 6 | Mancınıktaki rengin hücreleri | Bir kademe yüksek + daha canlı | `CellView.AwaitingMute` |
| 7 | Yeni şekiller (Line/Column/Plus/Diag) | Gövde bloklarla okunuyor, üstteki rakam doğru | `BallVisual.BuildRunBody` / `BuildCrossBody` |
| 8 | Kuyruktan top seçme | İlk 3 top biraz büyük; dokununca öne geliyor, ateş etmiyor | `BallQueueView._pickRadiusScreenFraction` (0.055) |
| 9 | Geri al butonu | Atış inince beliriyor, basınca hücreler iniyor + top geri geliyor | `UndoButtonUI` |
| 10 | `level1` oynanışı | İlk atıştan sonra **"Dead End"** → Keep Going → level bitebiliyor | Bu doğru davranış, bkz. §4 |
| 11 | Skor HUD'u | Sağda sayaç; çok hücre boyayan atışta altında "x6 +720" belirip soluyor | `ScoreHUD`, `GameConstants.GetShotMultiplier` |
| 12 | Kombo | Üst üste boyayan atışlarda çarpan büyüyor; boşa giden atıştan sonra "COMBO LOST" | `GameManager.OnShotPainted` |
| 13 | Geri al + skor | Undo skoru **düşürüyor** ve seriyi eski hâline alıyor (pop animasyonu çalmaz) | `GameManager.RevertLastAward` |
| 14 | Sonuç ekranı | Alt metinde "Score 1.240" satırı var | `ResultScreenUI.Show(reason, offer, score)` |
| 15 | Özel hücreler (test level'i gerekir) | Editörde buz/taş/joker bas → oyna: buz iki atışta doluyor, taş hiç dolmuyor ve arkasını gölgeliyor, joker her renkle doluyor | `CellView.Refresh`, `GameConstants.GetStampPath` |
| 16 | Taşa nişan alma | Nişan önizlemede **hiç** hayalet hücre yok (damga tamamen yutulur), sadece soluk çerçeve | `PaintingSystem.Preview` |
| 17 | Buz önizlemesi | İlk atışta hayalet yalnızca çatlak yüksekliğine kadar kalkıyor, tam yükseklik değil | `CellView.PreviewBreathe` |

> **15–17 için test level'i:** Level Editor'de yeni bir level açıp `Type: Ice/Stone/Joker`
> ile birkaç hücre bas, kuyruğa 3–4 top ekle, kaydet. Mevcut `level1–5` yalnızca `Normal`
> hücre içeriyor, yani özel hücreleri onlarla göremezsin.

### 2.3 Derleme kontrolü (Unity açmadan)

```bash
dotnet build CatapultGames.Editor.csproj -v q --nologo
```

Runtime'ı da `ProjectReference` üzerinden derler. **Not:** yeni bir `.cs` dosyası
eklendiğinde Unity'nin bir kez odaklanması gerekir — csproj'daki dosya listesini Unity
üretiyor, yoksa yeni dosya derlemeye girmez ve kafa karıştırıcı "tür bulunamadı" hataları
alırsın.

---

## 3. Kalan işler

### Faz 1 — Adalet ⏭️ (atlandı, borç)

Level verisi hâlâ bozuk. Faz 4'ün yıldız sistemi buna bağımlı: yıldız "artan top oranına"
göre veriliyor ve şu anki bolluk oranları anlamsız sayı üretir. **Faz 4'ten önce yapılmalı.**

| ID | İş | Süre |
|---|---|---|
| F1-1 | `level1`'i sıfırdan yaz: 6×6, tek renk, 3 P1 top, bolluk ~2.6× | 1–2 sa |
| F1-2 | `level2`'nin **boş** top kuyruğunu doldur | 30 dk |
| F1-3 | `(1,1)`'deki hayalet yeşil hücreyi `level1–3`'ten sil; `level5`'in 2'şer hücrelik pembe/morunu büyüt ya da kaldır | 15 dk |
| F1-4 | `level3–5`'i hedef bolluk bandına çek (L1–3: 2.4–3.0× · L4–10: 1.9–2.4× · L11–20: 1.6–1.9×) | 2–3 sa |
| F1-5 | Kademeli giriş için `level6–10`: 6×6 tek renk → 8×8 power farkı → 8×8 iki renk → 10×10 + L → 12×12 üç renk | 3–4 sa |

> **Kısayol:** `level1`'in siyahı iki adet 1-sıralık şerit. Kare damgayla tavan 18 (gereken 24)
> — imkânsız. Aynı 5 top **`Line`** olsaydı tavan 31 olurdu. F6-1 geldiği için artık top
> eklemek yerine **şekli değiştirmek** en ucuz düzeltme.

### Faz 4 — İlerleme ve meta ✅ (kodda bitti, 2026-10-04)

> F4-1 (yıldız) istenmediği için yapılmadı. F4-2 yıldız yerine "kazanıldı" bayrağı tutuyor
> (`Shared/PlayerProgress.cs`), F4-3/F4-4 sahneleri `Editor/MenuSceneBuilder.cs`
> (`CatapultGames/Build Menu Scenes`) üretir. Ek olarak sonuç ekranında **Next Level** var.
> Ayrıntı: `docs/ARCHITECTURE.md` § 6.5. Aşağıdaki tablo orijinal plandır.

| ID | İş | Dosyalar | Süre |
|---|---|---|---|
| F4-1 | Yıldız skoru: 1★ bitir, 2★ ≥%15 top artır, 3★ ≥%30. `EndGame(Won)` anında `BallQueue.Remaining` / başlangıç sayısı | `GameManager.cs`, `ResultScreenUI.cs` | 2–3 sa |
| F4-2 | İlerleme kaydı: `Shared/PlayerProgress.cs` — `GetStars/SetStars/IsUnlocked`, PlayerPrefs | + `Shared/PlayerProgress.cs` | 1–2 sa |
| F4-3 | `LevelSelect` sahnesi — **kod yazılmış, sahne dosyası yok**. `GameplaySceneBuilder` desenini izleyen bir üreteçle kur (`CatapultGames/Build Menu Scenes`), Build Settings'e ekle | + `Assets/Scenes/LevelSelect.unity`, + `Editor/MenuSceneBuilder.cs` | 3–4 sa |
| F4-4 | `MainMenu` sahnesi — aynı eksik | + `Assets/Scenes/MainMenu.unity` | 2 sa |

### Faz 5 — Öğretici ⬜ (~1 gün) · Faz 1-5'e bağımlı

| ID | İş | Dosyalar | Süre |
|---|---|---|---|
| F5-1 | İlk atış için parmak işareti. `TutorialHint` MonoBehaviour, yalnız `level1` + ilk oynayışta | + `Gameplay/UI/TutorialHint.cs`, `Editor/GameplaySceneBuilder.cs` | 2–3 sa |
| F5-2 | Yeni kavramları level'e göm: `LevelMetadata`'ya opsiyonel `hint` alanı (JsonUtility ile geriye uyumlu) | `Data/LevelData.cs`, `Gameplay/LevelLoader.cs` | 2–3 sa |

> Kural: ilk oturumda en fazla 2 adım, anlatma — göster. Her yeni kavram tek başına gelsin
> (power farkı L3, ikinci renk L5, ilk `L` topu L7).

### Faz 6 — Derinlik ✅ (bitti)

| ID | İş | Dosyalar | Süre |
|---|---|---|---|
| ~~F6-1~~ | ~~Yeni damga şekilleri~~ | ✅ `Line`, `Column`, `Plus`, `Diagonal` eklendi | — |
| ~~F6-2~~ | ~~Sıradaki üç toptan birini seç~~ | ✅ `BallQueue.SelectSlot` + `BallQueueView.TryPickSlot` | — |
| ~~F6-3~~ | ~~Özel hücreler: buz / taş / joker~~ | ✅ `CellType` enum + `CellData.cellType`; kural merkezi `GameConstants` (`ColorMatches` / `GetRequiredHits` / `GetStampPath`) | — |
| ~~F6-4~~ | ~~Kombo ve skor~~ | ✅ `BallLauncher.OnShotPainted` → `GameManager` skor/kombo → yeni `ScoreHUD` | — |

> **Kalan iş:** özel hücreleri kullanan level yok. Altyapı ve editör aracı hazır, ama
> `level1–5` yalnızca `Normal` hücre içeriyor — buz/taş/joker'in tasarımdaki yeri Faz 1
> ve Faz 5'te yazılacak level'lerde belirlenecek.

### Faz 7 — Büyüme ⬜ (menü)

| ID | İş | Not | Süre |
|---|---|---|---|
| F7-1 | **Kendi fotoğrafını boya.** `ImageImportUtility` işi zaten yapıyor ama Editor assembly'sinde kilitli → `Shared/`'a taşı. Kuyruğu `LevelAutoSolver`'ı tersten çalıştırarak üret (hedef bolluk bandını tutturana kadar top ekle) | `LevelAutoSolver` da `Shared/`'a taşınmalı. Üretilen level `Resources/`'a **yazılamaz** (build-time) → `Application.persistentDataPath` | 8–12 sa |
| F7-2 | Günlük bulmaca: tarihten türeyen tohum, herkese aynı level, seri sayacı | F4-2 (kayıt) ve F0-1 (doğrulama) gerekli | 4–6 sa |
| F7-3 | Telemetri: level başına deneme sayısı, kazanınca artan top oranı, ilk kaybettiren atış sırası, tamamlanma oranı | `Shared/Telemetry.cs` şu an sadece atışlar arası süreyi logluyor. **Yayına çıkacaksan opsiyonel değil** | 2–3 sa |

**Hedef bantlar (F7-3 için):** ilk 20 level 1–3 deneme · ilk sivri uç L20–30, 3–6 deneme ·
artan top %10–30 · ilk 10 level tamamlanma ≥%85 · D1 ≥%30 (puzzle ortalaması ~%32).

---

## 4. Bilinen durum ve tuzaklar

**`level1` ilk atıştan sonra "Dead End" veriyor — bu hata değil.** Level gerçekten
bitirilemez (siyah tavanı 18, gereken 24) ve `GameManager.IsDeadEnd()` tam olarak bunu
söylemek için var. "Keep Going" ile 3 top alınca çözülebilir hâle geliyor. F1-1 yapılınca
kendiliğinden geçecek.

**Level verisi özeti** (Faz 1'de düzelecek):

| Level | Sorun |
|---|---|
| `level1` | Siyah **imkânsız** (18/24) · beyaz 1.31× · yeşil tek hücre `(1,1)` |
| `level2` | Top kuyruğu **boş** — 87 hücre, 0 top |
| `level3` | Sağlıklı, ama `(1,1)` hayalet yeşil hücresi duruyor |
| `level4` | Sağlıklı ama gevşek — 48 topun 26'sı boşa gidiyor |
| `level5` | Pembe ve mor 2'şer **çapraz** hücre, tam 1.00× pay (sıfır hata payı) |

**Bozulmaması gerekenler** (`docs/ARCHITECTURE.md` §10, 16 madde). En sık tökezlenen beşi:

1. Kapsama **asla** damga alanından hesaplanmaz — tek doğru yer `CoverageAnalyzer`.
   `GameConstants.GetPaintCellCount` sadece damganın boyutunu verir.
2. `CellColor`, `BallShape` ve `CellType` enum'larına yalnızca **sona** eklenir; sayılar
   level JSON'larında.
3. Undo yalnızca `!IsBusy` iken açılır, kuyruğun **tüm** anlık görüntüsünü geri yükler
   (tek top geri koymak purge'ü geri almaz) ve **skoru da** geri alır.
4. `Stone` hiçbir sayımda hedef değildir. Bir taşı hedef saymak level'i sessizce
   bitirilemez yapar; tek yüklem `CellView.IsPaintTarget`.
5. Kapsamada özel hücreler daima üst sınırı **gevşetir**. `Impossible` oyunu bitirdiği
   için yanlış pozitif verilemez — joker hiçbir rengin `required`'ında yoktur ama her
   rengin `ceiling`'inde sayılır.

**F6-3'ün iki ince kuralı** (ikisi de sayısal olarak test edildi):

- **Taş gölgesi yalnızca düz ışınlarda işler** (dik ve 45°) — her damga şekli bu
  çizgilerden kurulu. 4×4 karenin ışın dışı köşeleri asla gölgelenmez, çünkü onların
  "arkası" tanımsız.
- **Taşa nişan alan damga tamamen yutulur.** Gölge yolu iniş hücresini içermediği için
  bu ayrı bir kontrol; olmasa taşa nişan alarak içinden boyamak mümkün olurdu
  (`PaintingSystem.Preview` ve `CoverageAnalyzer.Walk`, aynı gerekçeyle iki yerde).

**Yeni bileşen eklerken:** yeni `[SerializeField]` → `GameplaySceneBuilder`'a `SetRef` satırı
eklenmezse sahne yeniden üretildiğinde referans sessizce kaybolur (KURAL 3).

**İş bitince:** `docs/ARCHITECTURE.md` güncellenir (KURAL 6) — yeni dosya §3/§7, veri modeli
§4, boyama kuralı §5/§10, akış §6, sahne §8, teknik borç §12.

---

## 5. Yapılanların özeti

<details>
<summary>Faz 6 · F6-3 özel hücreler + F6-4 kombo/skor — <b>en yeni</b> (2026-08-12)</summary>

**F6-3 — özel hücreler.** `Data/CellType.cs` **(yeni)**: `Normal/Ice/Stone/Joker`,
`CellData.cellType` olarak JSON'a int yazılır (alan yoksa `Normal` → eski level'ler aynen
çalışır). Kural üçe indi ve üçünün de tek yeri `GameConstants`:
`GetPaintedCells` (şekil) · `ColorMatches` (eşleşme, joker/taş) · `GetStampPath` (erişim,
taş gölgesi). Vuruş maliyeti `GetRequiredHits`.

- `CellView`: `IsFilled` artık **türetilmiş** (`HitsTaken >= HitsRequired`) — "çatlak"
  senkronize tutulacak üçüncü bir durum değil. `AddHit`/`RemoveHit`, buz için `CrackPunch`
  animasyonu, taş/buz/joker görünümleri `Refresh()` içinde. Buz önizlemesi yalnızca
  çatlak yüksekliğine kadar kalkar — önizleme yalan söylemez.
- `GridRenderer`: `ApplyHit`/`UndoHit`, `HasUnfilledWildCells`, `IsBlocking`; tüm sayımlar
  `IsPaintTarget`'tan geçer.
- `CoverageAnalyzer`: dizi üçlüsü yerine `TargetBoard` (colors/hits/wild/stone). Ölçme ile
  uygulama **tek yürüyüşü** (`Walk`) paylaşıyor. Joker ayrı bir wild satırı; renk
  `ceiling`'leri joker hücrelerini de sayar (bilerek gevşek — yanlış "imkânsız" yasak).
- `LevelAutoSolver` kendi tahta/kapsama kodunu bıraktı, artık `TargetBoard` +
  `BestPlacement`/`ApplyPlacement` üzerinden oynuyor (~40 satır tekrar silindi).
- `LevelValidator`: joker satırı, "renkli taş" ve "renksiz joker" uyarıları.
- `LevelEditorWindow`: `Type:` satırı (Normal/Ice/Stone/Joker), ızgarada taş grisi + `❄` /
  `◆` glifleri, tip sayaçları, hover'da tip adı.

**F6-4 — kombo ve skor.** `BallLauncher.OnShotPainted(hücre, landPos)` iniş olayından
**önce** atılır (yoksa kazandıran atış toplamda görünmez). `GameManager` toplamı tutar:
`hücre × 10 × yoğunluk(1–4) × kombo(1–5)`, boş atış seriyi sıfırlar. Yeni
`Gameplay/UI/ScoreHUD.cs` sayaç + "x6 +720" patlaması gösterir; sonuç ekranı alt metne
skoru ekler.

**Yol üstünde bulunan iki hata** (`dotnet` doğrulama koşumu yakaladı):
1. Taşa doğrudan nişan alan damga hiç engellenmiyordu — gölge yolu iniş hücresini
   içermediği için taşın "içinden" boyanabiliyordu. İki tüketiciye de ayrı kontrol eklendi.
2. Undo skoru geri almıyordu → "boya, geri al, tekrar boya" sınırsız puan üretiyordu.
   `RevertLastAward` + `LevelLoader.Apply` → `ResetScore()`.
</details>

<details>
<summary>Faz 0 · Ölçüm zemini (2026-08-11)</summary>

- `Shared/CoverageAnalyzer.cs` **(yeni)** — gerçek kapsama tavanı. Damga alanı yerine her topun
  tahtadaki en iyi iniş noktası ölçülür. `ceiling < required` ⇒ kanıtlanmış imkânsızlık.
- `LevelValidator` baştan yazıldı: gerçek kapsama + `globalWarnings` (< 3 hücrelik renk,
  komşusuz yalıtılmış hücre). Eşikler: `< gereken` → Error, `< 1.4×` → Warning.
- `LevelEditorWindow.ConfirmSaveIfUnsound` — kaydetmeden önce doğrulayıcı + auto-solver.
- Yol üstünde bulundu: `DrawColorRequirements` üçüncü bir (hatalı) kapsama tablosu tutuyordu
  (`{0,1,4,9}`, doğrusu 4/9/16). Üçü de tek kaynağa bağlandı.
</details>

<details>
<summary>Faz 2 · Merhamet</summary>

- **F2-1** `GameManager.IsDeadEnd()` — her inişten sonra kalan kuyruk kalan hücreleri
  kapatabiliyor mu. `ResultScreenUI.Reason`: `Won` / `OutOfBalls` / `DeadEnd`.
- **F2-2** Geri alma. `BallQueue.Capture/Restore` (tüm kuyruk — purge'ü de geri alır),
  `BallLauncher.LastShot`, `UndoButtonUI` (Canvas'ta durur, butonun kendi objesinde **duramaz**).
- **F2-3** "+3 top" teklifi, level başına bir kez.
- **Sonradan elden geçirildi:** kurtarma topları artık tahtaya bakılarak seçiliyor
  (her renk × şekil × power adayı ölçülür, en iyisi alınır, tahtadan düşülür, sonraki ona göre).
  level1 çıkmazında eski hâli 1 hücre eksik bırakıyordu; yenisi level'i bitiriyor.
</details>

<details>
<summary>Faz 3 · Okunabilirlik</summary>

- **F3-1** Damganın tamamı önizleniyor (`CellView.SetFootprint`).
- **F3-2** Mancınıktaki rengin hücreleri vurgulanıyor (`SetAwaiting` + `GridRenderer.SetActiveColor`).
  Animasyon değil, `Refresh()`'in okuduğu durum bayrağı — nişan durumları kapanınca bedava geri geliyor.
- **F3-3** ProgressHUD renk çubuklarına döndü. Yol üstünde bulundu: `ProgressHUD`'un
  **RectTransform'u yoktu**, çocukları dejenere bir ebeveyne göre hizalanıyordu.
- **F3-4** `Black` `(65,65,75)` → `(100,103,118)`, soluklaştırma 0.62 → 0.54, `ThinH` 0.14 → 0.11.
</details>

<details>
<summary>Faz 6 · F6-1 / F6-2 (2026-08-11)</summary>

- **F6-1** `Line`, `Column`, `Plus`, `Diagonal` eklendi. Kural tek yerde
  (`GameConstants.GetPaintedCells`); boyama, önizleme, doğrulayıcı, auto-solver otomatik uyumlandı.
  Planda "T" vardı → `Column` ile değiştirildi (T'nin yönü otomatik seçilmek zorunda kalırdı,
  bu da damgayı tahmin edilemez yapardı).
- **F6-2** Kuyruktan seçim, kuyruğu **yeniden sıralayarak** modellendi — `Current` seçilen top
  olduğu için atış zincirinin geri kalanı seçimden habersiz. Seçim collider yerine
  ekran-uzayı mesafesiyle (oyunda collider yok).
- `LevelAutoSolver` artık kötümser (sabit sıra varsayıyor) — "solved" hâlâ garanti,
  "failed" daha zayıf kanıt.
</details>
