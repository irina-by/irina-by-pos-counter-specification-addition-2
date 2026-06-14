# Pre-build check: exactly one PaletteHost.cs (common copy-merge error).
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

Write-Host "Project root: $root"
Write-Host ""

$paletteFiles = Get-ChildItem -LiteralPath $root -Recurse -Filter "PaletteHost*.cs" |
    Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' }

Write-Host "PaletteHost*.cs files (excluding bin/obj):"
if ($paletteFiles.Count -eq 0) {
    Write-Host "  [FAIL] PaletteHost.cs not found"
    exit 1
}

foreach ($f in $paletteFiles) {
    $text = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8
    $classCount = ([regex]::Matches($text, 'public\s+static\s+class\s+PaletteHost')).Count
    $lines = (Get-Content -LiteralPath $f.FullName).Count
    Write-Host "  $($f.FullName)"
    Write-Host "    lines=$lines class_PaletteHost=$classCount"
    if ($classCount -ne 1) {
        Write-Host "    [FAIL] file must contain exactly one 'public static class PaletteHost'"
    }
}

Write-Host ""
if ($paletteFiles.Count -gt 1) {
    Write-Host "[FAIL] Found $($paletteFiles.Count) files. Keep only one PaletteHost.cs"
    Write-Host "Fix: rename PosCounter.Net to PosCounter.Net_old, copy fresh folder from Yandex."
    exit 1
}

$only = $paletteFiles[0]
$onlyText = Get-Content -LiteralPath $only.FullName -Raw -Encoding UTF8
$onlyClassCount = ([regex]::Matches($onlyText, 'public\s+static\s+class\s+PaletteHost')).Count
if ($onlyClassCount -ne 1) {
    Write-Host "[FAIL] PaletteHost.cs pasted twice inside one file (~657 lines expected)."
    exit 1
}

if (-not ($onlyText -match 'POSC-SINGLE-FILE')) {
    Write-Host "[WARN] Missing POSC-SINGLE-FILE marker - copy may be outdated."
}

Write-Host "[OK] Single PaletteHost.cs, single class. Build in VS: Release x64 net452."
