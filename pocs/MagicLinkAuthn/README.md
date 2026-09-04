# Magic Link Authentication POC

POC đăng ký và đăng nhập passwordless bằng email Magic Link, dùng ASP.NET Core
Identity, PostgreSQL, Resend và Render.

## Security model

- Token được tạo bằng `RandomNumberGenerator.GetBytes(32)`: 256-bit entropy từ OS CSPRNG.
- Token là opaque Base64URL; email hoặc user ID không nằm trong token.
- PostgreSQL chỉ lưu SHA-256 hash của token, không lưu raw token.
- Token hết hạn sau 10 phút, chỉ dùng một lần và token mới revoke token cũ.
- Redemption dùng conditional database update nên concurrent replay chỉ có một request thắng.
- Account chỉ được tạo và email chỉ được confirm sau khi link được redeem thành công.
- Request response không tiết lộ account state; rate limit áp dụng theo IP và normalized email.
- Magic Link đặt token trong URL fragment. Fragment không được browser gửi trong HTTP request;
  callback JavaScript xóa fragment trước khi POST token sang endpoint cùng origin.
- GET không consume token. User phải xác nhận bằng POST có antiforgery protection, tránh
  email scanner vô tình đăng nhập thay user.
- Authentication cookie là `HttpOnly`, `Secure` ở production và `SameSite=Lax`.
- Application đặt `Referrer-Policy: no-referrer` và không log token hoặc email URL.

Đây là POC, không phải authentication service production-ready.

## Flow

1. User nhập email tại `/`.
2. `POST /api/magic-links/request` tạo token và gửi email qua Resend.
3. Email mở `/magic-link/callback#token=...`; server không nhận fragment trong access log.
4. Callback client xóa fragment và POST token vào `/api/magic-links/prepare`.
5. Server chuyển token vào short-lived `HttpOnly` cookie.
6. User POST `/api/magic-links/redeem` để consume token.
7. Server tạo hoặc resolve Identity user, confirm email và phát authentication cookie.

## API

| Method | Endpoint | Mục đích |
| --- | --- | --- |
| `POST` | `/api/magic-links/request` | Request email Magic Link |
| `POST` | `/api/magic-links/prepare` | Chuyển fragment token vào temporary cookie |
| `POST` | `/api/magic-links/redeem` | Atomic consume và sign in |
| `POST` | `/api/auth/logout` | Sign out |
| `GET` | `/health` | PostgreSQL readiness |

## Local development

Prerequisites: .NET SDK 10.x, PostgreSQL và Node.js 22+.

```powershell
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=magiclinkauthn;Username=postgres;Password=<password>;SSL Mode=Prefer"
$env:MagicLink__PublicBaseUrl = "http://localhost:8080"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_HTTP_PORTS = "8080"

dotnet run --project pocs/MagicLinkAuthn/src/MagicLinkAuthn/MagicLinkAuthn.csproj
```

Development không gọi Resend. Sau khi request link, mở `/dev/outbox`; endpoint này không
được map trong Testing hoặc Production.

## Validation

```powershell
dotnet restore PasswordlessAuthn.slnx
dotnet build PasswordlessAuthn.slnx --configuration Release --no-restore
dotnet test PasswordlessAuthn.slnx --configuration Release --no-build

node --check pocs/MagicLinkAuthn/src/MagicLinkAuthn/wwwroot/js/magic-link.js
node --check pocs/MagicLinkAuthn/src/MagicLinkAuthn/wwwroot/js/magic-link-callback.js
node --test pocs/MagicLinkAuthn/tests/MagicLinkAuthn.Tests/Browser/magic-link-client-contract.test.mjs
```

Integration tests dùng Testcontainers PostgreSQL nên cần Docker daemon.

## Production configuration

| Environment variable | Yêu cầu |
| --- | --- |
| `ConnectionStrings__Default` | Npgsql connection string với `SSL Mode=Require` trở lên |
| `MagicLink__PublicBaseUrl` | Exact HTTPS origin, không có path/query/fragment |
| `MagicLink__LifetimeMinutes` | 1–30, mặc định 10 |
| `MagicLink__EmailCooldownSeconds` | 10–600, mặc định 60 |
| `Resend__ApiKey` | Resend API key bắt đầu bằng `re_` |
| `Resend__From` | Sender thuộc verified Resend domain |

Xem [deployment guide](docs/deployment.md) cho full Render flow.
