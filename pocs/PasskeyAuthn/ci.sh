#!/usr/bin/env bash
set -euo pipefail

# Each POC owns its validation so a matrix entry cannot accidentally validate another POC.
poc_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$poc_root/../.." && pwd)"
cd -- "$repo_root"

dotnet restore "$poc_root/tests/PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj"
dotnet build "$poc_root/tests/PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj" --configuration Release --no-restore
dotnet test "$poc_root/tests/PasskeyAuthn.Tests/PasskeyAuthn.Tests.csproj" --configuration Release --no-build --logger "console;verbosity=minimal"

node --check "$poc_root/src/PasskeyAuthn/wwwroot/js/passkey.js"
node --test "$poc_root/tests/PasskeyAuthn.Tests/Browser/passkey-client-contract.test.mjs"

# Building from the POC root keeps Docker isolation explicit and avoids a repository-root Dockerfile assumption.
docker build --tag passkeyauthn:ci --file "$poc_root/Dockerfile" "$poc_root"
