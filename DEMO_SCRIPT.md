# Kịch bản demo

Đã kiểm chứng bản local ngày 30/09/2026: 21 tests HTTP/SQL và Chrome workflow. Xem REQUIREMENTS_TRACEABILITY.md để đối chiếu từng mục của TXT.

Chạy toàn bộ demo tự động (tạo rồi dọn dữ liệu QA):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-ReaderBrowser.ps1 -Full
```

Chạy kiểm chứng bản publish:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify-Published.ps1
```

Ảnh và kết quả ở `.local/browser-qa`. Không chạy tests cùng lúc hoặc trên production.

1. Chạy Initialize-Development và Start-Development theo README; mở hai solution projects để giải thích luồng Web → HTTP → API → EF → SQL.
2. Mở Swagger: GET articles với pageSize=3, pageIndex=2; category=cong-nghe; search; sortColumn=title, sortOrder=asc. Thử pageSize=0 để thấy 400.
3. Gọi POST articles chưa login để thấy 401. Login user, Authorize, thử POST articles và GET admin/users để thấy 403.
4. Login journalist, tạo bài Draft với categoryId lấy từ categories. Đọc manage/{id}, sửa kèm rowVersion; thử rowVersion cũ để thấy 409. Xóa một Draft thử nghiệm.
5. Mở website, login journalist. CMS → Viết bài mới → Lưu → Gửi duyệt.
6. Login editor. CMS → lọc PendingReview → Mở bài → Duyệt → Xuất bản ngay. Hoặc chọn lịch tương lai trước khi duyệt và đợi worker chạy.
7. Mở reader, tìm bài vừa xuất bản. Xem detail, tag, related articles và SEO meta. Network của trình duyệt thấy `/gateway/articles` trả JSON; gateway gọi API bằng HttpClient, không truy cập DbContext.
8. Login user, lưu bài, bày tỏ cảm xúc, gửi bình luận. Mở bài đã lưu và lịch sử. Login moderator để duyệt bình luận, quay lại reader để thấy bình luận.
9. Login admin: xem dashboard, quản lý category/tag, thử xóa category còn bài để thấy 409; quản lý user (không sửa chính tài khoản đang dùng).
10. CMS → Media: upload PNG/JPG, sao chép đường dẫn vào bài viết. Thử file không phải ảnh và ảnh >5 MB để thấy validation. Ảnh đang được dùng không thể xóa.
11. Settings: cập nhật tên/footer rồi reload reader. Mở /rss. Xem notification khi bài được duyệt/từ chối hoặc bình luận có reply được duyệt.
12. Chạy `dotnet test NewsPortal.sln --no-restore`. Trình bày tests thực sự gọi HTTP và SQL Server; dữ liệu kiểm thử được dọn sau khi chạy.

Kiểm tra thủ công trước bảo vệ: desktop/mobile, browser console, các form và lỗi validation, thao tác back/refresh, tài khoản bị khóa. Không tuyên bố kiểm tra visual đã hoàn tất khi chưa thao tác trong browser.
