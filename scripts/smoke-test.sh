#!/usr/bin/env bash
# Installs the packed tool for each supported framework and checks that it actually serves.
#
# `--help` alone is not enough: it returns before Kestrel, the static assets, or the native
# SQLite library are ever touched, so a broken payload would still exit 0. Requesting the
# root page exercises all three -- resolving the page's injected IRecentProjectsStore runs
# SqliteRecentProjectsStore's static constructor, which loads libe_sqlite3.
set -euo pipefail

version="${1:?usage: smoke-test.sh <package-version>}"
package_source="${2:-artifacts/packages}"
# Overridable so a machine missing one of the runtimes can still exercise the rest.
frameworks="${SMOKE_FRAMEWORKS:-net8.0 net9.0 net10.0}"

port=54100
for framework in $frameworks; do
  port=$((port + 1))
  tool_path="artifacts/smoke/$framework"

  dotnet tool install \
    --tool-path "$tool_path" \
    --add-source "$package_source" \
    --framework "$framework" \
    SecretWorkbench \
    --version "$version"

  "$tool_path/secret-workbench" --help > /dev/null
  echo "$framework: --help ok"

  "$tool_path/secret-workbench" --port "$port" --no-open > "$tool_path/serve.log" 2>&1 &
  server_pid=$!
  # shellcheck disable=SC2064
  trap "kill $server_pid 2>/dev/null || true" EXIT

  status=""
  for _ in $(seq 1 60); do
    if ! kill -0 "$server_pid" 2>/dev/null; then
      echo "$framework: the tool exited during startup" >&2
      cat "$tool_path/serve.log" >&2
      exit 1
    fi
    status="$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$port/" || true)"
    [ "$status" = "200" ] && break
    sleep 1
  done

  if [ "$status" != "200" ]; then
    echo "$framework: GET / returned '${status:-no response}', expected 200" >&2
    cat "$tool_path/serve.log" >&2
    exit 1
  fi
  echo "$framework: GET / -> 200"

  # A hostname the tool does not own must be rejected, or a web page could reach it by
  # pointing its own DNS at loopback and read every secret.
  rebind_status="$(curl -s -o /dev/null -w '%{http_code}' -H 'Host: rebind.example.com' "http://127.0.0.1:$port/" || true)"
  if [ "$rebind_status" != "400" ]; then
    echo "$framework: foreign Host header returned '$rebind_status', expected 400" >&2
    exit 1
  fi
  echo "$framework: foreign Host header -> 400"

  kill "$server_pid" 2>/dev/null || true
  wait "$server_pid" 2>/dev/null || true
  trap - EXIT
done

echo "All frameworks smoke-tested."
