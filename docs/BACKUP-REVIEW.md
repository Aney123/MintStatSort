# Revisión del respaldo de MintStatSort — 28 de octubre de 2026

## Resultado

Fuente y paquete estable 0.6.10 para RimWorld 1.6, con trabajo local experimental
de 1.7 conservado por separado. No se eligió una carpeta por fecha: se cotejaron
metadatos, todos los archivos de las fuentes y hashes de las DLL de copias locales,
instalador, instalación y candidatas. Las cuatro candidatas 1.7 tienen exactamente
el mismo conjunto de fuentes; el snapshot instalado conserva su DLL original.

El proyecto puede continuar desde GitHub sin la carpeta de trabajo original:
incluye proyectos, fuentes, tests, instalador, recursos de publicación, traducciones,
DLL propia, arnés de QA, dependencias fijadas y documentación. Las referencias
NuGet se restauran y el juego y Harmony para el port se suministran legítimamente.

## Verificación actual

- Restauración desde nuget.org a una caché nueva, sin reutilizar bin/obj originales.
- Compilación estable y tests: cero errores y cero advertencias.
- La DLL recompilada es idéntica a la original:
  `2e428a460fdf0a7da3ba56073899780fe97d2951b40a9c60884cd325ebbbc31c`.
- 277 comprobaciones de lógica, contratos nativos de 1.6 y scripts, aprobadas.
- 16 comprobaciones del instalador en directorios temporales, aprobadas.
- Script portátil del port: compila con referencias locales de 1.7.4990 rev1069.
- Fuente del arnés de motor 1.6: compila; no se ejecutó el juego ni se incluyen saves.
- 253 claves y parámetros iguales entre English, Spanish y SpanishLatin.
- Descripción bilingüe sincronizada, 7965 bytes UTF-8, bajo el límite de 7999.
- Código original y archivos originales del candidato instalado permanecen iguales.

La validación actual no acredita el comportamiento del port 1.7 en el juego, la
compatibilidad de versiones recientes de CE/Nice/Dubs, ni el estado del artículo
de Steam. Las pruebas de motor de 2026-10-11 son evidencia histórica separada.

## GitHub

Repositorio: https://github.com/Aney123/MintStatSort
Base consultada: main, commit `7b8d7c7b7a8a6bb484b0f48259d1157822f2bf7e`.
Ese commit declara 0.3.0. Rama propuesta, todavía no creada:
`codex/backup-0.6.10-local-rw17-20261028`.
La comparación exacta por archivo está en GITHUB-COMPARISON.csv.
No hace falta conservar una dependencia de esta computadora o una ruta de usuario.
No se sube una carpeta padre con proyectos ajenos ni se arrastra un .git privado.

## Qué se conserva y qué queda fuera

| Grupo | Decisión |
| --- | --- |
| Fuente estable, tests y Windows | Copia byte por byte; se añaden documentación y herramientas portátiles. |
| Mod propio y tres idiomas | Incluidos, con DLL original lista y recursos de publicación. |
| Port 1.7 | Snapshot de código, idiomas y DLL propia, fuera del Mod estable. |
| QA 1.6 | Fuente incluida; ruta de laboratorio reemplazada por MSS_PROBE_ROOT. |
| Referencias y herramientas | Versiones y locks; se restauran, no se empaquetan DLL de terceros. |
| bin, obj, cachés, SDK y salidas de tests | Excluidos de GitHub y del ZIP publicable; son regenerables. |
| Juego, mods de terceros y partidas | Excluidos; no necesarios para almacenar la fuente. |
| ID de publicación local | Archivo preservado en respaldo privado; ID público documentado. |
| Informes históricos y generadores de una sola ejecución | Originales preservados privados, con rutas locales y proceso de trabajo. |
| Credenciales, claves privadas y archivos de usuario | No incluidos; revisión por patrones no equivale a auditoría de seguridad exhaustiva. |

El archivo más grande del proyecto original revisado es una captura PNG de
2,563,463 bytes. No se encontraron archivos cercanos al límite de 100 MB de GitHub;
no se requiere LFS para este conjunto. La revisión visual de las cinco capturas
no encontró chats, credenciales ni datos personales reales visibles; hay nombres
de personajes del escenario. La revisión de metadatos se registra aparte.
Las capturas y promociones contienen elementos de RimWorld y conservan sus
derechos de terceros; no son una licencia para redistribuir el juego.

NOTICE.md se conserva del repositorio existente sin cambiar sus condiciones.
Los datos privados y el historial de preparación no deben subirse al repositorio
público. El respaldo privado permite conservarlos sin convertirlos en dependencia.

## Integridad

INVENTORY.csv identifica los archivos y su procedencia; el MANIFEST.sha256 de la
raíz permite verificar todos los archivos entregados salvo el propio manifiesto.
El ZIP excluye bin/obj, DLL ajenas y datos privados por una lista de reglas
explícita. La copia de revisión y los originales no se reemplazan al empaquetar.
