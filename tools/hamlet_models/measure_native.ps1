param(
    [Parameter(Mandatory=$true)][int]$HamletProcessId,
    [ValidateRange(1,15)][int]$Seconds=3,
    [string]$Log=(Join-Path $env:USERPROFILE 'AppData\LocalLow\RedHook\Darkest Dungeon II\Player.log')
)
$ErrorActionPreference='Stop'
$script:hamletRendered=$false
$hamletProcess=Get-CimInstance Win32_Process -Filter "ProcessId = $HamletProcessId"
if (!$hamletProcess -or $hamletProcess.CommandLine -notmatch '-dd3-hamlet3d-preview') {
    throw 'Measure only the owned unsaved Hamlet preview process.'
}
function AllocationCount {
    $hamletStream=[IO.File]::Open($Log,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    $hamletReader=[IO.StreamReader]::new($hamletStream)
    try {
        $hamletCount=0
        while ($null -ne ($hamletLine=$hamletReader.ReadLine())) {
            if ($hamletLine -match 'RTHandleAccessWrapper.*Reallocated') { $hamletCount++ }
            if ($hamletLine -match '\[hamlet 3d\] built') { $script:hamletRendered=$true }
        }
        return $hamletCount
    } finally { $hamletReader.Dispose() }
}
$hamletBefore=AllocationCount
if (!$script:hamletRendered) { throw 'The 3D Hamlet must be fully rendered before measuring.' }
$hamletTimer=[Diagnostics.Stopwatch]::StartNew()
Start-Sleep -Seconds $Seconds
$hamletAfter=AllocationCount
Get-Process -Id $HamletProcessId -ErrorAction Stop | Out-Null
$hamletElapsed=$hamletTimer.Elapsed.TotalSeconds
$hamletDelta=$hamletAfter-$hamletBefore
if ($hamletDelta -lt 0) { throw 'Player.log was replaced during measurement.' }
[ordered]@{ process=$HamletProcessId; seconds=[Math]::Round($hamletElapsed,3); reallocations=$hamletDelta;
    reallocationsPerSecond=[Math]::Round($hamletDelta/$hamletElapsed,2); pass=($hamletDelta -le 4) } | ConvertTo-Json
if ($hamletDelta -gt 4) { exit 1 }
