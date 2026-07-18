#!/usr/bin/env bash
# Validates and builds ScreenDimmer. Runs identically locally (git-bash/WSL) and
# in CI (.github/workflows/ci.yml invokes this same script on windows-latest),
# so there is exactly one place build/validation logic lives.
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Release}"
PUBLISH_DIR="${PUBLISH_DIR:-publish}"

cd "$(dirname "${BASH_SOURCE[0]}")"

echo "== dotnet SDK =="
dotnet --version

echo "== restore =="
dotnet restore

echo "== validate: code style (dotnet format) =="
dotnet format --verify-no-changes --no-restore

echo "== validate: build with warnings as errors ($CONFIGURATION) =="
dotnet build --configuration "$CONFIGURATION" --no-restore -warnaserror

echo "== publish ($CONFIGURATION -> $PUBLISH_DIR) =="
rm -rf "$PUBLISH_DIR"
dotnet publish --configuration "$CONFIGURATION" --no-build --output "$PUBLISH_DIR"

echo "== done =="
echo "Build output: $PUBLISH_DIR/ScreenDimmer.exe"
