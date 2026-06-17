# Pre-build check: no duplicate PaletteHost / SpecGridService (common copy-merge error).
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$failed = $false

function Test-SingleClassFile {
    param(
        [string]$Label,
        [string]$Filter,
        [string]$ClassPattern,
        [int]$ExpectedLinesMax,
        [string]$Marker = $null
    )

    Write-Host "${Label} ($Filter):"
    $files = Get-ChildItem -LiteralPath $root -Recurse -Filter $Filter |
        Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' }

    if ($files.Count -eq 0) {
        Write-Host "  [FAIL] file not found"
        return $false
    }

    foreach ($f in $files) {
        $text = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8
        $classCount = ([regex]::Matches($text, $ClassPattern)).Count
        $lines = (Get-Content -LiteralPath $f.FullName).Count
        Write-Host "  $($f.FullName)"
        Write-Host "    lines=$lines class=$classCount"
        if ($classCount -ne 1) {
            Write-Host "    [FAIL] expected exactly one class match: $ClassPattern"
        }
        if ($ExpectedLinesMax -gt 0 -and $lines -gt $ExpectedLinesMax) {
            Write-Host "    [FAIL] too many lines ($lines > $ExpectedLinesMax) - file may be pasted twice"
        }
        if ($Marker -and -not ($text -match [regex]::Escape($Marker))) {
            Write-Host "    [WARN] missing marker '$Marker' - copy may be outdated"
        }
    }

    if ($files.Count -gt 1) {
        Write-Host "  [FAIL] found $($files.Count) files - keep only one"
        return $false
    }

    $only = $files[0]
    $onlyText = Get-Content -LiteralPath $only.FullName -Raw -Encoding UTF8
    $onlyClassCount = ([regex]::Matches($onlyText, $ClassPattern)).Count
    $onlyLines = (Get-Content -LiteralPath $only.FullName).Count
    if ($onlyClassCount -ne 1 -or ($ExpectedLinesMax -gt 0 -and $onlyLines -gt $ExpectedLinesMax)) {
        return $false
    }

    Write-Host "  [OK] single file, single class"
    Write-Host ""
    return $true
}

Write-Host "Project root: $root"
Write-Host ""

if (-not (Test-SingleClassFile -Label "PaletteHost" -Filter "PaletteHost*.cs" -ClassPattern 'public\s+static\s+class\s+PaletteHost' -ExpectedLinesMax 900 -Marker "POSC-SINGLE-FILE")) {
    $failed = $true
}

if (-not (Test-SingleClassFile -Label "SpecGridService" -Filter "SpecGridService*.cs" -ClassPattern 'internal\s+static\s+class\s+SpecGridService' -ExpectedLinesMax 2500 -Marker "POSC-SINGLE-FILE-SVC")) {
    $failed = $true
}

$tableGridPath = Join-Path $root "SpecGrid\TableGrid.cs"
if (Test-Path -LiteralPath $tableGridPath) {
    $tableText = Get-Content -LiteralPath $tableGridPath -Raw -Encoding UTF8
    $embeddedSvc = ([regex]::Matches($tableText, 'internal\s+static\s+class\s+SpecGridService')).Count
    $embeddedPick = ([regex]::Matches($tableText, 'internal\s+sealed\s+class\s+SpecPickResult')).Count
    $tableLines = (Get-Content -LiteralPath $tableGridPath).Count
    Write-Host "TableGrid.cs (no embedded SpecGridService):"
    Write-Host "  $tableGridPath"
    Write-Host "    lines=$tableLines embedded_SpecGridService=$embeddedSvc embedded_SpecPickResult=$embeddedPick"
    if ($embeddedSvc -gt 0 -or $embeddedPick -gt 0) {
        Write-Host "  [FAIL] SpecGridService pasted into TableGrid.cs - remove duplicate block at file end"
        $failed = $true
    }
    elseif ($tableLines -gt 8000) {
        Write-Host "  [FAIL] TableGrid.cs too long ($tableLines) - likely merged with SpecGridService"
        $failed = $true
    }
    else {
        Write-Host "  [OK] no embedded service classes"
    }
    Write-Host ""
}

if ($failed) {
    Write-Host "[FAIL] Duplicate or merged source files detected."
    Write-Host "Fix 1: powershell -ExecutionPolicy Bypass -File build\repair-duplicate-specgrid.ps1"
    Write-Host "Fix 2: rename PosCounter.Net to PosCounter.Net_old, copy fresh PosCounter.Net from Yandex."
    Write-Host "Fix 3: open SpecGridService.cs - if 'using System;' appears twice, delete the second copy."
    exit 1
}

Write-Host "[OK] PaletteHost + SpecGridService + TableGrid checks passed. Build: Release x64 net452."
