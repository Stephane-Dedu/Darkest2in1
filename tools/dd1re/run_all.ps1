# Rebuilds the private DD1 code map: Ghidra project, names, raw facts, per-function decompilation, map.
# Everything is written under -Root (default D:\dd1-decomp), outside the repo. Never commit its contents.
#   powershell -File tools\dd1re\run_all.ps1                  # all steps
#   powershell -File tools\dd1re\run_all.ps1 -From decomp     # restart at a step
#   powershell -File tools\dd1re\run_all.ps1 -From decomp -Resume   # finish an interrupted decompile
# Steps: import (copy exe, Ghidra auto-analysis), classes (virtual functions under their class), facts (string names +
# raw facts), decomp (one C file per function), map (build_map.py). See tools/dd1re/README.md.
param(
    [string]$Root = 'D:\dd1-decomp',
    [string]$Ghidra = 'D:\re-tools\ghidra_12.1.4_PUBLIC',
    [string]$Jdk = 'C:\Program Files\Microsoft\jdk-21.0.12.101-hotspot',
    [string]$Dd1 = 'C:\Program Files (x86)\Steam\steamapps\common\DarkestDungeon',
    [string]$Python = "$env:LOCALAPPDATA\Programs\Python\Python311\python.exe",
    [ValidateSet('import', 'classes', 'facts', 'decomp', 'map')][string]$From = 'import',
    [string]$MaxMem = '4G',
    [int]$Threads = 4,       # parallel decompilers; each is a native process, keep it low on a 16 GB machine
    [switch]$Resume          # with -From decomp: keep the C files already written by an interrupted run
)
$ErrorActionPreference = 'Stop'
$steps = 'import', 'classes', 'facts', 'decomp', 'map'
$start = [array]::IndexOf($steps, $From)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:JAVA_HOME = $Jdk
$env:PATH = "$Jdk\bin;$env:PATH"
$env:GHIDRA_HEADLESS_MAXMEM = $MaxMem
$headless = Join-Path $Ghidra 'support\analyzeHeadless.bat'
$project = Join-Path $Root 'ghidra'
$logs = Join-Path $Root 'logs'
New-Item -ItemType Directory -Force $project, $logs, (Join-Path $Root 'bin'), (Join-Path $Root 'raw') | Out-Null

function Invoke-Headless([string]$Name, [string[]]$HeadlessArgs) {
    # The .bat breaks on paths with parentheses, so every path passed here lives under $Root.
    # Start-Process keeps Ghidra's output bytes as they are (PowerShell 5.1 '*>' writes UTF-16).
    Write-Host "== $Name"
    $console = Join-Path $logs "$Name.console.txt"
    $argLine = (@($project, 'DD1') + $HeadlessArgs + @('-log', (Join-Path $logs "$Name.log")) |
        ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } }) -join ' '
    $p = Start-Process -FilePath $headless -ArgumentList $argLine -NoNewWindow -Wait -PassThru `
        -RedirectStandardOutput $console -RedirectStandardError (Join-Path $logs "$Name.stderr.txt")
    if ($p.ExitCode -ne 0) { throw "$Name failed ($($p.ExitCode)); see $console" }
    Select-String -Path $console -Pattern 'Dd1|REPORT|ERROR' | Select-Object -Last 15 | ForEach-Object { $_.Line }
}

if ($start -le 0) {
    $exe = Join-Path $Root 'bin\Darkest.exe'
    Copy-Item -LiteralPath (Join-Path $Dd1 '_windows\win64\Darkest.exe') -Destination $exe -Force
    Write-Host ('Darkest.exe sha256 ' + (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant())
    Invoke-Headless 'import' @('-import', $exe, '-overwrite', '-max-cpu', '10')
}
if ($start -le 1) {
    # Ghidra's RecoverClassesFromRTTIScript stops on this exe (folded lambda RTTI); Dd1NameVirtuals replaces it.
    Invoke-Headless 'classes' @('-process', 'Darkest.exe', '-noanalysis', '-scriptPath', $here,
        '-postScript', 'Dd1NameVirtuals.java', (Join-Path $Root 'raw'))
}
if ($start -le 2) {
    $raw = Join-Path $Root 'raw'
    Invoke-Headless 'facts' @('-process', 'Darkest.exe', '-noanalysis', '-scriptPath', $here,
        '-postScript', 'Dd1NameFromStrings.java', $raw, '-postScript', 'Dd1ExportFacts.java', $raw)
}
if ($start -le 3) {
    $decomp = Join-Path $Root 'decomp'
    if ((Test-Path -LiteralPath $decomp) -and -not ($Resume -and $start -eq 3)) {
        Remove-Item -LiteralPath $decomp -Recurse -Force
    }
    Invoke-Headless 'decomp' @('-process', 'Darkest.exe', '-noanalysis', '-readOnly', '-scriptPath', $here,
        '-postScript', 'Dd1ExportDecomp.java', $decomp, $Threads)
}
if ($start -le 4) {
    Write-Host '== map'
    $env:PYTHONUTF8 = '1'
    & $Python (Join-Path $here 'build_map.py') --root $Root --dd1 $Dd1
    if ($LASTEXITCODE -ne 0) { throw 'build_map.py failed' }
}
