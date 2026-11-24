#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

fail() {
  printf 'Error: %s\n' "$*" >&2
  exit 1
}

for command in curl docker setsid; do
  command -v "$command" >/dev/null || fail "Install $command before starting (Linux/WSL)."
done

for file in .env backend/.env frontend/.env.local; do
  [[ -f "$file" ]] || fail "Missing $file. Follow the first-run setup in README.md; existing settings are never overwritten."
done

node_version=$(tr -d '[:space:]' < .nvmrc)
if [[ "$(node --version 2>/dev/null || true)" != "v$node_version" ]]; then
  node_bin="${NVM_DIR:-$HOME/.nvm}/versions/node/v$node_version/bin"
  [[ -x "$node_bin/node" ]] || fail "Install Node $node_version with nvm install, or put it on PATH."
  export PATH="$node_bin:$PATH"
fi
command -v npm >/dev/null || fail "npm is missing; install the pinned Node distribution."

sdk_version=$(node -p "require('./global.json').sdk.version")
if [[ "$(dotnet --version 2>/dev/null || true)" != "$sdk_version" ]]; then
  dotnet_root="${DOTNET_ROOT:-$HOME/.dotnet}"
  [[ -x "$dotnet_root/dotnet" ]] || fail "Install .NET SDK $sdk_version and set DOTNET_ROOT/PATH (see README.md)."
  export DOTNET_ROOT="$dotnet_root"
  export PATH="$DOTNET_ROOT:$PATH"
  [[ "$(dotnet --version 2>/dev/null || true)" == "$sdk_version" ]] ||
    fail "Install .NET SDK $sdk_version in DOTNET_ROOT, or put the correct installation on PATH."
fi

[[ -f frontend/node_modules/next/package.json ]] ||
  fail "Frontend dependencies are missing. Run (cd frontend && npm ci)."
for project in backend/src/*/*.csproj; do
  [[ -f "$(dirname "$project")/obj/project.assets.json" ]] ||
    fail "Backend dependencies are missing. Run dotnet restore --locked-mode with the pinned SDK."
done

api_url=$(
  . backend/.env
  printf '%s' "${ASPNETCORE_URLS:-http://localhost:5080}"
)

# Reject mismatched URLs and occupied ports before starting any services.
node --env-file=frontend/.env.local --input-type=module - "$api_url" <<'NODE'
import net from "node:net";

const api = process.argv[2];
let url;
try {
  url = new URL(api);
} catch {
  console.error("Error: ASPNETCORE_URLS must be one loopback HTTP origin.");
  process.exit(1);
}
if (url.protocol !== "http:" || !["localhost", "127.0.0.1", "[::1]"].includes(url.hostname)
    || url.origin !== api || process.env.NEXT_PUBLIC_API_BASE_URL !== api) {
  console.error("Error: Set ASPNETCORE_URLS in backend/.env and NEXT_PUBLIC_API_BASE_URL in frontend/.env.local to the same loopback HTTP origin (default http://localhost:5080).");
  process.exit(1);
}
for (const port of new Set([3000, Number(url.port || 80)])) {
  for (const host of ["127.0.0.1", "::1"]) {
    const server = net.createServer();
    try {
      await new Promise((resolve, reject) => {
        server.once("error", reject);
        server.listen({ host, port, ipv6Only: true }, resolve);
      });
      await new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
    } catch (error) {
      if (host === "::1" && ["EAFNOSUPPORT", "EADDRNOTAVAIL"].includes(error.code)) continue;
      console.error(`Error: Cannot use local port ${port} (${error.code}). Stop the conflicting process yourself; this launcher will not stop existing services.`);
      process.exit(1);
    }
  }
}
NODE

docker info >/dev/null 2>&1 || fail "Docker is not reachable. Start Docker and check docker info."
docker compose version >/dev/null || fail "Install Docker Compose v2.20+."
docker compose config --quiet

pids=()
cleanup() {
  local status=$?
  trap - EXIT INT TERM
  if (( ${#pids[@]} )); then
    printf '\nStopping the frontend and API started by this launcher...\n'
    for pid in "${pids[@]}"; do
      kill -TERM -- "-$pid" 2>/dev/null || true
    done
    for ((attempt = 0; attempt < 10; attempt++)); do
      local running=false
      for pid in "${pids[@]}"; do
        if kill -0 -- "-$pid" 2>/dev/null; then running=true; fi
      done
      if [[ "$running" == false ]]; then break; fi
      sleep 1
    done
    for pid in "${pids[@]}"; do
      if kill -0 -- "-$pid" 2>/dev/null; then
        printf 'Process group %s did not stop; forcing shutdown.\n' "$pid" >&2
        kill -KILL -- "-$pid" 2>/dev/null || true
      fi
      wait "$pid" 2>/dev/null || true
    done
  fi
  printf 'PostgreSQL is left running; its data is unchanged. To stop it: docker compose stop postgres\n'
  exit "$status"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

printf 'Starting PostgreSQL...\n'
docker compose up -d --wait --wait-timeout 60 postgres
printf 'Starting API (.NET %s) and frontend (Node %s)...\n' "$sdk_version" "$node_version"

setsid bash -c 'set -a; . backend/.env; set +a; exec dotnet run --project backend/src/TradingAssistant.Api --no-launch-profile --no-restore' &
pids+=("$!")
setsid bash -c 'cd frontend; exec npm run dev -- --port 3000' &
pids+=("$!")

check_processes() {
  for pid in "${pids[@]}"; do
    if ! kill -0 "$pid" 2>/dev/null; then
      local status=0
      wait "$pid" || status=$?
      fail "An application process exited (status $status). See its output above; stopping the other service."
    fi
  done
}

wait_ready() {
  local name=$1 url=$2 expected=${3:-} body
  for ((attempt = 0; attempt < 120; attempt++)); do
    check_processes
    if body=$(curl --noproxy '*' --fail --silent --max-time 2 "$url"); then
      if [[ -z "$expected" || "$body" == "$expected" ]]; then return; fi
    fi
    sleep 1
  done
  fail "$name did not become ready. Check the service output and local configuration in README.md."
}

wait_ready "API health" "$api_url/api/v1/health" '{"status":"healthy"}'
wait_ready "Database readiness (check backend/.env matches Compose)" "$api_url/api/v1/ready" '{"status":"ready"}'
wait_ready "Frontend" "http://localhost:3000"

printf '\nFrontend: http://localhost:3000\nAPI:      %s\nReady - press Ctrl+C to stop the frontend and API.\n' "$api_url"
while true; do
  check_processes
  sleep 1
done
