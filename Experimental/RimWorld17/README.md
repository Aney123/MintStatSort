# Adaptación local experimental de RimWorld 1.7

Este snapshot conserva la fuente, traducciones, DLL propia y selector de carpetas
de la instalación encontrada al preparar el respaldo. Mantiene número 0.6.10,
pero no es una entrega validada. La marca original de candidata se conserva.
No sustituye la versión 1.6 de `Mod` y no se instala con el instalador principal.

La fuente difiere de la estable en AssemblyInfo generado y ComparisonWindow:
una columna visible sin criterio activo utiliza un criterio de respaldo en vez
de lanzar First. Los demás archivos C# de las candidatas cotejadas son iguales.

Para compilar sin redistribuir el juego, en Windows y con SDK .NET 8 instalado:
```
powershell -NoProfile -ExecutionPolicy Bypass -File Experimental/RimWorld17/Build.ps1 -GamePath "RUTA_RIMWORLD_17" -HarmonyPath "RUTA_HARMONY_17/0Harmony.dll"
```
El compilador Roslyn usa las referencias .NET 10 del juego local; la DLL de
salida va a artifacts/rimworld17, sin reemplazar el snapshot ni la instalación.
Compilar no demuestra funcionamiento dentro del juego. Revisa advertencias
CS1701, referencias del juego y compatibilidad de parches antes de distribuir.
No declares soporte estable de 1.7 ni publiques este overlay sin validarlo.
