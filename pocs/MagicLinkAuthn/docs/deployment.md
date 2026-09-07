# Render deployment

POC chạy bằng Docker Web Service trên Render, lưu Identity, Magic Link state và Data
Protection keys trong Supabase PostgreSQL, và gửi email qua Resend.

## 1. Supabase PostgreSQL

1. Tạo Supabase project và lấy Shared Pooler connection ở session mode.
2. Chuyển sang Npgsql key/value syntax:
   `Host=<host>;Port=5432;Database=postgres;Username=<user>;Password=<password>;SSL Mode=VerifyFull`.
3. Không commit hoặc paste credential vào source.

Phải dùng database/schema riêng cho POC. Application tự apply EF Core migration khi start.

> **Destructive onboarding release:** migration
> `20260907000000_AddUserOnboardingAndResetData` xóa toàn bộ Identity users, sessions,
> Data Protection keys, Magic Link requests và outbox jobs đúng một lần. Backup database
> trước khi merge/deploy nếu cần giữ dữ liệu hiện tại.

## 2. Resend

1. Add và verify sending domain trong Resend; hoàn tất DNS records Resend yêu cầu.
2. Tạo API key chỉ có quyền gửi email.
3. Chọn sender như `Magic Link POC <login@example.com>` trên verified domain.
4. Không dùng `onboarding@resend.dev` cho public acceptance test.

Ứng dụng gọi `POST https://api.resend.com/emails` và đặt stable `Idempotency-Key` theo
Magic Link request ID. Raw API key, token và email link không được log.

## 3. Render Blueprint

1. Tạo Blueprint với path `pocs/MagicLinkAuthn/render.yaml`.
2. Giữ service name `magic-link-authn`, Docker runtime, `rootDir` và health path như file.
3. Đặt các environment variables `sync: false`:
   - `ConnectionStrings__Default`
   - `MagicLink__PublicBaseUrl=https://<service>.onrender.com`
   - `Resend__ApiKey`
   - `Resend__From`
   Blueprint tự generate `MagicLink__OutboxEncryptionKey`; không rotate key khi còn pending
   outbox jobs.
4. Giữ Auto-Deploy off để CI kiểm soát deployment.
5. Deploy lần đầu và kiểm tra `/health`.

Production startup fail closed nếu base URL không phải exact HTTPS origin, PostgreSQL
không bật TLS, outbox encryption key không đủ 256 bit, token
lifetime/cooldown vượt giới hạn, hoặc Resend configuration thiếu.

Render là trusted ingress duy nhất: public traffic không thể truy cập trực tiếp container
port. Forwarded Headers Middleware chỉ xử lý hop gần nhất (`ForwardLimit=1`), nên giá trị
`X-Forwarded-For` do client chèn trước hop của Render không được dùng làm rate-limit key.

## 4. GitHub Actions deploy hook

1. Trong Render service, tạo Deploy Hook.
2. Trong GitHub Actions repository secrets, lưu URL dưới tên
   `RENDER_DEPLOY_HOOK_URL_MAGIC_LINK_AUTHN`.
3. `poc.json` khai báo đúng secret này; root workflow chỉ gọi hook sau khi aggregate
   solution, POC tests, JavaScript contract và Docker build đều thành công trên `main`.

## 5. Acceptance checklist

1. `/health` trả healthy sau cold start.
2. Request link bằng email mới; UI trả generic accepted message.
3. Resend nhận đúng một email và link dùng HTTPS origin chính xác.
4. Trước redemption, `AspNetUsers` chưa có account tương ứng.
5. Mở link: address bar được scrub fragment trước khi request prepare được gửi.
6. Confirmation POST chuyển account chưa có profile tới `/onboarding`.
7. `FullName` là required; bỏ trống `PhoneNumber` vẫn hoàn tất onboarding.
8. Nếu nhập `PhoneNumber`, giá trị phải theo E.164 và `PhoneNumberConfirmed` vẫn là `false`.
9. Trước khi hoàn tất onboarding, `/dashboard` redirect lại `/onboarding`.
10. Hoàn tất onboarding chuyển tới `/dashboard`; login lần sau bỏ qua onboarding.
11. Replay cùng link chuyển tới invalid page.
12. Link cũ không dùng được sau khi link mới được gửi thành công.
13. Link quá 10 phút không dùng được.
14. Anonymous request tới `/dashboard` redirect về `/`.
15. Invalid external `returnUrl` bị từ chối.
16. Sau destructive migration, các deploy tiếp theo giữ login cookie decryption keys và database state.
17. Xác nhận application logs không chứa raw token, URL fragment, API key hoặc email body.

## Operational limitations

- Render Free service có thể sleep và gây cold-start delay.
- Email delivery phụ thuộc Resend quota, verified domain và suppression state.
- Transactional outbox commit request trước khi gọi Resend. Worker lease job, retry với cùng
  idempotency key và chỉ revoke link cũ sau khi provider xác nhận delivery.
- Outbox token được encrypt bằng AES-256-GCM với key ngoài database và bị xóa ngay sau
  delivery; database compromise riêng lẻ không làm lộ pending raw token.
- Chưa có account recovery, admin UI, audit pipeline, multi-region consistency hoặc SLA.
