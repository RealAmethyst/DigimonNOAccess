# Copies the built mod into the game's Mods folder.
#
# Only the files the mod actually ships are copied. bin\ also contains every
# reference assembly the build pulled in (Il2Cpp interop, MelonLoader, Unity
# modules); those must NOT go into Mods\ - they are build-time references and
# copying them can shadow the loader's own copies.
#
# Usage:  pwsh -File scripts\deploy.ps1
#         pwsh -File scripts\deploy.ps1 -WhatIf     (show what would be copied)

[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Digimon World Next Order"
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$bin  = Join-Path $repo 'bin'
$mods = Join-Path $GameDir 'Mods'

if (-not (Test-Path $bin))  { throw "No build output at $bin - run the build first." }
if (-not (Test-Path $mods)) { throw "No Mods folder at $mods - is MelonLoader installed?" }

# The shipped set. Anything not listed here stays out of Mods\.
$files = @(
    'DigimonNOAccess.dll',
    'prism.dll',        # speech (Prism)
    'phonon.dll',       # Steam Audio HRTF
    'NAudio.dll',
    'NAudio.Core.dll',
    'NAudio.Wasapi.dll',
    'NAudio.WinMM.dll'
)

foreach ($f in $files) {
    $src = Join-Path $bin $f
    if (-not (Test-Path $src)) {
        Write-Warning "missing from build output, skipped: $f"
        continue
    }
    if ($PSCmdlet.ShouldProcess($f, 'copy to Mods')) {
        Copy-Item $src (Join-Path $mods $f) -Force
        Write-Host "copied  $f"
    }
}

# Sounds. Copied rather than mirrored, so anything the player added by hand
# survives - nothing here deletes files from the game folder.
$srcSounds = Join-Path $bin 'sounds'
$dstSounds = Join-Path $mods 'sounds'
if (Test-Path $srcSounds) {
    if (-not (Test-Path $dstSounds)) {
        if ($PSCmdlet.ShouldProcess('sounds', 'create folder')) {
            New-Item -ItemType Directory -Force $dstSounds | Out-Null
        }
    }
    foreach ($w in Get-ChildItem $srcSounds -File) {
        if ($PSCmdlet.ShouldProcess("sounds\$($w.Name)", 'copy')) {
            Copy-Item $w.FullName (Join-Path $dstSounds $w.Name) -Force
            Write-Host "copied  sounds\$($w.Name)"
        }
    }
}

# settings.json and hotkeys.ini are written by the mod at runtime and hold the
# player's own configuration. Never overwrite them from the build output.
Write-Host ""
Write-Host "Done. settings.json and hotkeys.ini left untouched."
