#!/bin/sh
set -e

MONGO_HOST="${MONGO_HOST:-}"
MONGO_PORT="${MONGO_PORT:-27017}"

if [ -n "$MONGO_HOST" ] && [ "$MONGO_HOST" != "127.0.0.1" ] && [ "$MONGO_HOST" != "localhost" ]; then
  echo "Waiting for MongoDB at ${MONGO_HOST}:${MONGO_PORT}..."
  i=0
  while [ "$i" -lt 60 ]; do
    if socat -u TCP:"${MONGO_HOST}:${MONGO_PORT}",connect-timeout=1 STDOUT >/dev/null 2>&1; then
      break
    fi
    i=$((i + 1))
    sleep 1
  done
  if [ "$i" -eq 60 ]; then
    echo "MongoDB not reachable at ${MONGO_HOST}:${MONGO_PORT}" >&2
    exit 1
  fi
  echo "Forwarding 127.0.0.1:27017 -> ${MONGO_HOST}:${MONGO_PORT}"
  socat TCP-LISTEN:27017,bind=127.0.0.1,fork,reuseaddr TCP:"${MONGO_HOST}:${MONGO_PORT}" &
fi

cd /app/Bin
exec dotnet App.dll "$@"
