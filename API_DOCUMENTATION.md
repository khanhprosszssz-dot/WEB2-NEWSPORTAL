# NewsPortal API

Base URL development: `http://localhost:5100/api/v1`. Swagger JSON: `/swagger/v1/swagger.json`. Swagger UI: `/swagger`. Bearer token lấy từ login, nhập vào nút Authorize. JSON enum dùng tên chuỗi.

## Quy ước

Danh sách: `{data,pageIndex,pageSize,totalCount,totalPages}`; một số danh sách quản trị có totalCount để client tính totalPages. pageIndex >= 1, pageSize 1–100. Lỗi validation trả ValidationProblemDetails, lỗi nghiệp vụ trả ProblemDetails hoặc `{message}`. HTTP: 200 đọc, 201 tạo, 204 cập nhật/xóa, 400 validation, 401 chưa xác thực, 403 không đủ quyền, 404 không tồn tại/không công khai, 409 xung đột dữ liệu.

## Endpoints

| Method | Route | Quyền | Request / Response |
|---|---|---|---|
| POST | /auth/register | Public | email,password,fullName → token,expiresAt,user |
| POST | /auth/login | Public | email,password → token,expiresAt,user |
| GET/PUT | /auth/me | Đăng nhập | GET UserDto; PUT fullName,bio |
| POST | /auth/logout | Đăng nhập | Thu hồi token bằng security stamp |
| GET | /articles | Public | ArticleQuery → PageDto<ArticleListDto> |
| GET | /articles/{id}, /articles/slug/{slug} | Public | ArticleDetailDto, chỉ Published + category active |
| GET | /articles/featured, /breaking, /latest, /popular | Public | Query + PageDto |
| GET | /articles/category/{slug}, /articles/tag/{slug} | Public | PageDto |
| GET | /articles/manage, /articles/manage/{id} | Admin/Editor/Journalist | Journalist chỉ xem bài mình |
| POST/PUT/DELETE | /articles, /articles/{id} | Admin/Editor/Journalist | ArticleInput; PUT cần rowVersion |
| POST | /articles/{id}/review | Admin/Editor/Journalist | action,note,scheduledAt |
| GET | /articles/{id}/revisions | Như manage | Phân trang phiên bản trước |
| GET | /categories, /categories/{id} | Public | CategoryDto; search,pageIndex,pageSize |
| POST/PUT/DELETE | /categories, /categories/{id} | Admin/Editor | name,slug,description,parentId,displayOrder,isActive |
| GET | /tags, /tags/{id} | Public | TagDto; search,pageIndex,pageSize |
| POST/PUT/DELETE | /tags, /tags/{id} | Admin/Editor | name,slug |
| GET/POST | /articles/{id}/comments | GET public; POST login | content,parentCommentId → id,status |
| PUT/DELETE | /comments/{id} | Chủ sở hữu; Admin/Moderator được xóa | content |
| GET | /comments/manage | Admin/Moderator | status, paging → PageDto<CommentDto> |
| PATCH | /comments/{id}/status | Admin/Moderator | status: Pending/Approved/Rejected/Hidden |
| GET | /articles/{id}/reactions | Public | type,count[] |
| PUT/DELETE | /articles/{id}/reaction | Login | reactionType: Like/Love/Useful/Wow |
| GET | /bookmarks, /history | Login | PageDto<ArticleListDto> của user |
| POST/DELETE | /bookmarks/{articleId} | Login | Idempotent save/remove |
| POST | /articles/{id}/view | Public | Tăng tối đa một lần/30 phút theo user hoặc IP |
| GET | /notifications | Login | Danh sách của user, paging |
| PATCH | /notifications/{id}/read | Chủ sở hữu | Không body |
| GET/POST/DELETE | /media, /media/{id} | Admin/Editor/Journalist | GET search+paging; POST multipart field file; DELETE theo ownership |
| GET | /admin/dashboard | Admin | Tổng bài/trạng thái/user/comment/view và theo category |
| GET | /admin/users | Admin | search,paging → UserDto, không password hash |
| GET | /admin/users/{id} | Admin | Hồ sơ UserDto: tên/email/bio/role/trạng thái, không Identity secrets |
| PUT | /admin/users/{id} | Admin | role,isActive; không tự đổi quyền; thu hồi token cũ |
| GET/PUT | /admin/settings, /admin/settings/{key} | Admin | key SiteName/FooterText, body value |
| GET | /settings | Public | Các cấu hình công khai |

ArticleQuery: pageIndex,pageSize,search,category (slug),tag (slug),author (id),status,fromDate,toDate,sortColumn (publishedAt/createdAt/title/viewCount),sortOrder (asc/desc),featured,breaking,editorsChoice.

ArticleInput: title,slug,subtitle,summary,content,thumbnailUrl,categoryId,tagIds[],isFeatured,isBreakingNews,isEditorsChoice,allowComments,seoTitle,seoDescription,rowVersion. Content là plain text. thumbnailUrl lấy từ upload response. Không cho client tự đặt author hoặc status.

Title tối thiểu 5 ký tự và Content tối thiểu 20 ký tự sau khi trim. ThumbnailUrl phải có bản ghi Media tương ứng, không chỉ đúng định dạng đường dẫn; ảnh chưa upload trả 400. ETag có namespace riêng cho mỗi lần chạy API để không tái sử dụng mã cache cũ sau restart.

## Cộng đồng bạn đọc

| Method | Endpoint (prefix `/api/v1`) | Quyền | Nội dung |
|---|---|---|---|
| GET | /newsletter/me | Đăng nhập | Trạng thái đăng ký bản tin |
| POST | /newsletter | Đăng nhập | Đăng ký email tài khoản; 201 lần đầu, 204 nếu đã đăng ký |
| DELETE | /newsletter/me | Đăng nhập | Hủy đăng ký; 204 |
| GET | /newsletter | Administrator | Danh sách email, search/pageIndex/pageSize |
| GET | /polls | Public | Khảo sát đang mở; Administrator dùng all=true để xem cả khảo sát đóng |
| POST | /polls | Administrator | question, options (2–8 lựa chọn khác nhau); 201 trả id |
| PATCH | /polls/{id} | Administrator | isActive để mở/đóng khảo sát |
| PUT | /polls/{id}/vote | Đăng nhập | optionId; mỗi tài khoản một phiếu, có thể đổi lựa chọn |
| DELETE | /polls/{id} | Administrator | Xóa khảo sát và phiếu; 204 |

Reader: `/cong-dong`. CMS: mục bản tin và khảo sát. Bản tin mới lưu đăng ký, chưa gửi email tự động.
Web cung cấp `/sitemap.xml`, lấy các trang bài đã xuất bản qua API; `/robots.txt` dẫn tới sitemap.

## Quy trình biên tập

Draft/Rejected → submit → PendingReview → approve → Approved → publish → Published → archive → Archived. Reject yêu cầu note. Journalist chỉ submit bài mình và sửa Draft/Rejected của mình; chỉ xóa Draft. Admin/Editor xử lý review. Approve kèm scheduledAt UTC tương lai được worker xuất bản trong khoảng 30 giây sau hạn. Revision lưu nội dung trước mỗi lần sửa.

## HTTP và bảo mật

GET công khai phù hợp trả ETag + `public,no-cache,must-revalidate`; gửi If-None-Match có thể nhận 304. Dữ liệu riêng trả no-store. CORS chỉ origin cấu hình. Giao diện dùng gateway cùng origin; gateway lưu token trong cookie mã hóa và yêu cầu X-CSRF-TOKEN cho mutation. API vẫn kiểm tra JWT và quyền độc lập với UI.

Swagger bổ sung schema cho các projection media/dashboard/notification/revision/community, response 201/204, lỗi 400/401/403/404/409/500 phù hợp và XML summary cho Articles/Auth. Endpoint public không bị gắn yêu cầu Bearer. GET/PUT auth/me trả 200 kèm UserDto; các mutation không có body trả 204.

Web gateway nhận multipart qua form đã parse, rồi dựng MultipartFormDataContent để chuyển file sang API; không đọc lại stream đã cạn bởi MVC. Giới hạn request gateway 6 MB, API giới hạn mỗi ảnh 5 MB.
