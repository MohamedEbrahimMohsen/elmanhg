#!/usr/bin/env bash
# Prepares a Claude Code cloud container to build and test Elmanhg exactly like CI.
# Idempotent: safe to re-run after the container restarts.
#  - starts the Docker daemon (Testcontainers needs it for the API tests)
#  - copies the .NET SDK pinned by global.json out of the official image (the dotnet download host is blocked)
set -euo pipefail

if ! docker info >/dev/null 2>&1; then
  (dockerd >/tmp/dockerd.log 2>&1 &)
  for _ in $(seq 1 30); do docker info >/dev/null 2>&1 && break; sleep 1; done
fi

want=$(python3 -c "import json;print(json.load(open('$(dirname "$0")/../global.json'))['sdk']['version'])")
if [ "$(dotnet --version 2>/dev/null || true)" != "$want" ]; then
  docker pull -q mcr.microsoft.com/dotnet/sdk:10.0 >/dev/null
  cid=$(docker create mcr.microsoft.com/dotnet/sdk:10.0)
  rm -rf /opt/dotnet && docker cp "$cid:/usr/share/dotnet" /opt/dotnet && docker rm "$cid" >/dev/null
  ln -sf /opt/dotnet/dotnet /usr/local/bin/dotnet
fi
echo "docker: $(docker info -f '{{.ServerVersion}}')  dotnet: $(dotnet --version)"
