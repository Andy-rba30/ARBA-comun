#!/usr/bin/env bash
# Compila el código común contra Revit 2021..2027 (net48 / net8.0-windows / net10.0-windows).
set -u
cd "$(dirname "$0")"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
fail=0
for v in ${REVIT_VERSIONS:-2021 2022 2023 2024 2025 2026 2027}; do
  printf '=== Revit %s: ' "$v"
  if out=$(dotnet build Arba.Comun.Check.csproj -c Release -p:RevitVersion="$v" -v q -nologo 2>&1); then
    echo "OK ($(echo "$out" | grep -oE '[0-9]+ Warning' | head -1 | tr -d '\n'))"
  else
    echo "FALLO"; echo "$out" | grep -E 'error|warning' | sort -u | head -40; fail=1
  fi
done
exit $fail
