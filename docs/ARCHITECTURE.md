# CatapultGames — Kod Mimarisi Haritası

> Bu dosya projenin **tek kaynak referansıdır**. Yeni bir geliştirme yaparken önce burayı
> oku; sadece burada adı geçen dosyaları aç. Yapı değişirse bu dosyayı da güncelle.
>
> Son güncelleme: 2026-10-04 · Unity 6000.3.16f1 · URP 17.3.0

---

## 1. Oyun Nedir?

Mobil (portrait) bir **mancınık + boyama bulmacası**. Zeminde renkli hücrelerden oluşan
bir ızgara var; her hücrenin bir "hedef rengi" var ve başlangıçta boş (alçak plaka).
Oyuncu, kuyruktaki topları ızgaraya fırlatır. Top bir hücreye düşer ve **kendi rengiyle
eşleşen** hücreleri, şekline göre bir alan içinde doldurur (küpler yükselir).

- **Kazanma:** Boyanması gereken tüm hücreler dolduğunda.
- **Kaybetme:** Kuyruk bittiğinde ve havada top kalmadığında hâlâ boş hedef hücre varsa.
  Kalan toplar bir rengi artık bitiremiyorsa (dead-end) oyun **bitmez**, yalnızca uyarı
  verilir — undo ya da bir booster hâlâ kurtarabilir. Bkz. § 6.2.
- Bir renk tamamen bittiğinde, kuyruktaki o renge ait kalan toplar **otomatik silinir** ve
  havai fişek olarak patlatılır.
- **Özel hücreler** (`CellType`): buz iki vuruş ister, taş bir **delik**tir (hiç boyanmaz,
  hedef değildir, başka hiçbir şeyi engellemez), joker her rengi kabul eder. Joker artık
  **legacy**: kodda ve editörde duruyor, üretici vermiyor. Bkz. § 4 ve § 5.
- **Booster'lar** (level başına birer tane): **Rainbow** seçili topu her rengi boyayan
  gökkuşağı topuna çevirir, **Recolor** onu tahtada en çok ihtiyaç duyulan renge boyar,
  **Bomb** onu 3×3 gökkuşağı damgasına çevirir. Bkz. § 7 `BoosterSystem`.
- **Akış ve ilerleme:** `MainMenu` → (Play: ilk kazanılmamış level · Levels: `LevelSelect`)
  → `Gameplay2`. Kazanılan level kaydedilir (`PlayerProgress`, PlayerPrefs) ve sonrakinin
  kilidini açar; sonuç ekranında **Next Level** var. Yıldız/derece sistemi **yok** (bilinçli
  karar, 2026-10-04). Bkz. § 6.5.
- **Öğretici:** ilk level'in ilk oynanışında iki adımlık parmak işareti (en iyi hücreye dokun,
  sonra tepsiden başka renk top seç); bir kez gösterilir. Her level `metadata.hint` ile
  başlangıçta kısa bir ipucu bandı gösterebilir. Bkz. § 7 `TutorialHint`.
- **Ses:** Tüm efektler `GameAudio`'da çalışma zamanında sentezlenir; projede ses varlığı
  yok. `Resources/Audio/<Sfx>` altına konan gerçek bir klip sentezin yerine geçer.
- **Skor:** Her atış boyadığı hücre sayısı × yoğunluk çarpanı × kombo çarpanı kadar puan
  öder; boş geçen atış komboyu sıfırlar. Bkz. § 5.

Fizik motoru **kullanılmıyor**. Tüm uçuş matematiği `TrajectorySimulator` içinde elle
simüle edilir (Rigidbody yok, Collider yok). Top, `AimPreview`'in çizdiği yayın
**tam olarak aynısını** sabit sürede kat eder — mesafe ne olursa olsun. Önizlenen
hücrenin boyanan hücre olması bu sayede yapısal bir garanti.

---

## 2. Teknoloji ve Proje Ayarları

| Konu | Değer |
|---|---|
| Unity | `6000.3.16f1` (Unity 6) |
| Render | URP `17.3.0` — `Assets/Settings/` altında Mobile_/PC_ RPAsset + Renderer |
| Input | **Yeni Input System** `1.19.0` (`UnityEngine.InputSystem`). Eski `Input.*` API'si kullanılmaz |
| UI | uGUI + TextMeshPro. Canvas'lar **koddan** kurulur (prefab yok) |
| Hedef | Android (minSdk 25), portrait, referans çözünürlük 1080×1920 |
| Ürün adı | `CatapultGames`, bundleVersion `0.1.0` |

### Assembly'ler

| asmdef | Yol | Namespace | Notlar |
|---|---|---|---|
| `CatapultGames.Runtime` | `Assets/_Project/Scripts/` | `CatapultGames` | Refs: `Unity.InputSystem`, `Unity.TextMeshPro` |
| `CatapultGames.Editor` | `Assets/_Project/Scripts/Editor/` | `CatapultGames.Editor` | Editor-only, `autoReferenced: false`, Runtime'a referanslı |

> Runtime kodu Editor kodunu **göremez**. Editor tarafında bir mantık runtime'da da
> gerekiyorsa `Shared/` altına taşınmalıdır (örn. `GameConstants` boyama kuralları).

---

## 3. Klasör Yapısı

```
Assets/
├─ _Project/                    ← Tüm proje varlıkları burada
│  ├─ Scenes/, Prefabs/, Sprites/, Input/  ← şu an boş (.gitkeep)
│  └─ Scripts/
│     ├─ Data/                  ← Saf serileştirilebilir veri modelleri
│     ├─ Gameplay/              ← Sahne çalışma zamanı davranışı (MonoBehaviour)
│     │  ├─ Aim/  Ball/  Grid/  UI/
│     ├─ Shared/                ← Statik yardımcılar, matematik, sabitler, FX
│     ├─ UI/                    ← Menü ekranları (MainMenu / LevelSelect)
│     └─ Editor/                ← Level editörü (4 sekmeli shell) ve sahne üreteci
│        ├─ UI/                 ← LevelEditorWindow.uxml + .uss, LevelGridElement.uss (yalnız markup/tema)
│        ├─ Gallery/            ← Gallery sekmesi (thumbnail kartları)
│        ├─ Produce/            ← Produce sekmesi (toplu üretim runner'ı)
│        └─ Solving/            ← Solving sekmesi (headless simülatör + bot roster + benchmark)
├─ Resources/Levels/*.json      ← Level'lerin TEK yeri: editör buraya yazar, oyun buradan okur
├─ Scenes/                      ← Gameplay2.unity (aktif), SampleScene.unity
├─ Settings/                    ← URP asset'leri
├─ TextMesh Pro/                ← Paket varlıkları (dokunma)
└─ _Recovery/0.unity            ← Kurtarma artığı, kullanılmıyor
```

---

## 4. Veri Modeli (`Scripts/Data/`)

Hepsi `[Serializable]` düz sınıflardır ve `JsonUtility` ile serileştirilir.

```
LevelData
├─ LevelMetadata metadata   { levelName, author, version, hint }
├─ GridConfig    grid       { width=8, height=8, cellSize=1 }
├─ CameraConfig  camera     { fieldOfView, tiltAngle, padding, gridScreenPos, offset }
├─ CellData[]    cells      { gridX, gridY, outlineColor, isFilled, cellType }
└─ BallData[]    balls      { color, powerLevel(1-3), shape }
```

- **`CellColor`** (`Data/CellColor.cs`) — `None=0, Red=1, Green=2, Blue=3, Black=4,
  White=5, Pink=6, Purple=7, Any=8`. Bu sayılar **JSON'a int olarak yazılır**, sırası asla
  değiştirilemez; sadece sona ekleme yapılabilir. **`Any` yalnızca top rengidir** (Rainbow /
  Bomb booster'ı üretir), hiçbir hücrenin rengi olamaz; level'ler onu yazmaz, editör sunmaz.
- **`CellType`** (`Data/CellType.cs`) — `Normal=0`, `Ice=1`, `Stone=2`, `Joker=3`.
  Renkten bağımsız: bir hücrenin hem rengi hem tipi vardır.
  - `Ice` — **iki vuruş** ister; ilki çatlatır, ikincisi doldurur. Yeni hücre eklemeden
    boya maliyetini artırır.
  - `Stone` — bir **delik**: asla boyanmaz, damga onun etrafını boyar (2026-09-16'dan beri
    gölge/yutma kuralı yok; taşa nişan alan damga da komşularını boyar). Taş hiçbir sayımda hedef
    değildir (ilerleme, kazanma, kapsama).
  - `Joker` — **her renk** doldurur, ama kendi yazılı rengine dolar (resim bozulmaz).
    **Legacy:** üretici reçeteleri artık joker vermiyor (Rainbow booster'ı aynı işi oyuncunun
    elinde yapıyor); eski level'ler için kod ve editör desteği duruyor.
    Kapsamada hiçbir rengin `required`'ına yazılmaz; ayrı bir "wild" satırı olur.
  JSON'da alan yoksa `Normal` olur (geri uyumlu). **Sadece sona eklenir.**
- **`BallShape`** — `Square=0`, `L=1`, `Line=2`, `Column=3`, `Plus=4`, `Diagonal=5`.
  JSON'da alan yoksa `Square` olur (geri uyumlu). **Sadece sona eklenir** — sayılar
  level dosyalarına yazılıyor. Kural tek yerde: `GameConstants.GetPaintedCells`;
  oraya bir `case` eklemek boyama, önizleme, doğrulayıcı ve auto-solver'ın hepsini
  otomatik uyumlar. `Line`/`Column` bilerek eksene sabit — kendi yönünü seçen bir
  damga önceden tahmin edilemez ve oyuncu topu harcamadan öğrenemez.
- **`LevelMetadata.hint`** — opsiyonel tek satır; level başlarken `TutorialHint` bandında
  gösterilir. Boş = bant yok. Eski dosyalarda yok → `""`. Editörde Level kartındaki **Hint**
  alanı yazar; Generate ve Produce (overwrite) mevcut ipucunu **korur** — ipucu level
  *slotuna* aittir (o sırada öğretilen şey), üretilen tahtaya değil. Şu an `level2–4`'te var.
- **`CameraConfig`** level başına kamera çerçevelemesi; eski level'ler varsayılanlarla yüklenir.
  Varsayılanlar "düz tahta" çerçevesi (2026-09-15): tilt 66°, padding 1.12, gridScreenPos 0.60
  (tahta üst 2/3'te, altta tepsi bandı).

### JSON kuralları
- `JsonUtility` kullanıldığı için: `Dictionary` yok, `null` dizi yok, polimorfizm yok.
- **Yeni alan eklerken** mutlaka mantıklı bir varsayılan ver — eski level'ler o alanı
  içermez ve sessizce `default` alır.

---

## 5. Sabitler ve Boyama Kuralı (`Shared/GameConstants.cs`)

Projenin **kural merkezi**. Hem runtime hem editör (validator, auto-solver) buradan okur.

| Üye | Anlamı |
|---|---|
| `Gravity = -9.81f` | **Oyundaki tek yerçekimi değeri** (işaretli, dünya Y'si). İki tüketicisi var: `TrajectorySimulator` ve `LaunchSolver`. Top fizik gövdesi olmadığı için Unity'nin `Physics.gravity`'si devrede değil. Büyüklüğü artırmak yayı düzleştirip hızlandırır |
| `GravityMagnitude` | `-Gravity` — menzil çözümü pozitif g ile çalıştığı için. Derleme zamanı sabiti, ayrı ayarlanamaz |
| `TrajectorySteps = 96`, `TrajectoryTimeStep = 0.04f` | Yörünge çözünürlüğü. `AimPreview` ve `BallLauncher` **aynı değerleri kullanmak zorunda**, yoksa önizleme gerçekten farklı yere düşer |
| `GetPaintOffset(power)` / `GetPaintSize(power)` | **Kare damgalar tek sayılı ve iniş hücresinde ortalı** (2026-09-15): power 1→1×1, 2→3×3, 3→5×5; offset = −(size−1)/2. Çift boyut (2×2/4×4) kaldırıldı — merkezi olmayan damga "hedeften saptı" hissinin tek kaynağıydı. Artık her şekil için kural aynı: dokunduğun hücre boyadığının ortasıdır |
| `GetPaintedCells(x, y, power)` | Kare topun kapladığı koordinatlar |
| `GetPaintedCells(x, y, ball, gridW, gridH)` | **Şekil kararının tek yeri.** `L`→`GetLCells`, `Line`/`Column`→`GetRunCells` (iniş hücresinde ortalı, uzunluk 3/5/7), `Plus`/`Diagonal`→`GetCrossCells` (kol 1/2/3 → 5/9/13 hücre), diğer→kare kuralı. Izgara dışına taşan koordinat dönebilir; her tüketici sınır kontrolü yapar |
| `GetLCells(...)` | L topu: iniş hücresinden ızgara **iç yönüne** iki kol uzatır, her kol kenara kadar gider. En yakın köşeye göre otomatik döner. 10×10'da 10+10'luk bir L |
| `GetPaintCellCount(ball, w, h)` | Damganın **boyutu**; kare için `size²`, L için `w + h − 1`. **Kapsama değildir** — bkz. `CoverageAnalyzer` |
| `ColorMatches(cellColor, cellType, ballColor)` | **Eşleşme kararının tek yeri.** `Joker` her rengi kabul eder, `Stone` hiçbirini, boş tahta hücresi hedef değildir; `Any` (gökkuşağı) top her gerçek hedefi doldurur |
| `GetRequiredHits(cellType)` | Hücrenin dolmak için yediği vuruş sayısı — `Ice` 2, diğerleri 1 |
| `PointsPerCell = 10` · `GetShotMultiplier(cells)` · `GetComboMultiplier(streak)` · `MaxComboMultiplier = 5` | Skor kuralı. Yoğunluk çarpanı 1/2/3/4 (eşikler 1, 2, 4, 8 hücre), kombo çarpanı = üst üste boyayan atış sayısı (5'te tavan). Toplamı `GameManager` tutar |
| `CellColorPalette` (`Color32[9]`) | **İndeksleri `CellColor` enum'ıyla birebir aynı olmalı.** 2026-10-04'ten beri **block-match paleti**: yedi doygun, birbirinden en uzak ton — Red→kırmızı, Green→yeşil, Blue→mavi, Black→**turuncu**, White→sarı, Pink→**camgöbeği**, Purple→**pembe**; Any→gökkuşağı topunun tabanı. Mor yok, çünkü tahtanın kendisi mor. Enum adları JSON uyumluluğu için eski kimliklerdir; oyuncuya ve editöre görünen ad `GetColorDisplayName`'dir. Önceki pastel set açık tahtada soluyordu, lacivert/lavanta/mavi birbirine karışıyordu |
| `GetColor` / `GetColorF` | Palet erişimi |
| `GetColorDisplayName(c)` | Oyuncuya gösterilen renk adı (Black→"Navy", White→"Yellow"…). Bir rengi **adıyla** anan her metin (ör. dead-end uyarısı) buradan okur, enum adından değil |

> Boyama kuralını değiştirecek her iş **sadece burada** yapılmalı; `PaintingSystem`,
> `AimPreview`, `LevelValidator`, `LevelAutoSolver` ve `CoverageAnalyzer` otomatik olarak
> uyumlu kalır. Bir damganın bir hücreyi boyayıp boyamadığı iki soruya iner ve ikisinin de
> tek cevap yeri burasıdır: **şekil** (`GetPaintedCells`) ve **eşleşme** (`ColorMatches`).
> Üçüncü soru olan **erişim** (`GetStampPath`, taş gölgesi) 2026-09-16'da kaldırıldı — taş
> artık bir delik.

---

## 6. Runtime Akışı

### 6.1 Level yükleme

```
LevelLoader.Start()
  └─ PlayerPrefs["SelectedLevel"]  (yoksa _defaultLevelName = "level1")
     └─ Resources.Load<TextAsset>("Levels/<ad>")   ← bulunamazsa LoadAll ile ilk level
        └─ LevelSerializer.FromJson  →  LevelData
           └─ Apply(data):
              1. GridRenderer.BuildGrid(data)        → CellView'leri üretir
              2. GridBoard.Rebuild(data.grid)        → arka plan quad'ı
              3. GridCameraController.FitToGrid(...) → kamerayı çerçeveler
              4. LaunchAreaAnchor.Reanchor()         → mancınığı ekran altına sabitler
              5. BallQueue.Load(SanitizeBalls(...))  → powerLevel 1..3'e clamp'lenir
              6. ProgressHUD.Bind(grid)
              7. GameManager.BeginRun(levelName)     → level değişimi yeni bir koşu: skor,
                                                       Keep Going teklifi, bitmiş oyun ve
                                                       dead-end uyarısı sıfırlanır
              8. BoosterSystem.ResetForLevel()       → booster sayaçları dolar
              9. TutorialHint.BeginLevel(ad, hint)   → ipucu bandı; ilk level + ilk kez ise
                                                       parmak işareti
```

> 7. adım şart: level seçici (dev dropdown) sahneyi yeniden yüklemeden level değiştirir,
> yoksa önceki tahtanın skoru (ve bitmiş bir oyunun sonuç paneli) devam eder.
> `LoadByName` ayrıca adı `SelectLevel` ile kaydeder: Retry sahneyi yeniden yüklediğinde
> dev seçiciyle açılmış level'i de yeniden oynatsın diye.

**Kritik sıra:** `Reanchor()` mutlaka `_queue.Load()`'dan **önce** çağrılır, yoksa kuyruk
topları eski waypoint konumlarında kalır.

### 6.2 Girdi → Atış → Boyama → Sonuç

```
TapLaunchController (basış tepsideki bir topun üstündeyse)
  └─ BallQueueView.TryPickSlot → BallQueueView.Select(slot)
       └─ BallQueue.SelectSlot(offset)  ← topu öne alır; view yuva→offset eşlemesini
          düzeltir, diğer iki top yerinde kalır (halka seçili yuvaya geçer)
       → OnChanged → AimPreview.RefreshActiveColor (tahtadaki renk vurgusu değişir)
     (bu hareket atış saymaz)

TapLaunchController (hücreye dokun / basılı tut-kaydır, bırak)
  · kaydırma yok, kaldırma yok: hedef = parmağın altındaki hücre
  · origin = BallQueueView.CurrentLaunchOrigin (seçili tepsi topunun yeri)
  └─ LaunchSolver.SolveToCell(origin) → tam o hücrenin merkezine düşen hız
     · basılıyken AimPreview.ShowArc(origin, hız) ile yay + damganın tamamı önizlenir
     · parmak tepsi bandına geri çekilip bırakılırsa iptal (IsOverTray)
                                                ▼
                                      BallLauncher.Launch(origin, velocity)
                                        1. TrajectorySimulator.Simulate → arc + landPos
                                        2. BallQueue.Consume()        (hemen tüketilir)
                                        3. coroutine DoLaunch:
                                           · BallVisual + trail, GameFX.LaunchPuff
                                           · arc boyunca tween (_flightDuration)
                                           · GameFX.Impact + ImpactRing
                                           · PaintWave: PaintingSystem.PaintTargetsOrdered
                                             → GridRenderer.ApplyHit (kademeli, Haptics)
                                                 └─ CellView.AddHit: Ice çatlar / dolar
                                             → GameAudio.PlayTick(n): dolan her küp
                                               pentatonik dizide bir basamak yukarı
                                             → GameFX.Bloom
                                           · OnShotPainted(boyanan sayısı, landPos)
                                                ▼
                                      GameManager.OnShotPainted
                                        · skor = hücre × PointsPerCell × yoğunluk × kombo
                                        · boş atış (0 hücre) komboyu sıfırlar
                                        · OnShotScored / OnScoreChanged → ScoreHUD
                                           · OnBallLanded(landPos)
                                                ▼
                                      GameManager.OnBallLanded
                                        · PurgeCompletedColors()  → BallQueue.RemoveColor
                                            (boş joker hücresi varsa purge YAPILMAZ —
                                             her renk hâlâ işe yarayabilir)
                                            → BallQueueView havai fişek + PulseColor + Shake
                                        · grid.AllColoredCellsFilled() → EndGame(Won)
                                        · launcher.IsBusy ise dur (volenin bitmesini bekle)
                                        · queue.IsEmpty                → EndGame(OutOfBalls)
                                        · CheckDeadEnd()               → yalnız UYARI, oyun sürer
                                             └─ CoverageAnalyzer: kalan toplar kalan
                                                hücreleri kapatabiliyor mu? İlk Impossible
                                                satır → _warningLabel "Navy can't be
                                                finished", Shake + Haptics + ses. Aynı renk
                                                tekrar uyarılmaz; çözülebilir hâle gelince
                                                (undo / booster / Keep Going) sıfırlanır
                                                ▼
                                      ResultScreenUI.Show(reason, offerExtraBalls, score)
                                        · Keep Going → GameManager.GrantExtraBalls()
                                             └─ kuyruğa +N top, _gameOver = false,
                                                panel gizlenir, level kaldığı yerden sürer
```

### 6.3 Booster

```
BoosterBarUI (3 buton, sol alt sütun)
  └─ BoosterSystem.Use(type)   ← CanUse: sayaç > 0, oyun sürüyor, Current var, etkisi olacak
       · seçili tepsi topunun BallData'sını YERİNDE değiştirir
         Rainbow → color = Any · Recolor → en çok boş hücresi kalan renk · Bomb → Any + Square + power 2
       · BallQueue.NotifyCurrentChanged → OnChanged
            → BallQueueView yeniden çizer · AimPreview.RefreshActiveColor (Any → her hedef parlar)
```

Booster bir atış **değildir**: kuyruk sırası, skor ve undo kaydı değişmez. Undo booster'ı
iade etmez — atışın kuyruk anlık görüntüsü top referanslarını tuttuğu için geri alınan
atış güçlendirilmiş topu geri verir, bu dürüst sonuç. Sayaçlar `LevelLoader.Apply` →
`ResetForLevel()` ile her level'de dolar (`_perLevel`, varsayılan 1).

### 6.4 Geri alma (undo)

```
BallLauncher.Launch()
  └─ queueBefore = BallQueue.Capture()      ← Consume()'dan ÖNCE
     └─ (uçuş + boyama dalgası; vuruş alan hücreler listelenir)
        └─ LastShot = { queue, painted, ball }   ← iniş anında yazılır
                                                ▼
UndoButtonUI (her kare GameManager.CanUndo okur; yoksa butonu gizler)
  └─ GameManager.UndoLastShot()
       · painted hücrelerine GridRenderer.UndoHit  ← boyama sadece vuruş EKLİYOR,
         (çatlamış buz eski hâline döner)             aynı vuruşları geri almak tam ters işlem
       · BallQueue.Restore(queue)               ← purge'ü de geri alır
       · RevertLastAward()                      ← skoru ve kombo serisini geri alır
       · launcher.ClearLastShot()               ← tek adım
```

Skorun geri alınması opsiyonel değil: yoksa "boya → geri al → aynı yeri boya" sonsuz
puan üretir.

`CanUndo` = `!IsOver && !launcher.IsBusy && LastShot != null`. Havadayken kapalı olması
şart: "son atış" birden fazla top uçarken belirsizdir ve boyama dalgası sürerken kuyruğu
geri sarmak `_activeFlights` sayacıyla çakışır.

**Eşzamanlı atış:** Oyuncu bir top havadayken tekrar ateş edebilir. `BallLauncher._activeFlights`
sayacı, boyama dalgası bitene kadar azaltılmaz; `GameManager` kaybetme kararını
`!_launcher.IsBusy` şartına bağlar. Bu yüzden `IsBusy`/`_activeFlights` mantığına dokunurken
dikkatli ol.

### 6.5 Menüler ve ilerleme

```
MainMenu (MainMenuUI)
  ├─ Play   → PlayerProgress.NextToPlay()  (ilk kazanılmamış level; hepsi bittiyse sonuncu)
  │           → LevelLoader.SelectLevel → Gameplay2
  └─ Levels → LevelSelect (LevelSelectUI: LevelOrder sırasında kutucuklar;
              kazanılmış = nane, açık = mavi, kilitli = gri ve tıklanamaz) → Gameplay2

Gameplay2 · EndGame(Won) → PlayerProgress.MarkWon(levelName)
  ResultScreenUI: Retry (sahneyi yeniden yükler) · Menu → MainMenu
                  Next Level (yalnız kazanınca ve sonraki varsa) → GameManager.NextLevel
                    → SelectLevel(LevelOrder.Next) + sahneyi yeniden yükler
                    (son level'den sonra → LevelSelect)
```

Kilit **türetilir, saklanmaz**: bir level ilk level'se ya da önceki kazanılmışsa açıktır
(`PlayerProgress.IsUnlocked`). Kayıt level **adına** göredir (`Won_<ad>`), sıraya değil —
araya level eklemek başka bir tahtayı "kazanılmış" yapmaz. Menü sahneleri henüz
üretilmediyse `GameManager` Menu/Next butonunda istisna atmak yerine uyarı loglar.

---

## 7. Dosya Dosya Sorumluluklar

### `Scripts/Gameplay/`

| Dosya | Tip | Sorumluluk / Önemli API |
|---|---|---|
| `GameManager.cs` | Mono | Kazanma/kaybetme kararı, biten renklerin kuyruktan temizlenmesi, sonuç ekranı, `RestartLevel()`, `GoToMainMenu()`, **`NextLevel()`** / `HasNextLevel`, `LevelName`. **`BeginRun(levelName)`** (`LevelLoader` çağırır): skor, Keep Going teklifi, bitmiş oyun ve uyarı sıfırlanır — in-place level değişiminde de temiz koşu. Kazanınca `PlayerProgress.MarkWon`. Sahne adları `_mainMenuScene` / `_levelSelectScene` (üreteç yazar); sahne Build Settings'te yoksa yükleme yerine uyarı. `IsOver` diğer sistemlerce okunur. **Skor:** `Score` / `ComboStreak`, `OnShotScored(ShotScore)` + `OnScoreChanged(int)` event'leri, `ResetScore()` (`BeginRun`'ın parçası). Puan `BallLauncher.OnShotPainted`'ten gelen hücre sayısıyla `GameConstants` kurallarından hesaplanır; son ödül saklanır ki **undo skoru da geri alsın**. `PurgeCompletedColors` boş **joker** hücresi varken hiç purge yapmaz. Ayrıca: `CheckDeadEnd()` (kalan toplar bir rengi bitiremiyorsa **uyarır, bitirmez**: `CoverageAnalyzer`'ın ilk `Impossible` satırı → opsiyonel `_warningLabel` üzerinde renkli "Navy can't be finished / Undo or use a booster", `_warningDuration` 2.5 sn, unscaled; `_warnedKey` aynı dead-end'i tekrar uyarmaz, çözülebilir olunca / undo / `BeginRun`'da sıfırlanır), `UndoLastShot()` / `CanUndo`, `GrantExtraBalls()` (level başına **bir kez**, `_extraBallCount`). Kurtarma topları `BuildRescueBalls` ile **tahtaya bakılarak** seçilir: her (renk, şekil, power) adayı `CoverageAnalyzer.BestPlacement` ile ölçülür, en iyisi alınır ve `ApplyPlacement` ile tahtadan düşülerek sonraki top ona göre seçilir — üç bağımsız tahmin değil, bir **plan**. Beraberlikte küçük damga kazanır (bitirmeye yeter, fazlası değil). `L` aday havuzunda yok: kolları ızgara kenarına gittiği için kurtarmaz, level'i siler |
| `LevelLoader.cs` | Mono | JSON → sahne. `LoadByName(name)`, `Apply(LevelData)`, statik `SelectLevel/ClearSelection` (PlayerPrefs `"SelectedLevel"`). `LoadByName` başarılı yüklemeyi `SelectLevel` ile de kaydeder (Retry aynı level'i açsın). `Apply` sonunda `GameManager.BeginRun(ad)` — level seçici sahneyi yeniden yüklemiyor; `Apply(data)` dışarıdan çağrılırsa ad `null` olur (ilerleme kaydedilmez) |
| `LaunchAreaAnchor.cs` | Mono | Tepsi kökünü ekranın alt bandına sabitler (`_screenY=0.10`). Sadece çözünürlük değişince yeniden hesaplar (shake ile titremesin diye) |
| `BoosterSystem.cs` | Mono | Üç booster (`BoosterType`: `Rainbow` / `Recolor` / `Bomb`) ve level başına sayaçları (`_perLevel`). `CanUse(type)` (sayaç, `IsOver`, seçili top var mı, etkisi olacak mı), `Use(type)` seçili tepsi topunun `BallData`'sını **yerinde** değiştirir ve `BallQueue.NotifyCurrentChanged()` çağırır — tepsi, nişan ve fırlatıcı yeni topu kendi mevcut yollarından görür, ikinci bir "hangi top" kavramı yok. `Recolor` = en çok boş hücresi kalan renk. `ResetForLevel()` (`LevelLoader`), `OnChanged` event'i |
| `BackgroundGradient.cs` | Mono | Kameraya bağlı, frustum'u dolduran tek unlit quad + çalışma zamanında üretilen 1×64 gradient dokusu (menekşe → koyu erik — tahtadan koyu, böylece tahta ekranın aydınlık merkezi olur). Aspect değişince yeniden boyutlanır; materyal/doku `OnDestroy`'da yok edilir |

### `Scripts/Gameplay/Grid/`

| Dosya | Tip | Sorumluluk / Önemli API |
|---|---|---|
| `GridRenderer.cs` | Mono | Izgaranın sahibi. `BuildGrid/ClearGrid`, `TryGetCell/GetCell`, `SetFilled(Batch)`, **`ApplyHit(x,y)`** (bir damga vuruşu; dolduysa `true` — boyama dalgası bunu kullanır) ve **`UndoHit(x,y)`**, `SetHighlight/SetPreview/SetFootprint/ClearHighlights` (üçü birlikte temizlenir), `SetActiveColor(color)` (mancınıktaki rengin boş hücrelerini vurgular — joker hücreleri **her** renkte parlar), `GridToWorld/WorldToGrid/RaycastToGrid/RaycastToGridClamped`, `CountByColor()` (tahsissiz, enum sırasında), `AllColoredCellsFilled()`, **`HasUnfilledWildCells()`** (purge kapısı), `WorldCenter`, `PulseColor`. `OnGridChanged` event'i. Tüm sayımlar `CellView.IsPaintTarget` üzerinden geçer → **taş hiçbir yerde hedef sayılmaz** |
| `CellView.cs` | Mono | Tek hücre = tahta düzleminde yatan **katmanlı sprite'lar** (block-match yapısı, 2026-10-04; çizimler `TileArt`): **Socket** (damalı tahta karesi; taşta koyu delik `TileArt.Hole`; footprint/landing'de açılır) · **Marker** (boş hedef: hücre ortasında **gerçek hedef renginde** küçük düz kare — `MarkerResting` 0.40, renk elindeyken `MarkerAwaiting` 0.56; dolu taşla asla karışmaz ve alfa ile soluklaşmaz; joker beyaz) · **Piece** (parlak taş: dolunca tam boy; nişanda nefes alan hayalet) · **Ice** (dolana kadar buz örtüsü, çatlayınca incelir). Yığılmayı derinlik değil **sortingOrder** belirler (`BoardOrder` −20 panel, Socket −10, Marker −5, Piece 0, Ice 5; toplar 20). `Visual` çocuğu X'te 90° yatırılır: sprite'ın üstü ekranın yukarısına (+Z), yüzü kameraya bakar; `Padding` 0.04. `Type` (`CellType`), `HitsTaken`/`HitsRequired`, **`IsFilled` türetilmiştir** (`HitsTaken >= HitsRequired`), `IsPaintTarget` (taş ve boş tahta hariç). `SetFilled`, **`AddHit()`** (dolarsa `PopIn` — marker boyundan taşa büyür, kameraya doğru hafif zıplar; dolmazsa `CrackPunch` — buz sallanır), **`RemoveHit()`** (undo), `SetHighlight`, `SetPreview` (yalnız çatlatacak atışta hayalet daha soluk — önizleme yalan söylemez), `SetFootprint`, `SetAwaiting`, `Pulse(delay)`. **`Refresh()` tek toplayıcıdır.** Renk `SpriteRenderer.color` (vertex rengi) ile verilir: hücre başına materyal yok, sızıntı riski yok |
| `PaintingSystem.cs` | static | Boyama kuralı uygulayıcı (canlı ızgara üstünde): `Paint` (mutasyon), `Preview` (salt okuma), `PaintTargetsOrdered` (iniş noktasına yakınlık sırasıyla), `CountPaintable`. İki kuralı da `GameConstants`'tan okur (şekil / eşleşme); taş bir delik olduğu için ayrı bir erişim kontrolü yoktur |
| `GridCameraController.cs` | Mono | Tek perspektif kamera. `FitToGrid(grid, camCfg)` FOV + tilt + padding + `gridScreenPos`'tan konumu otomatik çözer. Sahne `FrontZ = -9f` sabitiyle mancınığı da kadraja alır |
| `GridBoard.cs` | Mono | Izgaranın altındaki **koyu mor panel**: tek 9-slice sprite (`TileArt.Panel`), ızgaradan her yönde 0.30 hücre büyük, Y −0.01, `sortingOrder` `CellView.BoardOrder`. `Rebuild(GridConfig)` |

### `Scripts/Gameplay/Ball/`

| Dosya | Tip | Sorumluluk / Önemli API |
|---|---|---|
| `BallQueue.cs` | Mono | Saf mantık, görsel yok. `Load`, `Consume`, `Peek(offset)`, `RemoveColor(color)`, `IsEmpty/Remaining/Current`. Ayrıca `CopyRemaining(list)` (tahsissiz okuma), `Capture()`/`Restore(Snapshot)` (undo — tek top geri koymak yerine **tüm kuyruk** anlık görüntüsü, çünkü atış bir purge tetiklemiş olabilir), `Append(extra)` (+N top teklifi), `NotifyCurrentChanged()` (booster seçili topu yerinde değiştirdiğinde `OnChanged`'i elle atar), `SelectSlot(offset)` (seçilen topu **öne taşır** — takas değil, böylece diğerlerinin sırası korunur; seçim kuyruğu yeniden sıralamak olarak modellendiği için atış zincirinin geri kalanı bundan habersiz kalır). Event'ler: `OnBallConsumed`, `OnChanged`, `OnEmpty`, `OnColorCleared(color, count)` |
| `BallLauncher.cs` | Mono | Atış → uçuş → boyama dalgası. `Launch(origin, velocity)` — `TapLaunchController` çağırır (origin = seçili tepsi topunun yeri; `Launch(velocity)` `_launchOrigin` yedeğini kullanır). Topu simüle edilmiş yay boyunca `_flightDuration` sürede tween'ler (mesafeden bağımsız sabit süre). `OnBallLanded`, **`OnShotPainted(hücreSayısı, landPos)`** (skor için, `OnBallLanded`'den **hemen önce** — yoksa kazandıran atış toplamda görünmez), `IsBusy`. Ayrıca `LastShot` (`ShotRecord`: atış öncesi kuyruk anlık görüntüsü + bu atışın **vuruş yaptığı** hücreler) ve `ClearLastShot()` — undo'nun ham maddesi, iniş anında yazılır |
| `BallVisual.cs` | Mono | Top görseli (fabrika: `Create(parent, color, power, shape, scale)`). **Gövde damganın kendisidir** (2026-09-15): 3×3 top dokuz mini taş, 5'lik Line beş taş, Plus gerçek kol uzunluğu; sayı etiketi yok. Bloklar tahtadakiyle **aynı** düz `TileArt` taş sprite'ları (2026-10-04), yatık, `sortingOrder` 20 (tahtanın üstünde). Tek ölçek kuralı: her gövde aynı toplam boyda, 5×5 daha ince bir ızgara olur. `L`/`Diagonal` legacy: sembolik 3/5 blok. **Gökkuşağı topu** (`CellColor.Any`): her blok sıradaki gerçek rengin taşı; `Matches(color, power, shape)` tepsinin yeniden çizim gerekip gerekmediğini sorar. `EnableTrail`, `PlayFireworkAndDestroy(delay)` |
| `BallQueueView.cs` | Mono | **Üçlü tepsi** (block puzzle tepsisi gibi). `_slots[3]` yuva transform'ları, `_remainingLabel` "+N" sayacı. Kuyruk modeli değişmedi (`Current` = offset 0, seçim = `SelectSlot` reorder); view **yuva→offset eşlemesini** tutar, böylece seçilmeyen iki top her atışta yerinde kalır. `Select(slot)`, `TryPickSlot(screenPos, cam, out slot)` (ekran-uzayı mesafesi, collider yok), `IsOverTray` (iptal jesti), **`CurrentLaunchOrigin`** (seçili topun yeri; yay buradan başlar). Kendi `SelectSlot`/`Consume` çağrılarını `_expectQueueChange`/`_consumePending` ile ayırt eder; diğer her `OnChanged` (Load/Restore/purge) eşlemeyi sıfırdan kurar. Yeni gelen top overshoot'lu pop-in ile gelir; seçili topun altında beyaz halka. **`SelectedSlot`**, **`TryGetSlotBall(slot, out ball, out pos)`** ve **`OnSlotSelected(slot)`** event'i (seçim dokunuşu; öğretici bekler) |
| `TapLaunchController.cs` | Mono | **Tek girdi kaynağı, tek jest.** Tepsi topuna dokun → seçer (atış saymaz, basış anında karara bağlanır). Hücreye dokun → seçili top oraya uçar; basılı tut-kaydır → damga hayaleti + yay parmağı izler, bırakınca ateş. Parmak tepsi bandında bırakılırsa iptal. **Kaldırma/rampa yok** — damga ortalı olduğu için parmağın örttüğü hücre tek bilgi değil. UI üstünde başlayan hareket ateş etmez |

### `Scripts/Gameplay/Aim/`

| Dosya | Tip | Sorumluluk / Önemli API |
|---|---|---|
| `AimPreview.cs` | Mono | Yörünge önizleme + iniş hücresi vurgusu + **damganın tamamı**: boyanacak hücreler hayalet (`SetPreview`), damganın kapsayıp boyamayacağı hücreler soluk çerçeve (`SetFootprint`). İkisi birlikte, `GetPaintOffset`'in power'a göre kayan çapasını görünür kılar. Ayrıca `BallQueue.OnChanged`'e abone olup `GridRenderer.SetActiveColor` ile mancınıktaki rengi tahtada işaretler. Yay çizimi tamamen `TapLaunchController` tarafından sürülür (`ShowArc(velocity)` / `Hide()`), kendi girdi aboneliği yoktur. İki mod: akan noktalar (`_useDots=true`, havuzlanmış küreler) veya kesikli LineRenderer |

### `Scripts/Gameplay/UI/` ve `Scripts/UI/`

| Dosya | Sorumluluk |
|---|---|
| `ProgressHUD.cs` | Renk başına **çubuk** (renk kutusu + dolu/boş çubuk + `dolu/toplam` sayısı) ve üstte toplam etiketi. Satırlar koddan üretilir, yalnızca renk **kümesi** değişince yeniden kurulur. `OnGridChanged`'i `_dirty` ile kare başına **tek** yeniden çizime indirger. Koyu renkleri okunur hâle getirir. `[RequireComponent(typeof(RectTransform))]` — sahne üreteci düz bir GameObject'e eklediği için eskiden RectTransform yoktu ve çocuklar dejenere bir ebeveyne göre hizalanıyordu; `Awake` artık safe area'ya yayıyor. Görselleri `raycastTarget = false` — HUD nişan hareketini yutmamalı |
| `ResultScreenUI.cs` | Sonuç paneli, `EaseOutBack` giriş animasyonu, Retry/Menu/Keep Going/**Next Level** butonları (Next, Keep Going'in yerinde; yalnız kazanınca ve sonraki level varsa). `Show(Reason, offerExtraBalls, score, hasNextLevel)` — `Reason`: `Won` / `OutOfBalls` (`DeadEnd` 2026-10-04'te kaldırıldı — dead-end artık `GameManager`'da uyarı); `score` alt metne eklenir (negatif geçilirse yazılmaz). Metinlerin tamamı burada durur (GameManager string değil, sebep gönderir) |
| `TutorialHint.cs` | **Öğretici + ipucu bandı** (Faz 5). `BeginLevel(levelName, hint)` (`LevelLoader` çağırır). Öğretici yalnız `LevelOrder.First`'te ve `TutorialDone` (PlayerPrefs) yokken: adım 1 **TapCell** — mevcut topun en çok boyadığı hücre (`CoverageAnalyzer.BestPlacement`), "Tap a square to throw"; atıştan sonra tahta durunca adım 2 **PickBall** — tepsideki başka renkte bir top, "Tap a ball to pick it" (seçenek yoksa atlanır). Her adım oyuncu başka bir şey yapınca da biter (adım 2'de atış = gerek yok). Sona erince ya da level bitince `TutorialDone` yazılır; yarıda kapatılan ilk oturum tekrar görür. Parmak ucu ekran-uzayında hedefi izler (her kare `WorldToScreenPoint`, unscaled zaman); disk + halka sprite'ları runtime'da (`UiSprites`). İpucu bandı `_hintDuration` (4 sn) ya da ilk atışta söner. Hiçbiri raycast hedefi değildir. `ResetDone()` |
| `ScoreHUD.cs` | Skor sayacı (sağ üst) + atış başına kombo patlaması ("x6   +720", 0 hücrelik atış bir seriyi bozduysa "COMBO LOST") + **ekran ortasında praise** (`_praiseLabel`: 3+ hücre GOOD, 5+ GREAT!, 8+ AMAZING!; overshoot'lu pop, yukarı kayarak söner). `GameManager.OnScoreChanged` / `OnShotScored` dinler, kendi kuralı yoktur. Undo skoru düşürdüğünde pop animasyonu **çalmaz**. `ProgressHUD` ile aynı `RequireComponent(RectTransform)` tuzağına tabi: sahne üreteci düz GameObject ekliyor, çocuk anchor'ları yoksa dejenere ebeveyne hizalanır |
| `BoosterBarUI.cs` | Üç booster butonu + "×N" sayaç etiketleri. Saf görünüm: her kural `BoosterSystem`'in. `OnChanged` + `BallQueue.OnChanged` ve oyun-sonu kapısı için her kare yenilenir (sayaç metni yalnız değişince yazılır, kare başına string tahsisi yok). `UndoButtonUI` gibi **Canvas'ta** durur |
| `UndoButtonUI.cs` | Oyun içi "geri al" butonu. `GameManager.CanUndo`'yu her kare okur ve buton yoksa **gizler** (soluk bırakmaz). **Butonun kendi GameObject'inde duramaz** — butonu `SetActive(false)` ile gizlediği için kendi `Update`'i de dururdu; sahne üreteci onu Canvas'a koyar |
| `LevelPickerHUD.cs` | Geliştirici aracı: sağ üstte level seçme dropdown'ı. Kendi Canvas'ını ve gerekirse EventSystem'ini **koddan** kurar. Liste `LevelOrder` sırasında (sayısal) |
| `SafeAreaFitter.cs` | `Screen.safeArea`'ya göre RectTransform'u daraltır (çentik/home bar) |
| `UI/MainMenuUI.cs` | `MainMenu` sahnesi: Play → `PlayerProgress.NextToPlay()` level'i (etikette "Play · Level N"), Levels → LevelSelect, "3 / 5 levels" ilerleme etiketi |
| `UI/LevelSelectUI.cs` | `LevelSelect` sahnesi: `LevelOrder` sırasında numaralı kutucuklar, **koddan** (prefab yok; kapsayıcının `GridLayoutGroup`'u dizer). Kazanılmış / açık / kilitli renkleri; kilitli kutucuk tıklanamaz. Back → MainMenu |

### `Scripts/Shared/`

| Dosya | Tip | Sorumluluk |
|---|---|---|
| `GameConstants.cs` | static | Bkz. Bölüm 5 — kural merkezi (yerçekimi + boyama + palet) |
| `TrajectorySimulator.cs` | static | `Simulate(start, vel, steps, dt, out landing)` — `GameConstants.Gravity` ile entegre eder, `Y=0` düzleminde durur. `SamplePath(path, t)` yay üzerinde normalize zamanla örnekler (tween bunu kullanır) |
| `LaunchSolver.cs` | static | `SolveToCell(grid, origin, gx, gy, angle)` / `SolveToPoint` — sabit atış açısında, verilen hücrenin merkezine düşen hızı çözer. `GameConstants.GravityMagnitude` kullanır |
| `CoverageAnalyzer.cs` | static | **"Kalan toplar kalan hücreleri kapatabilir mi?"** `TargetBoard` (düz diziler: `colors` / `hits` / `wild` / `stone`; `hits == 0` ⇒ hedef değil) + `BuildTargets(LevelData)` / `BuildTargets(GridRenderer)`; `BestPlacement(board, ball, out landX, out landY)` bir topun **en iyi iniş noktası** ve orada indireceği vuruş sayısı (`BestCoverage` sayı-döndüren sarmalayıcı); `CountPlacement(board, ball, lx, ly)` **tek bir** yerleşimin vuruş sayısı, uygulamadan (Solving botları belirli bir hücreyi tartmak için kullanır); `ApplyPlacement(board, ball, lx, ly, hitInto, filledInto)` bir yerleşimi taslak tahtadan düşer (opsiyonel listeler vuruş alan / dolan hücreleri verir); Gökkuşağı (`Any`) top her rengin `ceiling`'ini yükseltir (gevşek yönde) ve hiçbir satıra ait değildir. `Analyze(...)` renk başına `required` (**vuruş** sayısı — buz iki sayar) vs `ceiling` + `Impossible` / `Tight` (< 1.4×) / `Headroom`, artı gerekiyorsa bir **wild (joker) satırı**. Ölçme ve uygulama **aynı yürüyüşü** paylaşır (`Walk`), böylece plan bir kuralla ölçülüp başka bir kuralla uygulanamaz. Özel hücreler bilerek üst sınırı **gevşetecek** yönde modellenir (yanlış "imkânsız" kararı kazanılabilir bir koşuyu bitirirdi): joker hiçbir rengin `required`'ında yoktur ama **her** rengin `ceiling`'inde sayılır. Üç tüketicisi olduğu için `Shared/`'da: `LevelValidator`, `LevelAutoSolver` (editör) ve `GameManager` (çalışma zamanı erken kayıp tespiti) |
| `LevelSerializer.cs` | static | `ToJson/FromJson`, `Save/Load` (dosya IO sadece editörde anlamlı) |
| `GameFX.cs` | Mono singleton | `GameFX.Instance` ilk erişimde kendini yaratır. `Impact`, `ImpactRing`, `Bloom`, `CellPop`, `LaunchPuff`, `Win`, `Firework`, **`Confetti(center, color)`** (renk bitince, Win'den küçük), **`HitStop(sec)`** (`Time.timeScale=0`, unscaled bekleme, üst üste binmez; 8+ hücrelik atışta 50 ms), `Shake`, `ZoomPunch`, `Flash`.
| `GameAudio.cs` | Mono singleton | **Ses hub'ı**, `GameFX`'in ses ikizi: `GameAudio.Play(Sfx, pitch, volume)` ilk çağrıda kendini yaratır, sahne bağlantısı yok. `Sfx`: `Launch`, `Land`, `CellTick`, `IceCrack`, `ColorFanfare`, `Praise`, `Win`, `Lose`, `Booster`, `Warning`, `Undo`, `Pop`. Projede ses varlığı **yok**: her klip `Awake`'te ton + filtrelenmiş gürültü + zarf ile **sentezlenir** (birkaç ms); `Resources/Audio/<Sfx adı>` altında bir klip varsa o kullanılır. `PlayTick(n)` boyama dalgasında pentatonik basamak, `PlayPraise(tier)` GOOD/GREAT/AMAZING. 10 `AudioSource`'luk round-robin havuz (pitch kaynak başına). `Muted` PlayerPrefs'te (`SoundMuted`), henüz UI'ı yok. Sahnede `AudioListener` yoksa kendine ekler; üretilen klipleri `OnDestroy`'da yok eder |
| `TileArt.cs` | static | **Tahta sanatı, koddan çizilir** (projede görsel yok): `Tile(color)` (bevel'li parlak taş, 128 px = 1 birim, mipmap'li), `Grey()`, `Socket()` / `Marker()` (beyaz yuvarlak kare, renderer renklendirir), `Panel()` (9-slice), `Ice()`; ton sabitleri `Board`, `SocketA/B`, `Hole` (tahta, tepsi ve menüler aynı aileden). Her sprite bir kez üretilir, uygulama ömrünce tutulur. **Gerçek sanat kodsuz takılır:** `Resources/Art/Tiles/<GetColorDisplayName>`, `Art/Board/Panel|Socket|Marker`, `Art/Ice` altındaki bir Sprite çizilenin yerine geçer | **`GameFX.CurrentShakeOffset`** — kamera sarsıntısı sadece öteleme yapar; ekrandan-dünyaya ışın atarken bu offset çıkarılmalıdır (`TapLaunchController.PickRay`) |
| `UiSprites.cs` | static | Koddan çizilen UI sprite'ları: `Disc()` (yumuşak kenarlı disk — parmak ucu), `Ring()` (ince halka — dokunuş darbesi). Beyaz, `Image` renklendirir. Bir kez üretilir, uygulama ömrünce tutulur (statik referans sahne değişiminde boşaltılmasını da önler). Projede UI görseli yok, sprite'sız `Image` yalnız kare olabilir |
| `LevelOrder.cs` | static | Oynanış sırası: `Resources/Levels/*.json` adları, **sondaki sayıya göre** (level2 < level10). `Names`, `IndexOf`, `Next`, `First`, `NumberOf(name)` (editörün `EditorConstants.LevelNumberOf`'u da buna yönlenir — tek numaralama kuralı). İlk erişimde taranıp önbelleklenir; `Refresh()` |
| `PlayerProgress.cs` | static | Kazanılan level'ler, PlayerPrefs `Won_<levelName>`. `IsWon`, `MarkWon` (anında `Save` — mobilde uygulama her an öldürülebilir), `IsUnlocked` (ilk level ya da öncekisi kazanılmış — **türetilir**), `NextToPlay`, `WonCount`, `ResetAll` |
| `Haptics.cs` | static | `Light/Medium/Heavy`. Android'de `AndroidJavaObject` ile Vibrator (API 26+ amplitüdlü), editörde no-op |
| `Telemetry.cs` | static | `RecordLaunch()` — atışlar arası süreyi loglar, `OnTimeBetweenLaunchesRecorded` |

### `Scripts/Editor/`

| Dosya | Menü | Sorumluluk |
|---|---|---|
| `LevelEditorWindow.cs` | `CatapultGames/Level Editor` | **Shell** (UI Toolkit). Level durumunu, sekme geçişini ve sekmeler arası köprüleri tutar; Editor sekmesi pencerenin kendi kodudur. `CreateGUI` yalnızca `UI/LevelEditorWindow.uxml`'i klonlar ve `Q<>(name)` ile **bağlar** — statik UI için kodda `new Button` yok. **Eleman adı = sözleşme**: UXML'de bir adı değiştirip C#'ı unutursan hata almazsın, sessiz null gelir; bu yüzden her `Find<T>` null-güvenli. İki geçiş metodu kasıtlı: `RequestTab` (kullanıcı tıklaması, kaydedilmemiş iş uyarısı) / `SetActiveTab` (mekanik, kapısız). Editor sekmesi kartları: Level (ad, **ipucu** (`level-hint` → `metadata.hint`), boyut, kamera diyagramı), Brushes (Paint/Erase/Fill/Brush/RectSelect/MultiSelect + renk + tip; stroke pointer-up'ta **tek undo adımı**, Ctrl+Z/Y), Balls, **Generate** (`LevelBuilder`, band reçetesi Produce'la ortak `LevelProduceBandSet`), AI preview (`LevelAutoSolver`, `schedule.Execute().Every()` ile sürülür, salt görsel), Status (validator + solver + son sweep'in hızlı kartı), File (kaydet/aç/çoğalt/görsel içe aktar, **tazelik satırı**: "oyun şu an baktığını mı oynuyor"). Dayanıklılık: taslak level `[SerializeField] _scratchJson` ile domain reload'dan kurtarılır; `OnDestroy` "Save / Don't save" sorar (veto yok); `IsDirty` = bellek JSON ≠ son kaydedilen JSON (LF'e normalize) |
| `LevelEditorTab.cs` | — | `enum { Editor, Solving, Produce, Gallery }` — her değer bir UXML paneli (`tab-panel-*`) ve bir tab butonu (`tab-*`) |
| `EditorConstants.cs` | — | Tüm yol sabitleri tek yerde: `LevelsAssetFolder`, UXML/USS yolları, `LevelFileName(n)` (= `level{n}`, **sıfır dolgusuz** — katalog sondaki sayıyla sıralar), `LevelNumberOf(name)` (→ runtime `LevelOrder.NumberOf`) |
| `LevelCatalog.cs` | — | `LevelCatalog.Scan()` → `Resources/Levels/*.json` (klasörle sınırlı, sayısal sıra) · `LevelCatalogBrowser` başlık çubuğundaki `◀ n / N ▶` durumu |
| `LevelCellPalette.cs` | — | **Editördeki tek renk kaynağı**: Editor grid'i ve Gallery thumbnail'leri hücre rengini yalnız `Resolve(color, type)`'tan okur (taş gri, buz tül, joker kendi rengi). Hue'lar `GameConstants.CellColorPalette`'ten |
| `LevelPaintTool.cs` | — | Fırça enum'u + `LevelPaintTools` tablosu (fırça → UXML buton adı; enum'a eklenip case yazılmayan fırça `ArgumentOutOfRange` ile **görünür** patlar) |
| `LevelEditOps.cs` | — | Fırçaların yazdığı **tüm** `cells`/`balls` mutasyonları ve **guard'lar**. Hücre dizisi normalize (tam `w×h`, satır-majör; eski seyrek dosyalar yüklemede doldurulur). Guard felsefesi: leveli geçersizleştirmek yerine yazmayı reddet — yalnız hücre-yerel kurallar (renksiz joker reddedilir; taş renk almaz), tahta-geneli kurallar `LevelValidator`'a bırakılır |
| `LevelGridElement.cs` | — | Tahta, UI Toolkit elemanı. Hücre = `VisualElement` (tıklanabilir), overlay'ler tembel çocuk eleman, kendi USS'ini kendi yükler. **Veri mutasyonu yapmaz**, yalnız niyet bildirir: `CellPressed / CellDragged / StrokeCommitted / CellHovered` |
| `LevelBuildSpec.cs` | — | Prosedürel üretimin düz parametre bloğu + `LevelDifficultyBandPresets` (band → yazılı reçete, reset hedefi) + `LevelProduceBandSet` (UI'ın düzenlediği canlı kopya; Generate kartı ve Produce **aynı** seti okur) |
| `LevelBuilder.cs` | — | **Üretimin tek doğruluk kaynağı.** `TryBuild(spec, seed, attempts, out error)`: maske (rastgele BFS blob) → renkler (çok kaynaklı büyüme) → özel hücreler (taş maskenin kenarında, sonra buz, sonra joker) → kuyruk (`CoverageAnalyzer` ile top top **plan**, + slack, + sınırlı drift) → doğrula (Save kapısıyla aynı: validator + greedy solver). Sığmazsa `seed + attempt·7919` ile tekrar; sessizce daha az özel hücreyle üretmez |
| `LevelDifficultySchedule.cs` | — | **Tek seam**: `For(n)` (n≤5 Easy, n%10==0 VeryHard, n%5==0 Hard, diğer Normal) · `Classify(winRate)` (0.85/0.65/0.40/0.20 eşikleri) · `MatchesAuthored` (her iki yönde bir kademe tolerans — Normal'in %99 kazanılması "uymuyor" demektir). Band ≠ top bütçesi; slack `LevelBuildSpec`'te ayrı eksen |
| `Gallery/LevelGalleryTabController.cs` | Gallery sekmesi | Katalog kartları: `PageSize=40` sanallaştırma (havuz yok), band + ad filtresi, tıkla → shell `OpenLevelFromGallery`. Rozetler: yazılan band · gate (validator+solver) · ölçülen band (sweep sonrası; uyuşmazlık kırmızı çerçeve). `Activate()` tembel ilk yükleme, `MarkStale()` yalnız yüklenmişse yeniden okur ve **ölçülen sonuçları düşürür** |
| `Gallery/LevelThumbnailElement.cs` | — | Tahtayı tek elemanın `generateVisualContent`'inde `Painter2D` ile boyar (40 kart × 225 hücre = 40 eleman) |
| `Produce/LevelProduceRunner.cs` | — | UI'sız runner: `LevelProduceRequest.Normalized()` (özet, onay, runner ve rapor **aynı** aralığı okur; `MaxCountedLevel=999`, span ≤ 300), level başına band `Schedule.For(n)`, mevcut dosya overwrite kapalıysa `Skipped`, üretilemezse `Failed` + sebep, JSON **yerinde** yazılır (.meta/GUID korunur). `LevelProduceReport` satır satır. Overwrite edilen dosyanın `metadata.hint`'i yeni level'e taşınır |
| `Produce/LevelProduceTabController.cs` | Produce sekmesi | Aralık + seed + attempts + overwrite; band reçete editörü (tip başına tek paylaşılan handler, alan adı `switch`'i); mevcut dosyalar varsa **önce onay**; koşu iptal edilebilir progress bar; bitince `AssetDatabase.Refresh()` + shell `OnCatalogProduced` |
| `Solving/PlayoutBoard.cs` | — | **Headless simülatör.** `CoverageAnalyzer.TargetBoard` + kuyruk; damga/eşleşme/buz `CoverageAnalyzer`'dan, öne alma penceresi `SelectableSlots=3` (`BallQueueView` ile aynı), purge `GameManager.PurgeCompletedColors` kuralı, dead-end `GameManager.CheckDeadEnd` ölçümü (oyun yalnız uyarır; bot undo/booster kullanamadığı için onun için kayıp). Bot **geri alamaz**; `Clone()` ile özel tahtada keşfeder. Sonuç: `Won / OutOfBalls / DeadEnd / Unplayable`. **Simüle edilmeyenler:** Keep Going (+N top), undo, booster, skor, fiziksel yay |
| `Solving/ISolverBot.cs` · `SolverRoster.cs` | — | Bot sözleşmesi + popülasyon (8 bot, her biri **farklı karar prosedürü**): `random` (sıfır hipotezi), `gate-greedy` (tavan = Save kapısının solver'ı; %100 altı **level kusuru**), `greedy-window`, `color-focus`, `impulsive`, `careless-15` (**band botu**, `SolverRoster.BandBotId`), `careless-35`, `lookahead-2` (pahalı). Skorlayıcı eklemek bot eklemek değildir |
| `Solving/LevelBenchmark.cs` | — | Sweep koşucusu: tohum `baseSeed + level·100003 + run·7919`, koşular arasında iptal yoklaması, bot başına `BotStat` (4 sonuç toplamı = koşu sayısı), level başına `LevelResult` (band botu → `measured`, `matches`) |
| `Solving/LevelSolvingTabController.cs` | Solving sekmesi | Kapsam (açık level / hepsi / aralık) + koşu + seed; maliyet butona basılmadan **önce** yazılır; sweep **daima tüm roster'la** koşar, bot filtresi yalnız çizimi filtreler. Level satırları (band botu win-rate çubuğu, uyuşmazlık vurgusu) → detay kartı: bot başına **yığılmış sonuç çubuğu**, band **gauge**'u, headroom, kayıp koşu çipleri, ham tablo foldout, en sonda verdict. `BuildQuickCard` Editor'ün Status kartında aynı veriyi gösterir |
| `GameplaySceneBuilder.cs` | `CatapultGames/Build Gameplay Scene` | Oynanabilir sahneyi (`Assets/Scenes/Gameplay2.unity`) sıfırdan üretir ve tüm referansları **reflection ile** bağlar (`SetRef/SetRefArray/SetFloat/SetStr`). `CG_Grid` layer'ını kaydeder, sahneyi Build Settings'e ekler. `MakeText/MakeButton/MakeCanvas/AddEventSystem/SetRef/SetStr/StretchToParent` `internal` — `MenuSceneBuilder` aynılarını kullanır |
| `MenuSceneBuilder.cs` | `CatapultGames/Build Menu Scenes` | `Assets/Scenes/MainMenu.unity` (başlık, ilerleme etiketi, Play, Levels) ve `LevelSelect.unity` (başlık, `ScrollRect` + `GridLayoutGroup` 4 sütun, Back) sahnelerini üretip bağlar; Build Settings'i **MainMenu = index 0**, LevelSelect, sonra diğerleri olarak sıralar. Sahne adı sabitleri (`MainMenuSceneName`, `LevelSelectSceneName`) `GameplaySceneBuilder` tarafından `GameManager`'a yazılır |
| `LevelValidator.cs` | — | Renk başına `required` (vuruş) vs **gerçek** `coverage` (`CoverageAnalyzer`) → `OK/Warning/Error`, artı joker satırı (`ColorRow.isWild`). `globalErrors` (top yok, renksiz top…) ve `globalWarnings` (< 3 hücrelik renk, komşusuz yalıtılmış hücre, **renkli taş**, **renksiz joker**). Taş ve joker hücreleri yalıtılmışlık/az-hücre uyarılarından muaf. `isValid` yalnızca Error'lara bakar |
| `LevelAutoSolver.cs` | — | Açgözlü AI: top **sırası sabit**, her top için en çok vuruş indiren iniş noktasını seçer. Tahtayı `CoverageAnalyzer.TargetBoard` olarak tutup `BestPlacement`/`ApplyPlacement` üzerinden oynar — şekil, eşleşme, erişim ve buz maliyeti böylece oyunun kendi koduyla aynı. `Move.hit` (vuruş alan) ile `Move.filled` (dolan) ayrı: buzda ilk vuruş yalnızca çatlatır. "solved" ⇒ level kesin çözülebilir; "failed" ⇒ tasarım uyarısı (kanıt değil). Oyuncu `SelectSlot` ile sıradaki toplardan birini öne alabildiği için solver **kötümser** kaldı |
| `ImageImportUtility.cs` | — | PNG/JPG → ızgara. Hücre bölgesinin ortalama rengini palete en yakın `CellColor`'a eşler (eşik dışıysa `None`). Doku alt-sol, ızgara üst-sol başlangıçlı olduğu için **Y çevrilir** |

---

## 8. Sahneler

| Sahne | Durum | İçerik |
|---|---|---|
| `Assets/Scenes/MainMenu.unity` | Build index **0** (açılış) | `MenuSceneBuilder` üretir. **Henüz üretilmedi** — Unity'de `CatapultGames/Build Menu Scenes` çalıştırılmalı |
| `Assets/Scenes/LevelSelect.unity` | Build index 1 | `MenuSceneBuilder` üretir (aynı not) |
| `Assets/Scenes/Gameplay2.unity` | Build'de etkin | Tek oynanış sahnesi: tap-to-target |
| `Assets/Scenes/SampleScene.unity` | Build'de var, kapalı | Şablon artığı |

> Dosya adı hâlâ `Gameplay2` — ileride `Gameplay.unity`'ye yeniden adlandırılabilir (sahne
> `.meta` GUID'i korunduğu sürece Build Settings bozulmaz); menülerin yüklediği ad
> `GameplaySceneBuilder.SceneName` sabitinden gelir, birlikte değiştirilmeli.

### Sahne hiyerarşisi (`GameplaySceneBuilder`'ın ürettiği)

```
DirectionalLight      (yumuşak gölge, açık ambient)
GridCamera            (MainCamera tag, GridCameraController, BackgroundGradient, AudioListener, post-processing açık)
PostFX                (global Volume → Assets/Settings/GameplayPostFX.asset: Bloom 0.30 / eşik 1.0, Vignette 0.18)
GridRoot              (GridRenderer + GridBoard)   → BoardPanel + Cell_x_y/Visual/{Socket,Marker,Piece,Ice} runtime'da
BallQueue             (BallQueue + BallQueueView)
LaunchArea            (LaunchAreaAnchor)           konum ~(5.5, 0, -8)
├─ TrayPlate                                      mor plaka (tahta ailesi, bir ton açık)
├─ TraySlots/Slot_0..Slot_2                       x = −1.65 / 0 / +1.65, y = 0.62
├─ LaunchOrigin                                   yedek origin (orta yuva)
└─ Catapult (dekor, 0.7 ölçek, tepsinin arkasında)
   └─ CatapultBase / CatapultArm / ForkL / ForkR
AimPreview            (LineRenderer + AimPreview)
BallLauncher
EventSystem
BoosterSystem         (_queue, _grid, _gameManager)
UICanvas              (ResultScreenUI + UndoButtonUI + BoosterBarUI)
├─ ResultPanel → TitleText, SubText, RetryBtn, MenuBtn, Keep GoingBtn, Next LevelBtn (kapalı başlar)
├─ UndoBtn                                        (kapalı başlar; UndoButtonUI açar)
├─ RainbowBtn / RecolorBtn / BombBtn → Label, Count  sol alt sütun, x 0.03–0.21, y 0.10'dan yukarı
├─ LevelHint → Text                              ipucu bandı (y 0.63–0.69, koyu yarı saydam, CanvasGroup alfa 0)
├─ TutorialCaption → Text                        öğretici yazısı (tepsi üstü, y 0.19–0.245)
├─ TutorialPointer → Ring                        parmak ucu (en üstte çizilir; sprite runtime'da)
├─ WarningLabel                                   dead-end uyarısı (üst-orta, y 0.70–0.77, raycast kapalı, alfa 0)
├─ TrayRemaining                                  "+N" tepsi sayacı (sağ alt, raycast kapalı)
├─ PraiseLabel                                    "GREAT!" (ekran ortası, raycast kapalı)
└─ ProgressSafeArea (SafeAreaFitter)
   ├─ ProgressHUD → ProgressLabel
   │                └─ ProgressBars → Row_&lt;Renk&gt; (runtime)
   └─ ScoreHUD    → ScoreLabel, ComboLabel        (sağda, level dropdown'ın altında)
TutorialHint          (_camera, _grid, _queue, _queueView, _launcher, _gameManager + yukarıdaki UI)
LevelPickerHUD
GameManager
LevelLoader
TapLaunchController
```

---

## 9. Level Yazma İş Akışı

Pencere dört sekmelik kapalı bir döngüdür — **üret / gör / düzelt / ölç**:

```
Produce (üret) → Gallery (gör) → Editor (düzelt) → Solving (ölç) → Produce (reçeteyi güncelle)
```

1. **Editor**: `CatapultGames/Level Editor` ile aç/düzenle (veya Generate kartıyla banda göre üret).
2. Kaydet → doğrudan `Assets/Resources/Levels/levelN.json` (Ctrl+S de çalışır).
3. **Gallery**: elimde ne var — kartlar, rozetler, tıkla-aç.
4. **Produce**: `N…M` aralığını banda göre toplu üret (`LevelDifficultySchedule.For(n)`).
5. **Solving**: bot popülasyonuyla oynat, ölçülen bandı yazılan bandla karşılaştır.

Sekmeler birbirini tanımaz; köprü penceredir (`OpenLevelFromGallery`, `OnCatalogProduced`,
`OnSweepFinished`). Yeni sekme eklemek dört bağlantı ekler, on altı değil.

**Kaydetme kapısı:** `SaveFile()` yazmadan önce iki kontrolü birden koşar —
`LevelValidator.Validate` (bu toplar bu hücreleri *kapatabilir mi*) ve
`LevelAutoSolver.Solve` (yazılan top **sırasıyla** açgözlü oyun bitirebiliyor mu).
İkisi de temizse dialog çıkmaz. Biri takılırsa hata/uyarı dökümü gösterilir ve
"Save anyway / Cancel" sorulur — sert blok değil, çünkü yarım kalmış bir level'i
kaydetmek meşru. İptal edilirse `SaveFileAs` yeni yolu benimsemez.

Hepsi bu — **ara kopyalama/export adımı yok**. Editör, oyunun okuduğu klasörün ta
kendisine yazar ve ardından `AssetDatabase.Refresh()` çağırarak JSON'un TextAsset
olarak yeniden import edilmesini sağlar (bu olmadan `Resources.Load` yeni dosyayı görmez).

Mevcut level'ler: `level1` … `level5` (level1: 12×12). Dosya adı `level{n}` — sıfır dolgusuz
(`LevelLoader` varsayılanı `level1`); katalog sondaki sayıya göre **sayısal** sıralar, alfabetik değil.

---

## 10. Değişmezler (Bozulursa Oyun Bozulur)

1. **`GameConstants.TrajectorySteps` / `TrajectoryTimeStep`** — `AimPreview` ve `BallLauncher`
   aynı değerleri kullanmalı; yoksa "gösterilen yer" ile "düşülen yer" ayrışır.
2. **Yerçekimi tek yerden gelir:** `GameConstants.Gravity`. `TrajectorySimulator` ve
   `LaunchSolver` ondan türer — hiçbirine elle ayrı bir sayı yazma. Yayın şeklini
   değiştirmek istiyorsan **sadece** o sabiti değiştir; ikisi birlikte hareket eder.
3. **`CellColor` enum sırası** = `CellColorPalette` indeksleri = JSON'daki int değerler.
   Araya değer eklemek tüm level'leri bozar. Aynı kural **`BallShape`** ve **`CellType`**
   için de geçerli: üçüne de yalnızca **sona** eklenir.
4. **`powerLevel` sadece 1–3.** `LevelLoader.SanitizeBalls` clamp eder; `GameConstants`
   dışındaki değerler için `size=1` döner.
5. **Izgara koordinatları:** local `x = gx * cellSize`, `z = gy * cellSize`, düzlem `y = 0`.
   Satırlar ızgaranın local `+Z` yönünde artar.
6. **`CG_Grid` layer'ı** — `CellView` ve `GridBoard` isimle arar; yoksa layer 0'a düşer.
   Sahne üreteci `EnsureLayer("CG_Grid")` ile kaydeder.
7. **Tahta katmanları sortingOrder ile yığılır** (2026-10-04'ten beri; eskiden "SRP Batcher /
   MaterialPropertyBlock" kuralıydı, küpler gitti). Tahtaya çizilen yeni bir şey
   `CellView`'daki sıraya yerleşmeli (panel −20 … buz 5, toplar 20) — perspektif kamerada
   derinlikle yığmaya güvenme, sprite'lar ZWrite yapmaz. Hücre rengi `SpriteRenderer.color`'dır;
   hücre başına materyal açma.
8. **Kamera sarsıntısı sadece öteleme yapar.** Ekran→dünya ışını atan her yeni kod
   `GameFX.CurrentShakeOffset`'i ışının origin'inden çıkarmalı.
9. **`LaunchAreaAnchor.Reanchor()` `BallQueue.Load()`'dan önce** çağrılmalı.
10. **Material/Texture sızıntısı:** Kod runtime'da `new Material(...)` yapıyorsa
    `OnDestroy`'da `Destroy` etmeli (`CellView`, `BallVisual`, `AimPreview`, `GridBoard`
    bunu yapıyor — yeni kod da yapmalı). Aynısı `AudioClip.Create` için: `GameAudio`
    sentezlediği klipleri yok eder.
11. **Undo yalnızca tahta dururken açılır** (`!IsBusy`). Toplar üst üste atılabildiği
    için uçuş sırasında "son atış" belirsizdir; ayrıca boyama dalgası sürerken kuyruğu
    geri sarmak `BallLauncher._activeFlights` sayacıyla çakışır. Geri alma, atışın
    doldurduğu hücreleri indirip kuyruğun **atış öncesi anlık görüntüsünü** geri
    yükler — tek top geri koymak yetmez, çünkü atış bir renk tamamlamış ve
    `RemoveColor` purge'ü tetiklemiş olabilir.
12. **Kapsama asla damga alanından hesaplanmaz.** Bir topun boyayacağı hücre sayısı
    düştüğü tahtaya bağlıdır: 4×4 damga tek sıralık bir şeride düşerse 16 değil 4
    hücre boyar. Bu yüzden "bu level bitirilebilir mi" sorusunun tek cevabı
    `CoverageAnalyzer`'dır; `GameConstants.GetPaintCellCount` yalnızca damganın
    boyutunu verir. (Bu ayrım gözden kaçtığı için `LevelValidator` bir dönem
    bitirilemeyen level'lere "OK" verdi ve `level1` öyle yayına girdi.)
13. **`Stone` hiçbir sayımda hedef değildir.** İlerleme, kazanma kontrolü, kapsama ve
    doğrulayıcı tek bir yüklem üzerinden geçer (`CellView.IsPaintTarget` /
    `LevelValidator.IsPaintTarget`). Bir taşı hedef saymak level'i **kalıcı olarak
    bitirilemez** yapar — üstelik sessizce, çünkü tahtada boyanacak bir hücre gibi durur.
14. **Kapsamada özel hücreler daima üst sınırı gevşetir.** `Impossible` kararı oyuncuya
    "bu renk bitmez" diye uyarı gösterir ve editörde level'i kırmızıya boyar; yanlış pozitif
    **verilemez**: joker hücreleri hiçbir rengin
    `required`'ına yazılmaz ama her rengin `ceiling`'inde sayılır, ayrı bir wild satırı
    yalnızca joker hücrelerine karşı ölçülür. Yeni bir hücre tipi eklerken de yön aynı:
    şüphede kal, kanıtlama.
15. **Boş joker hücresi varken renk purge'ü yapılmaz** (`GameManager.PurgeCompletedColors`).
    Joker her rengi kabul ettiği için "işi bitmiş" bir rengin topları hâlâ level'i
    bitirebilecek toplardır; purge onları çöpe atardı.
16. **Undo skoru da geri alır** (`GameManager.RevertLastAward`). Yoksa "boya → geri al →
    aynı yeri boya" sınırsız puan üretir. Aynı sebeple `LevelLoader.Apply`
    `GameManager.BeginRun()` çağırır: level seçici sahneyi yeniden yüklemiyor.
17. **`CellColor.Any` hiçbir zaman hücre rengi olmaz.** Yalnızca booster'ın ürettiği top
    rengidir. `ColorMatches` onu her hedefle eşler, `CoverageAnalyzer` onu her rengin
    tavanına ekler — ikisi de "Any bir hücre olamaz" varsayımıyla yazıldı. Level JSON'u
    `8` içermemeli; editör swatch'ı gizler (`LevelEditorWindow`), üretici vermez.
18. **Dead-end bir uyarıdır, kayıp değil.** Kayıp yalnızca kuyruk boşken
    (`OutOfBalls`) ilan edilir. `CheckDeadEnd`'i tekrar `EndGame`'e bağlamak booster ve
    undo'yu anlamsız kılar: oyuncunun kurtarabileceği bir koşu elinden alınır.

---

## 11. Kod Konvansiyonları

- Namespace: runtime `CatapultGames`, editör `CatapultGames.Editor`.
- Private alanlar `_camelCase`; Inspector'a açılanlar `[SerializeField] private`.
  **`public` alan kullanma** (istisna: `GridCameraController`'ın slider'ları).
- Referanslar Inspector'dan bağlanır; `GetComponent`/`Find` runtime aramaları kaçınılır.
  Opsiyonel referanslar `?.` ile korunur (`_board?.Rebuild(...)`).
- Sahne bağlantıları elle değil `GameplaySceneBuilder` üzerinden kurulur — yeni bir
  bileşen eklediysen **oraya da eklemelisin**, yoksa sahne yeniden üretildiğinde kaybolur.
- Animasyonlar `IEnumerator` + `Time.deltaTime` ile elle yazılır (DOTween vb. yok).
- Yorumlar İngilizce, "neden" odaklı; hizalı `─── Başlık ───` bölüm ayraçları kullanılır.
- Shader arayışları daima yedekli: `Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")`.
- Girdi daima yeni Input System: `Touchscreen.current?.primaryTouch` önce, sonra `Mouse.current`.

---

## 12. Bilinen Boşluklar / Teknik Borç

- **Eski elle yazılmış level verisi bozuktu** (tarihsel not — `level1–5` 2026-09-15'te
  `LevelBuilder` ile yeniden üretildi, bkz. paket 1; yenileri Unity'de oynanarak henüz
  doğrulanmadı). Doğrulayıcı 2026-08-11'de gerçek kapsamaya geçirilince eski dosyalarda
  şunlar ortaya çıkmıştı:
  `level1` **bitirilemiyor** (siyah iki adet 1 sıralık şerit: 24 hücre, kare
  damgaların tavanı 18); `level2`'nin top kuyruğu **boş**; `level1–3`'te
  `(1,1)`'de tek hücrelik bir yeşil renk var (içe aktarma artığı, oyuncuya
  özel bir top harcatıyor); `level5`'te pembe ve mor 2'şer çapraz hücre,
  tam 1.00× payla. Editörde bu level'ler artık kırmızı görünüyor.
  O dönem `level1` ilk atıştan hemen sonra "Dead End" ile bitiyordu; bugün aynı durum
  yalnızca bir uyarı gösterir (bkz. § 6.2, değişmez 18).
  **Not:** `level1`'in siyah şeridi tam olarak `Line` damgasının işi — aynı 5 top
  `Line` olsaydı tavan 18 yerine 31 olurdu (gereken 24). Tek sıralık şeritleri kare
  damgayla boyatmak yerine şekli değiştirmek, düzeltmenin en ucuz yolu.
- **Skor kalıcı değil.** Level bitince skor sonuç ekranında görünüp gider; yalnızca
  "kazanıldı" bilgisi kaydedilir (`PlayerProgress`). En iyi skor tutulmuyor, yıldız yok.
- **Özel hücreler yalnız `level5`'te var** (sol sütunda 10 joker, 2026-08-12). Buz ve taş
  hiçbir elle yazılmış level'de kullanılmıyor; `LevelBuilder` Normal+ bandlarda üretiyor.
- **Casual sadeleştirme, paket 1 (2026-09-15, `feature/casual-simplify`).** Üçlü tepsi,
  ortalanmış tek sayılı kare damgalar (1/3/5), tek-dokunuş girdi (lift/rampa kaldırıldı),
  gerçek şekilli top gövdeleri, pastel palet, 66° kamera. `L` ve `Diagonal` şekilleri kodda
  **duruyor** (JSON uyumluluğu, enum append-only) ama üretici ve kurtarma topları artık
  onları vermiyor; editör listesinde hâlâ seçilebilirler. `level1–5` yeni kurallarla
  `LevelBuilder` ile **yeniden üretildi** (eski elle yazılmış level'ler git geçmişinde,
  `bed37d0`).
- **Casual sadeleştirme, paket 2 (görsel).** Yuvarlatılmış küpler, açık tahta/soket teması,
  gradient arka plan, bloom + vignette volume, dolu hücre emisyonu, yumuşak gölge, "GREAT!"
  praise yazısı, renk bitince konfeti + zoom punch, 8+ hücrede hit-stop. **Sahne dosyası
  `Gameplay2.unity` yeniden üretilmeden bunların çoğu görünmez** — `CatapultGames/Build
  Gameplay Scene` çalıştırılmalı (ışık, volume, tepsi, etiketler sahneye üreteçle girer).
  Editor asmdef artık URP runtime assembly'lerine referanslı (Volume/Bloom kurulumu için);
  runtime kodu URP tipine dokunmaz.
- **Casual sadeleştirme, paket 3 (2026-10-04).** Booster çubuğu (Rainbow / Recolor / Bomb,
  `BoosterSystem` + `BoosterBarUI`, `CellColor.Any`), taş = delik (gölge/yutma kuralı ve
  `GameConstants.GetStampPath` / `GridRenderer.IsBlocking` silindi), üretici joker vermiyor,
  dead-end oyunu bitirmek yerine uyarı (`GameManager.CheckDeadEnd`, `ResultScreenUI.Reason.DeadEnd`
  silindi). Booster sütununun ve uyarı etiketinin yerleşimi **Unity'de görülmedi** — tepsi
  topları veya tahta ile çakışıyorsa `GameplaySceneBuilder.MakeBoosterButton` /
  `WarningLabel` anchor'ları ayarlanır.
- **Görsel dil: block-match (2026-10-04).** Soluk pastel 3D küpler ve açık tahta yerine:
  koyu mor panel + damalı soketler + doygun parlak taşlar (`TileArt`, `CellView`), boş
  hedefte gerçek renkli küçük marker, menekşe arka plan, mor tepsi, menüler aynı aile.
  Referans şirketin Block-Match oyununun **yapısı ve renkleri** (katmanlı
  SpriteRenderer, 9-slice panel, %35-alfa önizleme); oradan dosya kopyalanmadı, her sprite
  kodla çiziliyor. `RoundedCubeMesh` silindi. **Unity'de görülmedi**: kamera 66° eğimde düz
  sprite'lar hafif kısalır (gerekirse level `camera.tiltAngle`); marker boyu, soket tonları
  ve bloom bu ekranda ayarlanacak ilk sayılar.
- **Casual sadeleştirme, paket 4 (ses, 2026-10-04).** `GameAudio` tüm sesleri çalışma zamanında
  sentezler (ses varlığı yok, import ayarı yok). Sentez Unity dışında sayısal olarak kontrol
  edildi (NaN yok, tepe 0.70, uçlarda tık yok) ama **kulakla dinlenmedi**; ses tasarımı zevk
  meselesi — beğenilmeyen bir ses kod değiştirmeden `Resources/Audio/<Sfx>` ile değiştirilir.
  Sessize alma (`GameAudio.Muted`) var ama onu açıp kapatan bir ayar ekranı yok.
- **Level editörü UI Toolkit'e taşındı (2026-09-15).** Eski tek panelli IMGUI penceresi,
  dört sekmeli shell'e dönüştü (bkz. § 7 Editor tablosu, § 9). Bilinen sınırlar:
  · Solving simülatörü Keep Going / undo / skor / fiziksel yayı modellemiyor (kasıtlı).
  · Band botu `careless-15`; `Classify` eşikleri (0.85/0.65/0.40/0.20) ColorCover'dan
    alındı, **bu popülasyonla yeniden ayarlanmalı** — ilk sweep'ler eşik kalibrasyonu içindir.
  · `LevelBuilder` yerleşimi açgözlü; oyuncunun öne alma serbestliği planı gevşetir, sıkmaz.
    Yan etkisi: üretilmiş level'ler **"greedy şekilli"** — slack 0–1 olan bandlarda açgözlü
    oynayan kazanır, farklı ama makul bir strateji (`lookahead-2`) kaybedebilir. Elle yazılmış
    `level3/5`'te aynı bot açgözlüden **fazla** topla kazanıyor; yani bot değil, üretici dar.
    Slack'i artırmak veya plana ikinci bir çözüm yolu eklemek düzeltmenin yeri.
  · Kompozisyon doğrulaması Unity dışında koşturuldu: `dotnet build` ile tip kontrolü +
    gerçek DLL'lere karşı konsol koşumu (level1 dead-end, level2 oynanamaz, level3–5 kapı %100).
  · UXML/USS `.meta` dosyaları Unity ilk odakta üretir; commit'e onları da ekle.
- **Test assembly'si hâlâ yok.** F6-3'ün kapsama matematiği (buz vuruşları, o zamanki taş
  gölgesi, joker kovası) `dotnet` altında çalışan geçici bir konsol koşumuyla doğrulandı
  (`GameConstants` + `CoverageAnalyzer` + `LevelAutoSolver` + `LevelValidator` gerçek
  dosyaları, sahte `UnityEngine` tipleriyle derlenir). Kalıcı bir test assembly'si
  eklenirse ilk taşınacak şey bu.
- **Faz 5 (2026-10-04):** öğretici ve ipucu bandı kodda; Unity'de görülmedi (parmak ucunun
  hücre/tepsi üstüne oturması, bandın tahtayı ne kadar örttüğü). Öğreticiyi sıfırlayan bir
  ayar yok (`TutorialHint.ResetDone()` ya da PlayerPrefs temizliği). İpuçları İngilizce; TMP
  varsayılan fontunda olmayabilecek karakterlerden (— ★ ✓) kaçınıldı.
- **Faz 4 (2026-10-04):** menü akışı, ilerleme kaydı ve Next Level kodda hazır; `MainMenu` /
  `LevelSelect` sahne dosyaları Unity'de `Build Menu Scenes` ile **üretilmedi**, yerleşim
  görülmedi. Yıldız sistemi BACKLOG'da vardı, istenmediği için **yapılmadı**. İlerlemeyi
  sıfırlayan bir ayar ekranı yok (`PlayerProgress.ResetAll` hazır).
- `Assets/_Recovery/0.unity` ve `Assets/TutorialInfo/` (URP şablon artığı) kullanılmıyor.
- `applicationIdentifier` hâlâ şablon varsayılanı (`com.UnityTechnologies...`).
- Otomatik test yok (`com.unity.test-framework` kurulu ama test assembly'si yok).
- `Assets/_Project/Prefabs`, `Sprites`, `Input`, `Scenes` klasörleri boş.
- Tek oynanış sahnesinin adı hâlâ `Gameplay2.unity` (bkz. § 8).
- **Gerçek fizik (Rigidbody uçuşu) denendi ve geri alındı** (2026-08-11). Top gerçek
  fizik gövdesi olarak uçtuğunda uçuş süresi mesafeye bağlı hâle geliyor ve uzak
  atışlar sürükleniyordu; sabit süreli tween daha iyi hissettirdi. Tekrar denenecekse
  bilinmesi gerekenler: yörünge adım süresi Unity'nin Fixed Timestep'ine eşitlenmeli
  (yoksa önizleme ile uçuş ayrışır), iniş anı iki fizik adımı arasında interpolasyonla
  bulunmalı (yoksa yanlış hücre boyanır) ve inmeyen atış için timeout şart (yoksa
  `IsBusy` açık kalıp level bitmez).
