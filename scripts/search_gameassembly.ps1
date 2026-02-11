$target = [byte[]]@(0xFC, 0xFD, 0x69, 0xFF)
$gamePath = "C:\Program Files (x86)\Steam\steamapps\common\Digimon World Next Order"

# Search GameAssembly.dll and other key files
$filesToSearch = @(
    "$gamePath\GameAssembly.dll",
    "$gamePath\Digimon World Next Order_Data\il2cpp_data\Metadata\global-metadata.dat"
)

# Also search any .assets files
$assetFiles = Get-ChildItem -Path "$gamePath\Digimon World Next Order_Data" -Filter "*.assets" -Recurse -ErrorAction SilentlyContinue
foreach ($af in $assetFiles) {
    $filesToSearch += $af.FullName
}

foreach ($filePath in $filesToSearch) {
    if (-not (Test-Path $filePath)) {
        Write-Output "SKIP (not found): $filePath"
        continue
    }
    $fileInfo = Get-Item $filePath
    Write-Output "Searching $($fileInfo.Name) ($([math]::Round($fileInfo.Length / 1MB, 1)) MB)..."
    try {
        $bytes = [System.IO.File]::ReadAllBytes($filePath)
        $found = 0
        for ($i = 0; $i -le $bytes.Length - 4; $i++) {
            if ($bytes[$i] -eq $target[0] -and $bytes[$i+1] -eq $target[1] -and $bytes[$i+2] -eq $target[2] -and $bytes[$i+3] -eq $target[3]) {
                Write-Output ("  HIT at offset " + $i + " (0x" + $i.ToString("X") + ")")
                $found++
                if ($found -ge 20) {
                    Write-Output "  (stopping after 20 hits)"
                    break
                }
            }
        }
        if ($found -eq 0) { Write-Output "  No matches" }
    } catch {
        Write-Output "  Error: $_"
    }
}
Write-Output "Search complete"
