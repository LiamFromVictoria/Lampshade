#!/usr/bin/env bash
# Validates and builds Lampshade. Runs identically locally (git-bash/WSL) and
# in CI (.github/workflows/ci.yml invokes this same script on windows-latest),
# so there is exactly one place build/validation logic lives.
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Release}"
PUBLISH_DIR="${PUBLISH_DIR:-publish}"
VERSION="${VERSION:-}"
RUNTIME_ID="${RUNTIME_ID:-}"

cd "$(dirname "${BASH_SOURCE[0]}")"

echo "== dotnet SDK =="
dotnet --version

echo "== restore =="
dotnet restore

echo "== validate: code style (dotnet format) =="
dotnet format --verify-no-changes --no-restore

VERSION_ARGS=()
if [[ -n "$VERSION" ]]; then
  echo "== version: stamping build with $VERSION =="
  VERSION_ARGS=(-p:Version="$VERSION")
fi

echo "== validate: build with warnings as errors ($CONFIGURATION) =="
dotnet build --configuration "$CONFIGURATION" --no-restore -warnaserror "${VERSION_ARGS[@]}"

echo "== publish ($CONFIGURATION -> $PUBLISH_DIR) =="
rm -rf "$PUBLISH_DIR"
if [[ -n "$RUNTIME_ID" ]]; then
  echo "== publish: self-contained, runtime $RUNTIME_ID =="
  dotnet publish --configuration "$CONFIGURATION" --runtime "$RUNTIME_ID" --self-contained true \
    --output "$PUBLISH_DIR" "${VERSION_ARGS[@]}"
else
  dotnet publish --configuration "$CONFIGURATION" --no-build --output "$PUBLISH_DIR" "${VERSION_ARGS[@]}"
fi

echo "== done =="
echo "Build output: $PUBLISH_DIR/Lampshade.exe"
