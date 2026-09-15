# CatapultGames — Kod Mimarisi Haritası

> Bu dosya projenin **tek kaynak referansıdır**. Yeni bir geliştirme yaparken önce burayı
> oku; sadece burada adı geçen dosyaları aç. Yapı değişirse bu dosyayı da güncelle.
>
> Son güncelleme: 2026-09-15 · Unity 6000.3.16f1 · URP 17.3.0

---

## 1. Oyun Nedir?

Mobil (portrait) bir **mancınık + boyama bulmacası**. Zeminde renkli hücrelerden oluşan
bir ızgara var; her hücrenin bir "hedef rengi" var ve başlangıçta boş (alçak plaka).
Oyuncu, kuyruktaki topları ızgaraya fırlatır. Top bir hücreye düşer ve **kendi rengiyle
eşleşen** hücreleri, şekline göre bir alan içinde doldurur (küpler yükselir).

- **Kazanma:** Boyanması gereken tüm hücreler dolduğunda.
- **Kaybetme:** Kuyruk bittiğinde ve havada top kalmadığında hâlâ boş hedef hücre varsa.
- Bir renk tamamen bittiğinde, kuyruktaki o renge ait kalan toplar **otomatik silinir** ve
  havai fişek olarak patlatılır.
- **Özel hücreler** (`CellType`): buz iki vuruş ister, taş hiç boyanmaz ve damgayı yutar,
  joker her rengi kabul eder. Bkz. § 4 ve § 5.
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
├─ LevelMetadata metadata   { levelName, author, version }
├─ GridConfig    grid       { width=8, height=8, cellSize=1 }
├─ CameraConfig  camera     { fieldOfView, tiltAngle, padding, gridScreenPos, offset }
├─ CellData[]    cells      { gridX, gridY, outlineColor, isFilled, cellType }
└─ BallData[]    balls      { color, powerLevel(1-3), shape }
```

- **`CellColor`** (`Data/CellColor.cs`) — `None=0, Red=1, Green=2, Blue=3, Black=4,
  White=5, Pink=6, Purple=7`. Bu sayılar **JSON'a int olarak yazılır**, sırası asla
  değiştirilemez; sadece sona ekleme yapılabilir.
- **`CellType`** (`Data/CellType.cs`) — `Normal=0`, `Ice=1`, `Stone=2`, `Joker=3`.
  Renkten bağımsız: bir hücrenin hem rengi hem tipi vardır.
  - `Ice` — **iki vuruş** ister; ilki çatlatır, ikincisi doldurur. Yeni hücre eklemeden
    boya maliyetini artırır.
  - `Stone` — asla boyanmaz **ve damgayı yutar**: iniş hücresinden dışa uzanan düz ışın
    üzerinde arkasında kalan hücreler boyasız kalır (`GameConstants.GetStampPath`).
    Doğrudan taşa nişan alınırsa damga **tamamen** yutulur. Taş hiçbir sayımda hedef
    değildir (ilerleme, kazanma, kapsama).
  - `Joker` — **her renk** doldurur, ama kendi yazılı rengine dolar (resim bozulmaz).
    Kapsamada hiçbir rengin `required`'ına yazılmaz; ayrı bir "wild" satırı olur.
  JSON'da alan yoksa `Normal` olur (geri uyumlu). **Sadece sona eklenir.**
- **`BallShape`** — `Square=0`, `L=1`, `Line=2`, `Column=3`, `Plus=4`, `Diagonal=5`.
  JSON'da alan yoksa `Square` olur (geri uyumlu). **Sadece sona eklenir** — sayılar
  level dosyalarına yazılıyor. Kural tek yerde: `GameConstants.GetPaintedCells`;
  oraya bir `case` eklemek boyama, önizleme, doğrulayıcı ve auto-solver'ın hepsini
  otomatik uyumlar. `Line`/`Column` bilerek eksene sabit — kendi yönünü seçen bir
  damga önceden tahmin edilemez ve oyuncu topu harcamadan öğrenemez.
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
| `ColorMatches(cellColor, cellType, ballColor)` | **Eşleşme kararının tek yeri.** `Joker` her rengi kabul eder, `Stone` hiçbirini, boş tahta hücresi hedef değildir |
| `GetRequiredHits(cellType)` | Hücrenin dolmak için yediği vuruş sayısı — `Ice` 2, diğerleri 1 |
| `GetStampPath(landX, landY, cellX, cellY, into)` | **Erişim kararının tek yeri.** İniş hücresi ile hedef hücre **arasındaki** düz yol; üstünde `Stone` varsa hedef hücre boyasız kalır. Yalnızca dik ve 45° ışınlar yürünür (her damga şekli bu çizgilerden kurulu); 4×4 karenin ışın dışı köşeleri asla gölgelenmez. `into` temizlenip doldurulur — nişan her karede çağırıyor, tahsis olmamalı. **İniş hücresinin kendisi bu yola dahil değildir**; her tüketici onu ayrı sorar (taşa nişan alan damga tamamen yutulur) |
| `PointsPerCell = 10` · `GetShotMultiplier(cells)` · `GetComboMultiplier(streak)` · `MaxComboMultiplier = 5` | Skor kuralı. Yoğunluk çarpanı 1/2/3/4 (eşikler 1, 2, 4, 8 hücre), kombo çarpanı = üst üste boyayan atış sayısı (5'te tavan). Toplamı `GameManager` tutar |
| `CellColorPalette` (`Color32[8]`) | **İndeksleri `CellColor` enum'ıyla birebir aynı olmalı.** 2026-09-15'ten beri **candy/pastel** palet: Red→mercan, Green→nane, Blue→gök, Black→lacivert, White→limon, Pink→sakız pembesi, Purple→lavanta. Enum adları JSON uyumluluğu için eski kimliklerdir; saf siyah/beyaz hücre yok |
| `GetColor` / `GetColorF` | Palet erişimi |

> Boyama kuralını değiştirecek her iş **sadece burada** yapılmalı; `PaintingSystem`,
> `AimPreview`, `LevelValidator`, `LevelAutoSolver` ve `CoverageAnalyzer` otomatik olarak
> uyumlu kalır. Bir damganın bir hücreyi boyayıp boyamadığı üç soruya iner ve üçünün de
> tek cevap yeri burasıdır: **şekil** (`GetPaintedCells`), **eşleşme** (`ColorMatches`),
> **erişim** (`GetStampPath`).

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
              7. GameManager.ResetScore()            → level değişimi yeni bir koşu
```

> 7. adım şart: level seçici (dev dropdown) sahneyi yeniden yüklemeden level değiştirir,
> yoksa önceki tahtanın skoru devam eder.

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
                                        · IsDeadEnd()                  → EndGame(DeadEnd)
                                             └─ CoverageAnalyzer: kalan toplar kalan
                                                hücreleri kapatabiliyor mu?
                                                ▼
                                      ResultScreenUI.Show(reason, offerExtraBalls, score)
                                        · Keep Going → GameManager.GrantExtraBalls()
                                             └─ kuyruğa +N top, _gameOver = false,
                                                panel gizlenir, level kaldığı yerden sürer
```

### 6.3 Geri alma (undo)

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

---

## 7. Dosya Dosya Sorumluluklar

### `Scripts/Gameplay/`

| Dosya | Tip | Sorumluluk / Önemli API |
|---|---|---|
| `GameManager.cs` | Mono | Kazanma/kaybetme kararı, biten renklerin kuyruktan temizlenmesi, sonuç ekranı, `RestartLevel()`, `GoToMainMenu()`. `IsOver` diğer sistemlerce okunur. **Skor:** `Score` / `ComboStreak`, `OnShotScored(ShotScore)` + `OnScoreChanged(int)` event'leri, `ResetScore()` (`LevelLoader` çağırır). Puan `BallLauncher.OnShotPainted`'ten gelen hücre sayısıyla `GameConstants` kurallarından hesaplanır; son ödül saklanır ki **undo skoru da geri alsın**. `PurgeCompletedColors` boş **joker** hücresi varken hiç purge yapmaz. Ayrıca: `IsDeadEnd()` (kalan toplar yetmiyorsa kuyruk bitmeden bitirir, `CoverageAnalyzer`), `UndoLastShot()` / `CanUndo`, `GrantExtraBalls()` (level başına **bir kez**, `_extraBallCount`). Kurtarma topları `BuildRescueBalls` ile **tahtaya bakılarak** seçilir: her (renk, şekil, power) adayı `CoverageAnalyzer.BestPlacement` ile ölçülür, en iyisi alınır ve `ApplyPlacement` ile tahtadan düşülerek sonraki top ona göre seçilir — üç bağımsız tahmin değil, bir **plan**. Beraberlikte küçük damga kazanır (bitirmeye yeter, fazlası değil). `L` aday havuzunda yok: kolları ızgara kenarına gittiği için kurtarmaz, level'i siler |
| `LevelLoader.cs` | Mono | JSON → sahne. `LoadByName(name)`, `Apply(LevelData)`, statik `SelectLevel/ClearSelection` (PlayerPrefs `"SelectedLevel"`). `Apply` sonunda `GameManager.ResetScore()` — level seçici sahneyi yeniden yüklemiyor |
| `LaunchAreaAnchor.cs` | Mono | Mancınık + kuyruk kökünü ekranın alt bandına sabitler (`_screenY=0.08`). Sadece çözünürlük değişince yeniden hesaplar (shake ile titremesin diye) |

### `Scripts/Gameplay/Grid/`

| Dosya | Tip | Sorumluluk / Önemli API |
|---|---|---|
| `GridRenderer.cs` | Mono | Izgaranın sahibi. `BuildGrid/ClearGrid`, `TryGetCell/GetCell`, `SetFilled(Batch)`, **`ApplyHit(x,y)`** (bir damga vuruşu; dolduysa `true` — boyama dalgası bunu kullanır) ve **`UndoHit(x,y)`**, `SetHighlight/SetPreview/SetFootprint/ClearHighlights` (üçü birlikte temizlenir), `SetActiveColor(color)` (mancınıktaki rengin boş hücrelerini vurgular — joker hücreleri **her** renkte parlar), `GridToWorld/WorldToGrid/RaycastToGrid/RaycastToGridClamped`, `CountByColor()` (tahsissiz, enum sırasında), `AllColoredCellsFilled()`, **`HasUnfilledWildCells()`** (purge kapısı), **`IsBlocking(x,y)`** (taş mı — `PaintingSystem` gölge yürüyüşü için), `WorldCenter`, `PulseColor`. `OnGridChanged` event'i. Tüm sayımlar `CellView.IsPaintTarget` üzerinden geçer → **taş hiçbir yerde hedef sayılmaz** |
| `CellView.cs` | Mono | Tek hücre = küp. Dolu `FullH=0.80`, boş `ThinH=0.11`, `CubeGap=0.92`, taş `StoneH=0.50`, çatlak buz `CrackedH=0.35`. `Type` (`CellType`), `HitsTaken`/`HitsRequired`, **`IsFilled` türetilmiştir** (`HitsTaken >= HitsRequired` — "çatlak" ayrı bir durum değil), `IsPaintTarget` (taş ve boş tahta hariç). `SetFilled` (tümden dolu/boş), **`AddHit()`** (dolarsa RisePunch, dolmazsa CrackPunch), **`RemoveHit()`** (undo), `SetHighlight`, `SetPreview` (nefes alan hayalet — yalnız çatlatacak atışta **çatlak yüksekliğine** kadar kalkar, önizleme yalan söylemez), `SetFootprint`, `SetAwaiting`, `Pulse(delay)`. **`Refresh()` tek toplayıcıdır:** tip, hit sayısı, `_awaiting` ve `_footprint` orada okunur. **Her hücrenin kendi `Material` örneği ama tek ortak shader var → SRP Batcher tek batch'te toplar; MaterialPropertyBlock KULLANMA** |
| `PaintingSystem.cs` | static | Boyama kuralı uygulayıcı (canlı ızgara üstünde): `Paint` (mutasyon), `Preview` (salt okuma), `PaintTargetsOrdered` (iniş noktasına yakınlık sırasıyla), `CountPaintable`. Üç kuralı da `GameConstants`'tan okur (şekil / eşleşme / erişim); taş gölgesi için tahsissiz tek bir yol tamponu kullanır |
| `GridCameraController.cs` | Mono | Tek perspektif kamera. `FitToGrid(grid, camCfg)` FOV + tilt + padding + `gridScreenPos`'tan konumu otomatik çözer. Sahne `FrontZ = -9f` sabitiyle mancınığı da kadraja alır |
| `GridBoard.cs` | Mono | Izgaranın altındaki koyu zemin quad'ı. `Rebuild(GridConfig)` |

### `Scripts/Gameplay/Ball/`

| Dosya | Tip | Sorumluluk / Önemli API |
|---|---|---|
| `BallQueue.cs` | Mono | Saf mantık, görsel yok. `Load`, `Consume`, `Peek(offset)`, `RemoveColor(color)`, `IsEmpty/Remaining/Current`. Ayrıca `CopyRemaining(list)` (tahsissiz okuma), `Capture()`/`Restore(Snapshot)` (undo — tek top geri koymak yerine **tüm kuyruk** anlık görüntüsü, çünkü atış bir purge tetiklemiş olabilir), `Append(extra)` (+N top teklifi), `SelectSlot(offset)` (seçilen topu **öne taşır** — takas değil, böylece diğerlerinin sırası korunur; seçim kuyruğu yeniden sıralamak olarak modellendiği için atış zincirinin geri kalanı bundan habersiz kalır). Event'ler: `OnBallConsumed`, `OnChanged`, `OnEmpty`, `OnColorCleared(color, count)` |
| `BallLauncher.cs` | Mono | Atış → uçuş → boyama dalgası. `Launch(origin, velocity)` — `TapLaunchController` çağırır (origin = seçili tepsi topunun yeri; `Launch(velocity)` `_launchOrigin` yedeğini kullanır). Topu simüle edilmiş yay boyunca `_flightDuration` sürede tween'ler (mesafeden bağımsız sabit süre). `OnBallLanded`, **`OnShotPainted(hücreSayısı, landPos)`** (skor için, `OnBallLanded`'den **hemen önce** — yoksa kazandıran atış toplamda görünmez), `IsBusy`. Ayrıca `LastShot` (`ShotRecord`: atış öncesi kuyruk anlık görüntüsü + bu atışın **vuruş yaptığı** hücreler) ve `ClearLastShot()` — undo'nun ham maddesi, iniş anında yazılır |
| `BallVisual.cs` | Mono | Top görseli (fabrika: `Create(parent, color, power, shape, scale)`). **Gövde damganın kendisidir** (2026-09-15): 3×3 top dokuz mini küp, 5'lik Line beş küp, Plus gerçek kol uzunluğu; sayı etiketi yok. Tek ölçek kuralı: her gövde aynı toplam boyda, 5×5 daha ince bir ızgara olur. `L`/`Diagonal` legacy: sembolik 3/5 blok. `EnableTrail`, `PlayFireworkAndDestroy(delay)` |
| `BallQueueView.cs` | Mono | **Üçlü tepsi** (block puzzle tepsisi gibi). `_slots[3]` yuva transform'ları, `_remainingLabel` "+N" sayacı. Kuyruk modeli değişmedi (`Current` = offset 0, seçim = `SelectSlot` reorder); view **yuva→offset eşlemesini** tutar, böylece seçilmeyen iki top her atışta yerinde kalır. `Select(slot)`, `TryPickSlot(screenPos, cam, out slot)` (ekran-uzayı mesafesi, collider yok), `IsOverTray` (iptal jesti), **`CurrentLaunchOrigin`** (seçili topun yeri; yay buradan başlar). Kendi `SelectSlot`/`Consume` çağrılarını `_expectQueueChange`/`_consumePending` ile ayırt eder; diğer her `OnChanged` (Load/Restore/purge) eşlemeyi sıfırdan kurar. Yeni gelen top overshoot'lu pop-in ile gelir; seçili topun altında beyaz halka |
| `TapLaunchController.cs` | Mono | **Tek girdi kaynağı, tek jest.** Tepsi topuna dokun → seçer (atış saymaz, basış anında karara bağlanır). Hücreye dokun → seçili top oraya uçar; basılı tut-kaydır → damga hayaleti + yay parmağı izler, bırakınca ateş. Parmak tepsi bandında bırakılırsa iptal. **Kaldırma/rampa yok** — damga ortalı olduğu için parmağın örttüğü hücre tek bilgi değil. UI üstünde başlayan hareket ateş etmez |

### `Scripts/Gameplay/Aim/`

| Dosya | Tip | Sorumluluk / Önemli API |
|---|---|---|
| `AimPreview.cs` | Mono | Yörünge önizleme + iniş hücresi vurgusu + **damganın tamamı**: boyanacak hücreler hayalet (`SetPreview`), damganın kapsayıp boyamayacağı hücreler soluk çerçeve (`SetFootprint`). İkisi birlikte, `GetPaintOffset`'in power'a göre kayan çapasını görünür kılar. Ayrıca `BallQueue.OnChanged`'e abone olup `GridRenderer.SetActiveColor` ile mancınıktaki rengi tahtada işaretler. Yay çizimi tamamen `TapLaunchController` tarafından sürülür (`ShowArc(velocity)` / `Hide()`), kendi girdi aboneliği yoktur. İki mod: akan noktalar (`_useDots=true`, havuzlanmış küreler) veya kesikli LineRenderer |

### `Scripts/Gameplay/UI/` ve `Scripts/UI/`

| Dosya | Sorumluluk |
|---|---|
| `ProgressHUD.cs` | Renk başına **çubuk** (renk kutusu + dolu/boş çubuk + `dolu/toplam` sayısı) ve üstte toplam etiketi. Satırlar koddan üretilir, yalnızca renk **kümesi** değişince yeniden kurulur. `OnGridChanged`'i `_dirty` ile kare başına **tek** yeniden çizime indirger. Koyu renkleri okunur hâle getirir. `[RequireComponent(typeof(RectTransform))]` — sahne üreteci düz bir GameObject'e eklediği için eskiden RectTransform yoktu ve çocuklar dejenere bir ebeveyne göre hizalanıyordu; `Awake` artık safe area'ya yayıyor. Görselleri `raycastTarget = false` — HUD nişan hareketini yutmamalı |
| `ResultScreenUI.cs` | Sonuç paneli, `EaseOutBack` giriş animasyonu, Retry/Menu/Keep Going butonları. `Show(Reason, offerExtraBalls, score)` — `Reason`: `Won` / `OutOfBalls` / `DeadEnd`; `score` alt metne eklenir (negatif geçilirse yazılmaz). Metinlerin tamamı burada durur (GameManager string değil, sebep gönderir) |
| `ScoreHUD.cs` | Skor sayacı (sağ üst) + atış başına kombo patlaması ("x6   +720", 0 hücrelik atış bir seriyi bozduysa "COMBO LOST"). `GameManager.OnScoreChanged` / `OnShotScored` dinler, kendi kuralı yoktur. Undo skoru düşürdüğünde pop animasyonu **çalmaz**. `ProgressHUD` ile aynı `RequireComponent(RectTransform)` tuzağına tabi: sahne üreteci düz GameObject ekliyor, çocuk anchor'ları yoksa dejenere ebeveyne hizalanır |
| `UndoButtonUI.cs` | Oyun içi "geri al" butonu. `GameManager.CanUndo`'yu her kare okur ve buton yoksa **gizler** (soluk bırakmaz). **Butonun kendi GameObject'inde duramaz** — butonu `SetActive(false)` ile gizlediği için kendi `Update`'i de dururdu; sahne üreteci onu Canvas'a koyar |
| `LevelPickerHUD.cs` | Geliştirici aracı: sağ üstte level seçme dropdown'ı. Kendi Canvas'ını ve gerekirse EventSystem'ini **koddan** kurar |
| `SafeAreaFitter.cs` | `Screen.safeArea`'ya göre RectTransform'u daraltır (çentik/home bar) |
| `UI/MainMenuUI.cs` | `MainMenu` sahnesi: Play → Gameplay, Level Select → LevelSelect |
| `UI/LevelSelectUI.cs` | `LevelSelect` sahnesi: `Resources.LoadAll<TextAsset>("Levels")` → buton listesi |

### `Scripts/Shared/`

| Dosya | Tip | Sorumluluk |
|---|---|---|
| `GameConstants.cs` | static | Bkz. Bölüm 5 — kural merkezi (yerçekimi + boyama + palet) |
| `TrajectorySimulator.cs` | static | `Simulate(start, vel, steps, dt, out landing)` — `GameConstants.Gravity` ile entegre eder, `Y=0` düzleminde durur. `SamplePath(path, t)` yay üzerinde normalize zamanla örnekler (tween bunu kullanır) |
| `LaunchSolver.cs` | static | `SolveToCell(grid, origin, gx, gy, angle)` / `SolveToPoint` — sabit atış açısında, verilen hücrenin merkezine düşen hızı çözer. `GameConstants.GravityMagnitude` kullanır |
| `CoverageAnalyzer.cs` | static | **"Kalan toplar kalan hücreleri kapatabilir mi?"** `TargetBoard` (düz diziler: `colors` / `hits` / `wild` / `stone`; `hits == 0` ⇒ hedef değil) + `BuildTargets(LevelData)` / `BuildTargets(GridRenderer)`; `BestPlacement(board, ball, out landX, out landY)` bir topun **en iyi iniş noktası** ve orada indireceği vuruş sayısı (`BestCoverage` sayı-döndüren sarmalayıcı); `CountPlacement(board, ball, lx, ly)` **tek bir** yerleşimin vuruş sayısı, uygulamadan (Solving botları belirli bir hücreyi tartmak için kullanır); `ApplyPlacement(board, ball, lx, ly, hitInto, filledInto)` bir yerleşimi taslak tahtadan düşer (opsiyonel listeler vuruş alan / dolan hücreleri verir); `Analyze(...)` renk başına `required` (**vuruş** sayısı — buz iki sayar) vs `ceiling` + `Impossible` / `Tight` (< 1.4×) / `Headroom`, artı gerekiyorsa bir **wild (joker) satırı**. Ölçme ve uygulama **aynı yürüyüşü** paylaşır (`Walk`), böylece plan bir kuralla ölçülüp başka bir kuralla uygulanamaz. Özel hücreler bilerek üst sınırı **gevşetecek** yönde modellenir (yanlış "imkânsız" kararı kazanılabilir bir koşuyu bitirirdi): joker hiçbir rengin `required`'ında yoktur ama **her** rengin `ceiling`'inde sayılır. Üç tüketicisi olduğu için `Shared/`'da: `LevelValidator`, `LevelAutoSolver` (editör) ve `GameManager` (çalışma zamanı erken kayıp tespiti) |
| `LevelSerializer.cs` | static | `ToJson/FromJson`, `Save/Load` (dosya IO sadece editörde anlamlı) |
| `GameFX.cs` | Mono singleton | `GameFX.Instance` ilk erişimde kendini yaratır. `Impact`, `ImpactRing`, `Bloom`, `CellPop`, `LaunchPuff`, `Win`, `Firework`, `Shake`, `ZoomPunch`, `Flash`. **`GameFX.CurrentShakeOffset`** — kamera sarsıntısı sadece öteleme yapar; ekrandan-dünyaya ışın atarken bu offset çıkarılmalıdır (`TapLaunchController.PickRay`) |
| `Haptics.cs` | static | `Light/Medium/Heavy`. Android'de `AndroidJavaObject` ile Vibrator (API 26+ amplitüdlü), editörde no-op |
| `Telemetry.cs` | static | `RecordLaunch()` — atışlar arası süreyi loglar, `OnTimeBetweenLaunchesRecorded` |

### `Scripts/Editor/`

| Dosya | Menü | Sorumluluk |
|---|---|---|
| `LevelEditorWindow.cs` | `CatapultGames/Level Editor` | **Shell** (UI Toolkit). Level durumunu, sekme geçişini ve sekmeler arası köprüleri tutar; Editor sekmesi pencerenin kendi kodudur. `CreateGUI` yalnızca `UI/LevelEditorWindow.uxml`'i klonlar ve `Q<>(name)` ile **bağlar** — statik UI için kodda `new Button` yok. **Eleman adı = sözleşme**: UXML'de bir adı değiştirip C#'ı unutursan hata almazsın, sessiz null gelir; bu yüzden her `Find<T>` null-güvenli. İki geçiş metodu kasıtlı: `RequestTab` (kullanıcı tıklaması, kaydedilmemiş iş uyarısı) / `SetActiveTab` (mekanik, kapısız). Editor sekmesi kartları: Level (ad, boyut, kamera diyagramı), Brushes (Paint/Erase/Fill/Brush/RectSelect/MultiSelect + renk + tip; stroke pointer-up'ta **tek undo adımı**, Ctrl+Z/Y), Balls, **Generate** (`LevelBuilder`, band reçetesi Produce'la ortak `LevelProduceBandSet`), AI preview (`LevelAutoSolver`, `schedule.Execute().Every()` ile sürülür, salt görsel), Status (validator + solver + son sweep'in hızlı kartı), File (kaydet/aç/çoğalt/görsel içe aktar, **tazelik satırı**: "oyun şu an baktığını mı oynuyor"). Dayanıklılık: taslak level `[SerializeField] _scratchJson` ile domain reload'dan kurtarılır; `OnDestroy` "Save / Don't save" sorar (veto yok); `IsDirty` = bellek JSON ≠ son kaydedilen JSON (LF'e normalize) |
| `LevelEditorTab.cs` | — | `enum { Editor, Solving, Produce, Gallery }` — her değer bir UXML paneli (`tab-panel-*`) ve bir tab butonu (`tab-*`) |
| `EditorConstants.cs` | — | Tüm yol sabitleri tek yerde: `LevelsAssetFolder`, UXML/USS yolları, `LevelFileName(n)` (= `level{n}`, **sıfır dolgusuz** — katalog sondaki sayıyla sıralar), `LevelNumberOf(name)` |
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
| `Produce/LevelProduceRunner.cs` | — | UI'sız runner: `LevelProduceRequest.Normalized()` (özet, onay, runner ve rapor **aynı** aralığı okur; `MaxCountedLevel=999`, span ≤ 300), level başına band `Schedule.For(n)`, mevcut dosya overwrite kapalıysa `Skipped`, üretilemezse `Failed` + sebep, JSON **yerinde** yazılır (.meta/GUID korunur). `LevelProduceReport` satır satır |
| `Produce/LevelProduceTabController.cs` | Produce sekmesi | Aralık + seed + attempts + overwrite; band reçete editörü (tip başına tek paylaşılan handler, alan adı `switch`'i); mevcut dosyalar varsa **önce onay**; koşu iptal edilebilir progress bar; bitince `AssetDatabase.Refresh()` + shell `OnCatalogProduced` |
| `Solving/PlayoutBoard.cs` | — | **Headless simülatör.** `CoverageAnalyzer.TargetBoard` + kuyruk; damga/eşleşme/erişim/buz `CoverageAnalyzer`'dan, öne alma penceresi `SelectableSlots=3` (`BallQueueView` ile aynı), purge `GameManager.PurgeCompletedColors` kuralı, dead-end `GameManager.IsDeadEnd`. Bot **geri alamaz**; `Clone()` ile özel tahtada keşfeder. Sonuç: `Won / OutOfBalls / DeadEnd / Unplayable`. **Simüle edilmeyenler:** Keep Going (+N top), undo, skor, fiziksel yay |
| `Solving/ISolverBot.cs` · `SolverRoster.cs` | — | Bot sözleşmesi + popülasyon (8 bot, her biri **farklı karar prosedürü**): `random` (sıfır hipotezi), `gate-greedy` (tavan = Save kapısının solver'ı; %100 altı **level kusuru**), `greedy-window`, `color-focus`, `impulsive`, `careless-15` (**band botu**, `SolverRoster.BandBotId`), `careless-35`, `lookahead-2` (pahalı). Skorlayıcı eklemek bot eklemek değildir |
| `Solving/LevelBenchmark.cs` | — | Sweep koşucusu: tohum `baseSeed + level·100003 + run·7919`, koşular arasında iptal yoklaması, bot başına `BotStat` (4 sonuç toplamı = koşu sayısı), level başına `LevelResult` (band botu → `measured`, `matches`) |
| `Solving/LevelSolvingTabController.cs` | Solving sekmesi | Kapsam (açık level / hepsi / aralık) + koşu + seed; maliyet butona basılmadan **önce** yazılır; sweep **daima tüm roster'la** koşar, bot filtresi yalnız çizimi filtreler. Level satırları (band botu win-rate çubuğu, uyuşmazlık vurgusu) → detay kartı: bot başına **yığılmış sonuç çubuğu**, band **gauge**'u, headroom, kayıp koşu çipleri, ham tablo foldout, en sonda verdict. `BuildQuickCard` Editor'ün Status kartında aynı veriyi gösterir |
| `GameplaySceneBuilder.cs` | `CatapultGames/Build Gameplay Scene` | Oynanabilir sahneyi (`Assets/Scenes/Gameplay2.unity`) sıfırdan üretir ve tüm referansları **reflection ile** bağlar (`SetRef/SetRefArray/SetFloat/SetStr`). `CG_Grid` layer'ını kaydeder, sahneyi Build Settings'e ekler |
| `LevelValidator.cs` | — | Renk başına `required` (vuruş) vs **gerçek** `coverage` (`CoverageAnalyzer`) → `OK/Warning/Error`, artı joker satırı (`ColorRow.isWild`). `globalErrors` (top yok, renksiz top…) ve `globalWarnings` (< 3 hücrelik renk, komşusuz yalıtılmış hücre, **renkli taş**, **renksiz joker**). Taş ve joker hücreleri yalıtılmışlık/az-hücre uyarılarından muaf. `isValid` yalnızca Error'lara bakar |
| `LevelAutoSolver.cs` | — | Açgözlü AI: top **sırası sabit**, her top için en çok vuruş indiren iniş noktasını seçer. Tahtayı `CoverageAnalyzer.TargetBoard` olarak tutup `BestPlacement`/`ApplyPlacement` üzerinden oynar — şekil, eşleşme, erişim ve buz maliyeti böylece oyunun kendi koduyla aynı. `Move.hit` (vuruş alan) ile `Move.filled` (dolan) ayrı: buzda ilk vuruş yalnızca çatlatır. "solved" ⇒ level kesin çözülebilir; "failed" ⇒ tasarım uyarısı (kanıt değil). Oyuncu `SelectSlot` ile sıradaki toplardan birini öne alabildiği için solver **kötümser** kaldı |
| `ImageImportUtility.cs` | — | PNG/JPG → ızgara. Hücre bölgesinin ortalama rengini palete en yakın `CellColor`'a eşler (eşik dışıysa `None`). Doku alt-sol, ızgara üst-sol başlangıçlı olduğu için **Y çevrilir** |

---

## 8. Sahneler

| Sahne | Durum | İçerik |
|---|---|---|
| `Assets/Scenes/Gameplay2.unity` | **Build'de etkin (tek)** | Tek oynanış sahnesi: tap-to-target |
| `Assets/Scenes/SampleScene.unity` | Build'de var, kapalı | Şablon artığı |
| `MainMenu`, `LevelSelect` | **DOSYA YOK** | `MainMenuUI`/`LevelSelectUI`/`GameManager.GoToMainMenu` bu isimleri arıyor ama sahne dosyaları oluşturulmamış. Menü akışı istenirse önce bu sahneler yaratılmalı |

> Dosya adı hâlâ `Gameplay2` — tek sahne kaldığı için ileride `Gameplay.unity`'ye
> yeniden adlandırılabilir (sahne `.meta` GUID'i korunduğu sürece Build Settings bozulmaz).

### Sahne hiyerarşisi (`GameplaySceneBuilder`'ın ürettiği)

```
DirectionalLight
GridCamera            (MainCamera tag, GridCameraController)
GridRoot              (GridRenderer + GridBoard)   → Cell_x_y çocukları runtime'da
BallQueue             (BallQueue + BallQueueView)
LaunchArea            (LaunchAreaAnchor)           konum ~(5.5, 0, -8)
├─ TrayPlate                                      açık renk plaka
├─ TraySlots/Slot_0..Slot_2                       x = −1.65 / 0 / +1.65, y = 0.62
├─ LaunchOrigin                                   yedek origin (orta yuva)
└─ Catapult (dekor, 0.7 ölçek, tepsinin arkasında)
   └─ CatapultBase / CatapultArm / ForkL / ForkR
AimPreview            (LineRenderer + AimPreview)
BallLauncher
EventSystem
UICanvas              (ResultScreenUI + UndoButtonUI)
├─ ResultPanel → TitleText, SubText, RetryBtn, MenuBtn, Keep GoingBtn (kapalı başlar)
├─ UndoBtn                                        (kapalı başlar; UndoButtonUI açar)
├─ TrayRemaining                                  "+N" tepsi sayacı (sağ alt, raycast kapalı)
└─ ProgressSafeArea (SafeAreaFitter)
   ├─ ProgressHUD → ProgressLabel
   │                └─ ProgressBars → Row_&lt;Renk&gt; (runtime)
   └─ ScoreHUD    → ScoreLabel, ComboLabel        (sağda, level dropdown'ın altında)
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
7. **SRP Batcher:** `CellView` hücre başına `Material` örneği yaratır ama hepsi tek shader'ı
   paylaşır. `MaterialPropertyBlock` eklemek batching'i **bozar** — kullanma.
8. **Kamera sarsıntısı sadece öteleme yapar.** Ekran→dünya ışını atan her yeni kod
   `GameFX.CurrentShakeOffset`'i ışının origin'inden çıkarmalı.
9. **`LaunchAreaAnchor.Reanchor()` `BallQueue.Load()`'dan önce** çağrılmalı.
10. **Material/Texture sızıntısı:** Kod runtime'da `new Material(...)` yapıyorsa
    `OnDestroy`'da `Destroy` etmeli (`CellView`, `BallVisual`, `AimPreview`, `GridBoard`
    bunu yapıyor — yeni kod da yapmalı).
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
14. **Kapsamada özel hücreler daima üst sınırı gevşetir.** `Impossible` kararı oyunu
    bitirdiği için yanlış pozitif **verilemez**: joker hücreleri hiçbir rengin
    `required`'ına yazılmaz ama her rengin `ceiling`'inde sayılır, ayrı bir wild satırı
    yalnızca joker hücrelerine karşı ölçülür. Yeni bir hücre tipi eklerken de yön aynı:
    şüphede kal, kanıtlama.
15. **Boş joker hücresi varken renk purge'ü yapılmaz** (`GameManager.PurgeCompletedColors`).
    Joker her rengi kabul ettiği için "işi bitmiş" bir rengin topları hâlâ level'i
    bitirebilecek toplardır; purge onları çöpe atardı.
16. **Undo skoru da geri alır** (`GameManager.RevertLastAward`). Yoksa "boya → geri al →
    aynı yeri boya" sınırsız puan üretir. Aynı sebeple `LevelLoader.Apply`
    `ResetScore()` çağırır: level seçici sahneyi yeniden yüklemiyor.

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

- **Level verisi bozuk (tespit edildi, düzeltilmedi).** Doğrulayıcı 2026-08-11'de
  gerçek kapsamaya geçirilince şunlar ortaya çıktı ve hâlâ duruyor:
  `level1` **bitirilemiyor** (siyah iki adet 1 sıralık şerit: 24 hücre, kare
  damgaların tavanı 18); `level2`'nin top kuyruğu **boş**; `level1–3`'te
  `(1,1)`'de tek hücrelik bir yeşil renk var (içe aktarma artığı, oyuncuya
  özel bir top harcatıyor); `level5`'te pembe ve mor 2'şer çapraz hücre,
  tam 1.00× payla. Editörde bu level'ler artık kırmızı görünüyor.
  **Beklenen davranış:** `GameManager.IsDeadEnd()` devreye girdiği için `level1`
  ilk atıştan hemen sonra "Dead End" ile biter — bu bir hata değil, doğru teşhis.
  Level verisi düzeltilince kendiliğinden geçer.
  **Not:** `level1`'in siyah şeridi tam olarak `Line` damgasının işi — aynı 5 top
  `Line` olsaydı tavan 18 yerine 31 olurdu (gereken 24). Tek sıralık şeritleri kare
  damgayla boyatmak yerine şekli değiştirmek, düzeltmenin en ucuz yolu.
- **Skor kalıcı değil.** Level bitince skor sonuç ekranında görünüp gider; kayıt
  (`PlayerPrefs`) ve yıldız eşiği Faz 4'ün işi (F4-1 / F4-2). Yıldızlar "artan top
  oranına" bakacak, skora değil — ikisi ayrı ölçüler.
- **Özel hücreler yalnız `level5`'te var** (sol sütunda 10 joker, 2026-08-12). Buz ve taş
  hiçbir elle yazılmış level'de kullanılmıyor; `LevelBuilder` Normal+ bandlarda üretiyor.
- **Casual sadeleştirme, paket 1 (2026-09-15, `feature/casual-simplify`).** Üçlü tepsi,
  ortalanmış tek sayılı kare damgalar (1/3/5), tek-dokunuş girdi (lift/rampa kaldırıldı),
  gerçek şekilli top gövdeleri, pastel palet, 66° kamera. `L` ve `Diagonal` şekilleri kodda
  **duruyor** (JSON uyumluluğu, enum append-only) ama üretici ve kurtarma topları artık
  onları vermiyor; editör listesinde hâlâ seçilebilirler. `level1–5` yeni kurallarla
  `LevelBuilder` ile **yeniden üretildi** (eski elle yazılmış level'ler git geçmişinde,
  `bed37d0`). Sırada: paket 2 (yuvarlatılmış küp, gradient arka plan, bloom, "Great!" yazısı,
  konfeti), paket 3 (booster çubuğu, taş gölgesi/joker hücresinin kaldırılması, dead-end'in
  uyarıya dönmesi), paket 4 (ses).
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
- **Test assembly'si hâlâ yok.** F6-3'ün kapsama matematiği (buz vuruşları, taş gölgesi,
  joker kovası) `dotnet` altında çalışan geçici bir konsol koşumuyla doğrulandı
  (`GameConstants` + `CoverageAnalyzer` + `LevelAutoSolver` + `LevelValidator` gerçek
  dosyaları, sahte `UnityEngine` tipleriyle derlenir). Kalıcı bir test assembly'si
  eklenirse ilk taşınacak şey bu.
- `MainMenu` ve `LevelSelect` sahneleri kodda referanslı ama **mevcut değil**.
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
