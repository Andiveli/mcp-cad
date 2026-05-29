# Build script for InventorBridge.exe
# Requires: .NET Framework 4.8 SDK, Autodesk Inventor installed
# Usage: .\build.ps1

$ErrorActionPreference = "Stop"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$inventorDir = "C:\Program Files\Autodesk\Inventor 2025\Bin"
$source = Join-Path $PSScriptRoot "InventorBridge\Program.cs"

Write-Host "Building InventorBridge.exe..."

# Find Inventor interop DLL
$interop = Get-ChildItem -Path $inventorDir -Filter "Autodesk.Inventor.Interop.dll" -Recurse | Select-Object -First 1
if (-not $interop) {
    Write-Error "Autodesk.Inventor.Interop.dll not found in $inventorDir"
    exit 1
}

Write-Host "  Source : $source"
Write-Host "  Interop: $($interop.FullName)"

& $csc /target:exe /out:"InventorBridge.exe" /reference:$($interop.FullName) $source

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build OK — InventorBridge.exe"
} else {
    Write-Error "Build failed (exit code $LASTEXITCODE)"
    exit 1
}
