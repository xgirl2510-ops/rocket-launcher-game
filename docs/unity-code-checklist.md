# Unity Code Quality Checklist

> Hoàn thành toàn bộ checklist này trước khi tạo Pull Request.
> Mọi item chưa tick đều phải có lý do rõ ràng trong phần mô tả PR.

---

## 1. Architecture & Project Structure

- [ ] Codebase đang theo đúng 1 architectural pattern đã thống nhất (MVC / MVP / ECS) — không lẫn lộn
- [ ] Thư mục được tổ chức theo feature/domain, không phải theo file type
- [ ] Game Logic, Presentation, Data, Infrastructure được tách biệt rõ ràng — không lẫn vào nhau
- [ ] Không tạo ra circular dependency mới giữa các module
- [ ] File mới được đặt đúng Assembly Definition (.asmdef) tương ứng

---

## 2. SOLID & Design Patterns

- [ ] Mỗi class mới chỉ làm đúng 1 việc — không có class vừa handle input vừa chứa business logic
- [ ] Code depend vào interface hoặc abstract class, không depend vào concrete class
- [ ] Thêm tính năng bằng cách extend, không sửa vào class/method cũ đang hoạt động
- [ ] Các system giao tiếp qua event/observer — không giữ direct reference lẫn nhau khi không cần thiết
- [ ] Không có God Object — không class nào đang ôm quá 3 responsibility khác nhau
- [ ] State machine được dùng cho game states, không dùng chuỗi if-else lồng nhau

---

## 3. ScriptableObject Architecture

- [ ] Mọi data và config đều nằm trong ScriptableObject, không hardcode trong MonoBehaviour
- [ ] Các system giao tiếp qua ScriptableObject event channel, không tìm nhau qua Find hoặc Singleton
- [ ] "Data" (ScriptableObject) và "Behavior" (MonoBehaviour) được tách biệt hoàn toàn

---

## 4. Code Quality & Conventions

- [ ] Đặt tên đúng convention: `PascalCase` cho class/method/property — `camelCase` cho local variable — `_camelCase` cho private field
- [ ] Không có magic number hoặc magic string — tất cả phải là constant hoặc lấy từ config
- [ ] Không có method nào dài quá 30 dòng — nếu dài hơn thì phải tách ra
- [ ] Không có logic rẽ nhánh nào quá 10 nhánh trong 1 method
- [ ] Không có dead code — không để lại code bị comment out hoặc method không được gọi
- [ ] Không có logic bị duplicate — đã extract ra method hoặc utility dùng chung
- [ ] Tất cả public method và public property có XML doc comment
- [ ] Dùng `[SerializeField] private` thay vì `public` — không expose field không cần thiết

---

## 5. Performance & Optimization

- [ ] Không có `new`, LINQ, hoặc string concatenation bên trong `Update()` / `FixedUpdate()` / `LateUpdate()`
- [ ] Object Pool được dùng cho mọi object cần spawn/destroy thường xuyên — không dùng `Instantiate` / `Destroy` trực tiếp trong gameplay loop
- [ ] Tất cả component reference được cache trong `Awake()` hoặc `Start()` — không gọi `GetComponent` trong Update
- [ ] Không dùng `Camera.main` trong Update — phải cache lại
- [ ] Coroutine chỉ dùng cho async wait — không dùng thay thế cho state machine
- [ ] Physics layer và collision matrix chỉ bật đúng cặp cần thiết

---

## 6. Scene & Prefab Management

- [ ] Không có game logic nào bị hardcode vào Scene — Scene chỉ là nơi khai báo composition
- [ ] Prefab mới được tạo bằng Prefab Variant nếu kế thừa từ prefab có sẵn — không duplicate
- [ ] Asset được load qua Addressables đúng cách — không load toàn bộ vào memory
- [ ] Không có `GameObject.Find()`, `FindObjectOfType()`, hoặc Find by name trong runtime code
- [ ] Scene transition đi qua loading manager — không load scene trực tiếp từ gameplay code

---

## 7. Testing

- [ ] Logic thuần C# (không phụ thuộc MonoBehaviour) đã có Unit Test tương ứng
- [ ] Các critical path đã có test: game state transitions, economy logic, save/load
- [ ] Test mới đã pass toàn bộ trước khi tạo PR
- [ ] Không có test nào bị skip hoặc comment out mà không có lý do

---

## 8. Tooling & DevOps

- [ ] Commit message rõ ràng và theo đúng convention của team
- [ ] Không commit thư mục `Library/`, `Temp/`, `Logs/`, hoặc file `.DS_Store`
- [ ] Editor script nằm trong thư mục `Editor/` — không để lẫn vào runtime code
- [ ] Log statement dùng đúng log level (Debug / Info / Warning / Error) và được wrap bằng `#if UNITY_EDITOR` hoặc custom logger — không xuất hiện trong production build
- [ ] Gọi analytics event qua abstraction layer — không gọi SDK trực tiếp từ gameplay code

---

## Trước khi tạo PR

- [ ] Đã tự review toàn bộ checklist trên
- [ ] Đã chạy lại game ít nhất 1 lần end-to-end sau khi sửa
- [ ] Đã chạy toàn bộ test suite và không có test nào fail
- [ ] Mô tả PR ghi rõ: thay đổi gì, tại sao, và cách test lại
- [ ] Các item chưa tick (nếu có) đã được giải thích rõ trong mô tả PR
