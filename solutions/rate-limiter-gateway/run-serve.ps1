#!/usr/bin/env pwsh
# Starts the stack for MANUAL exploration (curl / your own load tool):
#   - the downstream service on :5090
#   - one gateway HOST process running 3 gateway instances (:5081/:5082/:5083) that share ONE
#     embedded Zaris store, so the limit is global across the three ports.
#
# Try it:
#   curl -i http://localhost:5081/api/ping                       # anonymous -> per-IP sliding window (20/10s)
#   for ($i=0; $i -lt 30; $i++) { curl -s -o nul -w "%{http_code} " http://localhost:5081/api/ping }
#   curl -i -H "X-API-Key: k1" http://localhost:5082/api/ping    # api key  -> token bucket (100 burst, 50/s)
#
# Or drive it with the load tool against the running gateways:
#   dotnet run -c Release --project src/LoadDriver -- --target http://localhost:5081,http://localhost:5082,http://localhost:5083
$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot
try {
    dotnet build -c Release | Out-Host
    $down = Start-Process dotnet -ArgumentList "run -c Release --no-build --project src/Downstream" -PassThru
    Start-Sleep -Seconds 2
    $env:GATEWAY_URLS   = "http://localhost:5081,http://localhost:5082,http://localhost:5083"
    $env:ZARIS_CONN     = "zaris://inproc/gateway"
    $env:DOWNSTREAM_URL = "http://localhost:5090"
    Write-Host "Gateways on 5081/5082/5083 share one embedded Zaris store. Ctrl+C to stop."
    dotnet run -c Release --no-build --project src/Gateway
} finally {
    if ($down -and -not $down.HasExited) { $down.Kill() }
    Pop-Location
}
