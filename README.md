# Windows Splitter (Ultrawide Zone & Smart Borderless Manager)

Ứng dụng desktop gọn nhẹ trên Windows xây dựng bằng **C# (.NET 8 WPF)** và **WPF-UI**, hỗ trợ chia vùng cửa sổ thông minh (thay thế PowerToys FancyZones) kết hợp quản lý chế độ cửa sổ không viền (Borderless) cho game và ứng dụng. Được tối ưu hóa cho màn hình **Ultrawide (21:9, 32:9)**, màn hình tiêu chuẩn **(16:9, 16:10)**, màn hình dọc và hệ thống **đa màn hình (Extend & Duplicate)**.

---

## Tính năng chính

### 1. Quản lý Chia Vùng (Zone Manager)
* **Điều hướng 2D thông minh (`Win + Arrow`):** Di chuyển cửa sổ qua lại giữa các vùng theo hướng hình học (Trái / Phải / Lên / Xuống). Khi ở vùng trên cùng không còn vùng phía trên thì `Win + Up` mới phóng to toàn màn hình (`Win + Down` khôi phục lại vùng cũ); khi ở vùng dưới cùng không còn vùng phía dưới thì `Win + Down` mới thu nhỏ xuống Taskbar.
* **Trình chỉnh sửa chia vùng đệ quy không chồng chéo (Recursive Split Editor):**
  * Cho phép tách bất kỳ vùng nào theo chiều dọc hoặc chiều ngang để tạo nhiều vùng trên một màn hình mà **không bao giờ chồng chéo**.
  * Hỗ trợ kéo thả trực tiếp các đường phân chia trên sơ đồ 2D, chỉnh khoảng hở giữa các vùng (`Zone Gap px`), và chuyển đổi nhập thông số theo **Pixel (`X, Y, W, H`)** hoặc **Phần trăm (`%`)**.
  * Tự động đánh số `Zone 1..N` từ trên xuống dưới, trái sang phải.
* **Tự động sinh Preset theo tỷ lệ màn hình:**
  * **Màn hình Ultrawide (`Width / Height >= 2.0`):** Preset 1/3 - 2/3 chuẩn 2K, Chia đôi 50% / 50%, Chia ba cột, Trung tâm + 2 Cánh, Priority Grid (4 vùng), Lưới 2x2 (4 vùng), và Bố cục 5 Vùng lệch tâm.
  * **Màn hình tiêu chuẩn (16:9, 16:10):** Preset 1/3 - 2/3, Chia đôi 50% / 50%, 2/3 - 1/3, Chia ba cột, Priority Grid (3 vùng), Lưới 2x2, và Bố cục 5 Vùng lệch tâm.
  * **Màn hình dọc (`Height > Width`):** Preset chia đôi Trên - Dưới (50% / 50%), chia ba tầng (33% / 33% / 33%), và Lưới 2x2.
* **Custom Profile:** Lưu bố cục vừa thiết kế thành Profile riêng (`Save as New Profile`), cập nhật hoặc xóa Profile tùy chỉnh. Các Preset mặc định của hệ thống được khóa để tránh ghi đè ngoài ý muốn.
* **Tối ưu hiển thị Windows 10 / 11:**
  * **Bù trừ viền tàng hình DWM 2 lượt:** Đo `DWMWA_EXTENDED_FRAME_BOUNDS` trước và sau `SetWindowPos` (mặc định fallback 7px) để cửa sổ bám khít cả 4 cạnh (không hở mép trên).
  * **Chống bung toàn màn hình khi bấm nhầm mép trên (`TopEdgeSnapGuard`):** Ngăn Windows Snap tự phóng to cửa sổ khi vô tình bấm vào dải viền resize phía trên của cửa sổ đang nằm trong Zone sát đỉnh màn hình.
  * **Tắt bo góc Windows 11:** Tùy chọn đặt `DWMWA_WINDOW_CORNER_PREFERENCE` thành `DWMWCP_DONOTROUND` khi cửa sổ nằm trong Zone.
  * **Khung viền mờ trực quan (`ZoneOverlayWindow`):** Hiển thị vùng đích và kích thước pixel khi snap cửa sổ.

### 2. Đa Màn Hình & DPI Scaling (Multi-Monitor Engine)
* **Cấu hình độc lập cho từng màn hình:** Mỗi màn hình vật lý (`MonitorConfigs` theo `DeviceName`) lưu trữ danh sách Preset và Profile riêng.
* **Phát hiện thay đổi hiển thị theo thời gian thực (`WM_DISPLAYCHANGE`):** Tự động cập nhật danh sách màn hình khi nhấn `Win + P` hoặc cắm/rút màn hình.
* **Nhận diện chế độ Duplicate:** Tự phát hiện khi các màn hình nhân bản cùng tọa độ/kích thước và hiển thị trạng thái Duplicate Mode.
* **Di chuyển cửa sổ giữa các màn hình:** Hỗ trợ `Win + Shift + Left / Right` để chuyển thẳng sang màn hình liền kề, hoặc bật `Cross-Monitor Wrap` để cuộn vòng khi nhấn `Win + Left / Right` tới mép màn hình.
* **Hỗ trợ Per-Monitor DPI (`PerMonitorV2`):** Tự động quy đổi tọa độ pixel vật lý và tỷ lệ DPI khi định vị khung overlay và thanh điều khiển nổi.

### 3. Quản lý Không Viền Thông Minh (Smart Borderless)
* **Danh sách cửa sổ đang hoạt động:** Quét các cửa sổ hiển thị kèm biểu tượng ứng dụng, hỗ trợ tìm kiếm nhanh theo tiêu đề hoặc tên tiến trình và bật/tắt Borderless chỉ với một lần bấm.
* **Phím tắt nhanh `Win + Ctrl + B`:** Bật/tắt chế độ Borderless cho cửa sổ đang focus kèm thông báo mờ (`BorderlessNoticeWindow`).
* **Gỡ bỏ viền & giữ trên Taskbar:** Xóa `WS_CAPTION`, `WS_THICKFRAME` và các khung viền chuẩn, căn chỉnh theo `DWMWA_EXTENDED_FRAME_BOUNDS`, giữ cửa sổ theo Zone đang snap (hoặc phủ toàn bộ màn hình nếu chưa snap) và duy trì vị trí trên Taskbar thông qua `WinEventHookService` (`EVENT_SYSTEM_FOREGROUND`).
* **Thanh điều khiển nổi thu gọn (`FloatingCaptionBar`):** Cửa sổ nổi `114 x 36` (quy đổi theo DPI) gắn ở góc trên bên phải cửa sổ Borderless, không cướp focus (`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`). Mặc định mờ và trượt ẩn (`Opacity = 0.15`), tự trượt hiện rõ khi rê chuột lên góc trên phải hoặc giữ phím `Alt`, cung cấp 3 nút: Thu nhỏ (`—`), Khôi phục/Chuyển viền (`▢`) và Đóng (`✕`).
* **Khôi phục an toàn:** Tự động khôi phục style và kích thước gốc cho tất cả cửa sổ đang ở chế độ Borderless khi thoát ứng dụng.

### 4. Hệ thống, Khay Hệ Thống & Đa Ngôn Ngữ
* **Chạy đơn phiên bản (Single Instance):** Sử dụng `Mutex` và thông điệp `WM_SHOW_SPLITTER` để gọi lại cửa sổ đang chạy nếu mở ứng dụng lần thứ hai.
* **Khay hệ thống (System Tray):** Hỗ trợ thu nhỏ xuống khay hệ thống, tùy chọn `Start minimized to System Tray` và `Minimize to System Tray when closing (X)`.
* **Khởi động cùng Windows với quyền Administrator:** Đăng ký tác vụ Task Scheduler (`WindowsSplitter_Elevated`, `/RL HIGHEST /IT`, hỗ trợ `--minimized`) để khởi động không bị kẹt UAC, kèm phương án dự phòng qua Registry `Run`.
* **Đa ngôn ngữ động:** Hỗ trợ chuyển đổi tức thì giữa **English** (mặc định) và **Tiếng Việt**.

---

## Phím tắt mặc định

| Phím tắt | Chức năng |
| :--- | :--- |
| `Win + Left / Right / Up / Down` | Điều hướng 2D giữa các vùng; khi chạm mép trên thì `Win + Up` phóng to (`Win + Down` trả về vùng cũ), chạm mép dưới thì `Win + Down` thu nhỏ |
| `Win + Shift + Left / Right` | Chuyển cửa sổ sang màn hình liền kề bên trái / phải |
| `Win + Ctrl + B` | Bật / tắt chế độ Borderless cho cửa sổ đang hoạt động |

---

## Yêu cầu hệ thống

* **Hệ điều hành:** Windows 10 hoặc Windows 11 (64-bit).
* **Quyền thực thi:** Yêu cầu quyền **Administrator** (`requireAdministrator` trong `app.manifest`) để global keyboard hook (`WH_KEYBOARD_LL`) và các thao tác thay đổi style cửa sổ hoạt động với mọi ứng dụng/game chạy ở quyền cao.
* **Môi trường phát triển (khi build từ mã nguồn):** .NET 8 SDK (`net8.0-windows`), Inno Setup 6 (nếu đóng gói file cài đặt).

---

## Tải về & Cài đặt (GitHub Releases)

Các bản phát hành chính thức được đăng tải trực tiếp tại trang **GitHub Releases**:
* `https://github.com/Thanhloi-lab/Window_Splitter/releases`
* Mỗi phiên bản bao gồm bộ cài đặt (`WindowsSplitter-Setup-<version>-x64.exe`), bản portable (`WindowsSplitter-Portable-<version>-win-x64.zip`), và file kiểm tra toàn vẹn `SHA256SUMS.txt`.

### Lưu ý khi chạy bản phát hành chưa ký số (Unsigned Release)
* Bản dựng cộng đồng không gắn chứng chỉ Code Signing thương mại, nên Windows Defender SmartScreen có thể hiển thị cảnh báo *"Windows protected your PC"* ở lần chạy đầu tiên.
* Để kiểm tra an toàn trước khi chạy:
  1. Đối chiếu mã băm SHA-256 của file tải về với `SHA256SUMS.txt` bằng PowerShell:
     ```powershell
     Get-FileHash .\WindowsSplitter-Setup-1.0.0-x64.exe -Algorithm SHA256
     ```
  2. Hoặc tự build trực tiếp từ mã nguồn theo hướng dẫn bên dưới.
  3. Tại màn hình SmartScreen, chọn **More info** -> **Run anyway** để tiếp tục cài đặt.

---

## Build, Chạy & Đóng gói

### Build và chạy từ mã nguồn
```powershell
dotnet build WindowsSplitter.csproj -c Release
dotnet run --project WindowsSplitter.csproj
```

### Đóng gói bộ cài đặt (Installer x64)
Chạy script đóng gói (tự động publish self-contained `win-x64`, biên dịch bằng Inno Setup 6 và tạo file `.sha256`):
```powershell
installer\build-installer.bat
```
File cài đặt sau khi đóng gói được xuất ra tại `installer\output\WindowsSplitter-Setup-1.0.0-x64.exe`. Xem thêm chi tiết tại [installer/README.md](installer/README.md).
Khi đẩy tag phiên bản mới dạng `v*` (ví dụ `git tag v1.1.0 && git push origin v1.1.0`), GitHub Actions ([.github/workflows/release.yml](.github/workflows/release.yml)) sẽ tự động build và tạo bản phát hành trên GitHub Releases.

---

## Lưu trữ cấu hình

Toàn bộ cấu hình người dùng, danh sách Preset mặc định/tùy chỉnh và cấu hình theo từng màn hình được lưu tại:
```text
%AppData%\WindowsSplitter\appsettings.json
```

---

## Cấu trúc mã nguồn

```text
Window_Splitter/
├── Core/
│   ├── NativeMethods.cs            # Khai báo P/Invoke Win32 API (user32, dwmapi, shcore, kernel32)
│   ├── MonitorManager.cs           # Quản lý đa màn hình, DPI scaling, phát hiện chế độ Duplicate
│   ├── LowLevelKeyboardHook.cs     # Global hook WH_KEYBOARD_LL bắt phím tắt hệ thống
│   ├── TopEdgeSnapGuard.cs         # Hook WH_MOUSE_LL chặn click nhầm HTTOP mép trên gây bung full window
│   ├── WinEventHookService.cs      # Hook EVENT_SYSTEM_FOREGROUND duy trì cửa sổ borderless trên Taskbar
│   ├── ZoneEngine.cs               # Chia vùng 2D, điều hướng lân cận, bù trừ DWM 2 lượt và bo góc Win11
│   ├── BorderlessEngine.cs         # Gỡ/khôi phục viền cửa sổ, quản lý WindowStateBackup & FloatingCaptionBar
│   ├── WindowEnumerator.cs         # Quét danh sách cửa sổ hiển thị & trích xuất icon tiến trình
│   ├── LocalizationManager.cs      # Chuyển đổi ResourceDictionary ngôn ngữ (en / vi)
│   └── StartupManager.cs           # Quản lý tự khởi động qua Task Scheduler & Registry
├── Models/
│   ├── AppSettings.cs              # Mô hình cấu hình & đọc/ghi appsettings.json
│   ├── ZoneLayout.cs               # Zone, ZonePreset (Preset mặc định & Custom Profile), MonitorLayoutConfig
│   └── RunningWindowItem.cs        # ViewModel cho từng cửa sổ trong danh sách Smart Borderless
├── UI/
│   ├── MainWindow.xaml(.cs)        # Cửa sổ chính (3 tab: Zone Manager, Smart Borderless, Settings) & System Tray
│   ├── SaveProfileDialog.xaml(.cs) # Hộp thoại lưu Custom Profile mới
│   ├── ZoneOverlayWindow.xaml(.cs) # Overlay hiển thị vùng đích khi snap cửa sổ
│   ├── FloatingCaptionBar.xaml(.cs)# Thanh điều khiển nổi (Thu nhỏ / Chuyển viền / Đóng) tự ẩn/hiện
│   └── BorderlessNoticeWindow.xaml(.cs) # Thông báo nổi ngắn khi kích hoạt chế độ Borderless
├── Resources/
│   └── Languages/
│       ├── Strings.en.xaml         # Chuỗi giao diện tiếng Anh
│       └── Strings.vi.xaml         # Chuỗi giao diện tiếng Việt
├── installer/
│   ├── build-installer.bat         # Script publish win-x64 và build Inno Setup
│   ├── WindowsSplitter.iss         # Định nghĩa trình cài đặt Inno Setup
│   ├── License.txt                 # Bản quyền MIT hiển thị trong trình cài đặt
│   └── README.md                   # Hướng dẫn đóng gói bộ cài
├── .github/
│   └── workflows/
│       └── release.yml             # Workflow tự động đóng gói & phát hành lên GitHub Releases
├── App.xaml(.cs)                   # Khởi tạo Single-Instance Mutex, ngôn ngữ và Dark Theme
├── app.manifest                    # Yêu cầu quyền Administrator và PerMonitorV2 DPI awareness
├── WindowsSplitter.csproj          # Cấu hình project .NET 8 WPF, WPF-UI 4.3.0, Hardcodet.NotifyIcon.Wpf 2.0.1
├── LICENSE                         # Giấy phép mã nguồn mở MIT License
├── THIRD-PARTY-NOTICES.txt         # Ghi nhận bản quyền thư viện bên thứ ba
├── CHANGELOG.md                    # Lịch sử thay đổi qua các phiên bản
├── TECHNICAL_SPEC.md               # Đặc tả kỹ thuật chi tiết của hệ thống
├── WPF_UI_DESIGN_SYSTEM.md         # Quy chuẩn giao diện Dark UI dùng trong dự án
└── AGENTS.md                       # Tài liệu ngữ cảnh kiến trúc dành cho AI Agent
```

---

## Lịch sử thay đổi (Changelog)

Xem chi tiết các thay đổi theo từng phiên bản tại [CHANGELOG.md](CHANGELOG.md).

---

## Giấy phép (License)

Dự án được phát hành theo giấy phép **MIT License** — xem chi tiết tại [LICENSE](LICENSE).
Thông tin bản quyền các thư viện mã nguồn mở đi kèm (`WPF-UI`, `Hardcodet.NotifyIcon.Wpf`, `.NET 8 Runtime`) được ghi nhận tại [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
