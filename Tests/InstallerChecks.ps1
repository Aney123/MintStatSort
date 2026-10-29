param([string]$Installer = (Join-Path $PSScriptRoot '../Windows/Instalar-mod.ps1'))
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('MintStatSort-installer-' + [Guid]::NewGuid().ToString('N'))
$global:mssInstallerTestState = @{
    SteamRoot = (Join-Path $testRoot 'Steam')
    ManualPath = ''
    Prompts = 0
    Checks = 0
}

# Simulate Steam registry data; all disk operations below use real temporary
# folders. No registry keys or actual game installations are modified.
function Test-Path {
    [CmdletBinding()]
    param([string]$LiteralPath, [string]$Path, [string]$PathType)
    if ($LiteralPath -eq 'HKCU:\Software\Valve\Steam') { return $true }
    if ($LiteralPath -eq 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam') { return $false }
    Microsoft.PowerShell.Management\Test-Path @PSBoundParameters
}
function Get-ItemProperty {
    [CmdletBinding()]
    param([string]$LiteralPath)
    if ($LiteralPath -ne 'HKCU:\Software\Valve\Steam') { throw 'Unexpected registry access.' }
    [pscustomobject]@{ SteamPath = $global:mssInstallerTestState.SteamRoot; InstallPath = $null }
}
function Get-Process {
    [CmdletBinding()]
    param([string[]]$Name)
    # These tests simulate a closed game.
}
function Read-Host {
    param([string]$Prompt)
    $global:mssInstallerTestState.Prompts++
    return $global:mssInstallerTestState.ManualPath
}
function Assert-Check([bool]$Success, [string]$Message) {
    if (-not $Success) { throw $Message }
    $global:mssInstallerTestState.Checks++
}
function Write-Libraries([string[]]$Paths) {
    $content = '"libraryfolders" { ' + (($Paths | ForEach-Object { '"path" "' + $_.Replace('\', '\\') + '"' }) -join ' ') + ' }'
    $content | Set-Content -LiteralPath (Join-Path $global:mssInstallerTestState.SteamRoot 'steamapps/libraryfolders.vdf') -Encoding UTF8
}
function Check-Installed([string]$GamePath) {
    Assert-Check (Test-Path -LiteralPath (Join-Path $GamePath 'Mods/MintStatSort/Assemblies/MintStatSort.dll') -PathType Leaf) 'Expected DLL was not installed.'
    Assert-Check (Test-Path -LiteralPath (Join-Path $GamePath 'Mods/MintStatSort/About/About.xml') -PathType Leaf) 'Expected metadata was not installed.'
}
try {
    New-Item -ItemType Directory -Path (Join-Path $global:mssInstallerTestState.SteamRoot 'steamapps') -Force | Out-Null
    # A guaranteed nonexistent drive reproduces the reported failure under Stop.
    $missingDrive = 'MSSMissing' + [Guid]::NewGuid().ToString('N')
    $missingLibrary = $missingDrive + ':\SteamLibrary'
    $joinRejected = $false
    try { Join-Path $missingLibrary 'steamapps\common\RimWorld' | Out-Null } catch { $joinRejected = $true }
    Assert-Check $joinRejected 'The missing-drive precondition did not reproduce the original failure.'

    # Stale library first, usable library second, including literal [] in the path.
    $liveLibrary = Join-Path $testRoot 'Library [test]'
    $liveGame = Join-Path $liveLibrary 'steamapps/common/RimWorld'
    New-Item -ItemType Directory -Path (Join-Path $liveGame 'Mods') -Force | Out-Null
    Write-Libraries @($missingLibrary, $liveLibrary)
    & $Installer
    Check-Installed $liveGame
    Assert-Check ($global:mssInstallerTestState.Prompts -eq 0) 'Detection should find the live library without prompting.'

    # Installing again backs up the previous mod, including user-added files.
    $marker = Join-Path $liveGame 'Mods/MintStatSort/local-marker.txt'
    'preserve this file' | Set-Content -LiteralPath $marker
    & $Installer
    Check-Installed $liveGame
    $backups = @(Get-ChildItem -LiteralPath (Join-Path $liveGame 'ModBackups') -Directory)
    Assert-Check ($backups.Count -eq 1) 'Exactly one backup is expected after an update.'
    Assert-Check ((Get-Content -LiteralPath (Join-Path $backups[0].FullName 'local-marker.txt') -Raw).Trim() -eq 'preserve this file') 'Backup did not preserve the old mod.'

    # Only stale libraries remain: prompt for an existing installation.
    $manualGame = Join-Path $testRoot 'Manual RimWorld'
    New-Item -ItemType Directory -Path (Join-Path $manualGame 'Mods') -Force | Out-Null
    $global:mssInstallerTestState.ManualPath = '  "' + $manualGame + '"  '
    Write-Libraries @($missingLibrary)
    & $Installer
    Check-Installed $manualGame
    Assert-Check ($global:mssInstallerTestState.Prompts -eq 1) 'Missing libraries should reach the manual path prompt.'

    # A stale Steam registry root itself must also fall back to the prompt.
    $global:mssInstallerTestState.SteamRoot = $missingDrive + ':\Steam'
    $registryFallback = Join-Path $testRoot 'Registry fallback'
    New-Item -ItemType Directory -Path (Join-Path $registryFallback 'Mods') -Force | Out-Null
    $global:mssInstallerTestState.ManualPath = $registryFallback
    & $Installer
    Check-Installed $registryFallback
    Assert-Check ($global:mssInstallerTestState.Prompts -eq 2) 'A stale registry root should not abort detection.'

    # A bad explicit path reports a useful error and cannot start copying.
    $global:LASTEXITCODE = 0
    $failureOutput = (& $Installer -RimWorldPath ($missingDrive + ':\RimWorld') 6>&1 | Out-String)
    Assert-Check ($LASTEXITCODE -eq 1) 'A missing explicitly selected drive must fail.'
    Assert-Check ($failureOutput -match 'La ruta indicada no existe') 'An invalid manual path needs a clear error.'
    Write-Host ('Passed ' + $global:mssInstallerTestState.Checks + ' installer regression checks.')
} finally {
    Remove-Variable -Name mssInstallerTestState -Scope Global
    if (Microsoft.PowerShell.Management\Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
