# Code Review: Core Game Flow Scripts
**Date:** 2026-04-06 | **Score: 9.0 / 10**

## Scope
- Files: `RoundManager.cs`, `round-manager-auto-play-restart-and-target.cs`, `round-manager-hud.cs`, `GameRoundTracker.cs`, `GameConstants.cs`, `runtime-sprite-factory.cs`
- Focus: null safety, event subscribe/unsubscribe, singleton, state management, race conditions

---

## Overall Assessment
Code sạch, kiến trúc tốt. Partial class tách hợp lý. Null guard đầy đủ ở hầu hết chỗ. Một số vấn đề nhỏ về race condition và edge case đáng chú ý.

---

## Critical Issues
_Không có._

---

## High Priority

**1. Race condition: double-fire khi rocket hit sau khi `StopAllCoroutines` (RoundManager)**
- `HandleTargetHit` / `HandleRocketMiss` vẫn có thể bị gọi sau khi `HandleRestart()` hoặc `HandleAutoPlay()` đã gọi `StopAllCoroutines()` — nếu `DelayedAction` coroutine đang chạy VÀ Rocket event còn pending trong cùng frame.
- Rocket event không bị unsubscribe khi restart, chỉ coroutine bị stop.
- **Fix đề xuất:** Thêm `bool _isHandlingRound` hoặc check `_isAutoPlaying` + `_rocket.IsFlying` trước khi xử lý event miss/hit.

**2. `_launchController` không được null-check ở `Start()` khi `_cameraController != null` (RoundManager.cs:56-68)**
```csharp
// Nếu _cameraController != null nhưng _launchController == null
// → OnIntroDone() sẽ gọi _launchController.EnableInput() → NullReferenceException
```
- **Fix:** Thêm null-check `if (_launchController != null)` trong `OnIntroDone()` và `OnLookTargetDone()`.

---

## Medium Priority

**3. Event `OnLookTargetComplete` unsubscribe trong `OnDestroy` nhưng không subscribe trong `Start`**
- `RoundManager.Start()` không subscribe `OnLookTargetComplete`, chỉ subscribe trong `HandleLookTarget()`.
- `OnDestroy` unsubscribe cả hai — an toàn nhưng hơi misleading.
- Không phải bug vì unsubscribe delegate null là no-op trong C#, nhưng nên comment rõ.

**4. `HandleRestart()` unsubscribe rồi re-subscribe `OnIntroComplete` mỗi lần (auto-play.cs:39-40)**
```csharp
_cameraController.OnIntroComplete -= OnIntroDone;
_cameraController.OnIntroComplete += OnIntroDone;
```
- Đây là pattern phòng ngừa double-subscribe — tốt. Nhưng nên comment lý do để tránh ai đó xóa dòng `-=` nhầm tưởng là thừa.

**5. `GameRoundTracker.TryUpdateBest` không được gọi khi auto-play hit**
- `HandleTargetHit()` gọi `TryUpdateBest` chỉ khi `!_isAutoPlaying` — đúng về game design.
- Nhưng `ReloadAfterAutoPlay()` gọi `NewRound()` mà không reset `_missCount` → `_missCount` tích lũy qua các auto-play rounds cho đến khi player restart thủ công.
- Xem xét reset `_missCount = 0` trong `ReloadAfterAutoPlay()`. *(Đã có ở dòng 128 — OK, bỏ qua.)*

**6. `RuntimeSpriteFactory.GetSolidSprite()` — `Color[16]` allocation mỗi lần tạo**
- Nhỏ, chỉ chạy một lần nhờ cache. Không phải vấn đề thực tế.

---

## Low Priority

**7. `GameConstants` thiếu `Layer` constant cho Rocket (layer 8)**
- Layer 8 ("Rocket") được dùng ở nhiều nơi nhưng không có trong `GameConstants`.
- **Fix:** `public const int LayerRocket = 8;`

**8. `RoundManagerHUD.Start()` không có null-check `_roundManager`**
- `UpdateStatsUI(_roundManager.RoundTracker)` gọi trực tiếp khi `_roundManager != null` — đã check. OK.

**9. `RuntimeSpriteFactory` — fallback `UI/Default` shader có thể gây lỗi render trên particle systems**
- Shader `UI/Default` không phù hợp cho ParticleSystem. Nên log error rõ hơn và không tạo material nếu shader not found.

---

## Positive Observations
- `ResetStaticState()` bằng `RuntimeInitializeOnLoadMethod` — xử lý Enter Play Mode tốt
- `DisallowMultipleComponent` trên HUD — phòng ngừa duplicate singleton
- Partial class tách `RoundManager` hợp lý, không bloat một file
- Event unsubscribe trong `OnDestroy` đầy đủ với null-check
- `DelayedAction` coroutine DRY, tái dụng tốt
- `GameRoundTracker` là plain C# class, không phụ thuộc Unity — testable

---

## Recommended Actions
1. **[High]** Thêm guard trong `HandleTargetHit`/`HandleRocketMiss` để bỏ qua nếu round đã được reset (flag `_isHandlingRound` hoặc check object active state)
2. **[High]** Null-check `_launchController` trong `OnIntroDone()` và `OnLookTargetDone()`
3. **[Low]** Thêm `LayerRocket = 8` vào `GameConstants`
4. **[Low]** Comment lý do pattern `-=` rồi `+=` trong `HandleRestart`

---

## Metrics
- Files reviewed: 6 | LOC: ~350
- Critical: 0 | High: 2 | Medium: 3 | Low: 3
- Null safety: 9/10 | Event balance: 9/10 | Singleton: 10/10 | State management: 8.5/10

---

## Unresolved Questions
- `HandleAutoPlay()` gọi `_rocket.Launch()` trực tiếp, bypass `LaunchController` — có thể bỏ qua `OnShotFired()` → `_roundShots` không tăng khi auto-play. Đây có phải behavior mong muốn không?
