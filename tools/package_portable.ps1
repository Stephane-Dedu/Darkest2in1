# Pack the modded game folder (DD2, DD1's files beside it, the plugin and the private art folders) into one zip that
# plays wherever it is unpacked, with no DD1 or DD2 install. The mod's Paths settings are written relative to the game
# folder inside the zip. Saves are not packed: they stay in %USERPROFILE%\AppData\LocalLow\RedHook\Darkest Dungeon II.
# Private use only: the zip holds both games' files.
#   powershell -File tools\package_portable.ps1 -Output F:\Darkest2in1.zip
param(
    [Parameter(Mandatory=$true)][string]$Output,
    [string]$GameDir = 'C:\Users\Piral\darkestwithdlc\game',
    [string]$FolderName = 'Darkest2in1'
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
        -Output $Output -GameDir $GameDir -FolderName $FolderName
    if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
    return
}
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$GameDir = (Resolve-Path -LiteralPath $GameDir).Path.TrimEnd('\')
if (Test-Path -LiteralPath $Output -PathType Container) { $Output = Join-Path $Output "$FolderName.zip" }
$Output = [IO.Path]::GetFullPath($Output)
if ($Output.StartsWith($GameDir + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Write the zip outside the game folder.' }
foreach ($need in 'Darkest Dungeon II.exe', 'winhttp.dll', 'BepInEx\plugins\DarkestDungeon3\DarkestDungeon3.dll', 'scripts\map_generator.darkest') {
    if (-not (Test-Path -LiteralPath (Join-Path $GameDir $need))) { throw "$need is missing from $GameDir (the zip needs DD2, BepInEx, the mod and DD1's files in one folder)." }
}
if (Get-Process -Name 'Darkest Dungeon II' -ErrorAction SilentlyContinue) { throw 'Close Darkest Dungeon II first: it locks its files.' }

# The mod's config, with every path inside the game folder made relative ("." for the folder itself).
$cfgName = 'BepInEx\config\piral.darkestdungeon3.cfg'
$cfgText = [IO.File]::ReadAllText((Join-Path $GameDir $cfgName))
$prefix = [regex]::Escape($GameDir)
$cfgText = [regex]::Replace($cfgText, '(?m)^(\w+ = )' + $prefix + '(\r?)$', '$1.$2')
$cfgText = [regex]::Replace($cfgText, '(?m)^(\w+ = )' + $prefix + '\\', '$1')
foreach ($outside in [regex]::Matches($cfgText, '(?m)^\w+ = [A-Za-z]:\\.*$')) {
    Write-Warning "Not inside the game folder, so it will not travel: $($outside.Value.Trim())"
}
$cfgBytes = (New-Object Text.UTF8Encoding($false)).GetBytes($cfgText)

$readme = @'
Darkest2in1, portable copy
==========================
1. Unzip this folder anywhere with about 12 GB free (an SSD loads faster; 7-Zip unpacks faster than Windows does).
2. Start "Darkest Dungeon II.exe" in it. DD1 and DD2 need no install: DD1's files are in this folder, and the mod's
   config (BepInEx\config\piral.darkestdungeon3.cfg) finds them and its art folders relative to it.

Saves stay on each PC, in %USERPROFILE%\AppData\LocalLow\RedHook\Darkest Dungeon II. Copy that folder across to carry
a campaign to another PC.
DD1's two opening films need ffmpeg once (winget install Gyan.FFmpeg). Without it they play as narration over DD1's
title art.
This is a private copy of both games. Keep it in your own storage and do not share it.
'@
$readmeBytes = (New-Object Text.UTF8Encoding($false)).GetBytes($readme.Replace("`r`n", "`n").Replace("`n", "`r`n"))

# Logs, BepInEx's rebuildable cache and config backups stay behind.
$skip = '^BepInEx\\(LogOutput\.log.*|cache\\.*|config\\.*\.bak.*)$'
$files = New-Object Collections.Generic.List[IO.FileInfo]
$total = 0L
foreach ($path in [IO.Directory]::EnumerateFiles($GameDir, '*', [IO.SearchOption]::AllDirectories)) {
    if ($path.Substring($GameDir.Length + 1) -match $skip) { continue }
    $file = New-Object IO.FileInfo($path)
    $files.Add($file)
    $total += $file.Length
}
$free = (New-Object IO.DriveInfo([IO.Path]::GetPathRoot($Output))).AvailableFreeSpace
if ($free -lt $total + 256MB) {
    throw ('{0:N1} GB to pack, but only {1:N1} GB is free on {2}. Choose another -Output drive.' -f ($total / 1GB), ($free / 1GB), [IO.Path]::GetPathRoot($Output))
}
Write-Host ('Packing {0:N0} files ({1:N1} GB) from {2}' -f $files.Count, ($total / 1GB), $GameDir)

$partial = "$Output.partial"
$stream = [IO.File]::Open($partial, [IO.FileMode]::Create)
$zip = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
$done = 0L
$count = 0
try {
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($GameDir.Length + 1)
        $entry = $zip.CreateEntry("$FolderName/" + $relative.Replace('\', '/'), [IO.Compression.CompressionLevel]::Fastest)
        if ($file.LastWriteTime.Year -ge 1980) { $entry.LastWriteTime = $file.LastWriteTime }
        $out = $entry.Open()
        try {
            if ($relative -ieq $cfgName) { $out.Write($cfgBytes, 0, $cfgBytes.Length) }
            else {
                $in = $file.OpenRead()
                try { $in.CopyTo($out, 1MB) } finally { $in.Dispose() }
            }
        }
        finally { $out.Dispose() }
        $done += $file.Length
        if ((++$count % 250) -eq 0) {
            Write-Progress -Activity "Packing $FolderName" -Status $relative -PercentComplete ([int](100 * $done / [Math]::Max($total, 1)))
        }
    }
    $out = $zip.CreateEntry("$FolderName/READ ME - Darkest2in1.txt").Open()
    try { $out.Write($readmeBytes, 0, $readmeBytes.Length) } finally { $out.Dispose() }
}
finally {
    $zip.Dispose()
    $stream.Dispose()
    Write-Progress -Activity "Packing $FolderName" -Completed
}
Move-Item -LiteralPath $partial -Destination $Output -Force
Write-Host ('Wrote {0} ({1:N1} GB). Unzip it anywhere and start "Darkest Dungeon II.exe" inside {2}\.' -f $Output, ((Get-Item -LiteralPath $Output).Length / 1GB), $FolderName)
