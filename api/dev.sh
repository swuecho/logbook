#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

source ./env.sh

dotnet restore
dotnet watch run

