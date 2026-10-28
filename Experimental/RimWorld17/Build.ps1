param(
    [Parameter(Mandatory=$true)][string]$GamePath,
    [Parameter(Mandatory=$true)][string]$HarmonyPath,
    [string]$DotNet = 'dotnet',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'artifacts\rimworld17' }
foreach ($required in @('Assembly-CSharp.dll','UnityStubs.dll','System.Runtime.dll','System.Private.CoreLib.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $GamePath $required) -PathType Leaf)) { throw ('Falta referencia de RimWorld 1.7: ' + $required) }
}
if (-not (Test-Path -LiteralPath $HarmonyPath -PathType Leaf)) { throw 'Indica la DLL de Harmony para RimWorld 1.7.' }
$sdkLines = @(& $DotNet --list-sdks)
if ($LASTEXITCODE -ne 0 -or $sdkLines.Count -eq 0) { throw 'No se encontro un SDK .NET.' }
$sdkMatch = [regex]::Match($sdkLines[-1], '^([^ ]+) \[(.+)\]$')
if (-not $sdkMatch.Success) { throw 'No se pudo localizar Roslyn del SDK.' }
$compiler = Join-Path (Join-Path $sdkMatch.Groups[2].Value $sdkMatch.Groups[1].Value) 'Roslyn\bincore\csc.dll'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'Falta el compilador Roslyn.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$output = Join-Path $OutputDirectory 'MintStatSort.dll'
$references = @(Get-ChildItem -LiteralPath $GamePath -Filter 'System*.dll' -File | Where-Object {
    try { [void][Reflection.AssemblyName]::GetAssemblyName($_.FullName); $true }
    catch [BadImageFormatException] { $false }
} | ForEach-Object { $_.FullName })
$references += @('mscorlib.dll','netstandard.dll','Microsoft.CSharp.dll','Assembly-CSharp.dll','UnityStubs.dll') | ForEach-Object { Join-Path $GamePath $_ }
$references += $HarmonyPath
$arguments = @('/nologo','/noconfig','/nostdlib+','/target:library','/langversion:latest','/optimize+','/deterministic+','/debug-',('/out:' + $output))
$arguments += $references | Select-Object -Unique | ForEach-Object { '/reference:' + $_ }
$arguments += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Snapshot\1.7\Source') -Filter '*.cs' -File | ForEach-Object { $_.FullName }
& $DotNet $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion experimental; no se cambio la instalacion.' }
Write-Output ('DLL experimental: ' + $output)
Write-Output 'Compilacion unicamente: necesita validacion dentro de RimWorld 1.7.'
