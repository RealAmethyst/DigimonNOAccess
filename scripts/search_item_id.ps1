$target = [byte[]]@(0xFC, 0xFD, 0x69, 0xFF)
$searchPath = "C:\Program Files (x86)\Steam\steamapps\common\Digimon World Next Order\StreamingAssets"
$files = Get-ChildItem -Path $searchPath -Recurse -File
foreach ($f in $files) {
    try {
        $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
        for ($i = 0; $i -le $bytes.Length - 4; $i++) {
            if ($bytes[$i] -eq $target[0] -and $bytes[$i+1] -eq $target[1] -and $bytes[$i+2] -eq $target[2] -and $bytes[$i+3] -eq $target[3]) {
                Write-Output ("FOUND in " + $f.Name + " at offset " + $i)
                break
            }
        }
    } catch {}
}
Write-Output "Search complete"
