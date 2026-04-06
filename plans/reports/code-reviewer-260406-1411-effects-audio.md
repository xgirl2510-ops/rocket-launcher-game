# Code Review: Effects & Audio Scripts

**Date:** 2026-04-06  
**Scope:** Effects (5 files) + Audio (2 files)  
**Reviewer:** code-reviewer agent

---

## Code Review Summary

### Scope
- Files reviewed: 7
  - `Assets/Scripts/Effects/explosion-burst-particle-effect.cs`
  - `Assets/Scripts/Effects/ground-scorch-mark.cs`
  - `Assets/Scripts/Effects/rocket-debris-shatter-effect.cs`
  - `Assets/Scripts/Effects/rocket-trail-particle-effect.cs`
  - `Assets/Scripts/Effects/impact-effects-handler.cs`
  - `Assets/Scripts/Audio/AudioManager.cs`
  - `Assets/Scripts/Audio/ProceduralAudioClipGenerator.cs`
- Lines analyzed: ~530
- Review focus: Memory leaks, static state, ParticleSystem config, audio resource management, procedural math

### Overall Assessment

Code chất lượng tốt — DRY pattern qua `RuntimeSpriteFactory`, static cleanup dùng `RuntimeInitializeOnLoadMethod`, `OnDestroy` cleanup AudioClip đầy đủ. Tuy nhiên có một số vấn đề đáng chú ý: `RocketDebris._allDebris` có thể leak khi debris bị destroy bởi game logic ngoài `ClearAll()`, `GroundScorch` không cleanup `_groundPrepared` + `_allCraters` khi scene reload, và `PlayHitTarget()` có race condition nhỏ với `pitch`.

**Score: 8.2 / 10**

---

## Critical Issues

Không có critical issues (no data loss, no breaking crash risk).

---

## High Priority Findings

### 1. `RocketDebris` — `_allDebris` stale reference khi debris tự hủy
**File:** `rocket-debris-shatter-effect.cs` dòng 121-125

```csharp
Destroy(gameObject, 2f);   // destroy sau 2 giây
```
`OnDestroy` gọi `_allDebris.Remove(gameObject)` (dòng 131) — đúng rồi. Nhưng `ResetStaticState` (dòng 38-41) chỉ `.Clear()` list mà **không Destroy** các GameObjects còn tồn tại. Nếu `SubsystemRegistration` chạy trong khi debris vẫn sống trong scene, các GO đó sẽ mồ côi (không bị destroy, không còn được track).

**Fix:**
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStaticState()
{
    // Destroy còn sót nếu có
    for (int i = _allDebris.Count - 1; i >= 0; i--)
    {
        if (_allDebris[i] != null)
            Object.Destroy(_allDebris[i]);
    }
    _allDebris.Clear();
}
```

### 2. `AudioManager.PlayHitTarget()` — pitch race condition
**File:** `AudioManager.cs` dòng 68-72

```csharp
_oneShotSource.pitch = 1.3f;
_oneShotSource.PlayOneShot(_boomClip);
_oneShotSource.pitch = 1.0f;   // ← reset ngay lập tức
```
`PlayOneShot` không bị ảnh hưởng bởi `pitch` sau khi đã bắt đầu play, nhưng nếu một `PlayOneShot` khác (vd `PlayHitGround`) chạy đồng thời trên cùng source, pitch sẽ bị set 1.3f không mong muốn.

**Fix tốt hơn:** Dùng source riêng hoặc `AudioSource.PlayClipAtPoint()` tại điểm va chạm với pitch override:
```csharp
public void PlayHitTarget()
{
    var clip = _boomClip != null ? _boomClip : _targetHitClip;
    if (clip == null) return;
    // Tạo temp source với pitch 1.3f, tự hủy
    AudioSource.PlayClipAtPoint(clip, Vector3.zero);
    // Hoặc dùng _oneShotSource chỉ khi không có gì đang play
}
```
Hoặc đơn giản nhất: dùng `_targetHitClip` (đã được generate) thay vì pitch-shift `_boomClip`:
```csharp
public void PlayHitTarget()
{
    var clip = _targetHitClip ?? _boomClip;
    if (clip != null) _oneShotSource.PlayOneShot(clip);
}
```

---

## Medium Priority Improvements

### 3. `GroundScorch.ResetStaticState` — không clear `_allCraters` GameObjects
**File:** `ground-scorch-mark.cs` dòng 30-37

`ResetStaticState` gọi `DestroyMaskVariants()` và `.Clear()` các list, nhưng không `Destroy` các crater GOs đang tồn tại trong scene. Khi domain reload (Enter Play Mode), Unity có thể destroy scene objects, nhưng `SubsystemRegistration` chạy **trước** scene unload nên có thể mồ côi. So sánh với `ClearAll()` (dòng 138-148) làm đúng.

**Fix:** Mirror `ClearAll()` logic trong `ResetStaticState`:
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStaticState()
{
    for (int i = _allCraters.Count - 1; i >= 0; i--)
        if (_allCraters[i] != null) Object.Destroy(_allCraters[i]);
    _allCraters.Clear();
    _craters.Clear();
    DestroyMaskVariants();
    _groundPrepared = false;
}
```

### 4. `GroundScorch.BuildMaskSprite` — thiếu `tex.wrapMode`
**File:** `ground-scorch-mark.cs` dòng 164-192

Texture được tạo nhưng không set `wrapMode = TextureWrapMode.Clamp`. SpriteMask với texture không clamp có thể tạo artifact ở edge khi scale lớn.

```csharp
tex.filterMode = FilterMode.Point;
tex.wrapMode = TextureWrapMode.Clamp; // thêm dòng này
```

### 5. `ExplosionEffect` — không có `RocketTrail` pattern cho pooling
**File:** `explosion-burst-particle-effect.cs` dòng 26-32

Mỗi explosion tạo 1 `new GameObject`. Với auto-play mode, số lượng có thể tích lũy. YAGNI cho desktop, nhưng `Destroy(gameObject, _particleLifetime + 0.2f)` (dòng 52) đã tự cleanup nên không phải memory leak thực sự — chỉ là spike GC allocation. Chấp nhận được.

### 6. `ProceduralAudioClipGenerator.CreateGroundHit` — phase tích lũy không chính xác
**File:** `ProceduralAudioClipGenerator.cs` dòng 31-32

```csharp
float boomFreq = Mathf.Lerp(150f, 25f, Mathf.Sqrt(t));
float boom = Mathf.Sin(2f * Mathf.PI * boomFreq * phase);
```
`phase = i / SampleRate` = thời gian tuyệt đối, nhưng `boomFreq` thay đổi theo `t` — đây là **instantaneous frequency**, không phải **accumulated phase**. Kết quả: pitch sweep nghe không smooth, có thể có discontinuity. Đây là pattern sai về DSP.

**Fix đúng (accumulated phase):**
```csharp
float accumulatedPhase = 0f;
for (int i = 0; i < samples; i++)
{
    float t = (float)i / samples;
    float boomFreq = Mathf.Lerp(150f, 25f, Mathf.Sqrt(t));
    accumulatedPhase += boomFreq / SampleRate;
    float boom = Mathf.Sin(2f * Mathf.PI * accumulatedPhase);
    // ...
}
```
**Impact:** âm thanh hiện tại vẫn hoạt động và nghe được (game không crash), nhưng pitch sweep không tuyến tính như thiết kế. Low risk cho game-feel, high correctness debt.

### 7. `ProceduralAudioClipGenerator.CreateStretch` / `CreateClick` — phase sai tương tự
**File:** `ProceduralAudioClipGenerator.cs` dòng 85-90, 124-128

```csharp
// CreateStretch
float freq = Mathf.Lerp(200f, 500f, t);
data[i] = Mathf.Sin(2f * Mathf.PI * freq * t * duration) * envelope * 0.3f;

// CreateClick
data[i] = Mathf.Sin(2f * Mathf.PI * 1000f * t * duration) * envelope * 0.35f;
```
Cả hai dùng `t * duration` làm phase thay vì `i / SampleRate`. Với clip ngắn (0.05–0.15s), artifact không quá noticeable nhưng vẫn là DSP error. `CreateClick` dùng frequency cố định nên sai ít hơn; `CreateStretch` có pitch sweep nên sai nhiều hơn.

---

## Low Priority Suggestions

### 8. `ImpactEffectsHandler` — thiếu `[RequireComponent(typeof(...))]` hoặc null check cho Rocket
**File:** `impact-effects-handler.cs` dòng 11

`[SerializeField] private Rocket _rocket` — nếu không wire trong inspector, `OnEnable`/`OnDisable` skip silently. Không crash nhưng effects sẽ không chạy và không có warning. Thêm `OnValidate`:
```csharp
private void OnValidate()
{
    if (_rocket == null)
        Debug.LogWarning("[ImpactEffectsHandler] Rocket reference not assigned.", this);
}
```

### 9. `RocketTrail.StopTrail` — dùng `StopEmitting` thay vì `StopEmittingAndClear`
**File:** `rocket-trail-particle-effect.cs` dòng 40

`StopEmitting` để particles hiện tại bay hết — behavior đúng cho trail. `ClearTrail()` dùng `StopEmittingAndClear` cho reset ngay lập tức — cũng đúng. Phân biệt rõ ràng, không cần thay đổi. ✓

### 10. `AudioManager` — `_thrustClip` null không log warning
**File:** `AudioManager.cs` dòng 80-83

`StartThrust()` return silently nếu `_thrustClip == null`. Nên thêm 1 log warning để dev biết khi chưa assign clip. Low priority vì game vẫn chạy bình thường.

---

## Positive Observations

- `RuntimeSpriteFactory` — DRY xuất sắc, loại bỏ duplicate texture creation
- `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` — pattern đúng để reset static state, được dùng nhất quán ở 4 nơi
- `RocketDebris.OnDestroy` remove từ list — tránh stale reference đúng cách
- `AudioManager.OnDestroy` destroy tất cả procedural clips — không leak
- `GroundScorch` dùng struct `CraterData` thay vì 2 parallel lists — tốt hơn code trước
- `ImpactEffectsHandler` decouples Rocket từ visual effects — architecture sạch
- `ExplosionEffect` và `RocketTrail` share `RuntimeSpriteFactory.GetParticleMaterial()` — DRY
- `GroundScorch.BuildMaskSprite` dùng Perlin noise cho jagged edge — creative solution

---

## Recommended Actions (Ưu tiên)

1. **[High]** Fix `RocketDebris.ResetStaticState` — thêm Destroy loop trước Clear
2. **[High]** Fix `AudioManager.PlayHitTarget` — bỏ pitch manipulation, dùng `_targetHitClip` trực tiếp
3. **[Medium]** Fix `GroundScorch.ResetStaticState` — thêm Destroy crater GOs trước Clear
4. **[Medium]** Fix `ProceduralAudioClipGenerator` — dùng accumulated phase cho pitch sweep (ảnh hưởng `CreateGroundHit`, `CreateStretch`)
5. **[Low]** Thêm `tex.wrapMode = Clamp` trong `BuildMaskSprite`
6. **[Low]** Thêm `OnValidate` warning cho `ImpactEffectsHandler._rocket`

---

## Metrics

- Type Coverage: N/A (C# Unity, tất cả types explicit)
- Test Coverage: Effects/Audio chưa có unit tests (chỉ GroundScorch có test file)
- Linting Issues: 0 syntax errors
- Memory Leaks: 0 confirmed leaks, 2 potential orphan scenarios (items 1, 3)
- DSP Correctness: 3 functions có phase accumulation sai (items 6, 7)

---

## Unresolved Questions

- `GroundScorch._groundPrepared` không được reset trong `ClearAll()` (dòng 147 thiếu `_groundPrepared = false`... thực ra dòng 147 có `_groundPrepared = false`) — đã đúng. ✓
- `RocketTrail` không có static list tracking — nếu nhiều rockets cùng lúc (future feature), không có cleanup path. YAGNI hiện tại.
- `ProceduralAudioClipGenerator` không có `[RuntimeInitializeOnLoadMethod]` — đúng vì là pure static factory (không cache gì), AudioManager quản lý lifetime. ✓
