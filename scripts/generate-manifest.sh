#!/usr/bin/env bash
# Emits a Jellyfin repository manifest to stdout.
#
# Usage: generate-manifest.sh <base_url> <zip_filename> <checksum> <version>
set -euo pipefail

BASE_URL="${1%/}"
ZIP_NAME="$2"
CHECKSUM="$3"
VERSION="$4"
TIMESTAMP="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

cat <<EOF
[
  {
    "guid": "7f3c9d2a-5b61-4e8f-a0c4-9d2b6e1f8a73",
    "name": "Cloud Bucket",
    "description": "Mirrors an S3-compatible bucket (Cloudflare R2, Backblaze B2, ...) into a separate Jellyfin library using .strm files and can redirect playback straight to the bucket.",
    "overview": "Serve media from a Cloudflare R2 / S3 bucket as a separate Jellyfin library.",
    "owner": "huzaim550",
    "category": "General",
    "versions": [
      {
        "version": "${VERSION}",
        "changelog": "See the repository commits.",
        "targetAbi": "12.0.0.0",
        "sourceUrl": "${BASE_URL}/${ZIP_NAME}",
        "checksum": "${CHECKSUM}",
        "timestamp": "${TIMESTAMP}"
      }
    ]
  }
]
EOF
