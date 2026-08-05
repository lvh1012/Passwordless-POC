# IDE path fix report

## Scope

Configuration-only fix trong `.idea/.idea.PasswordlessAuthn/.idea/workspace.xml`.

Đã đổi hai reference cũ sang:

`pocs/PasskeyAuthn/src/PasskeyAuthn/PasskeyAuthn.csproj`

Không thay đổi IDE settings khác và không chỉnh application, source, test, deployment hoặc CI.

## Validation

Command đã chạy trước khi sửa:

```powershell
rg -n -F -e '<projectFile>PasskeyAuthn/PasskeyAuthn.csproj</projectFile>' -e '$PROJECT_DIR$/PasskeyAuthn/PasskeyAuthn.csproj' '.idea/.idea.PasswordlessAuthn/.idea/workspace.xml'
```

Kết quả: hai match tại dòng 4 và 77.

Command search sau khi sửa:

```powershell
rg -n -F -e '<projectFile>PasskeyAuthn/PasskeyAuthn.csproj</projectFile>' -e '$PROJECT_DIR$/PasskeyAuthn/PasskeyAuthn.csproj' '.idea/.idea.PasswordlessAuthn/.idea/workspace.xml'
```

Kết quả: không còn match reference cũ.

Command xác nhận path mới:

```powershell
rg -n -F -e '<projectFile>pocs/PasskeyAuthn/src/PasskeyAuthn/PasskeyAuthn.csproj</projectFile>' -e '$PROJECT_DIR$/pocs/PasskeyAuthn/src/PasskeyAuthn/PasskeyAuthn.csproj' '.idea/.idea.PasswordlessAuthn/.idea/workspace.xml'
```

Kết quả: hai match tại dòng 4 và 77.

Không chạy test behavior vì đây là configuration-only fix.
