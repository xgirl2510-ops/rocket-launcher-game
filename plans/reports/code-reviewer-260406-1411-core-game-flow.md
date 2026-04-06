# Code Review — Core Game Flow Scripts

**Date:** 2026-04-06  
**Reviewer:** code-reviewer  
**Score: 8.8 / 10**

---

## Scope

| File | Dòng |
|---|---|
| `Assets/Scripts/Core/RoundManager.cs` | 160 |
| `Assets/Scripts/Core/round-manager-auto-play-restart-and-target.cs` | 132 |
| `Assets/Scripts/Core/round-manager-hud.cs` | 127 |
| `Assets/Scripts/Core/GameRoundTracker.cs` | 46 |
| `Assets/Scripts/Core/GameConstants.cs` | 19 |
| `Assets/Scripts/Core/runtime-sprite-factory.cs` | 64 |

Tổng ~548 dòng. Tập trung vào null safety, race conditions, memory leaks, event balance, singleton correctness, state management.

---

## Overall Assessment

Code chất lượng cao, kiến trúc rõ ràng. Partial class split hợp lý. Event subscribe/unsubscribe cân bằng tốt. Singleton pattern đúng chuẩn. Vài vấn đề nhỏ nhưng không gây crash.

---

## Critical Issues

_Không có._

---

## High Priority

### 1. `ReloadRocket()` gọi `_launchController.EnableInput()` nhưng thiếu null guard

**File:** `RoundManager.cs` dòng 134

`_launchController` có null guard trong `Start()` và `OnValidate()` nhưng `ReloadRocket()` gọi trực tiếp không check:

```csharp
// Dòng 129–134
_rocket.ResetToPosition(_spawnPoint.position);
// ...
_launchController.EnableInput();  // NullReferenceException nếu _launchController = null
```

Tương tự `_spawnPoint.position` tại dòng 129 — nếu `_spawnPoint` null sẽ crash.

**Fix:**
```csharp
_launchController?.EnableInput();
// và
if (_spawnPoint != null) _rocket.ResetToPosition(_spawnPoint.position);
```

Tương tự `ReloadAfterAutoPlay()` dòng 126 trong file `round-manager-auto-play-restart-and-target.cs`.

---

### 2. `HandleAutoPlay()` gọi `_rocket.Launch()` khi rocket có thể đang bay

**File:** `round-manager-auto-play-restart-and-target.cs` dòng 51–77

Không có guard check `_rocket.IsFlying` trước khi gọi `_rocket.Launch()`. Nếu player mở HUD hint và click "Auto Play" trong khi rocket đang bay → reset + re-launch ngay lập tức, physics không nhất quán.

**Fix:**
```csharp
public void HandleAutoPlay()
{
    if (_rocket != null && _rocket.IsFlying) return;  // thêm dòng này
    if (_obstacleSpawner == null) return;
    // ...
}
```

---

### 3. `HandleRestart()` không check `_rocket` null trước `SetActive`

**File:** `round-manager-auto-play-restart-and-target.cs` dòng 27

```csharp
_rocket.gameObject.SetActive(true);  // crash nếu _rocket = null
_rocket.ResetToPosition(_spawnPoint.position);
```

Trong khi `RoundManager.cs Start()` đã có `if (_rocket != null)`, nhưng `HandleRestart()` giả định `_rocket` không null. Với YAGNI thì tạm chấp nhận vì _rocket phải có — nhưng pattern không nhất quán.

---

## Medium Priority

### 4. `RuntimeSpriteFactory.ResetStaticState()` — Destroy texture trước sprite

**File:** `runtime-sprite-factory.cs` dòng 19–23

```csharp
Object.Destroy(_solidSprite.texture);
Object.Destroy(_solidSprite);
```

Unity khuyến cáo không destroy texture đang được sprite tham chiếu trước khi destroy sprite. Thứ tự nên đảo lại — destroy sprite trước, texture sau (hoặc để GC tự xử lý). Trong thực tế `SubsystemRegistration` chạy trước scene unload nên ít bị lỗi, nhưng order vẫn sai về nguyên tắc.

**Fix:**
```csharp
var tex = _solidSprite.texture;
Object.Destroy(_solidSprite);
Object.Destroy(tex);
_solidSprite = null;
```

---

### 5. `GameRoundTracker.TryUpdateBest()` không được gọi khi auto-play hit target

**File:** `RoundManager.cs` dòng 89–93

```csharp
if (_isAutoPlaying)
{
    StartCoroutine(DelayedAction(_reloadDelay, ReloadAfterAutoPlay));
    return;  // TryUpdateBest bị skip — đúng, vì auto-play không tính score
}
_roundTracker.TryUpdateBest(_roundTracker.RoundShots);
```

Đây là **behavior đúng** về game design (auto-play không tính best score). Nhưng thiếu comment giải thích intent → dễ confuse dev sau này. Nên thêm comment:

```csharp
// Auto-play không tính vào best score — bỏ qua
if (_isAutoPlaying) { ... }
```

---

### 6. `RoundManagerHUD.Instance` dùng pattern singleton nhưng không `DontDestroyOnLoad`

Đây là **thiết kế có chủ đích** (comment dòng 31 ghi rõ "Single-scene only"). OK. Nhưng nếu sau này có multi-scene thì cần refactor. Ghi nhận như tech debt.

---

### 7. `RoundManager.OnDestroy()` unsubscribe `OnLookTargetComplete` nhưng `Start()` không subscribe

**File:** `RoundManager.cs` dòng 149–150

```csharp
// OnDestroy:
_cameraController.OnLookTargetComplete -= OnLookTargetDone;  // safe, -= trên null delegate không crash
```

`OnLookTargetComplete` chỉ được subscribe trong `HandleLookTarget()` (file partial), không subscribe trong `Start()`. Unsubscribe trong `OnDestroy()` là defensive coding — đúng, không crash, nhưng hơi dư thừa nếu `HandleLookTarget()` đã tự unsubscribe trong `OnLookTargetDone()`. Trong trường hợp LookTarget đang pan và scene unload → unsubscribe trong `OnDestroy()` là cần thiết. Giữ nguyên, OK.

---

## Low Priority

### 8. `RuntimeSpriteFactory.GetSolidSprite()` — pixels initialization có thể dùng `Color32`

```csharp
var pixels = new Color[16];
for (int i = 0; i < 16; i++) pixels[i] = Color.white;
```

Có thể thay bằng `tex.SetPixels32(new Color32[16])` với `Color32` default là `(0,0,0,0)` nên không dùng được trực tiếp. Hoặc `System.Array.Fill`. Không ảnh hưởng runtime — micro-optimization, bỏ qua theo YAGNI.

### 9. `GameConstants.GroundObjectName = "Ground"` vs `TagGround = "Ground"` — giá trị trùng nhau

Hai constant khác mục đích (tag vs object name) nhưng cùng value `"Ground"`. Không phải DRY violation vì semantic khác nhau. OK.

### 10. `GetStatsText()` dùng Unicode `\u00b7` (·) hardcoded

Không phải vấn đề chức năng, nhưng nếu thêm localization sau này cần refactor. YAGNI — giữ nguyên.

---

## Positive Observations

- **Event balance tốt**: Tất cả `+=` đều có `−=` tương ứng trong `OnDestroy()` hoặc trong callback chính nó (unsubscribe-on-fire pattern cho `OnIntroComplete`, `OnLookTargetComplete`).
- **`ResetStaticState()`** với `RuntimeInitializeOnLoadMethod` — pattern chuẩn, tránh static leak giữa các play sessions trong Editor.
- **`GameRoundTracker`** là plain C# class (không MonoBehaviour) — đúng, không cần lifecycle Unity.
- **`DisallowMultipleComponent`** trên `RoundManagerHUD` — tốt.
- **`OnValidate()` guard** với `gameObject.scene.isLoaded` — tránh false warning khi AddComponent từ editor tool.
- **Partial class split** hợp lý: core lifecycle tách khỏi restart/auto-play/target logic. File size hợp lệ (đều < 200 dòng).
- **`HandleAutoPlay()` guard** `dir.sqrMagnitude < 0.01f` — tránh launch với zero vector.
- **`TryUpdateBest()` guard** `shots <= 0` — tránh corrupt best score.

---

## Recommended Actions

1. **(High)** Thêm `_launchController?.EnableInput()` và `_spawnPoint != null` guard trong `ReloadRocket()` và `ReloadAfterAutoPlay()`.
2. **(High)** Thêm `if (_rocket != null && _rocket.IsFlying) return;` vào đầu `HandleAutoPlay()`.
3. **(Medium)** Đảo thứ tự Destroy trong `RuntimeSpriteFactory.ResetStaticState()` — sprite trước, texture sau.
4. **(Medium)** Thêm comment giải thích tại sao `TryUpdateBest` bị skip khi `_isAutoPlaying`.

---

## Metrics

- Linting Issues: 0 syntax errors
- Type Safety: Tốt — không có `dynamic`, không có `object` cast
- Memory Leaks: Không phát hiện — static reset đầy đủ
- Event Balance: Cân bằng (4/4 subscribe có unsubscribe)
- Null Safety: 2 chỗ thiếu guard (High #1, #3)
- File Size: Tất cả < 200 dòng — đạt chuẩn

---

## Unresolved Questions

1. `HandleRestart()` gọi `RocketDebris.ClearAll()` và `GroundScorch.ClearAll()` — nếu auto-play kết thúc (`ReloadAfterAutoPlay`) thì KHÔNG clear debris/scorch. Có phải intentional không? Nếu nhiều auto-play rounds liên tiếp, debris sẽ tích lũy.
2. `_isAutoPlaying` không được reset nếu scene bị unload giữa chừng khi auto-play đang chạy — không crash nhưng static state của `_isAutoPlaying` là instance field nên OK (không phải static). Không vấn đề.
