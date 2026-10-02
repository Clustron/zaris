#!/usr/bin/env pwsh
# Builds, runs the full test suite, then runs the end-to-end demo and tees its output to docs/demo-output.txt.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

Write-Host "`n== dotnet test ==" -ForegroundColor Cyan
dotnet test (Join-Path $root 'tests/LiveBetting.Tests/LiveBetting.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "tests failed" }

Write-Host "`n== dotnet run (demo) ==" -ForegroundColor Cyan
$out = Join-Path $root 'docs/demo-output.txt'
dotnet run --project (Join-Path $root 'src/LiveBetting.Demo/LiveBetting.Demo.csproj') -c Release | Tee-Object -FilePath $out
exit $LASTEXITCODE
