# Ảnh minh họa bài demo NewsPortal

Bộ ảnh được tạo bằng công cụ ImageGen tích hợp cho 35 bài demo, mỗi bài một ảnh riêng. Đây là ảnh minh họa do AI tạo, không phải ảnh ghi nhận sự kiện thực tế.

`manifest.json` ghi tên bài, slug, tệp PNG và prompt tương ứng. Các ảnh gốc được giữ tại thư mục này; bản tải lên được quản lý trong thư viện ảnh CMS.

Để gắn lại ảnh sau khi tạo database development mới, khởi động API rồi chạy từ thư mục dự án:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Set-DemoArticleImages.ps1
```

Script dùng tài khoản Administrator development, ghép bài bằng slug, giữ nguyên nội dung và trạng thái xuất bản. Ảnh đã được gắn đúng sẽ được bỏ qua khi chạy lại.
