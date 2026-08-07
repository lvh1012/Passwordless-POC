# PasskeyAuthn CDA Design

## Mục tiêu

Ưu tiên Cross-Device Authentication (CDA) cho registration và username-less
login của POC PasskeyAuthn. Browser thực hiện QR, Bluetooth proximity và xác
thực trên phone; ứng dụng chỉ cung cấp WebAuthn options có hint phù hợp.

## Thiết kế

- Client thêm `hints: ["hybrid"]` vào một bản sao của WebAuthn creation và
  request options trước khi gọi `navigator.credentials.create()` hoặc
  `navigator.credentials.get()`.
- Server-side ASP.NET Core Identity, các endpoint, authentication state,
  cookie, database schema, migration và cấu hình `Passkey` giữ nguyên.
- `hybrid` là browser hint, không phải yêu cầu bắt buộc. Browser không hỗ trợ
  CDA có thể bỏ qua hint và tiếp tục lựa chọn WebAuthn/authenticator khác.
- UI mô tả rằng browser có thể yêu cầu dùng phone hoặc thiết bị khác và quét
  QR. Ứng dụng không tự tạo QR, quản lý pairing state hay log credential.

## Kiểm thử và nghiệm thu

- Node browser contract test xác nhận registration và login truyền chính xác
  `hints: ["hybrid"]` vào WebAuthn.
- Build, .NET test và JavaScript syntax check phải đạt.
- HTTPS manual acceptance dùng desktop/laptop và phone tương thích để kiểm
  tra registration/login qua QR, hủy ceremony và fallback an toàn.
