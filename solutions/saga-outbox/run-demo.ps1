#!/usr/bin/env pwsh
# Self-contained end-to-end demo: one process, one embedded Zaris store. Runs the happy path,
# a payment/compensation failure, a crash-and-recovery, and an at-least-once relay with consumer
# dedupe — printing the state and events that prove no lost or duplicated effects.
$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot
try {
    dotnet build -c Release | Out-Host
    dotnet run -c Release --no-build --project src/SagaOutbox.Demo
} finally {
    Pop-Location
}
