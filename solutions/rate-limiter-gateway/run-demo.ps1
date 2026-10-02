#!/usr/bin/env pwsh
# Self-contained end-to-end demo: starts a downstream + 3 gateway instances sharing ONE embedded
# Zaris store, fires concurrent load, and proves the limit holds globally across the instances.
$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot
try {
    dotnet build -c Release | Out-Host
    dotnet run -c Release --no-build --project src/LoadDriver
} finally {
    Pop-Location
}
