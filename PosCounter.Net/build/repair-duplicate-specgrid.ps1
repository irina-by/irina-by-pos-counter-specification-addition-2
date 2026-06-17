# Repair: SpecGridService pasted twice in one file, or embedded in TableGrid.cs
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$repaired = $false

function Repair-DoubledFile {
    param(
        [string]$Path,
        [string]$ClassPattern,
        [int]$ExpectedLinesMax
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.AddRange([string[]](Get-Content -LiteralPath $Path -Encoding UTF8))
    $classLines = New-Object System.Collections.Generic.List[int]
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $ClassPattern) {
            [void]$classLines.Add($i)
        }
    }

    if ($classLines.Count -le 1 -and $lines.Count -le $ExpectedLinesMax) {
        return $false
    }

    if ($classLines.Count -le 1 -and $lines.Count -gt $ExpectedLinesMax) {
        Write-Host "[REPAIR] $($Path): $($lines.Count) lines, truncating to $ExpectedLinesMax"
        $lines = $lines.GetRange(0, $ExpectedLinesMax)
    }
    elseif ($classLines.Count -gt 1) {
        $secondLine = $classLines[1]
        $cut = $secondLine
        for ($j = $secondLine - 1; $j -ge 0; $j--) {
            if ($lines[$j] -match '^using\s+') {
                $cut = $j
                break
            }
        }

        Write-Host "[REPAIR] $($Path): class count=$($classLines.Count), keeping lines 1..$cut"
        if ($cut -le 0) {
            Write-Host "  [FAIL] cannot find safe cut point"
            return $false
        }

        $lines = $lines.GetRange(0, $cut)
    }

    while ($lines.Count -gt 0 -and [string]::IsNullOrWhiteSpace($lines[$lines.Count - 1])) {
        $lines.RemoveAt($lines.Count - 1)
    }

    $backup = "$Path.bak-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item -LiteralPath $Path -Destination $backup -Force
    Set-Content -LiteralPath $Path -Value $lines -Encoding UTF8
    Write-Host "  backup: $backup"
    Write-Host "  new lines: $($lines.Count)"
    return $true
}

function Repair-TableGridEmbedded {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $text = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $embedded = ([regex]::Matches($text, 'internal\s+static\s+class\s+SpecGridService')).Count
    if ($embedded -eq 0) {
        return $false
    }

    $idx = $text.IndexOf('internal static class SpecGridService')
    if ($idx -lt 0) {
        $idx = $text.IndexOf('internal sealed class SpecPickResult')
    }

    if ($idx -lt 0) {
        Write-Host "[FAIL] embedded SpecGridService found but cut point unknown"
        return $false
    }

    $cutText = $text.Substring(0, $idx)
    $lastClose = $cutText.LastIndexOf("}`r`n}")
    if ($lastClose -lt 0) {
        $lastClose = $cutText.LastIndexOf("}`n}")
    }

    if ($lastClose -lt 0) {
        Write-Host "[FAIL] cannot find namespace close before embedded block"
        return $false
    }

    $newText = $cutText.Substring(0, $lastClose + 3)
    $backup = "$Path.bak-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item -LiteralPath $Path -Destination $backup -Force
    Set-Content -LiteralPath $Path -Value $newText -Encoding UTF8 -NoNewline
    Write-Host "[REPAIR] TableGrid.cs: removed embedded SpecGridService block"
    Write-Host "  backup: $backup"
    return $true
}

Write-Host "Repair duplicate SpecGrid sources in: $root"
Write-Host ""

$svcPath = Join-Path $root "SpecGrid\SpecGridService.cs"
if (Repair-DoubledFile -Path $svcPath -ClassPattern 'internal\s+static\s+class\s+SpecGridService' -ExpectedLinesMax 2500) {
    $repaired = $true
}

$extraSvc = Get-ChildItem -LiteralPath $root -Recurse -Filter "SpecGridService*.cs" |
    Where-Object { $_.FullName -notmatch '\\obj\\' -and $_.FullName -notmatch '\\bin\\' }
if ($extraSvc.Count -gt 1) {
    Write-Host "[REPAIR] multiple SpecGridService*.cs files:"
    foreach ($f in $extraSvc | Sort-Object FullName | Select-Object -Skip 1) {
        $bak = "$($f.FullName).removed-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        Move-Item -LiteralPath $f.FullName -Destination $bak -Force
        Write-Host "  moved: $($f.FullName) -> $bak"
        $repaired = $true
    }
}

$tablePath = Join-Path $root "SpecGrid\TableGrid.cs"
if (Repair-TableGridEmbedded -Path $tablePath) {
    $repaired = $true
}

Write-Host ""
if ($repaired) {
    Write-Host "[OK] Repair done. Run verify-no-duplicate-sources.ps1 then rebuild."
}
else {
    Write-Host "[OK] Nothing to repair in this folder (or copy fresh PosCounter.Net from Yandex)."
}
