# MintStatSort

**Respaldo de desarrollo revisado el 28 de octubre de 2026.** Versión estable
0.6.10 para RimWorld 1.6; adaptación local experimental para RimWorld 1.7 incluida
por separado. El número 0.6.10 no acredita por sí solo compatibilidad con 1.7.

Ordena y compara recetas de las mesas de trabajo por estadísticas, sin cambiar
la prioridad de los proyectos ya creados. Funciona con el menú nativo; Harmony
es obligatorio y Nice Bill Tab, Dubs Mint Menus y Combat Extended son opcionales.
Interfaz completa en English, Spanish y SpanishLatin.

## Empezar desde GitHub

1. Clona este repositorio e instala un SDK de .NET 8 (8.0.425 o superior de la rama 8).
2. Ejecuta `dotnet restore Source/MintStatSort.csproj --locked-mode`.
3. Ejecuta `dotnet build Source/MintStatSort.csproj -c Release --no-restore`.
4. La DLL queda en `Mod/Assemblies/MintStatSort.dll`; el juego no es necesario
   para compilar esta versión, porque las referencias se restauran desde NuGet.
5. Lee [DEVELOPING.md](DEVELOPING.md) para las pruebas y dependencias.

## Instalar y usar

Para RimWorld 1.6, cierra el juego, extrae o clona el proyecto y ejecuta
`Instalar-mod.cmd`. También puedes copiar `Mod` a `RimWorld/Mods/MintStatSort`.
Activa Harmony antes de MintStatSort. Los menús opcionales y CE, si los usas,
deben ir antes. Evita copias duplicadas activas. El instalador respalda el mod
anterior y conserva su ID de Workshop.

Aplicar comparación guarda los ajustes; Añadir proyecto crea la orden nativa.
Elige Español(Latinoamérica) en las opciones del juego para la traducción latina.
No instales el Mod estable en RimWorld 1.7 como si fuera una versión validada:
lee [la adaptación experimental](Experimental/RimWorld17/README.md).

## Funciones incluidas

- Búsqueda, favoritas y selección múltiple de categorías con filtros independientes.
- Comparación por estadísticas, nombre u orden original; mayor/menor, importancia,
  límites, calidad y material de referencia. Explicación de puntuaciones relativas.
- Hasta tres recetas fijadas y columnas con orden, ancho y visibilidad ajustables.
- Perfiles personales, duplicación, cambio de nombre e importación/exportación XML;
  cuatro perfiles iniciales: protección, frío, ligereza y alcance.
- Ingredientes para 1–999 ejecuciones, faltantes estimados y sustitutos opcionales.
- Lecturas de CE, munición de comparación y DPS sostenido teórico.
- Ventana compacta, atajos de teclado, diagnóstico local y trabajo gradual en UI.
- Título, descripción bilingüe, portada, galería y notas preparadas para Workshop.

## Documentación y estado

- [Instalación](docs/INSTALLATION.md), [compilación y pruebas](DEVELOPING.md).
- [Estructura y diseño](docs/PROJECT-STRUCTURE.md).
- [Estado, pendientes y limitaciones](docs/STATUS.md).
- [Dependencias y contenido de terceros](docs/DEPENDENCIES.md).
- [Inventario y verificación](docs/BACKUP-REVIEW.md).
- [Identidad de Workshop](docs/WORKSHOP-IDENTITY.md), [historial](CHANGELOG.md).
- Guías completas: `Mod/Publicacion-Workshop/GUIA-*.txt`.

No se incluyen el juego, mods de terceros, partidas, credenciales ni cachés de
paquetes. Las referencias y herramientas se restauran o se suministran localmente.
Consulta [NOTICE.md](NOTICE.md); se mantiene la política de derechos existente.
