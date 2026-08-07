# PasskeyAuthn CDA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `subagent-driven-development` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ưu tiên browser-mediated Cross-Device Authentication cho cả Passkey registration và username-less login.

**Architecture:** `passkey.js` thêm `hints: ["hybrid"]` vào bản sao options trước khi parse và gọi WebAuthn. ASP.NET Core Identity tiếp tục phát hành/kiểm chứng ceremony như cũ; browser tự điều phối QR, proximity và phone.

**Tech Stack:** .NET 10, ASP.NET Core Identity Passkey APIs, Razor Pages, Vanilla JavaScript WebAuthn, Node built-in test runner.

## Global Constraints

- Không thêm dependency, endpoint, server state, database schema, migration hoặc cấu hình mới.
- Dùng đúng giá trị `hints: ["hybrid"]` cho cả `navigator.credentials.create()` và `navigator.credentials.get()`.
- CDA là hint: browser không hỗ trợ có quyền bỏ qua và tiếp tục WebAuthn fallback hiện có.
- Mọi code change có comment/JSDoc giải thích lý do khi logic không hiển nhiên.
- Không tự tạo QR, pairing state, Bluetooth protocol hoặc log credential/pairing data.

---

### Task 1: Client CDA contract

**Files:**

- Modify: `pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Browser/passkey-client-contract.test.mjs`
- Modify: `pocs/PasskeyAuthn/src/PasskeyAuthn/wwwroot/js/passkey.js`

**Interfaces:**

- Consumes: JSON WebAuthn options từ `/api/passkeys/register/options` và `/api/passkeys/login/options`.
- Produces: `PublicKeyCredentialCreationOptions` và `PublicKeyCredentialRequestOptions` mang `hints: ["hybrid"]` tại browser boundary.

- [ ] Viết hai contract test thất bại, mỗi test chạy public `startRegistration` hoặc `startLogin` với fake browser tối thiểu và xác nhận public-key options truyền tới WebAuthn có literal `hints: ["hybrid"]`.
- [ ] Chạy `node --test pocs/PasskeyAuthn/tests/PasskeyAuthn.Tests/Browser/passkey-client-contract.test.mjs` và xác nhận fail vì hint chưa tồn tại.
- [ ] Thêm một helper nội bộ tạo shallow copy options với `hints: ["hybrid"]`; gọi helper cho cả creation và request options trước parser hiện có.
- [ ] Chạy lại cùng command và xác nhận toàn bộ tests pass; chạy `node --check pocs/PasskeyAuthn/src/PasskeyAuthn/wwwroot/js/passkey.js`.

### Task 2: CDA guidance and manual acceptance

**Files:**

- Modify: `pocs/PasskeyAuthn/src/PasskeyAuthn/Pages/Register.cshtml`
- Modify: `pocs/PasskeyAuthn/src/PasskeyAuthn/Pages/Index.cshtml`
- Modify: `pocs/PasskeyAuthn/README.md`
- Modify: `pocs/PasskeyAuthn/docs/deployment.md`

**Interfaces:**

- Consumes: browser-managed CDA from Task 1.
- Produces: user-facing guidance and a reproducible HTTPS acceptance checklist without application-managed QR or pairing data.

- [ ] Cập nhật registration/login copy để giải thích browser có thể yêu cầu phone/thiết bị khác và QR.
- [ ] Cập nhật README user flow, bảo mật và limitation để mô tả browser-mediated CDA, HTTPS và fallback.
- [ ] Thay checklist HTTPS manual acceptance: thêm registration và login trên desktop/laptop bằng phone/QR, hủy ceremony và fallback an toàn.
- [ ] Chạy Node contract test, JavaScript syntax check, `dotnet build PasswordlessAuthn.slnx --configuration Release`, và `dotnet test PasswordlessAuthn.slnx --configuration Release`.
