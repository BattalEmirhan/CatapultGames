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
`Assets/_Project/Scripts/Editor/GameplaySceneBuilder.cs` sahneyi zaten kod olarak tarif eder.

---

## KURAL 2 — Değişmezleri bozma

`ARCHITECTURE.md` § 10'daki liste pazarlığa açık değil. Özellikle sık tökezlenen üçü:

1. `GameConstants.TrajectorySteps` / `TrajectoryTimeStep` — `AimPreview` ile `BallLauncher`
   aynı değerleri kullanmak zorunda, yoksa nişan önizlemesi yalan söyler. Aynı şekilde
   yerçekimi **yalnızca** `GameConstants.Gravity`'dir; `TrajectorySimulator` ve
   `LaunchSolver` ondan türer. Hiçbirine ayrı sayı yazma.
2. `CellColor` enum sırası = palet indeksi = JSON'daki int. Araya değer eklemek tüm
   level'leri bozar; sadece **sona** ekle.
3. Tahta sprite katmanları `sortingOrder` ile yığılır (`CellView`: panel −20 … buz 5, toplar 20);
   derinliğe güvenme. Hücre rengi `SpriteRenderer.color`, hücre başına materyal yok. Çizimler `Shared/TileArt.cs`.

Boyama kuralı (kaç hücre, hangi şekil) değişecekse **tek yer**: `Shared/GameConstants.cs`.
Oradan `PaintingSystem`, `AimPreview`, `LevelValidator`, `LevelAutoSolver` otomatik uyumlanır.

---

## KURAL 3 — Yeni bileşen eklerken sahne üretecini güncelle

Sahne bağlantıları elle değil `GameplaySceneBuilder.cs` üzerinden kurulur. Yeni bir
MonoBehaviour eklediysen veya mevcut birine `[SerializeField]` alan eklediysen,
`GameplaySceneBuilder`'a da ekle (`SetRef` / `SetRefArray` / `SetFloat` / `SetStr`).

Aksi hâlde sahne yeniden üretildiğinde referans kaybolur ve hata sessizce çalışma
zamanında ortaya çıkar.

---

## KURAL 4 — Level'lerin tek yeri: Resources/Levels

Level'ler **yalnızca** `Assets/Resources/Levels/*.json` içinde yaşar. Level Editor
oraya yazar, oyun oradan okur. Ara kopyalama/export adımı **yoktur** — ikinci bir
level klasörü oluşturma.

Koddan bir level JSON'u yazarsan ardından `AssetDatabase.Refresh()` çağrılmalı;
yoksa Unity dosyayı TextAsset olarak import etmez ve `Resources.Load` göremez.

---

## KURAL 5 — Kod stili

- Namespace: runtime `CatapultGames`, editör `CatapultGames.Editor`. Runtime kodu Editor
  assembly'sini göremez; ortak mantık `Scripts/Shared/`'a gider.
- `[SerializeField] private _camelCase` — public alan açma.
- Girdi daima yeni Input System (`Touchscreen.current?.primaryTouch` → `Mouse.current`
  fallback). Eski `Input.GetMouseButton` vb. kullanma.
- Fizik motoru yok: Rigidbody/Collider ekleme. Uçuş `TrajectorySimulator` ile simüle edilir
  ve top bu yay boyunca sabit sürede tween'lenir. (Gerçek fizik denenip geri alındı —
  gerekçe: ARCHITECTURE.md § 12.)
- Animasyonlar `IEnumerator` + `Time.deltaTime`. Yeni tween kütüphanesi ekleme.
- Runtime'da `new Material(...)` yaptıysan `OnDestroy`'da `Destroy` et.
- `Shader.Find` daima yedekli: `... ?? Shader.Find("Standard")`.
- Yorumlar İngilizce ve "neden"i anlatır; çevredeki dosyaların yoğunluğuna uy.

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
Menü ekranları                                 → Scripts/UI/MainMenuUI.cs + LevelSelectUI.cs
Öğretici + level ipucu bandı                   → Scripts/Gameplay/UI/TutorialHint.cs
Girdi (hücreye dokun → ateş)                   → Scripts/Gameplay/Ball/TapLaunchController.cs
Nişan önizleme (yay + boyanacak hücreler)      → Scripts/Gameplay/Aim/AimPreview.cs
Sahne kurulumu (referans bağlama)              → Scripts/Editor/GameplaySceneBuilder.cs
Menü sahneleri kurulumu                        → Scripts/Editor/MenuSceneBuilder.cs
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
- `CatapultGames/Build Gameplay Scene`
- `CatapultGames/Build Menu Scenes`
