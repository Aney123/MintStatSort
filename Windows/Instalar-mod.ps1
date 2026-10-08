param([string]$RimWorldPath)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
try {
    if (Get-Process -Name 'RimWorldWin64', 'RimWorldWin', 'RimWorld' -ErrorAction SilentlyContinue) {
        throw 'Cierra RimWorld antes de instalar el mod.'
    }
    if (-not $RimWorldPath) {
        $candidates = [System.Collections.Generic.List[string]]::new()
        foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
            if (-not (Test-Path $key)) { continue }
            $entry = Get-ItemProperty $key
            foreach ($steam in @($entry.SteamPath, $entry.InstallPath)) {
                if (-not $steam) { continue }
                $candidates.Add((Join-Path $steam 'steamapps\common\RimWorld'))
                $libraries = Join-Path $steam 'steamapps\libraryfolders.vdf'
                if (Test-Path $libraries) {
                    foreach ($match in [regex]::Matches((Get-Content $libraries -Raw), '"path"\s+"([^"]+)"')) {
                        $library = $match.Groups[1].Value.Replace('\\', '\')
                        $candidates.Add((Join-Path $library 'steamapps\common\RimWorld'))
                    }
                }
            }
        }
        $RimWorldPath = $candidates | Where-Object { Test-Path (Join-Path $_ 'Mods') } | Select-Object -First 1
        if (-not $RimWorldPath) { $RimWorldPath = Read-Host 'Ruta de RimWorld (la carpeta que contiene Mods)' }
    }
    $RimWorldPath = $RimWorldPath.Trim('"')
    if (-not (Test-Path (Join-Path $RimWorldPath 'Mods'))) { throw 'La carpeta indicada no contiene Mods.' }
    $source = Join-Path $project 'Mod'
    if (-not (Test-Path (Join-Path $source 'Assemblies\MintStatSort.dll'))) { throw 'Falta la DLL del mod. Extrae el ZIP completo antes de instalar.' }
    $destination = Join-Path $RimWorldPath 'Mods\MintStatSort'
    $backup = $null
    if (Test-Path $destination) {
        $metadata = Join-Path $destination 'About\About.xml'
        if (-not (Test-Path $metadata)) { throw 'La carpeta MintStatSort existente no parece ser este mod. Revisala antes de continuar.' }
        [xml]$about = Get-Content -LiteralPath $metadata -Raw
        if ($about.ModMetaData.packageId -ne 'mintstatsort.recipes') { throw 'La carpeta MintStatSort contiene otro mod. Revisala antes de continuar.' }
        $backupRoot = Join-Path $RimWorldPath 'ModBackups'
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        $backup = Join-Path $backupRoot ('MintStatSort-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6))
        Move-Item -LiteralPath $destination -Destination $backup
    }
    try {
        Copy-Item -LiteralPath $source -Destination $destination -Recurse
    } catch {
        if ($backup) {
            if (Test-Path $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
            Move-Item -LiteralPath $backup -Destination $destination
        }
        throw
    }
    Write-Host ('Mod instalado en ' + $destination)
    if ($backup) { Write-Host ('Copia de la version anterior: ' + $backup) }
    Write-Host 'Activa Harmony, Nice Bill Tab y Mint Stat Sort, en ese orden. Tambien admite Dubs Mint Menus. Reinicia RimWorld.'
} catch {
    Write-Host ('Error: ' + $_.Exception.Message)
    Write-Host 'Tambien puedes copiar la carpeta Mod manualmente a RimWorld\Mods\MintStatSort.'
    exit 1
}
