# Compilación y continuación del desarrollo

## Herramientas

- SDK .NET 8.0.425 o una versión posterior 8.x. `global.json` fija la familia.
- Windows y PowerShell para instalar y ejecutar los scripts de pruebas de Windows.
- Acceso a nuget.org en la primera restauración. No se necesita el SDK original
  de esta computadora ni sus rutas de usuario; todas las fuentes son relativas.

## Compilar la versión estable

```
dotnet restore Source/MintStatSort.csproj --locked-mode
dotnet build Source/MintStatSort.csproj -c Release --no-restore
dotnet restore Tests/MintStatSort.Tests.csproj --locked-mode
dotnet build Tests/MintStatSort.Tests.csproj -c Release --no-restore
```

La fuente apunta a net472 y genera `Mod/Assemblies/MintStatSort.dll`. Las versiones
directas y transitivas están en ambos `packages.lock.json`; no copies referencias
del juego al repositorio. Los bloqueos se regeneran deliberadamente con
`dotnet restore PROYECTO --force-evaluate -p:RestorePackagesWithLockFile=true`
cuando actualices dependencias, y deben revisarse y versionarse.

## Pruebas

Lógica y presupuestos sin una instalación del juego:
```
dotnet run --project Tests/MintStatSort.Tests.csproj -c Release --no-build -- --performance
```

Para el conjunto de lógica y contratos de RimWorld 1.6, proporciona una DLL
legítima de 1.6 (no la Assembly-CSharp de 1.7):
```
dotnet run --project Tests/MintStatSort.Tests.csproj -c Release --no-build -- --vanilla "RUTA_16/Assembly-CSharp.dll" "Mod/Assemblies/MintStatSort.dll"
```

Opcionales, con tus propias copias autorizadas de las versiones correspondientes:
```
dotnet run --project Tests/MintStatSort.Tests.csproj -c Release --no-build -- --optional "DubsMintMenus.dll" "NiceBillTab.dll"
dotnet run --project Tests/MintStatSort.Tests.csproj -c Release --no-build -- --ce "CombatExtended.dll"
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/InstallerChecks.ps1
```

El arnés `QA/Runtime16` necesita una instalación y perfil de pruebas aislados.
No contiene partidas ni el juego; no debe activarse en partidas reales. Sus
comprobaciones crean objetos y proyectos de prueba. Las pruebas históricas de
motor no equivalen a validar una nueva versión de RimWorld, CE u otro mod.

## Nueva versión

Mantén coherentes Source/MintStatSort.csproj, About.xml, las tres traducciones,
CHANGELOG.md, notas-actualizacion-steam.txt y publicacion-version.txt. El archivo
histórico descripcion-workshop-es.txt es la descripción bilingüe real que lee
la integración; debe ser idéntico a descripcion-workshop-bilingue.txt y medir
como máximo 7999 bytes UTF-8. Las notas describen solo cambios del mod; si no
los hay, usa «Sin cambios recientes.» después de la línea MintStatSort VERSION.
No publiques una nueva DLL hasta comprobar compatibilidad y revisar el paquete.
