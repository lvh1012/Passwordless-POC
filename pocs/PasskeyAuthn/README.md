# Passkey Authentication POC

POC đăng ký và đăng nhập bằng Passkey end-to-end trên browser, sử dụng
ASP.NET Core Identity built-in Passkey support và PostgreSQL.

## Mục tiêu

- Đăng ký Passkey cho một email address.
- Đăng nhập bằng platform authenticator như Windows Hello, Touch ID hoặc
  device screen lock.
- Lưu user, Passkey credential và Data Protection keys trong PostgreSQL.
- Chạy độc lập như một POC riêng trong solution `PasswordlessAuthn.slnx`.

POC này không có password fallback, email verification hoặc account recovery.

## Tech stack

- .NET 10 / ASP.NET Core Razor Pages
- ASP.NET Core Identity built-in Passkey APIs
- Vanilla JavaScript WebAuthn APIs
- Entity Framework Core và Npgsql
- PostgreSQL, khuyến nghị Supabase PostgreSQL khi deploy
- Docker và Render Free Web Service

## Cấu trúc

```text
pocs/PasskeyAuthn/
├── src/PasskeyAuthn/             # ASP.NET Core web application
├── tests/PasskeyAuthn.Tests/     # xUnit và browser contract tests
├── Dockerfile                    # Multi-stage production image
├── .dockerignore
├── render.yaml                   # Render Blueprint cho POC này
└── docs/deployment.md            # Supabase, Render và HTTPS acceptance
```

## User flow

### Registration

1. Mở `/register` và nhập email address.
2. Browser gọi `navigator.credentials.create()` qua client script.
3. Server xác thực attestation bằng ASP.NET Core Identity.
4. Passkey được lưu vào `AspNetUserPasskeys` và user được sign in bằng cookie.

### Login

1. Mở `/` và nhập email address.
2. Browser gọi `navigator.credentials.get()`.
3. Server xác thực assertion và tạo authentication cookie.
4. User được chuyển tới `/dashboard`.

Các API ceremony chính:

| Method | Endpoint | Mục đích |
| --- | --- | --- |
| `POST` | `/api/passkeys/register/options` | Tạo registration options |
| `POST` | `/api/passkeys/register/complete` | Hoàn tất registration |
| `POST` | `/api/passkeys/login/options` | Tạo authentication options |
| `POST` | `/api/passkeys/login/complete` | Hoàn tất login |
| `POST` | `/api/auth/logout` | Xóa authentication cookie |
| `GET` | `/health` | Kiểm tra database readiness |

## Chạy local

### Prerequisites

- .NET SDK 10.x
- PostgreSQL đang chạy và database connection có thể truy cập
- Browser hỗ trợ WebAuthn

Ứng dụng chạy trên `http://localhost:8080` theo mặc định. `localhost` được
browser xem là secure context cho WebAuthn khi chạy local; khi deploy phải dùng
HTTPS thật.

Đặt connection string và origin cho đúng địa chỉ local rồi chạy từ repository
root:

```powershell
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=passkeyauthn;Username=postgres;Password=<local-password>;SSL Mode=Prefer"
$env:Passkey__ServerDomain = "localhost"
$env:Passkey__ExpectedOrigin = "http://localhost:8080"

dotnet run --project pocs/PasskeyAuthn/src/PasskeyAuthn/PasskeyAuthn.csproj
```

Ứng dụng tự apply EF Core migrations khi khởi động. Mở
`http://localhost:8080/register` để tạo Passkey đầu tiên.

Không đưa password hoặc connection string thật vào source control. Khi chạy
production, PostgreSQL phải dùng TLS và `Passkey__ExpectedOrigin` phải khớp
chính xác với HTTPS origin.

## Build và test

Các command sau chạy từ repository root:

```powershell
dotnet restore PasswordlessAuthn.slnx
dotnet build PasswordlessAuthn.slnx --configuration Release --no-restore
dotnet test PasswordlessAuthn.slnx --configuration Release --no-build

node --check pocs/PasskeyAuthn/src/PasskeyAuthn/wwwroot/js/passkey.js
node --test pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Browser/passkey-client-contract.test.mjs
```

Integration tests sử dụng Testcontainers PostgreSQL, vì vậy cần Docker daemon
đang chạy. Browser contract test chỉ kiểm tra WebAuthn JSON serialization và
không thay thế việc acceptance test bằng browser thật.

## CI/CD

Root workflow discover POC matrix từ contract `poc.json`; discovery emit path
của mỗi POC, dùng convention `ci.sh` cố định cho validation, và emit tùy chọn
`deployHookSecret`. `ci.sh` không phải cấu hình do người dùng lựa chọn.
`deployHookSecret` không rỗng phải là duy nhất giữa các POC; discovery sẽ reject
các tên bị trùng. Vì discovery là convention chung, workflow không chứa path
hoặc tên POC cụ thể.

Để thêm POC mới, tạo `poc.json` và `ci.sh` trong thư mục POC; thêm `render.yaml`
nếu POC đó được deploy trên Render. Nếu có deployment, tạo một GitHub Actions
repository secret riêng cho POC và khai báo tên secret trong `poc.json`. Không
cần sửa root workflow cho từng POC.

`ci.sh` của từng POC chịu trách nhiệm build, test và kiểm tra Docker context của
POC đó. Root workflow còn restore/build `PasswordlessAuthn.slnx` để validate
aggregate solution, sau đó chạy các POC độc lập qua validation matrix. Chỉ
push thành công lên `main` mới chạy deploy matrix; entry không có deploy hook
secret sẽ được bỏ qua.

## Docker

Build image với context chỉ giới hạn trong POC này:

```powershell
docker build `
  --file pocs/PasskeyAuthn/Dockerfile `
  --tag passkeyauthn:local `
  pocs/PasskeyAuthn
```

Docker image dùng production configuration. Việc chạy image cần PostgreSQL,
HTTPS origin và các environment variables hợp lệ; deployment chuẩn được mô tả
trong [docs/deployment.md](docs/deployment.md).

## Configuration

| Environment variable | Ý nghĩa |
| --- | --- |
| `ConnectionStrings__Default` | PostgreSQL connection string dạng key/value của Npgsql |
| `Passkey__ServerDomain` | WebAuthn RP ID, chỉ gồm hostname |
| `Passkey__ExpectedOrigin` | Origin đầy đủ, ví dụ `https://service.onrender.com` |
| `Passkey__AuthenticatorTimeoutSeconds` | Thời gian authenticator timeout |
| `Passkey__MaxPasskeysPerUser` | Tối đa 3 Passkeys mỗi user |
| `Passkey__MaxDisplayNameLength` | Tối đa 100 ký tự |

## Deploy

POC này được thiết kế cho Render Free Web Service và Supabase PostgreSQL. Dùng
[deployment guide](docs/deployment.md) để cấu hình:

- Supabase Shared Pooler session mode và TLS.
- Render custom Blueprint Path `pocs/PasskeyAuthn/render.yaml`.
- HTTPS `onrender.com` origin và WebAuthn RP ID.
- GitHub Actions validation và Render Deploy Hook.
- Browser acceptance test sau khi deploy.

## Security notes

- Authentication cookie là `HttpOnly`, `Secure` và `SameSite=Lax`.
- State-changing POST endpoints yêu cầu antiforgery header.
- Public Passkey endpoints có fixed-window rate limit.
- Production startup từ chối localhost/mismatched origin và PostgreSQL không bật
  TLS.
- Không log credential JSON, private key material, authentication cookie hoặc
  database connection string.
- Đây là POC, không phải authentication service production-ready. Cần security
  review, monitoring, recovery policy và operational controls trước khi sử dụng
  cho production.
