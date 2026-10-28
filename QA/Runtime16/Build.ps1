param(
    [Parameter(Mandatory=$true)][string]$GamePath,
    [Parameter(Mandatory=$true)][string]$HarmonyPath,
    [string]$ModAssembly,
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $ModAssembly) { $ModAssembly = Join-Path $projectRoot 'Mod\Assemblies\MintStatSort.dll' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'artifacts\runtime16' }
$managed = Join-Path $GamePath 'RimWorldWin64_Data\Managed'
$names = @('mscorlib.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.dll','netstandard.dll','UnityEngine.ScreenCaptureModule.dll')
$references = @($names | ForEach-Object { Join-Path $managed $_ }) + @($HarmonyPath,$ModAssembly)
foreach ($file in $references) { if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw ('Falta referencia del arnes: ' + $file) } }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'Se requiere el compilador .NET Framework de Windows para este arnes.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$arguments = @('/nologo','/noconfig','/nostdlib+','/target:library','/langversion:5','/optimize+','/debug-',('/out:' + (Join-Path $OutputDirectory 'MintStatSortSmoke.dll')))
$arguments += $references | ForEach-Object { '/reference:' + $_ }
$arguments += Join-Path $PSScriptRoot 'Source\SmokeTests.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion del arnes aislado.' }
Write-Output 'Arnes compilado. No se instalo ni se ejecuto el juego.'
