# Devir Teslim — Nerede Kaldık (2026-09-16)

> Başka bir bilgisayardan devam etmek için. Önce bu dosyayı, sonra
> [ARCHITECTURE.md](ARCHITECTURE.md) § 7 ve § 12'yi oku. Bu dosya oturum sonunda
> güncellenir; iş bitince silinebilir.

Branch: **`feature/casual-simplify`** (main'den açıldı, main'e henüz merge edilmedi).

---

## 1. Ne yapılıyordu?

Oyun, güncel sort/block puzzle hitleri (Block Blast, Hexa Sort) gibi **basitleştiriliyor
ve görsel olarak güçlendiriliyor**. İş dört pakete bölündü; her paket ayrı commit.

| Paket | İçerik | Durum | Commit |
|---|---|---|---|
| 0 | Level editörü 4 sekmeli UI Toolkit shell'e taşındı (Editor / Gallery / Produce / Solving) | ✅ | `bed37d0` |
| 1 | Üçlü top tepsisi, iniş hücresinde ortalı tek sayılı damgalar (1/3/5), tek-dokunuş girdi (kaldırma/rampa yok), gerçek şekilli top gövdeleri, pastel palet, 66° kamera, level1–5 yeniden üretildi | ✅ | `7169b97` |
| 2 | Yuvarlatılmış küpler, açık tahta/soket teması, gradient arka plan, bloom + vignette, dolu hücre emisyonu, "GREAT!" praise, renk bitince konfeti, hit-stop | ✅ | `d4c1c12` |
| 3 | Booster çubuğu (Rainbow / Recolor / Bomb), taş = delik (gölge kuralı kalktı), joker üretimi durdu, dead-end otomatik kayıp yerine uyarı | 🟡 **yarım** | bu commit (WIP) |
| 4 | Ses paketi (atış, iniş, hücre tık, renk fanfarı, praise stinger) | ⬜ başlanmadı | — |

## 2. Paket 3'te ne bitti, ne kaldı?

**Bitti (derleniyor, 0 hata):**
- `PaintingSystem` ve `CoverageAnalyzer`: taş gölgesi ve "taşa nişan = damga yutulur" kuralı
  kaldırıldı; taş artık sadece boyanmayan bir delik. `GameConstants.GetStampPath` silindi.
- `CellColor.Any = 8` eklendi (yalnız top rengi, hücre değil). `ColorMatches` Any'yi her hedefle
  eşler; `CoverageAnalyzer` Any topu her rengin tavanına ekler; palet 9 elemanlı.
- `BoosterSystem` (Gameplay/) ve `BoosterBarUI` (Gameplay/UI/) yazıldı: seçili tepsi topunu
  yerinde değiştirir (`BallQueue.NotifyCurrentChanged`), tepsi `BallVisual.Matches` ile yeniden
  çizer, Rainbow top blokları gökkuşağı renklerinde.
- `LevelLoader` `_boosters?.ResetForLevel()` çağırıyor (alan eklendi).
- Üretici reçetelerinde joker 0; editörde Any swatch'ı gizli; taş/joker tooltip'leri güncel.

**Kaldı (sıra önemli):**
1. **`GameManager`**: `OnBallLanded` içindeki `if (IsDeadEnd()) EndGame(DeadEnd)` bloğunu
   uyarıya çevir. Plan: `CoverageAnalyzer.Analyze` satırlarından ilk `Impossible` rengi bul,
   `_warnedColor` ile aynıysa tekrar uyarma, `[SerializeField] TextMeshProUGUI _warningLabel`
   (opsiyonel) üzerinde "Blue can't be finished — undo or use a booster" yaz, 2.5 sn sonra
   söndür, hafif `Shake` + `Haptics.Medium`. `ResetScore`'da `_warnedColor = None`.
   `using TMPro;` ekle. Kayıp yalnızca top bitince (`OutOfBalls`, Keep Going teklifi mevcut).
2. **`GameplaySceneBuilder`** (KURAL 3): `BoosterSystem` GO'su + referansları (`_queue`,
   `_grid`, `_gameManager`); canvas'a `BoosterBarUI` + 3 buton (öneri: sol altta, undo
   butonunun simetriği, anchor y 0.10–0.17) + 3 sayaç TMP etiketi, `SetRef` ile bağla;
   `LevelLoader._boosters`; `GameManager._warningLabel` için canvas'ta bir TMP etiketi
   (ekranın üst-orta, raycast kapalı). Sonra § 8 sahne hiyerarşisini güncelle.
3. **Derleme kontrolü** (bkz. § 4) ve Unity'de `CatapultGames/Build Gameplay Scene` çalıştır —
   sahne dosyası üreteçle yeniden yapılmadan paket 2–3'ün çoğu görünmez.
4. **ARCHITECTURE.md** güncelle: § 1 kurallar (taş delik, joker legacy, booster'lar),
   § 4 `CellColor.Any`, § 5 (`GetStampPath` yok, `ColorMatches` Any), § 6.2 (dead-end →
   uyarı), § 7 (PaintingSystem, GameManager, BoosterSystem, BoosterBarUI, BallQueue),
   § 10 (gölge maddesini sil; "Any hiçbir zaman hücre rengi olmaz" değişmezi ekle), § 12.
5. Commit: "Casual rework, package 3: boosters, stone as hole, dead-end warning".
6. Paket 4 (ses): `GameFX` zaten tüm olayları merkezileştiriyor; `AudioSource` + klipler
   oraya eklenir. Projede şu an **hiç ses varlığı yok**, klipler eklenmeli.

## 3. Henüz Unity'de görsel olarak doğrulanmayanlar

Bu oturumun tamamı Unity dışında derleme kontrolüyle yapıldı. **Hiçbir paket Play modunda
izlenmedi.** İlk açılışta beklenebilecekler:
- Yeni level editörü penceresi (UI Toolkit): yerleşim/odak pürüzleri.
- Tepsi: yuva konumları (`Slot_0..2`, x = ±1.65), seçili top yüksekliği, "+N" sayacı yeri.
- Kamera 66° ile tahta + tepsi kadrajı; `LaunchAreaAnchor._screenY = 0.10`.
- Bloom yoğunluğu (0.55) ve emisyon (0.22×) — fazla parlaksa `GameplayPostFX.asset` ve
  `CellView.SetEmission` katsayısı.
- Yuvarlatılmış küp mesh'i (`RoundedCubeMesh`) ince plakalarda nasıl duruyor.

## 4. Unity açıkken derleme kontrolü (bu oturumun yöntemi)

Unity Editor açıkken batchmode çalışmaz. Bunun yerine `dotnet` 10 ile Unity'nin ürettiği
csproj'lardan glob'lu kopyalar üretilip derlendi. Script bu repoda değil (scratchpad'deydi);
yeniden yazmak 20 satır:

1. `CatapultGames.Runtime.csproj` / `CatapultGames.Editor.csproj`'u kopyala
   (`*.Check.csproj` adıyla, gitignore'da `*.csproj` var).
2. Tüm `<Compile Include="..." />` satırlarını sil; yerine
   `<Compile Include="Assets/_Project/Scripts/**/*.cs" Exclude="Assets/_Project/Scripts/Editor/**/*.cs" />`
   (runtime) ve `<Compile Include="Assets/_Project/Scripts/Editor/**/*.cs" />` (editor) ekle.
   **İleri bölü kullan**, ters bölü bash'te bozuluyor.
3. `Temp\obj\$(MSBuildProjectName)` → `Temp\obj\check\...`, `Temp\bin\Debug\` → `Temp\bin\check\`.
4. Editor check'te `ProjectReference` `CatapultGames.Runtime.Check.csproj`'a çevrilir ve
   `Library/ScriptAssemblies/Unity.RenderPipelines.Core.Runtime.dll` +
   `Unity.RenderPipelines.Universal.Runtime.dll` `<Reference HintPath>` olarak eklenir
   (Editor asmdef artık URP'ye referanslı).
5. `dotnet build X.Check.csproj -nologo -v q -clp:ErrorsOnly`. LangVersion C# 9.

Level üretimi/solver mantığı Unity dışında da koşturulabilir: `net10.0` konsol projesi,
`Temp/bin/check/*.dll` + `…/Managed/UnityEngine/*.dll` referanslı; `JsonUtility` native
olduğu için JSON'u elle yaz (level1–5 böyle üretildi: `LevelBuilder.TryBuild` → JSON).

## 5. Bilinen açık noktalar (paketlerden bağımsız)

- `MainMenu` / `LevelSelect` sahneleri yok; sonuç ekranındaki Menu butonu var olmayan sahneye
  gidiyor. Kazanınca "Next level" yok. (İlk değerlendirmede tespit edildi, bu branch'te ele
  alınmadı.)
- Skor/ilerleme kalıcı değil; ses yok; test assembly'si yok; `applicationIdentifier` şablon.
- Solving sekmesi eşikleri (0.85/0.65/0.40/0.20) kalibre edilmedi; band botu `careless-15`.
- Üretilmiş level'ler "greedy şekilli" (bkz. ARCHITECTURE § 12).
- `L` ve `Diagonal` şekilleri kodda duruyor ama üretilmiyor; enum append-only.
