# Tình trạng bản rà soát ngày 12/10/2026

Chưa đủ điều kiện xác nhận sẵn sàng nộp. Restore/build thành công (0 warning, 0 error), 2 test được chọn Pass, 16/19 HTTP probe Pass và 3 Blocked do SQL. 19 test cần dữ liệu chưa chạy; không tính là Pass hoặc framework Skip. Các báo cáo nghiệm thu cũ trong source là lịch sử.

Thay đổi: thêm `/health/ready`; download ảnh theo ID với quyền biên tập; Swagger binary và test anonymous download; cleanup của test ưu tiên `ConnectionStrings__DefaultConnection`. Không thay đổi migrations hoặc dữ liệu thật.

Trước khi demo, cấu hình cùng biến môi trường kết nối tới **DB thử riêng**, chạy Initialize-Development.ps1, Start-Development.ps1; readiness phải 200. Sau đó chạy đủ 21 tests và browser workflow. Không dựa vào `/health` hay HTML 200 để kết luận SQL hoạt động.

Khoảng cách với tài liệu môn học: chưa có Repository interface P4, chưa có Serilog rolling file P9 và chưa có minh chứng GitHub P1 trong gói. Source dùng service/DbContext và ILogger; cần giải quyết theo yêu cầu giảng viên. Newsletter chưa gửi email; upload chỉ kiểm signature sơ bộ.

Chi tiết tại báo cáo, CHANGELOG_REVIEW.md và HUONG_DAN_DEMO.md ở bộ bàn giao. Không đưa `.local/development.json`, key/token/password, bin/obj hoặc log nhạy cảm vào bộ nộp.
