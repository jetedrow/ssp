#!/usr/bin/env bash
# Makes sure a .NET SDK matching global.json is on PATH.
#
# Claude Code web sessions run in a fresh container each time, and Microsoft's SDK download host
# (builds.dotnet.microsoft.com) is blocked by the network policy there. The Ubuntu archive carries
# the same SDK, so install from apt instead. On a developer machine that already has the SDK this
# exits immediately and changes nothing.

set -euo pipefail

if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  exit 0
fi

# Only attempt an install where we can actually do one unattended.
if [ "$(id -u)" -ne 0 ] || ! command -v apt-get >/dev/null 2>&1; then
  echo "No .NET 10 SDK found. Install it from https://dotnet.microsoft.com/download to build this repo." >&2
  exit 0
fi

echo "Installing the .NET 10 SDK from the distribution archive..."

# The image ships with a stale package index, which makes the first install 404. Refresh it first.
apt-get update -qq
apt-get install -y -qq dotnet-sdk-10.0

dotnet --list-sdks
