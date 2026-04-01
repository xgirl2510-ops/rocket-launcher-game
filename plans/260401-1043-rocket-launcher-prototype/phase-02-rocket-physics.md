# Phase 2: Rocket Physics

## Priority: HIGH | Status: Pending | Depends on: Phase 1

## Overview
Tạo script `Rocket.cs` xử lý physics: bay vòng cung, xoay theo velocity, detect collision với ground/target. Test bằng hardcoded launch trong `Start()` (tạm).

## Deliverables
- `Assets/Scripts/Rocket/Rocket.cs`
- Hardcoded test launch (xóa sau khi xong Phase 3)

## Key Insights
- Rocket bắt đầu Kinematic → chuyển Dynamic khi launch
- Xoay theo velocity: `Atan2(vel.y, vel.x) * Rad2Deg - 90f`
- CircleCollider2D ở đầu nose (offset 0, 0.5)
- Ground dùng `OnCollisionEnter2D`, Target dùng `OnTriggerEnter2D`

## Implementation Steps

### Step 1: Tạo Rocket.cs
File: `Assets/Scripts/Rocket/Rocket.cs`

Script cần có:
- **Events**: `OnRocketLaunched`, `OnRocketLanded`, `OnTargetHit` (C# Action)
- **Launch()**: Chuyển bodyType → Dynamic, AddForce Impulse, fire event
- **ResetToPosition()**: Về spawn point, zero velocity, Kinematic
- **FixedUpdate()**: Nếu đang bay → xoay sprite theo velocity
- **OnCollisionEnter2D()**: Check tag "Ground" → fire OnRocketLanded
- **OnTriggerEnter2D()**: Check tag "Target" → fire OnTargetHit

### Step 2: Gắn script vào Rocket GameObject
- Select Rocket trong Hierarchy → Add Component → Rocket.cs
- Đảm bảo Rigidbody2D vẫn = Kinematic (script sẽ chuyển Dynamic khi launch)

### Step 3: Test bằng hardcoded launch
Thêm tạm vào `Start()` của Rocket.cs:
```
// TEST ONLY - xóa sau Phase 3
Launch(new Vector2(1f, 1.5f).normalized, 12f);
```
→ Nhấn Play, rocket phải bay vòng cung sang phải-lên rồi rơi xuống

### Step 4: Xóa test code
Sau khi verify xong, comment hoặc xóa dòng test trong `Start()`

## Verification Checklist

Nhấn **Play** (với test code trong Start):

- [ ] **Bay vòng cung**: Rocket bay lên rồi rơi xuống theo đường parabolic
- [ ] **Xoay theo hướng bay**: Đầu rocket (nose) luôn hướng về phía trước theo quỹ đạo
- [ ] **Chạm ground dừng**: Rocket chạm dải nâu → dừng lại, không xuyên qua
- [ ] **Console log**: Thấy log "Rocket Landed" (hoặc event fire) khi chạm ground
- [ ] **Không lỗi**: Console không có error đỏ

Test thêm (điều chỉnh hardcoded values):
- [ ] Thử force lớn (20f) → rocket bay xa hơn
- [ ] Thử force nhỏ (5f) → rocket rơi gần
- [ ] Thử hướng (0, 1) → rocket bay thẳng lên rồi rơi xuống

> Nếu tất cả OK → xóa test code → Phase 2 hoàn thành. Qua Phase 3.
