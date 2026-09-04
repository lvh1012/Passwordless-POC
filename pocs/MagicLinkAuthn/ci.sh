#!/usr/bin/env bash
set -euo pipefail

poc_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$poc_root/../.." && pwd)"
cd -- "$repo_root"

dotnet restore "$poc_root/tests/MagicLinkAuthn.Tests/MagicLinkAuthn.Tests.csproj"
dotnet build "$poc_root/tests/MagicLinkAuthn.Tests/MagicLinkAuthn.Tests.csproj" --configuration Release --no-restore
dotnet test "$poc_root/tests/MagicLinkAuthn.Tests/MagicLinkAuthn.Tests.csproj" --configuration Release --no-build --logger "console;verbosity=minimal"

node --check "$poc_root/src/MagicLinkAuthn/wwwroot/js/magic-link.js"
node --check "$poc_root/src/MagicLinkAuthn/wwwroot/js/magic-link-callback.js"
node --test "$poc_root/tests/MagicLinkAuthn.Tests/Browser/magic-link-client-contract.test.mjs"

docker build --tag magiclinkauthn:ci --file "$poc_root/Dockerfile" "$poc_root"
