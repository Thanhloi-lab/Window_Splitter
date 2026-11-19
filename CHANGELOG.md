# Changelog

Tất cả các thay đổi đáng chú ý của dự án **Windows Splitter** được ghi nhận tại đây theo chuẩn [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- **Trình chỉnh sửa chia vùng 2D đệ quy (`ZoneSplitNode`):** Cho phép tách bất kỳ vùng nào theo chiều dọc hoặc chiều ngang để tạo nhiều vùng không bao giờ chồng chéo, kéo thả trực tiếp các đường phân chia trên `CanvasZoneEditor`, và tự đánh số `Zone 1..N` từ trên xuống dưới, trái sang phải.
- **Chuyển đổi đơn vị Pixel / Phần trăm (`%`):** Hỗ trợ nhập thông số `X, Y, Width, Height` theo Pixel hoặc `%` trong bảng chỉnh sửa vùng và tự động đồng bộ vào cây chia vùng.
- **Khoảng hở giữa các vùng (`Zone Gap`):** Thêm thanh trượt chỉnh khoảng cách `0..32 px` giữa các vùng.
- **Mẫu bố cục 2D mặc định mới:** Bổ sung `preset_grid_2x2` (Lưới 2x2), `preset_priority_grid` (Priority Grid xếp tầng), và `preset_asymmetric_5` (Bố cục 5 vùng lệch tâm).
- **Chống bung toàn màn hình khi bấm nhầm mép trên (`TopEdgeSnapGuard`):** Dùng `WH_MOUSE_LL` chặn sự kiện `HTTOP` trên dải `8px` sát đỉnh màn hình của các cửa sổ đang snap ở vùng sát mép trên (kèm tùy chọn bật/tắt `PreventAccidentalTopEdgeMaximize` trong Settings).
- **Tài liệu & Giấy phép Open Source:** Thêm `LICENSE` (MIT), `THIRD-PARTY-NOTICES.txt`, `CHANGELOG.md`, `TECHNICAL_SPEC.md`, `AGENTS.md`, và workflow phát hành tự động lên GitHub Releases (`.github/workflows/release.yml`).

### Changed
- **Điều hướng `Win + Arrow` theo không gian 2D (`ZoneEngine`):** Nâng cấp thuật toán tìm vùng lân cận 4 hướng (`Left`, `Right`, `Up`, `Down`) dựa trên diện tích giao 2D và khoảng cách cạnh; khi ở vùng mép trên cùng mới `Maximize` (`Win + Down` khôi phục lại vùng cũ), ở vùng mép dưới cùng mới `Minimize`, và cuộn vòng qua màn hình liền kề khi bật `Cross-Monitor Wrap`.
- **Bù trừ viền DWM 2 lượt (`ApplyWindowToZone`):** Đo lại `DWMWA_EXTENDED_FRAME_BOUNDS` sau khi gọi `SetWindowPos` để hiệu chỉnh triệt tiêu khe hở nhỏ ở mép trên và các cạnh cửa sổ.
- **Quy đổi vùng theo `WorkArea`:** Tính tọa độ các vùng trực tiếp theo `monitor.WorkArea` thay vì `Bounds` toàn màn hình để các vùng xếp tầng theo chiều dọc không bị tràn xuống dưới Taskbar.
- **Chuẩn hóa giao diện & tài liệu:** Viết lại `README.md`, cập nhật `installer/License.txt` sang MIT License, cập nhật `installer/build-installer.bat` nhận diện đường dẫn Inno Setup 6 cài qua `winget` và xuất checksum SHA-256, đồng thời loại bỏ emoji trang trí khỏi các thành phần mới chỉnh sửa.

## [1.0.0] - 2026-11-14

### Added
- **Zone Manager đa màn hình:** Hỗ trợ phát hiện `WM_DISPLAYCHANGE`, nhận diện chế độ `Duplicate`, cấu hình Preset độc lập theo từng màn hình, và tự sinh Preset mặc định cho Ultrawide (21:9), tiêu chuẩn (16:9 / 16:10) và màn hình dọc.
- **Custom Profile (`SaveProfileDialog`):** Lưu, cập nhật và xóa các Profile chia vùng tùy chỉnh trong khi khóa ghi đè đối với Preset mặc định.
- **Smart Borderless Manager:** Gỡ bỏ thanh tiêu đề/viền cửa sổ, giữ cửa sổ trên Taskbar thông qua `WinEventHookService`, phím tắt `Win + Ctrl + B`, thông báo mờ `BorderlessNoticeWindow`, và thanh điều khiển nổi tự ẩn/hiện `FloatingCaptionBar` (`WS_EX_NOACTIVATE`).
- **Hệ thống & Khay hệ thống:** Single-Instance (`Mutex` + `WM_SHOW_SPLITTER`), chạy ẩn xuống System Tray, tự khởi động với quyền Administrator qua Task Scheduler (`WindowsSplitter_Elevated`, tham số `--minimized`).
- **Đa ngôn ngữ:** Hỗ trợ chuyển đổi động giữa tiếng Anh (`en`) và tiếng Việt (`vi`).
- **Bộ cài đặt Windows x64 (`installer/`):** Đóng gói self-contained `.NET 8` kết hợp Inno Setup 6 (`WindowsSplitter-Setup-1.0.0-x64.exe`).
