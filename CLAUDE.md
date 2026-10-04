# CatapultGames — Çalışma Kuralları

Unity 6 (6000.3.16f1) + URP mobil oyun projesi. Mancınık/boyama bulmacası.

---

## KURAL 0 — Önce mimari haritasını oku

Her görevin ilk adımı: **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** dosyasını oku.

Orada şunlar var: oyunun kuralları, klasör yapısı, veri modeli, runtime akış diyagramı,
dosya dosya sorumluluk tablosu, sahne hiyerarşisi, değişmezler listesi, kod konvansiyonları.

**Projeyi baştan sona tarama.** Mimari haritası bu tarama işini zaten yapılmış hâlde
sunuyor; tekrar taramak bağlamı ve zamanı boşa harcar.

---

## KURAL 1 — Hedefli dosya açma

`ARCHITECTURE.md`'yi okuduktan sonra, **sadece göreve doğrudan dokunan dosyaları** aç.

| Durum | Yapılacak |
|---|---|
| Harita hangi dosyanın sorumlu olduğunu söylüyor | O dosyayı (ve varsa 1–2 doğrudan bağımlısını) aç, işi yap |
| Harita yetersiz kalıyor / bir sembolün nerede olduğu belirsiz | `Grep` ile hedefli ara — dosya dosya `Read` ile gezme |
| Değişiklik 3+ sistemi ilgilendiriyor | Önce haritadaki akış diyagramından etkilenen düğümleri çıkar, sonra sadece onları aç |

Açmadan önce şunu düşün: *"Bu dosyayı harita zaten yeterince anlattı mı?"* Evetse açma.

**Asla toplu okuma yapma:** `Assets/TextMesh Pro/`, `Library/`, `Temp/`, `Logs/`,
`*.csproj`, `.unity` sahne dosyaları, `.meta` dosyaları, `ProjectSettings/*.asset`.
Sahne içeriğini öğrenmek için `.unity` YAML'ını okuma —
`Assets/_Project/Scripts/Editor/Scenes/` sahneleri zaten kod olarak tarif eder.

---

## KURAL 2 — Değişmezleri bozma

`ARCHITECTURE.md` § 10'daki liste pazarlığa açık değil. Özellikle sık tökezlenen üçü:

1. `GameConstants.TrajectorySteps` / `TrajectoryTimeStep` — `AimPreview` ile `BallLauncher`
   aynı değerleri kullanmak zorunda, yoksa nişan önizlemesi yalan söyler. Aynı şekilde
   yerçekimi **yalnızca** `GameConstants.Gravity`'dir; `TrajectorySimulator` ve
   `LaunchSolver` ondan türer. Hiçbirine ayrı sayı yazma.
2. `CellColor` enum sırası = palet indeksi = JSON'daki int. Araya değer eklemek tüm
   level'leri bozar; sadece **sona** ekle.
3. `CellView` hücrelerinde `MaterialPropertyBlock` kullanma — SRP Batcher'ı bozar.

Boyama kuralı (kaç hücre, hangi şekil) değişecekse **tek yer**: `Shared/GameConstants.cs`.
Oradan `PaintingSystem`, `AimPreview`, `LevelValidator`, `LevelAutoSolver` otomatik uyumlanır.

---

## KURAL 3 — Yeni bileşen eklerken sahne üretecini güncelle

Sahneler elle değil `Scripts/Editor/Scenes/` altındaki üreteçlerle kurulur
(`CatapultGames/Build Scenes`). Yeni bir MonoBehaviour eklediysen veya mevcut birine
`[SerializeField]` alan eklediysen ilgili üretece de ekle (`SceneKit.SetRef` / `SetRefArray` /
`SetFloat` / `SetStr`):

| Ne | Nerede |
|---|---|
| Dünya + oynanış sistemleri (GameScene) | `GameSceneBuilder.cs` |
| Oyun içi HUD (`Canvas_Game`) | `GameHudBuilder.cs` |
| Meta UI (`Canvas_Meta`: menü, level seçimi) | `UiSceneBuilder.cs` (+ `LevelPickerFrame.cs`) |
| Init / Boot sahneleri, Build Settings | `SceneSetBuilder.cs` |

Alan adı string'le bağlanır: alanı yeniden adlandırırsan üreteçteki string'i de değiştir.
Aksi hâlde sahne yeniden üretildiğinde referans kaybolur (yalnız `[SceneKit] Field not found`
uyarısı düşer) ve hata sessizce çalışma zamanında ortaya çıkar.

---

## KURAL 4 — Level'lerin tek yeri: Resources/Levels

Level'ler **yalnızca** `Assets/Resources/Levels/*.json` içinde yaşar. Level Editor
oraya yazar, oyun oradan okur. Ara kopyalama/export adımı **yoktur** — ikinci bir
level klasörü oluşturma.

Koddan bir level JSON'u yazarsan ardından `AssetDatabase.Refresh()` çağrılmalı;
yoksa Unity dosyayı TextAsset olarak import etmez ve `Resources.Load` göremez.

---

## KURAL 5 — Kod stili ve sahne düzeni

Şirketin mobil oyun kod kuralları uygulanır; sapmalar aşağıda açıkça listelidir.

**İsim ve tip**
- Namespace: runtime `CatapultGames`, editör `CatapultGames.Editor`. Runtime kodu Editor
  assembly'sini göremez; ortak mantık `Scripts/Shared/`'a gider.
- `[SerializeField] private camelCase` (alt çizgi **yok**); serileşmeyen private alanlar
  `_camelCase`; sabitler `PascalCase`. Public alan açma — dışarıya property ile ver.
- Sınıflar varsayılan `sealed`. Dosya başına tek tip (iç içe tip yok; enum/struct kendi dosyasına).

**Üye sırası:** event'ler → property'ler → serialized alanlar → private/const/static alanlar →
ctor/Dispose → Unity mesajları (Awake, OnEnable, Start, Update, OnDisable, OnDestroy) →
public metotlar → private metotlar. Bölücü yorum (`// ── X ──`) yok.

**Kod biçimi**
- `if`/`for`/`foreach` gövdesi her zaman alt satırda; süslü parantez yalnız çok satırlı gövdede.
- Metot ≤ 20 satır — **yeni ve dokunulan kodda**. Eski uzun metotlar dokunulunca bölünür.
- Kullanılmayan `using` bırakma (ama `#if` bloklarının ihtiyacı olanı silme — örn. `Haptics`).

**UI**
- Listener'lar `OnEnable`'da eklenir, `OnDisable`'da çıkarılır (Awake/Start'ta değil).
- Canvas'lar **Screen Space - Camera**; `CanvasScaler` referans çözünürlüğü
  `UiLayout.ReferenceResolution` (1320×2868), match 0.5. Eski 1080×1920 düzeninden gelen
  piksel/punto değerleri ×1.35 ölçeklenir.
- Statik UI çerçevesi sahne üretecinde kurulur; runtime yalnız veriye bağlı parçaları
  (satır, karo) üretir.

**Sahne düzeni:** `InitScene` (index 0) → `BootScene` → `GameScene` → `UIScene`, additive ve
bu sırayla (`InitSceneLoader`). Sahneler arası istekler `GameFlow` olayları üzerinden;
level'ler yerinde yüklenir, sahne yeniden yüklenmez. Meta UI `UI` layer'ında (5), Base
kameraya stack'lenen Overlay kamerada çizilir; `GameScene`'in Base kamerası bu layer'ı görmez.

**Malzeme / kayıt**
- Runtime'da `Shader.Find` yok: malzemeler `MaterialSet` asset'inden (`materials` alanı)
  gelir; `Shader.Find` yalnız editör üreteçlerinde (`MaterialSetAsset`).
- Runtime'da `new Material(...)` yaptıysan `OnDestroy`'da `Destroy` et.
- `PlayerPrefs`'e doğrudan erişme: `SaveStore.Current` (`ISaveStore`) üzerinden.

**Proje kuralları (değişmedi)**
- Girdi daima yeni Input System (`Touchscreen.current?.primaryTouch` → `Mouse.current`
  fallback). Eski `Input.GetMouseButton` vb. kullanma.
- Fizik motoru yok: Rigidbody/Collider ekleme. Uçuş `TrajectorySimulator` ile simüle edilir
  ve top bu yay boyunca sabit sürede tween'lenir. (Gerçek fizik denenip geri alındı —
  gerekçe: ARCHITECTURE.md § 12.)
- Animasyonlar `IEnumerator` + `Time.deltaTime`. Yeni tween kütüphanesi ekleme.

**Bilinçli sapmalar** (şirket kurallarından)
- Yorumlar silinmez: İngilizce "neden" yorumları korunur, yalnız "ne" anlatan yorumlar atılır;
  çevredeki dosyaların yoğunluğuna uy.
- DI container / mesaj kütüphanesi / async / tween paketi (VContainer, MessagePipe, UniTask,
  PrimeTween, Odin) ve şirket buton bileşeni **eklenmedi**: bağlantı sahne üretecinde,
  sahneler arası iletişim statik `GameFlow` hub'ında, animasyon coroutine'de.
- Combo/skor isimlendirmesi ve `BallLauncher`'ın mantık/animasyon katmanlaması olduğu gibi kaldı.
- 20 satır kuralı geriye dönük toplu uygulanmadı (yukarıda).

---

## KURAL 6 — Mimari haritasını güncel tut

Şunlardan biri olduysa, işi bitirmeden `docs/ARCHITECTURE.md`'yi de güncelle:

- Yeni script/klasör eklendi veya silindi → § 3 ve § 7 tabloları
- Veri modeli (`Data/`) veya JSON şeması değişti → § 4
- Boyama/yörünge kuralı değişti → § 5 ve § 10
- Runtime akışı değişti (yeni girdi modu, yeni sistem) → § 6 diyagramı
- Sahne yapısı değişti → § 8
- Yeni bir değişmez ortaya çıktı → § 10
- Teknik borç kapandı veya eklendi → § 12

Güncelleme kozmetik değil: bir sonraki oturum **sadece bu dosyayı** okuyacağı için,
harita eskirse sonraki geliştirme yanlış varsayımla başlar.

---

## Hızlı referans

```
Kural merkezi (yerçekimi/boyama/palet/yörünge) → Assets/_Project/Scripts/Shared/GameConstants.cs
Izgara sahibi + koordinat dönüşümleri         → Scripts/Gameplay/Grid/GridRenderer.cs
Atış → uçuş → boyama zinciri                   → Scripts/Gameplay/Ball/BallLauncher.cs
Kazanma/kaybetme kararı + dead-end uyarısı    → Scripts/Gameplay/GameManager.cs
Booster'lar (Rainbow/Recolor/Bomb)             → Scripts/Gameplay/BoosterSystem.cs (+ UI/BoosterBarUI.cs)
Ses (sentez + çalma, sahne bağlantısı yok)     → Scripts/Shared/GameAudio.cs
JSON → sahne yükleme                           → Scripts/Gameplay/LevelLoader.cs
Level sırası / ilerleme (kazanılan, kilit)     → Scripts/Shared/LevelOrder.cs + PlayerProgress.cs
Menü panelleri (UIScene)                       → Scripts/UI/MainMenuUI.cs + LevelSelectUI.cs
Öğretici + level ipucu bandı                   → Scripts/Gameplay/UI/TutorialHint.cs
Girdi (hücreye dokun → ateş)                   → Scripts/Gameplay/Ball/TapLaunchController.cs
Nişan önizleme (yay + boyanacak hücreler)      → Scripts/Gameplay/Aim/AimPreview.cs
Sahne kurulumu (4 sahne, referans bağlama)     → Scripts/Editor/Scenes/ (SceneSetBuilder → Game/GameHud/UiSceneBuilder)
Sahneler arası istekler (level, menü)          → Scripts/Shared/GameFlow.cs
Açılış zinciri                                 → Scripts/App/InitSceneLoader.cs + Bootstrap.cs
Malzemeler / kayıt                             → Scripts/Shared/MaterialSet.cs + Shared/Save/SaveStore.cs
Level yazma aracı (shell + Editor sekmesi)     → Scripts/Editor/LevelEditorWindow.cs
Pencere ağacı / tema (UI Toolkit)              → Scripts/Editor/UI/LevelEditorWindow.uxml + .uss
Gallery / Produce / Solving sekmeleri          → Scripts/Editor/Gallery|Produce|Solving/
Prosedürel level üretimi (tek kaynak)          → Scripts/Editor/LevelBuilder.cs + LevelBuildSpec.cs
Zorluk eğrisi (tek seam)                       → Scripts/Editor/LevelDifficultySchedule.cs
```

Level editörü kuralları: UXML eleman adı = C# `Q<>(name)` sözleşmesi (birini değiştirirsen
diğerini de değiştir, yoksa sessiz null); statik UI koddan değil UXML'den; gizleme yalnız
`cg-hidden` sınıfıyla; sekme controller'ları birbirini tanımaz, köprü penceredir.

Editör menüleri (hepsi tek üst menüde, `CatapultGames`):
- `CatapultGames/Level Editor`
- `CatapultGames/Build Scenes` (Init/Boot/Game/UI + Build Settings; Play'e `InitScene`'den bas)
