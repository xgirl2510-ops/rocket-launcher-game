# Code Review: Effects & Audio Scripts

**Ngày:** 2026-04-06 | **Điểm tổng:** 8.5/10

## Phạm vi review

Files reviewed: 7 files (~500 dòng)
- `explosion-burst-particle-effect.cs`
- `ground-scorch-mark.cs`
- `rocket-debris-shatter-effect.cs`
- `rocket-trail-particle-effect.cs`
- `impact-effects-handler.cs`
- `AudioManager.cs`
- `ProceduralAudioClipGenerator.cs`
- (tham chiếu) `runtime-sprite-factory.cs`

---

## Đánh giá tổng quan

Code sạch, cấu trúc tốt. `RuntimeSpriteFactory` DRY hiệu quả — tránh tạo duplicate Texture2D/Material. Static cleanup qua `RuntimeInitializeOnLoadMethod` đã được implement ở hầu hết chỗ. Một vài vấn đề nhỏ cần chú ý.

---

## Critical Issues

Không có.

---

## High Priority

**1. `GroundScorch.ClearAll()` không destroy mask variant textures**

`ClearAll()` destroy crater GameObjects nhưng KHÔNG gọi `DestroyMaskVariants()`. Texture2D bên trong `_maskVariants` vẫn còn trong GPU memory sau mỗi round restart. `_maskVariants` chỉ được clean trong `ResetStaticState()` (domain reload), không phải khi restart round thông thường.

```csharp
// ground-scorch-mark.cs, dòng ~139
public static void ClearAll()
{
    // ... destroy craters ...
    _groundPrepared = false;
    // BUG: thiếu DestroyMaskVariants(); ở đây
}
```

Fix: Thêm `DestroyMaskVariants();` vào `ClearAll()`. Mask sẽ được rebuild lần sau khi `EnsureMaskVariants()` được gọi.

**2. `RocketDebris._allDebris` có thể giữ stale refs**

`OnDestroy` gọi `_allDebris.Remove(gameObject)` — đúng. Nhưng nếu debris bị destroy bởi Unity (scene unload) trước khi `ClearAll()` được gọi, các entry null vẫn tồn tại trong list. `ClearAll()` có null-check nên sẽ không crash, nhưng list sẽ phình ra theo thời gian. Minor issue vì game là single-scene.

---

## Medium Priority

**3. `AudioManager.PlayHitTarget()` — pitch không được reset nếu PlayOneShot throw**

```csharp
_oneShotSource.pitch = 1.3f;
if (_boomClip != null) _oneShotSource.PlayOneShot(_boomClip); // nếu throw?
_oneShotSource.pitch = 1.0f; // không chạy nếu có exception
```

Dùng try/finally hoặc đặt pitch về 1.0 trước khi play để an toàn hơn. Trong thực tế Unity hiếm throw ở đây, nhưng là bad pattern.

**4. `ExplosionEffect` — Material từ `RuntimeSpriteFactory` không được cleanup**

`ExplosionEffect` gọi `RuntimeSpriteFactory.GetParticleMaterial()` — material được share. Tốt. Nhưng nếu `ParticleSystemRenderer` giữ reference riêng sau khi `Destroy(gameObject)`, Unity có thể log warning "Destroying assets is not permitted". Thực tế Unity tự handle ref count cho material share, nhưng cần confirm không có `renderer.material = new Material(...)` ẩn nào.

**5. `GroundScorch.BuildMaskSprite()` — `Texture2D` tạo không có TextureFormat explicit**

```csharp
var tex = new Texture2D(size, size); // mặc định RGBA32, tốn bộ nhớ hơn cần
```

Với mask sprite chỉ cần alpha, dùng `new Texture2D(size, size, TextureFormat.Alpha8, false)` tiết kiệm 75% VRAM (64x64x1 thay vì 64x64x4). Minor nhưng có 8 variants = 8 textures.

---

## Low Priority

**6. `ProceduralAudioClipGenerator` — `System.Random` allocate mỗi lần gọi**

`CreateGroundHit()` và `CreateTargetHit()` mỗi lần `new System.Random()`. Vì chỉ gọi một lần trong `Awake()`, không phải issue thực sự. Nhưng nếu gọi nhiều lần trong tương lai thì cân nhắc `static readonly` instance.

**7. `RocketTrail.CreateTrailParticleSystem()` — `TrailParticles` GameObject không cleanup**

Khi Rocket được reset/reuse, `trailGO` child vẫn sống. `Awake()` chỉ chạy một lần nên không tạo duplicate. Không phải bug hiện tại nhưng cần chú ý nếu Rocket được pool/reinstantiate.

---

## Điểm nổi bật

- `RuntimeSpriteFactory` — pattern DRY tốt, tránh N texture/material allocations
- `ResetStaticState()` với `SubsystemRegistration` — đúng lifecycle cho static classes
- `ImpactEffectsHandler` — decouple Rocket khỏi effect systems rất clean
- `RocketDebris` dùng manual gravity thay Rigidbody — tránh fall-through bug
- `AudioManager.OnDestroy()` destroy tất cả procedural clips — đúng
- `GroundScorch` dùng struct `CraterData` thay parallel lists — tốt

---

## Recommended Actions

1. **[HIGH]** `GroundScorch.ClearAll()` — thêm `DestroyMaskVariants()` để giải phóng 8 Texture2D sau mỗi round
2. **[MED]** `AudioManager.PlayHitTarget()` — refactor pitch reset dùng try/finally
3. **[LOW]** `BuildMaskSprite()` — đổi sang `TextureFormat.Alpha8` để giảm VRAM

---

## Metrics

- Memory leaks thực sự: 1 (mask textures không được free khi ClearAll)
- Static state cleanup: 4/5 files có `ResetStaticState()` (thiếu `RocketTrail`, không cần thiết)
- Audio resource cleanup: đầy đủ
- ParticleSystem config: đúng (loop=false, auto-destroy, shared material)

---

## Unresolved Questions

- `GroundScorch._groundPrepared` được reset trong `ClearAll()` nhưng không reset trong `ResetStaticState()` — có thể gây lỗi nếu `SubsystemRegistration` fire trước khi `_groundPrepared` được clear? (Thực tế `SubsystemRegistration` reset toàn bộ state nên không issue, nhưng inconsistent)
- `ImpactEffectsHandler._rocket` assign qua Inspector: nếu scene auto-setup không wire đúng, tất cả effects silent fail — cần verify editor tool đã wire field này
