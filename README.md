# Mint Stat Sort

Ordena las recetas de tus mesas de trabajo por daño, protección y otras estadísticas en **RimWorld 1.6**. Compatible con **Nice Bill Tab** y **Dubs Mint Menus**. Interfaz en español e inglés.

**[Descargar Mint Stat Sort 0.3 para Windows](https://github.com/Aney123/MintStatSort/releases/download/v0.3.0/MintStatSort-0.3-SelectorVisual.zip)** · [Todas las versiones](https://github.com/Aney123/MintStatSort/releases) · [Guía completa en español](LEEME.md) · [English](README.en.md)

## Qué permite hacer

- Marcar varias características: por ejemplo, **daño cortante y contundente**, o **protección alta y peso bajo**.
- Buscar el equilibrio entre todas las características seleccionadas, con la misma importancia para cada una.
- Ver características agrupadas y empezar con las recomendadas para la mesa y la categoría.
- Elegir categorías con imágenes y cantidades de recetas, evitando nombres repetidos.
- Elegir un material de referencia con su icono y cantidad almacenada. Por defecto muestra materiales de almacenes y estanterías del mapa actual.
- Mantener las tarjetas y acciones del menú de fabricación que utilizas.

Ordena las recetas disponibles en la mesa. La cola de órdenes ya añadidas conserva su prioridad.

## Requisitos

- RimWorld **1.6**.
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077).
- Al menos uno de estos menús: [Nice Bill Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671) o [Dubs Mint Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=1446523594).

## Instalar en Windows

1. Descarga el **ZIP del mod** con el enlace de arriba y extrae su contenido.
2. Cierra RimWorld y ejecuta **Instalar-mod.cmd**. Si no encuentra el juego, indica la carpeta que contiene `Mods`.
3. Activa **Harmony**, tu menú y **Mint Stat Sort**, en ese orden, y reinicia RimWorld.

La DLL está incluida: para jugar no necesitas compilar, instalar Python ni conectarte a internet.

Para instalar manualmente, copia la carpeta `Mod` del paquete a `RimWorld/Mods/MintStatSort`. Dentro de la carpeta instalada deben quedar `About`, `Assemblies` y `Languages`. El instalador guarda las versiones anteriores en `RimWorld/ModBackups`. Evita mantener dos copias activas del mod.

## Ejemplo: armas buenas en dos tipos de daño

1. Abre una mesa de armas y elige **Armas cuerpo a cuerpo**.
2. Pulsa **Características…** y marca **Daño cortante (CaC)** y **Daño contundente (CaC)**.
3. Deja **Más** en ambas y pulsa **Aplicar**.

Las armas que destaquen en conjunto aparecerán primero. Con varias características, la cifra es una puntuación comparativa de las recetas visibles; puedes consultar sus valores reales en la información. Los valores de referencia utilizan **calidad Normal**.

## Compatibilidad y comprobaciones

Compilado contra referencias de RimWorld **1.6.4871**, con comprobaciones automáticas de ordenación, materiales, firmas de Nice Bill Tab/Dubs Mint Menus y sintaxis del instalador. La reproducción completa de la interfaz dentro del juego no se ha verificado en el entorno de desarrollo.

Nice Bill Tab - Expansion, el parche multijugador, Combat Extended y los sistemas especiales de ataques o almacenamiento pueden necesitar adaptación. Las recetas sin datos comparables aparecen al final. Consulta las limitaciones y el significado de la puntuación en la [guía completa](LEEME.md).

## Informar de un problema

Abre un [reporte en Issues](https://github.com/Aney123/MintStatSort/issues/new?template=bug_report.yml) e indica la versión de RimWorld, el menú que utilizas y los pasos para reproducirlo. Si aparece un error, adjunta `Player.log`.

## Código

El código fuente y las instrucciones para compilar están en [DEVELOPING.md](DEVELOPING.md). La publicación incluye únicamente la DLL de este mod y su código; las dependencias se obtienen por separado.

El código se publica para consulta y descarga. No se ha otorgado una licencia general para modificarlo o redistribuirlo; véase [NOTICE.md](NOTICE.md).
