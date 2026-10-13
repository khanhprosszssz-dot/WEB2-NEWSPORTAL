# Hướng dẫn chạy demo NewsPortal – Nhịp Sống

Chuẩn bị cho bản dự án trên máy hiện tại, ngày 13/10/2026.

**Luồng chính:** mở trang chủ → đăng nhập → tải ảnh → tạo bản nháp → gửi duyệt → duyệt → xuất bản → xem bài ngoài trang chủ.

## 1. Chuẩn bị trước giờ trình bày

- Mở thư mục dự án `WEB2-NEWSPORTAL-main` có file `NewsPortal.sln` bằng VS Code.
- Chuẩn bị mật khẩu trong `.local/development.json`, trường `Password`. Chỉ lấy phần giá trị, không lấy dấu ngoặc kép. Có thể dùng Ctrl + P trong VS Code để mở file này.
- Chuẩn bị ảnh `assets/article-images/doi-song-30.png` cho bài demo vườn rau.
- Mở sẵn các tab trang chủ, CMS và Swagger theo bảng bên dưới.
- Thử luồng một lần trước buổi demo. Nếu đã dùng slug mẫu, đổi số cuối khi trình bày lần tiếp theo.

| Trang | Địa chỉ |
| --- | --- |
| Trang chủ | http://localhost:5200/ |
| Đăng nhập | http://localhost:5200/dang-nhap |
| CMS | http://localhost:5200/cms |
| Swagger API | http://localhost:5100/swagger |
| Kiểm tra database | http://localhost:5100/health/ready |

## 2. Khởi động web và API

Nếu trang chủ đã có tin và `/health/ready` trả `{"status":"ready"}`, bỏ qua bước khởi động.

Nếu ứng dụng chưa chạy, mở Terminal PowerShell tại thư mục chứa `NewsPortal.sln`, chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Development.ps1
```

Chờ dòng thông báo:

```text
Reader: http://localhost:5200 | Swagger: http://localhost:5100/swagger
```

Kiểm tra API kết nối được database:

```powershell
Invoke-RestMethod http://localhost:5100/health/ready
```

Kết quả cần có `status: ready`. Sau đó mở trang chủ và nhấn Ctrl + F5.

**Cấu hình hiện tại:** API ở cổng 5100, web ở cổng 5200; database dùng SQL LocalDB `(localdb)\MSSQLLocalDB`, tên `NewsPortal_BaoCao`. Web đã được cấu hình gọi `http://localhost:5100/`.

Nếu cài lại từ source và chưa có dữ liệu, chạy lần lượt:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' restore NewsPortal.sln
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Initialize-Development.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Development.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Set-DemoArticleImages.ps1
```

Máy hiện tại đã có dữ liệu và 35 ảnh demo, không cần chạy lại phần cài mới để trình bày.

## 3. Kịch bản trình bày chính, khoảng 7–10 phút

### Bước 1 – Giới thiệu trang độc giả, khoảng 1 phút

1. Mở http://localhost:5200/.
2. Chỉ vào danh sách chuyên mục, tin nổi bật và phần đọc nhiều.
3. Bấm một chuyên mục để xem danh sách bài.
4. Tìm từ khóa `vườn rau`, mở bài để xem tiêu đề, nội dung và ảnh.

**Lời nói gợi ý:** “Đây là trang đọc tin của Nhịp Sống. Người đọc có thể xem tin theo chuyên mục, tìm kiếm và mở nội dung chi tiết. Nội dung và ảnh trong bản này dùng để minh họa đồ án.”

### Bước 2 – Đăng nhập quản trị, khoảng 30 giây

1. Mở http://localhost:5200/dang-nhap. Nếu đã đăng nhập Administrator, chuyển sang bước 3.
2. Email: `administrator@newsportal.local`.
3. Mật khẩu: giá trị `Password` trong `.local/development.json`.
4. Bấm **Đăng nhập**.
5. Bấm **CMS** ở góc trên bên phải, cạnh tên tài khoản.
6. Xem nhanh màn hình **Tổng quan** với thống kê bài viết và trạng thái.

**Lời nói gợi ý:** “Tài khoản quản trị có quyền quản lý bài viết, thư viện ảnh và kiểm duyệt. Phần CMS có các mục phù hợp với vai trò tài khoản.”

### Bước 3 – Tải ảnh lên, khoảng 1 phút

1. Trong CMS, chọn **Thư viện ảnh**.
2. Bấm ô chọn file.
3. Chọn `assets/article-images/doi-song-30.png` trong thư mục dự án.
4. Bấm **Tải ảnh lên**.
5. Ảnh mới xuất hiện đầu danh sách. Dưới ảnh có ô **Đường dẫn ảnh**.
6. Bấm ô đường dẫn, nhấn Ctrl + A rồi Ctrl + C để copy toàn bộ.

Đường dẫn có dạng `/uploads/… .png` với tên file do hệ thống sinh. Dùng đúng giá trị trong ô; không dán đường dẫn file trên ổ C vào bài.

**Lời nói gợi ý:** “Ảnh được tải vào thư viện và có đường dẫn để dùng trong bài viết. Hệ thống hỗ trợ JPG, PNG, WebP với dung lượng tối đa 5 MB.”

### Bước 4 – Tạo bài viết, khoảng 2 phút

1. Chọn **Bài viết → Viết bài mới**.
2. Điền nội dung mẫu bên dưới.
3. Chọn chuyên mục **Đời sống**.
4. Dán đường dẫn đã copy vào **Đường dẫn ảnh từ thư viện**.
5. Tích **Nổi bật** và **Cho phép bình luận**. Chủ đề và các trường SEO có thể để trống trong luồng demo này.
6. Bấm **Xem trước** để xem tiêu đề, tóm tắt và nội dung. Phần xem trước này chỉ hiển thị văn bản.
7. Bấm **Lưu bản thảo**. Trạng thái lúc này là `Draft`.

**Tiêu đề:**

```text
Vườn rau nhỏ trên ban công: góc xanh làm mới nhịp sống mỗi ngày
```

**Slug:**

```text
vuon-rau-ban-cong-demo-01
```

Nếu slug đã tồn tại, dùng `vuon-rau-ban-cong-demo-02` hoặc một số mới. Slug không dấu, không có khoảng trắng và không trùng bài khác.

**Tóm tắt:**

```text
Bài demo hư cấu: Từ vài chậu cây nhỏ, một gia đình tạo góc rau xanh trên ban công, thêm niềm vui chăm sóc cây vào sinh hoạt mỗi ngày.
```

**Nội dung:**

```text
Đây là nội dung demo hư cấu phục vụ đồ án NewsPortal. Ảnh là minh họa do AI tạo.

Trong câu chuyện minh họa này, một gia đình tận dụng góc ban công để trồng rau và các loại cây gia vị. Những chậu cây được sắp xếp gọn gàng, tạo một không gian xanh ngay trong căn hộ.

Mỗi buổi sáng, các thành viên cùng quan sát cây và chăm sóc góc vườn. Hoạt động nhỏ giúp cả nhà có thêm thời gian trò chuyện, thư giãn và tìm hiểu sự phát triển của cây.

Bài viết được sử dụng để trình diễn thao tác tạo bản nháp, tải ảnh, gửi duyệt và xuất bản trên website Nhịp Sống.
```

**Lời nói gợi ý:** “Bài được lưu dưới dạng bản nháp. Lưu bài chưa làm bài xuất hiện công khai; bài cần đi qua quy trình duyệt.”

### Bước 5 – Duyệt và xuất bản, khoảng 1 phút

Sau khi lưu, cuộn xuống các nút thao tác bên dưới form:

1. Bấm **Gửi duyệt** → trạng thái `PendingReview`.
2. Để trống **Lịch xuất bản sau khi duyệt (tùy chọn)**.
3. Bấm **Duyệt bài** → trạng thái `Approved`.
4. Bấm **Xuất bản ngay** → trạng thái `Published`.

```text
Draft → PendingReview → Approved → Published
Bản nháp → Chờ duyệt → Đã duyệt → Đã xuất bản
```

**Lời nói gợi ý:** “Trong phần demo ngắn, em dùng tài khoản Administrator để thực hiện toàn bộ luồng. Hệ thống cũng có vai trò Journalist viết bài và Editor duyệt, xuất bản.”

### Bước 6 – Xem bài công khai, khoảng 1 phút

1. Mở lại http://localhost:5200/ và nhấn Ctrl + F5.
2. Tìm `Vườn rau nhỏ trên ban công` hoặc mở thẳng:

```text
http://localhost:5200/tin-tuc/vuon-rau-ban-cong-demo-01
```

Nếu dùng slug khác, thay phần cuối URL tương ứng.

3. Chỉ ra tiêu đề, tóm tắt, ảnh vừa tải và nội dung.
4. Có thể bấm **Lưu bài** hoặc một nút cảm xúc để minh họa tương tác của tài khoản đã đăng nhập.

**Lời nói gợi ý:** “Sau khi xuất bản, bài đã truy cập được ở giao diện độc giả. Ảnh tải từ CMS được hiển thị trong bài.”

## 4. Nếu còn thời gian: kiểm duyệt bình luận, khoảng 2 phút

1. Ở bài vừa đăng, nhập bình luận `Bài demo có ảnh rõ và nội dung dễ theo dõi.`.
2. Bấm **Gửi bình luận**. Bình luận cần được kiểm duyệt trước khi hiển thị công khai.
3. Mở tab CMS → **Kiểm duyệt bình luận**.
4. Tìm bình luận mới, bấm **Duyệt**.
5. Về tab bài viết và tải lại trang để xem bình luận.

**Lời nói gợi ý:** “Bình luận có bước kiểm duyệt riêng. Sau khi quản trị duyệt, bình luận mới được hiển thị công khai.”

## 5. Nếu thầy yêu cầu xem API

1. Mở http://localhost:5100/swagger.
2. Tìm nhóm **Articles**, mở `GET /api/v1/articles`.
3. Bấm **Try it out**, đặt `pageSize` là `3`, rồi bấm **Execute**.
4. Chỉ vào mã phản hồi `200` và dữ liệu JSON của các bài đã xuất bản.

**Lời nói gợi ý:** “Giao diện MVC gọi API qua gateway. API cung cấp dữ liệu JSON và sử dụng EF Core để truy cập SQL Server.”

## 6. Các ảnh khác có thể dùng khi demo

Tất cả nằm trong `assets/article-images`:

| File | Chủ đề phù hợp |
| --- | --- |
| `thoi-su-1.png` | Công viên, không gian xanh |
| `the-gioi-2.png` | Thư viện, đọc sách |
| `cong-nghe-4.png` | Sinh viên, ứng dụng giao thông |
| `van-hoa-7.png` | Nghề làm gốm, làng nghề |
| `doi-song-30.png` | Vườn rau ban công |

## 7. Xử lý nhanh khi gặp lỗi

| Hiện tượng | Cách xử lý |
| --- | --- |
| Không mở được localhost | Chạy `Start-Development.ps1` như phần 2. |
| Có thông báo process đang chạy | Nếu web hoạt động, tiếp tục demo. Nếu cần khởi động lại, dùng hai lệnh bên dưới. |
| Trang báo 500 hoặc không tải được tin | Kiểm tra `/health/ready`, đọc log `.local/NewsPortal.Api.log` và `.local/NewsPortal.Web.log`. Kiểm tra Web `Api:BaseUrl` là `http://localhost:5100/`. |
| `/health/ready` trả 503 | API chưa kết nối được database. Kiểm tra LocalDB `MSSQLLocalDB` và connection string trong `NewsPortal.Api/appsettings.Development.json`. |
| Đăng nhập thất bại | Dùng đúng email, copy lại `Password` không kèm dấu ngoặc kép. |
| Slug đã tồn tại | Đổi số cuối slug rồi lưu lại. |
| Không thấy nút Xuất bản ngay | Phải **Gửi duyệt → Duyệt bài** trước; dùng tài khoản Administrator hoặc Editor. |
| Bài chưa hiển thị công khai | Kiểm tra trạng thái phải là `Published`; tải lại hoặc mở URL bài trực tiếp. |
| Ảnh không tồn tại trong thư viện | Tải ảnh lên CMS trước, copy đường dẫn `/uploads/…` từ ô dưới ảnh. |

Khởi động lại bản dự án hiện tại:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Stop-Development.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start-Development.ps1
```

Nếu cổng 5100/5200 vẫn bị chiếm bởi một bản dự án ở thư mục khác, dừng bản đó từ terminal đã chạy nó trước khi khởi động bản này.

## 8. Kết thúc buổi demo

Nếu muốn dừng hai ứng dụng:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Stop-Development.ps1
```

Ghi nhớ chuỗi thao tác chính để trình bày: **CMS → Thư viện ảnh → Tải ảnh lên → Copy đường dẫn → Bài viết → Viết bài mới → Lưu bản thảo → Gửi duyệt → Duyệt bài → Xuất bản ngay → Xem bài trên trang chủ**.
