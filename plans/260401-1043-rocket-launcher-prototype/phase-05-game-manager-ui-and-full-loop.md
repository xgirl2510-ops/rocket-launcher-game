# Phase 5: GameManager, UI & Full Game Loop

## Priority: HIGH | Status: Pending | Depends on: Phase 4

## Overview
Tạo `GameManager.cs` — singleton quản lý toàn bộ game state, UI, reset logic. Kết nối tất cả scripts lại. Kết thúc phase: full game loop hoạt động — bắn, WIN/MISS, auto-reset, bắn lại.

## Deliverables
- `Assets/Scripts/Core/GameManager.cs`
- Kết nối UI elements (WinText, MissText, RestartButton)
- Full game loop hoàn chỉnh

## Key Insights
- GameManager là Singleton, khởi tạo trước (Script Execution Order = -100)
- State machine: WaitingForInput → Aiming → Flying → Win/Miss → Resetting
- Miss → hiện "MISS!" 1.5s → camera quay về xe → reset rocket → WaitingForInput
- Win → hiện "YOU WIN!" + nút RESTART → player nhấn RESTART → reset
- Set `Application.targetFrameRate = 60` trong Awake()

## Implementation Steps

### Step 1: Tạo GameManager.cs
File: `Assets/Scripts/Core/GameManager.cs`

**Enum**:
```
GameState { WaitingForInput, Aiming, Flying, Win, Miss, Resetting }
```

**SerializedFields**:
- `_launchController` (LaunchController)
- `_rocket` (Rocket)
- `_cameraController` (CameraController)
- `_winText` (TextMeshProUGUI)
- `_missText` (TextMeshProUGUI)
- `_restartButton` (Button)
- `_missDisplayDuration` (float, 1.5f)

**Singleton**:
- `public static GameManager Instance { get; private set; }`
- Awake(): set Instance, `Application.targetFrameRate = 60`

**Event subscriptions** (trong Start hoặc OnEnable):
- `_rocket.OnRocketLaunched` → state = Flying, disable input
- `_rocket.OnRocketLanded` → HandleRocketLanded()
- `_rocket.OnTargetHit` → HandleTargetHit()

**SetState(GameState)**:
- WaitingForInput: enable input, hide all UI
- Aiming: (LaunchController tự handle)
- Flying: disable input, camera starts following (tự động qua Rocket event)
- Win: hiện winText + restartButton
- Miss: hiện missText, start ResetCoroutine
- Resetting: camera quay về xe

**HandleTargetHit()**:
- SetState(Win)
- `_winText.gameObject.SetActive(true)`
- `_restartButton.gameObject.SetActive(true)`

**HandleRocketLanded()**:
- SetState(Miss)
- `_missText.gameObject.SetActive(true)`
- StartCoroutine(ResetCoroutine())

**ResetCoroutine()**:
- Wait `_missDisplayDuration` seconds (1.5s)
- Hide missText
- Camera.ReturnToVehicle()
- Wait until camera arrives (check state == Idle hoặc wait thêm ~1s)
- ResetRound()

**ResetRound()** (public — cũng được gọi bởi Restart button):
- `_rocket.ResetToPosition(_spawnPoint)` (cần reference hoặc lấy từ LaunchController)
- Hide all UI
- SetState(WaitingForInput)

### Step 2: Gắn GameManager.cs
- Select GameManager GameObject → Add Component → GameManager.cs
- Drag references:
  - `_launchController` → LaunchController
  - `_rocket` → Rocket
  - `_cameraController` → Main Camera
  - `_winText` → WinText (trong Canvas)
  - `_missText` → MissText (trong Canvas)
  - `_restartButton` → RestartButton (trong Canvas)

### Step 3: Setup Restart Button OnClick
- Select RestartButton → Button component → OnClick()
- Nhấn `+` → Drag GameManager object → chọn `GameManager.ResetRound()`

### Step 4: Set Execution Order
**Edit → Project Settings → Script Execution Order**:
- GameManager: **-100** (chạy trước)
- CameraController: **100** (chạy sau)
- Các script khác: 0 (default)

### Step 5: Kết nối LaunchController với GameManager
LaunchController cần thông báo khi player bắt đầu aim:
- Khi touch began (trúng vehicle) → `GameManager.Instance.SetState(GameState.Aiming)`
- Hoặc đơn giản: GameManager subscribe event từ LaunchController

### Step 6: Test full loop

## Verification Checklist

### Test Case 1: MISS Flow
- [ ] Nhấn Play → thấy xe, rocket, ground (camera ở xe)
- [ ] Click/touch vào xe → kéo lùi → thấy mũi tên
- [ ] Thả tay → rocket bay, camera follow
- [ ] Rocket chạm ground → hiện "MISS!" text
- [ ] Sau 1.5s → "MISS!" biến mất
- [ ] Camera smooth quay về xe
- [ ] Rocket reset về vị trí trên nóc xe
- [ ] Có thể bắn lại ngay → lặp lại quy trình

### Test Case 2: WIN Flow
- [ ] Bắn rocket trúng target (hình vuông đỏ ở xa)
- [ ] Hiện "YOU WIN!" text (vàng gold)
- [ ] Hiện nút "RESTART" (xanh lá)
- [ ] Nhấn RESTART → mọi thứ reset, có thể chơi lại
- [ ] Sau RESTART: camera ở xe, rocket ở spawn point, UI ẩn hết

### Test Case 3: Multiple Rounds
- [ ] Bắn miss 3 lần liên tiếp → mỗi lần auto-reset đúng
- [ ] Bắn miss 2 lần → bắn win lần 3 → hiện WIN + RESTART
- [ ] Nhấn RESTART → bắn miss → auto-reset đúng

### Test Case 4: Edge Cases
- [ ] Bắn thẳng lên trời → rocket rơi xuống ground → MISS → auto-reset
- [ ] Bắn rất nhẹ → rocket rơi gần xe → MISS → auto-reset
- [ ] Nhấn RESTART nhiều lần liên tiếp → không crash

### General
- [ ] Không có error đỏ trong Console
- [ ] UI text hiển thị đúng font size, color, position
- [ ] Game chạy mượt, không lag

> Nếu tất cả OK → Phase 5 hoàn thành. Game loop đầy đủ! Qua Phase 6.
