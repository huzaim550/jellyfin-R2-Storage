#!/usr/bin/env bash
# Builds the plugin zip and a Jellyfin repository manifest with a real sourceUrl.
#
# Usage:
#   BASE_URL=https://media.example.com/cloudbucket scripts/build-repo.sh
#   scripts/build-repo.sh https://media.example.com/cloudbucket
#
# Upload the resulting build/Jellyfin.Plugin.CloudBucket_<version>.zip and
# build/manifest.json to the same BASE_URL, then add BASE_URL/manifest.json as a
# repository in Jellyfin (Dashboard -> Plugins -> Repositories).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/Jellyfin.Plugin.CloudBucket/Jellyfin.Plugin.CloudBucket.csproj"
OUT="$ROOT/build"
BASE_URL="${BASE_URL:-${1:-}}"

if [ -z "$BASE_URL" ]; then
    echo "error: set BASE_URL, e.g. BASE_URL=https://media.example.com/cloudbucket $0" >&2
    exit 1
fi

BASE_URL="${BASE_URL%/}"
VERSION="$(grep -oP '(?<=<Version>)[^<]+' "$PROJECT" | head -1)"
ZIP_NAME="Jellyfin.Plugin.CloudBucket_${VERSION}.zip"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

echo "Building version ${VERSION}..."
dotnet publish "$PROJECT" -c Release -o "$OUT/publish" --nologo -v quiet

rm -f "$OUT/$ZIP_NAME"
(
    cd "$OUT/publish"
    zip -q "$OUT/$ZIP_NAME" \
        Jellyfin.Plugin.CloudBucket.dll \
        Jellyfin.Plugin.CloudBucket.deps.json \
        AWSSDK.S3.dll \
        AWSSDK.Core.dll
)

CHECKSUM="$(md5sum "$OUT/$ZIP_NAME" | cut -d' ' -f1)"

"$ROOT/scripts/generate-manifest.sh" "$BASE_URL" "$ZIP_NAME" "$CHECKSUM" "$VERSION" > "$OUT/manifest.json"

echo
echo "Done."
echo "  zip:      $OUT/$ZIP_NAME"
echo "  manifest: $OUT/manifest.json"
echo "  checksum: $CHECKSUM"
echo
echo "Upload BOTH files to: ${BASE_URL}/"
echo "Repository URL to add in Jellyfin: ${BASE_URL}/manifest.json"
