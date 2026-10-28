param([string]$RimWorldPath)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
function Assert-Within([string]$Path, [string]$Root) {
    $full = [System.IO.Path]::GetFullPath($Path)
    $base = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { throw 'La ruta de instalacion esta fuera de la carpeta prevista.' }
}
try {
    if (Get-Process -Name 'RimWorldWin64', 'RimWorldWin', 'RimWorld' -ErrorAction SilentlyContinue) {
        throw 'Cierra RimWorld antes de instalar el mod.'
    }
    if (-not $RimWorldPath) {
        $candidates = [System.Collections.Generic.List[string]]::new()
        foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
            if (-not (Test-Path -LiteralPath $key -ErrorAction SilentlyContinue)) { continue }
            $entry = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
            foreach ($steam in @($entry.SteamPath, $entry.InstallPath)) {
                if (-not $steam) { continue }
                # Build candidate paths without resolving their drive. Steam may retain
                # libraries on disconnected disks, which Join-Path can reject.
                $candidates.Add([System.IO.Path]::Combine($steam, 'steamapps\common\RimWorld'))
                $libraries = [System.IO.Path]::Combine($steam, 'steamapps\libraryfolders.vdf')
                if (Test-Path -LiteralPath $libraries -PathType Leaf -ErrorAction SilentlyContinue) {
                    $libraryText = Get-Content -LiteralPath $libraries -Raw -ErrorAction SilentlyContinue
                    if (-not $libraryText) { continue }
                    foreach ($match in [regex]::Matches($libraryText, '"path"\s+"([^"]+)"')) {
                        $library = $match.Groups[1].Value.Replace('\\', '\')
                        $candidates.Add([System.IO.Path]::Combine($library, 'steamapps\common\RimWorld'))
                    }
                }
            }
        }
        $RimWorldPath = $candidates | Where-Object {
            Test-Path -LiteralPath ([System.IO.Path]::Combine($_, 'Mods')) -PathType Container -ErrorAction SilentlyContinue
        } | Select-Object -First 1
        if (-not $RimWorldPath) { $RimWorldPath = Read-Host 'Ruta de RimWorld (la carpeta que contiene Mods)' }
    }
    $RimWorldPath = $RimWorldPath.Trim().Trim('"')
    if (-not $RimWorldPath -or -not (Test-Path -LiteralPath ([System.IO.Path]::Combine($RimWorldPath, 'Mods')) -PathType Container -ErrorAction SilentlyContinue)) {
        throw 'La ruta indicada no existe, su unidad no esta disponible o no contiene la carpeta Mods.'
    }
    $source = Join-Path $project 'Mod'
    if (-not (Test-Path -LiteralPath (Join-Path $source 'Assemblies\MintStatSort.dll') -PathType Leaf)) { throw 'Falta la DLL del mod. Extrae el ZIP completo antes de instalar.' }
    foreach ($required in @('About\About.xml', 'About\Preview.png', 'Publicacion-Workshop\titulo.txt', 'Publicacion-Workshop\descripcion-workshop-es.txt', 'Publicacion-Workshop\descripcion-workshop-en.txt', 'Publicacion-Workshop\notas-version-es.txt', 'Publicacion-Workshop\datos-publicacion.txt', 'Publicacion-Workshop\portada-workshop-16x9.jpg')) {
        if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf)) { throw ('Falta un archivo del paquete: ' + $required + '. Extrae el ZIP completo.') }
    }
    $sourceMetadata = [xml]::new()
    $sourceMetadata.Load((Join-Path $source 'About\About.xml'))
    if ($sourceMetadata.ModMetaData.packageId -ne 'mintstatsort.recipes') { throw 'El paquete no contiene MintStatSort.' }
    $RimWorldPath = [System.IO.Path]::GetFullPath($RimWorldPath)
    $destination = Join-Path $RimWorldPath 'Mods\MintStatSort'
    Assert-Within $destination (Join-Path $RimWorldPath 'Mods')
    if ([IO.Path]::GetFullPath($source).Equals([IO.Path]::GetFullPath($destination), [StringComparison]::OrdinalIgnoreCase)) { throw 'La fuente y el destino de instalacion son iguales.' }
    $backup = $null
    $publishedId = $null
    if (Test-Path -LiteralPath $destination) {
        if ((Get-Item -LiteralPath $destination -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'La carpeta MintStatSort es un enlace. No se sustituira automaticamente.' }
        $metadata = Join-Path $destination 'About\About.xml'
        if (-not (Test-Path -LiteralPath $metadata -PathType Leaf)) { throw 'La carpeta MintStatSort existente no parece ser este mod. Revisala antes de continuar.' }
        $about = [xml]::new()
        $about.Load($metadata)
        if ($about.ModMetaData.packageId -ne 'mintstatsort.recipes') { throw 'La carpeta MintStatSort contiene otro mod. Revisala antes de continuar.' }
        $idFile = Join-Path $destination 'About\PublishedFileId.txt'
        if (Test-Path -LiteralPath $idFile -PathType Leaf) { $publishedId = [IO.File]::ReadAllBytes($idFile) }
    }
    $stageRoot = Join-Path $RimWorldPath 'ModInstallStaging'
    $stage = Join-Path $stageRoot ('MintStatSort-' + [Guid]::NewGuid().ToString('N'))
    Assert-Within $stage $stageRoot
    New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
    try {
        # Prepare every file before moving the existing mod out of place.
        Copy-Item -LiteralPath $source -Destination $stage -Recurse
        if ($null -ne $publishedId) { [IO.File]::WriteAllBytes((Join-Path $stage 'About\PublishedFileId.txt'), $publishedId) }
        if (Test-Path -LiteralPath $destination) {
            $backupRoot = Join-Path $RimWorldPath 'ModBackups'
            New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
            $backup = Join-Path $backupRoot ('MintStatSort-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6))
            Assert-Within $backup $backupRoot
            Assert-Within $destination (Join-Path $RimWorldPath 'Mods')
            Move-Item -LiteralPath $destination -Destination $backup
        }
        Move-Item -LiteralPath $stage -Destination $destination
    } catch {
        if ($backup -and (Test-Path -LiteralPath $backup)) {
            Assert-Within $backup (Join-Path $RimWorldPath 'ModBackups')
            Assert-Within $destination (Join-Path $RimWorldPath 'Mods')
            if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
            Move-Item -LiteralPath $backup -Destination $destination
        }
        throw
    } finally {
        Assert-Within $stage $stageRoot
        if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    }
    Write-Host ('Mod instalado en ' + $destination)
    if ($backup) { Write-Host ('Copia de la version anterior: ' + $backup) }
    if ($null -ne $publishedId) { Write-Host 'Identificador de Workshop conservado.' }
    Write-Host ('Documentacion y portada: ' + (Join-Path $destination 'Publicacion-Workshop'))
    Write-Host 'Activa Harmony antes de MintStatSort. CE, Nice Bill Tab y Dubs Mint Menus son opcionales; si los usas, colocalos antes de MintStatSort.'
    Write-Host 'El mod queda preparado en Mods. Abre RimWorld para publicar o actualizar desde su menu; este instalador no sube contenido a Steam.'
} catch {
    Write-Host ('Error: ' + $_.Exception.Message)
    Write-Host 'Tambien puedes copiar la carpeta Mod manualmente a RimWorld\Mods\MintStatSort.'
    exit 1
}
