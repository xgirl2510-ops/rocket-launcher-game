# Phase 6: Polish, Tuning & Mobile Testing

## Priority: MEDIUM | Status: Pending | Depends on: Phase 5

## Overview
Tinh chỉnh physics values, camera feel, input responsiveness. Test trên mobile device thật. Kết thúc phase: game chơi "feel good" trên cả Editor lẫn mobile.

## Deliverables
- Tuned physics/camera values
- Mobile-ready input
- Playable prototype hoàn chỉnh

## Tuning Areas

### 1. Physics Tuning
Chỉnh trong Inspector (không cần sửa code):

| Parameter | Location | Điều chỉnh |
|---|---|---|
| Min Launch Force | LaunchController | Tăng/giảm nếu bắn yếu nhất quá ngắn/xa |
| Max Launch Force | LaunchController | Tăng nếu không bắn tới target, giảm nếu bay quá xa |
| Max Drag Distance | LaunchController | Tăng nếu cảm giác kéo quá ngắn |
| Min Drag Distance | LaunchController | Giảm nếu khó trigger launch |
| Gravity Scale | Rocket Rigidbody2D | Tăng = rơi nhanh hơn, giảm = bay xa hơn |

**Mục tiêu**: Bắn max force phải tới được target. Bắn min force rơi gần xe. Cảm giác kéo thả tự nhiên.

### 2. Camera Tuning

| Parameter | Location | Điều chỉnh |
|---|---|---|
| Follow Smooth Time | CameraController | Giảm = camera bám sát hơn, tăng = mượt hơn nhưng delay |
| Return Smooth Time | CameraController | Tốc độ camera quay về xe |
| Follow Offset Y | CameraController | Tăng = nhìn xa hơn phía trước rocket |
| Camera Size | Main Camera | Tăng = zoom out thấy rộng hơn |

**Mục tiêu**: Camera follow không quá bám sát (gây say), không quá lag (mất rocket). Quay về xe mượt mà.

### 3. Target Position
- Nếu quá khó bắn trúng → di chuyển Target gần hơn hoặc tăng size
- Nếu quá dễ → di chuyển xa hơn hoặc giảm size
- Giá trị gợi ý: Position X = 15-20, Size = 1.5x1.5

### 4. Vehicle Touch Area
- Nếu khó touch vào xe trên mobile → tăng BoxCollider2D size trên LauncherVehicle
- Gợi ý: Size (4, 3) thay vì (3, 2)

### 5. Mobile Input Testing

#### Build & Run trên device:
1. **Android**: File → Build Settings → Android → Build and Run
2. **iOS**: File → Build Settings → iOS → Build → mở Xcode → Run

#### Hoặc test touch trong Editor:
- Unity Remote 5 (Google Play / App Store) — connect device, test touch trực tiếp

#### Mobile-specific checks:
- Touch responsiveness: kéo mượt, không delay
- Touch area: đủ lớn để ngón tay chạm chính xác
- Portrait orientation: game hiển thị đúng
- Performance: 60fps stable

### 6. Miss Display Duration
- Nếu 1.5s quá nhanh/chậm cho MISS text → chỉnh `_missDisplayDuration` trong GameManager Inspector

## Implementation Steps

### Step 1: Playtest trong Editor
- Chơi 10-15 lần liên tiếp
- Ghi lại những gì "feel off"
- Chỉnh values trong Inspector (KHÔNG stop Play mode — chỉnh realtime)
- **Lưu ý**: Values chỉnh trong Play mode sẽ mất khi stop. Copy values ra notepad trước khi stop, rồi paste lại.

### Step 2: Chỉnh Target difficulty
- Bắn 5 lần → nếu trúng < 1 lần = quá khó → gần hơn/to hơn
- Bắn 5 lần → nếu trúng > 3 lần = quá dễ → xa hơn/nhỏ hơn
- Sweet spot: ~30-40% hit rate

### Step 3: Test mobile (nếu có device)
- Build → test touch
- Chỉnh touch area nếu cần

### Step 4: Final pass
- Chạy game từ đầu đến cuối 5 lần
- Đảm bảo không có bug, crash, hoặc visual glitch

## Verification Checklist

### Feel & Gameplay
- [ ] **Kéo thả tự nhiên**: Cảm giác slingshot, không cần tutorial
- [ ] **Force phù hợp**: Min force rơi gần, max force tới target
- [ ] **Camera mượt**: Không giật, không lag quá nhiều
- [ ] **MISS reset nhanh**: Không phải chờ lâu để bắn lại
- [ ] **WIN rõ ràng**: Text + button hiện rõ, dễ nhấn
- [ ] **Difficulty hợp lý**: Không quá khó, không quá dễ

### Technical
- [ ] **60 FPS stable**: Không drop frame
- [ ] **No console errors**: Sạch log
- [ ] **Portrait mode**: Game view đúng 9:16
- [ ] **10 rounds liên tiếp**: Không crash, không bug state

### Mobile (nếu test được)
- [ ] **Touch hoạt động**: Kéo thả đúng
- [ ] **Touch area đủ lớn**: Dễ chạm vào xe
- [ ] **UI đọc được**: Text không quá nhỏ trên màn hình thật
- [ ] **Performance OK**: Mượt, không lag

> Nếu tất cả OK → Phase 6 hoàn thành. Prototype hoàn chỉnh!
>
> **Next**: Thay placeholder shapes bằng real assets (post-prototype phase).
