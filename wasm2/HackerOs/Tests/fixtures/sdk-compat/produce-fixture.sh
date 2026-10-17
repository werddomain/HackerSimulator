#!/usr/bin/env bash
# ADR 0098 §8, ADR 0114: builds the SDK compatibility fixture (src/Contoso.Sdk1Compat) against the
# HackerOS packages of a local feed, the way another repository would, and writes its package to
# packages/. Run once per major version (1.0.0 today); the committed package is never rebuilt, so
# the tests keep loading an app compiled against the old SDK into the current OS.
#
#   Tests/fixtures/sdk-compat/produce-fixture.sh [--feed <dir>]   (default: artifacts/local-nupkg)
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
feed="$root/artifacts/local-nupkg"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --feed) feed="$(cd "$2" && pwd)"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

version="$(sed -n 's:.*<HackerOsVersion>\(.*\)</HackerOsVersion>.*:\1:p' "$root/build/Versions.props")"
[[ -f "$feed/HackerOs.AppSdk.$version.nupkg" ]] || { echo "No HackerOs.AppSdk.$version.nupkg in $feed: run build/pack-local.sh first." >&2; exit 1; }

# Built outside the repository: no Directory.Build.* of HackerOS, an isolated NuGet cache.
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cp -r "$here/src/." "$work/"
cat > "$work/nuget.config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config><add key="globalPackagesFolder" value="$work/packages" /></config>
  <packageSources>
    <clear />
    <add key="hackeros-local" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="hackeros-local"><package pattern="HackerOs.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
XML

mkdir -p "$here/packages"
MSBUILDDISABLENODEREUSE=1 dotnet pack "$work/Contoso.Sdk1Compat/Contoso.Sdk1Compat.csproj" -c Release \
  -p:HackerOsVersion="$version" -p:ContinuousIntegrationBuild=true -o "$here/packages"
echo "Fixture: $here/packages/Contoso.Sdk1Compat.$version.nupkg"
