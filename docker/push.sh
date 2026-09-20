#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck disable=SC1091
set -a
source "$ROOT/docker/tcr.env"
set +a

IMAGE="${TCR_REGISTRY}/${TCR_NAMESPACE}/${TCR_REPOSITORY}:${TCR_TAG}"
STAMP_IMAGE="${TCR_REGISTRY}/${TCR_NAMESPACE}/${TCR_REPOSITORY}:$(date +%Y%m%d-%H%M%S)"

echo "Login: ${TCR_REGISTRY}  user=${TCR_USERNAME}  region=${TCR_REGION}"
if [[ -n "${TCR_PASSWORD:-}" ]]; then
  printf '%s' "$TCR_PASSWORD" | docker login "$TCR_REGISTRY" --username "$TCR_USERNAME" --password-stdin
else
  docker login "$TCR_REGISTRY" --username "$TCR_USERNAME"
fi

echo "Build ${IMAGE}"
docker build --platform linux/amd64 -t "$IMAGE" -t "$STAMP_IMAGE" "$ROOT"

echo "Push ${IMAGE}"
docker push "$IMAGE"
docker push "$STAMP_IMAGE"

echo "Done."
echo "  ${IMAGE}"
echo "  ${STAMP_IMAGE}"
