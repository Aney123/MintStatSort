# Compilar y comprobar Mint Stat Sort

## Requisitos

- .NET SDK 8 o posterior.
- Restauración de NuGet para `Krafs.Rimworld.Ref` 1.6.4871, `Lib.Harmony` 2.3.3 y las referencias de .NET Framework.
- Para las comprobaciones de integración, las DLL de Nice Bill Tab y Dubs Mint Menus compatibles con RimWorld 1.6, obtenidas por separado.

## Compilar

Desde la raíz del repositorio:

```text
dotnet build Source/MintStatSort.csproj -c Release
```

La DLL se escribe en `Mod/Assemblies/MintStatSort.dll`. El destino del mod es .NET Framework 4.7.2, el entorno de ejecución utilizado por RimWorld.

## Comprobaciones

```text
dotnet run --project Tests/MintStatSort.Tests.csproj -c Release -- RUTA_A_DUBSMINTMENUS_1.6.dll RUTA_A_NICEBILLTAB.dll
```

Comprueba la puntuación conjunta, valores ausentes, estabilidad, daños por tipo, cantidades almacenadas, firmas y llamadas de ambos menús y sintaxis PowerShell. Usa las versiones de los menús indicadas en [LEEME.md](LEEME.md) para reproducir las comprobaciones de esta publicación.

Las pruebas automatizadas no ejecutan RimWorld, Unity ni una partida. Los cambios a los parches de Harmony y a los selectores requieren también una prueba dentro del juego.

## Empaquetar

```text
python empaquetar.py
```

Genera el ZIP de la versión 0.3 y un texto de PowerShell para reconstruir el mismo archivo. Comprueba el ZIP, las traducciones y la reconstrucción. Excluye los metadatos de Git, los resultados temporales y las dependencias externas.

Las publicaciones incluyen el mod compilado y el código original de este repositorio. No añadas DLL de otros mods o del juego a los paquetes.
