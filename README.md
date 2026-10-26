# Ultrawide Zone & Smart Borderless Manager (Windows Splitter)

Ứng dụng Desktop gọn nhẹ trên Windows được xây dựng bằng **C# (.NET 8 WPF)** kết hợp giao diện **Fluent Design (WPF-UI)**, thay thế hoàn hảo cho **PowerToys FancyZones** và **Borderless Gaming**, được tối ưu hóa đặc biệt cho màn hình **Ultrawide 21:9 (3440 x 1440)** và hệ thống **đa màn hình (Multi-Monitor Extend & Duplicate)**.

---

## 🌟 Tính Năng Nổi Bật

### 1. 🖥️ Tối Ưu Hóa Đa Màn Hình & Giao Thức Hiển Thị (Multi-Monitor Engine)
* **Bắt sự kiện chuyển đổi giao thức nóng (`WM_DISPLAYCHANGE`):** Tự động phát hiện khi bấm `Win + P` (chuyển giữa *Extend*, *Duplicate*, *Second Screen Only*) hoặc cắm/rút dây màn hình để cập nhật tức thì sơ đồ vùng.
* **Nhận diện chế độ Duplicate (Nhân bản):** Tự động phát hiện khi màn hình bị clone/nhân bản để đồng bộ hóa kích thước và hiển thị huy hiệu `Duplicate Mode Active`.
* **Cấu hình Preset độc lập cho từng Màn hình (Per-Monitor Presets):**
  * Mỗi màn hình kết nối có danh sách Preset và vùng Zone riêng biệt phù hợp với tỷ lệ khung hình:
    * *Màn hình Ultrawide 21:9:* Preset 1/3 - 2/3 Chuẩn 2K (880px / 2560px), Trung tâm 2K + 2 Cánh.
    * *Màn hình tiêu chuẩn 16:9:* Preset 50%/50%, 1/3 - 2/3, 3 Cột.
    * *Màn hình dọc (Vertical/Pivot):* Preset chia tầng Trên / Dưới (50%/50%, 33%/33%/33%).
* **Phím tắt chuyển màn hình:**
  * `Win + Shift + Left (←) / Right (→)`: Đưa cửa sổ active nhảy trực tiếp sang màn hình liền kề bên trái / phải.
  * `Win + Left (←) / Right (→)`: Tự động cuộn vòng qua các màn hình liền kề (Cross-Monitor Wrap-around) khi đi hết mép màn hình hiện tại.
* **Chuẩn hóa Per-Monitor DPI Scaling:** Tự động quy đổi tọa độ Pixel vật lý và tỷ lệ phóng đại DPI (100%, 125%, 150%) cho cụm nút nổi và khung viền hiển thị, không bị lệch vị trí khi kéo qua các màn hình khác DPI.

---

### 2. ⊞ Quản lý Chia Vùng (Zone Manager - Thay thế FancyZones)
* **Điều khiển nhanh bằng phím tắt `Win + Arrow`:**
  * `Win + Left (←)`: Đưa cửa sổ active sang Vùng 1 (hoặc Vùng bên trái).
  * `Win + Right (→)`: Đưa cửa sổ active sang Vùng 2 (hoặc Vùng bên phải).
  * `Win + Up (↑)`: Phóng to toàn bộ màn hình / Khôi phục.
  * `Win + Down (↓)`: Thu nhỏ (Minimize) cửa sổ xuống Taskbar.
* **Tối ưu hóa hiển thị Windows 11:**
  * **Bù trừ viền tàng hình DWM (DWM Invisible Border Compensation):** Bù trừ drop-shadow 7px của Windows để mép các cửa sổ ghép khít sát không hở khe.
  * **Tắt góc bo tròn Windows 11 (`DWMWA_WINDOW_CORNER_PREFERENCE`):** Giúp cửa sổ bám vuông vức vào vùng chia.
  * **Khung viền mờ (`ZoneOverlayWindow`):** Hiển thị trực quan vị trí và kích thước vùng khi chuyển cửa sổ.

---

### 3. 🗖 Quản lý Không Viền (Smart Borderless Manager - Thay thế Borderless Gaming)
* **Danh sách Cửa sổ đang mở (Active Processes List):** Quét các cửa sổ đang hoạt động kèm Icon, Tên tiến trình (`.exe`), Tiêu đề cửa sổ. Nút chuyển nhanh **"Bỏ viền (Borderless)"** và **"+ Whitelist"**.
* **Danh sách Chỉ định Không Viền (Borderless Whitelist):**
  * **Tự động áp dụng (Auto-apply):** Tự động phát hiện khi game khởi chạy để xóa viền và ép vào Zone.
  * **Chỉ định Màn hình mục tiêu (Target Monitor):** Chỉ định cụ thể màn hình muốn mở game (ví dụ: luôn mở trên màn Ultrawide thay vì mở nhầm sang màn phụ).
  * **Đè lên Taskbar (Cover Taskbar):** Chiều cao đạt tối đa để tràn đè qua thanh Taskbar ở vùng game.
  * **Gắn cố định vào Zone:** Chọn Zone 1, Zone 2 hoặc Toàn màn hình.

---

### 4. 🛸 Cụm Nút Điều Khiển Nổi Thông Minh (Floating Mini-Control Bar)
* **Cửa sổ Overlay nổi siêu nhỏ (114px × 36px):** Đặt ở góc trên cùng bên phải cửa sổ borderless.
* **Không cướp Focus (`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`):** Bấm nút hay di chuột hoàn toàn không làm giật lag hay mất focus của Game.
* **Cơ chế Tự ẩn/hiện thông minh (Auto-Hide Hover):**
  * Khi đang chơi game: Thanh nút mờ tàng hình (`Opacity = 0.15`, trượt lên ẩn).
  * Khi di chuột lên sát mép đỉnh trên góc phải hoặc **giữ phím `Alt`**: Thanh nút trượt nhẹ xuống và hiện rõ 100%.
* **3 nút điều khiển:**
  * `—` (Minimize): Thu nhỏ game xuống Taskbar qua `ShowWindow(SW_MINIMIZE)`.
  * `▢` (Toggle/Restore): Chuyển đổi nhanh giữa Borderless và viền gốc.
  * `✕` (Close): Đóng game/ứng dụng an toàn qua `WM_CLOSE`.

---

### 5. ⚙️ Chạy Ngầm & Khay Hệ Thống (System Tray)
* Ẩn xuống System Tray khi bấm nút thu nhỏ hoặc đóng cửa sổ (`X`).
* Menu chuột phải tại khay hỗ trợ bật/tắt phím tắt, snap nhanh cửa sổ sang Vùng 1/2, chuyển màn hình và thoát hoàn toàn ứng dụng.

---

## 🛠️ Kiến Trúc Mã Nguồn

```text
c:\PersonalProject\WindowsSplitter\
├── Core/
│   ├── NativeMethods.cs         # P/Invoke Win32 API (user32, dwmapi, kernel32, shcore)
│   ├── MonitorManager.cs        # Quản lý đa màn hình, DPI Scaling, phát hiện Duplicate/Extend
│   ├── LowLevelKeyboardHook.cs  # Bắt phím tắt Win + Arrow và Win + Shift + Arrow
│   ├── WinEventHookService.cs   # Bắt sự kiện Focus / Khởi chạy app để Auto-apply
│   ├── ZoneEngine.cs            # Thuật toán chia vùng, bù trừ viền DWM 7px, chuyển màn hình
│   ├── BorderlessEngine.cs      # Xóa WS_CAPTION, quản lý viền, gắn Floating Caption Bar
│   └── WindowEnumerator.cs      # Quét danh sách cửa sổ đang chạy & trích xuất Icon
├── Models/
│   ├── ZoneLayout.cs            # Zone, ZonePreset, MonitorLayoutConfig
│   ├── BorderlessAppRule.cs     # Quy tắc cấu hình Borderless (hỗ trợ TargetMonitorDevice)
│   ├── AppSettings.cs           # Lưu trữ appsettings.json (%AppData%\WindowsSplitter)
│   └── RunningWindowItem.cs     # Thông tin tiến trình & cửa sổ đang hoạt động
├── UI/
│   ├── MainWindow.xaml(.cs)     # Giao diện chính: Bộ chọn màn hình, Presets, Whitelist
│   ├── ZoneOverlayWindow.xaml   # Khung viền mờ hiển thị hiệu ứng theo DPI chuẩn xác
│   ├── FloatingCaptionBar.xaml  # Cụm nút [- [] X] nổi tự ẩn/hiện định vị pixel vật lý
│   └── AddAppDialog.xaml        # Hộp thoại thêm ứng dụng (chọn Màn hình & Zone mục tiêu)
├── app.manifest                 # Quyền Administrator (Bắt buộc cho Game & System Hook)
├── app.ico                      # Icon ứng dụng chuẩn Fluent Ultrawide
└── WindowsSplitter.csproj       # .NET 8 WPF, WPF-UI 4.3.0, Hardcodet.NotifyIcon.Wpf
```
