# Tài khoản development

Chạy `scripts/Initialize-Development.ps1` để tạo tài khoản. Mật khẩu ngẫu nhiên được
lưu riêng tại `.local/development.json`, trường `Password`; file này bị loại khỏi Git.
Không có mật khẩu production trong source. Không seed tài khoản demo ở Production.

| Email | Vai trò |
|---|---|
| administrator@newsportal.local | Administrator |
| editor@newsportal.local | Editor |
| journalist@newsportal.local | Journalist |
| moderator@newsportal.local | Moderator |
| user@newsportal.local | User |

Các tài khoản demo dùng chung mật khẩu development được sinh tại máy này.
Tài khoản đăng ký mới luôn là User. Vai trò không nhận từ form đăng ký.
