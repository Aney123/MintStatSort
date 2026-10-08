# Mint Stat Sort — Selector visual de recetas

Versión **0.3 para Windows y RimWorld 1.6**. Compatible con **Nice Bill Tab** y **Dubs Mint Menus**. Permite marcar varias características para buscar productos que destaquen en conjunto, con categorías y materiales visuales.

## Actualizar o instalar

1. Cierra RimWorld y extrae el ZIP completo.
2. Ejecuta **Instalar-mod.cmd**. Detecta Steam y permite indicar otra ruta. Si ya existe Mint Stat Sort, guarda la versión anterior en `RimWorld\ModBackups` y actualiza su carpeta.
3. Activa **Harmony**, tu menú (**Nice Bill Tab** o **Dubs Mint Menus**) y **Mint Stat Sort**, en ese orden. Reinicia RimWorld.

Mantiene el identificador del mod y convierte tu estadística anterior en una casilla seleccionada. No actives dos copias de Mint Stat Sort. Para instalar manualmente, mueve la versión anterior fuera de `Mods` y copia el contenido de `Mod` a `RimWorld\Mods\MintStatSort`.

Necesita [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) y al menos uno de estos menús: [Nice Bill Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671) o [Dubs Mint Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=1446523594). La DLL viene compilada. No necesita Python, Ollama, servicios ni Internet durante el juego. Si usas Nice Bill Tab - Expansion, coloca este añadido después de él; esa combinación todavía no se ha probado.

## Buscar las mejores armas en dos características

1. Abre una mesa de trabajo y pulsa el botón de categoría. Elige **Armas cuerpo a cuerpo**.
2. Pulsa **Características…**. Dentro de **Daño y armas**, marca **Daño cortante (CaC)** y **Daño contundente (CaC)**.
3. Deja **Más ↑** en las dos y pulsa **Aplicar**.
4. La lista mostrará primero las armas con la mejor combinación. El botón de la barra resumirá **2 características**.

También puedes pedir **más daño y menos peso**, o **más protección cortante y contundente** para las armaduras. Cada casilla tiene su botón **Más/Menos**. El selector mantiene abierto el diálogo mientras marcas casillas; solo aplica la selección al pulsar **Aplicar**.

## Una lista de opciones más corta

Las características aparecen agrupadas en **Daño y armas**, **Protección**, **Aislamiento y temperatura**, **Peso, valor y fabricación** y **Otras características**. Al abrir el selector se muestran las recomendadas que tienen datos para esa mesa y categoría. **Ver todas** permite acceder a las estadísticas avanzadas y a las expuestas por otros mods. Las características que ya tengas seleccionadas siguen visibles aunque sean avanzadas.

Los modos **Nombre** y **Orden original** están separados de las casillas. **Desmarcar todo** permite elegir otra combinación; **Limpiar**, en la barra de la mesa, restablece categoría, comparación y material. La flecha **↓** muestra los mejores primero y **↑** invierte el orden; las recetas sin datos siguen al final.

## Qué significa la puntuación

Con una sola característica aparece su valor real delante del nombre. Con dos o más aparece una puntuación comparativa, por ejemplo **65 pts**. Su información muestra los valores reales de cada característica y el material utilizado.

Todas las características tienen la misma importancia. La combinación favorece el equilibrio: una característica excelente no compensa por completo estar en el mínimo de otra. La puntuación se calcula con las recetas que quedan visibles tras las búsquedas y filtros, de modo que puede cambiar al cambiar esa lista. No representa daño, porcentaje de protección, probabilidad de victoria ni una clasificación absoluta de todo el juego.

Para cada característica variable se usa una escala relativa de 0 a 1 entre las recetas plenamente comparables, invirtiéndola cuando pides **Menos**. La combinación es su media geométrica × 100. Las columnas constantes no influyen. Los valores ausentes o no finitos no reciben puntuación y quedan al final en ambos sentidos. Cero es un dato válido. Los empates utilizan el promedio relativo de los criterios, después el nombre y el orden original entre nombres iguales.

## Categorías visuales

El selector muestra tarjetas con imagen y número de recetas: **Todas**, **Armaduras y ropa**, **Armas a distancia**, **Armas cuerpo a cuerpo** y **Otros**, según lo disponible en la mesa. **Mostrar categorías específicas** despliega las categorías del juego y de otros mods. La búsqueda también las encuentra sin desplegarlas todas.

Las categorías específicas con el mismo nombre se agrupan en una sola opción que incluye sus productos, evitando entradas repetidas como varios «Escudos». Al cambiar de categoría se conservan los criterios que todavía tengan datos y se retiran los que no sirven para ninguna receta de la nueva categoría.

## Materiales con iconos y cantidades

El selector de material muestra tarjetas con **imagen, nombre y cantidad almacenada**. Por defecto está marcada **Solo materiales almacenados**. Cuenta los materiales compatibles con las recetas de la categoría, presentes en zonas de almacenamiento y estanterías del mapa de la mesa. Las cantidades se actualizan mientras el selector está abierto.

No cuenta materiales tirados fuera de almacenamiento, en inventarios de personajes, en caravanas ni en otros mapas. Mods que guardan objetos dentro de contenedores propios sin mantenerlos registrados como objetos del mapa pueden necesitar adaptación.

Puedes desmarcar **Solo materiales almacenados** para ver todos los compatibles, incluidos los que todavía no tienes. La opción **Predeterminado de cada objeto** siempre permite volver a la comparación original; puede usar materiales que no estén almacenados.

Elige un solo material de referencia para comparar objetos en las mismas condiciones. Los que no lo aceptan aparecen sin datos. Los productos con material fijo o sin elección de material mantienen su referencia propia. Esto no cambia los ingredientes, reservas ni restricciones de las órdenes. La cantidad almacenada no garantiza que una receta pueda fabricarse.

Todos los valores usan **calidad Normal**. El material elegido para ordenar pertenece a este añadido; el panel derecho de Nice Bill Tab conserva su propia selección, por lo que sus cifras pueden diferir.

## Daño, protección y sistemas especiales

**Daño cortante/contundente (CaC)** es el mayor daño de un golpe estándar de ese tipo que expone el arma, ajustado mediante las funciones del juego para el material y sin un atacante. No es DPS. Si el arma tiene ataques estándar, pero ninguno de ese tipo, muestra cero. Si no expone ataques estándar comparables, muestra **—**. No incluye efectos secundarios, habilidades del personaje ni modos de combate propios.

**Protección cortante/contundente** procede de las estadísticas de armadura. Se muestra por separado del daño de las armas.

El daño por proyectil usa el primer ataque a distancia con proyectil definido y el multiplicador de daño del arma. El **DPS a distancia es teórico**: daño × proyectiles por ráfaga dividido por preparación, enfriamiento y pausas entre disparos. No incorpora precisión, cobertura, habilidades, selección de munición ni recargas especiales.

Mods que cambian munición, ataques, armadura o almacenamiento pueden necesitar adaptación. No se afirma que los cálculos coincidan con Combat Extended. Las recetas sin producto único, productos especiales y estadísticas que necesitan un personaje pueden quedar sin datos.

## Integración con los menús

Conserva las imágenes, tarjetas, búsqueda y acciones propias de los menús. En Nice Bill Tab se mantienen la selección y comparación con Shift, el doble clic, el botón de fabricar y las variantes de estilos.

La vista de árbol de Nice agrupa las recetas por categoría. Mientras ordenas por características, nombre o filtras con este selector, usa temporalmente las tarjetas compactas del propio Nice. **Limpiar** recupera el árbol sin cambiar sus preferencias.

Solo ordena las recetas disponibles. La cola de órdenes ya añadidas mantiene su prioridad. No afecta al inventario, comercio, construcción ni plantillas de Clones. Si ambos menús están cargados, se conecta a ambos y actúa en el que estés usando.

En **Opciones → Ajustes de mods → Mint Stat Sort** puedes desactivar la barra o restablecer las preferencias. Se guardan la categoría, las casillas y sus direcciones, el sentido del orden, el material y el filtro de almacenamiento.

## Validación y primera prueba

La DLL compila contra referencias de RimWorld **1.6.4871**, destino .NET Framework 4.7.2, con Harmony 2.3.3. Se han comprobado los puntos de integración de la DLL 1.6 publicada en el [repositorio de Dubs Mint Menus](https://github.com/Dubwise56/Dubs-Mint-Menus), versión declarada **1.3.1684**, revisión `23ad4d21035f7c98ec7374b569e7e54cab3f0cab`.

La integración de Nice Bill Tab se comprobó contra la DLL de referencia publicada en el [repositorio del parche multijugador](https://github.com/Keullaeseu/Multiplayer-Nice-Bill-Tab-Patch/blob/ecb2ecbf9990ae804081321cc2eaf230e2462189/References/NiceBillTab.dll), revisión `ecb2ecbf9990ae804081321cc2eaf230e2462189`, subida el 24 de septiembre de 2026, con SHA-256 `6624ed77a7fbd2fb419501d92d0bb721f1a1a12b810d87f4cdec068cb49ee73d`. Esto no confirma que tu copia de Steam sea idéntica.

Las comprobaciones verifican la comparación conjunta, el equilibrio frente a armas especializadas, la independencia de las unidades y del orden de las casillas, valores ausentes/inválidos, empates, conservación de recetas, selección del daño por tipo, recuentos de almacén y estanterías, firmas y llamadas reales de ambos menús y sintaxis PowerShell. La DLL compila sin errores ni advertencias.

**Todavía falta ejecutar esta versión dentro de RimWorld.** El entorno de desarrollo no contiene el juego. Las comprobaciones de las DLL verifican sus métodos y llamadas; no sustituyen una prueba de Harmony y de la interfaz en pantalla. Nice Bill Tab - Expansion y el parche multijugador no se han probado junto a este añadido.

Para la primera prueba, abre una mesa de armas y marca daño cortante y contundente. Comprueba los valores de cada arma en su información, la puntuación conjunta, la flecha y la búsqueda. Después elige una sola protección en una mesa de armaduras. Revisa los iconos y cantidades del selector de materiales, que excluya objetos tirados fuera del almacén, y añade una orden. Las cantidades representan existencia almacenada: no comprueban reservas, prohibiciones ni si hay suficientes ingredientes para fabricar.

Si uno de los menús cambia sus métodos en una actualización, su integración se desactiva y deja el motivo en **Player.log**, con el prefijo `[Mint Stat Sort]`. Su ubicación habitual es:

```text
%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log
```

## Desinstalación y código fuente

Desactiva el mod, cierra RimWorld y elimina `Mods\MintStatSort`. Las recetas y las órdenes pertenecen al juego y a sus mods originales.

El ZIP incluye el código fuente y las pruebas, sin redistribuir DLL de RimWorld, Dubs ni Nice Bill Tab. Para recompilar necesitas .NET SDK 8 o posterior:

```text
dotnet build Source/MintStatSort.csproj -c Release
dotnet run --project Tests/MintStatSort.Tests.csproj -c Release -- RUTA_A_DUBSMINTMENUS_1.6.dll RUTA_A_NICEBILLTAB.dll
```

Si la descarga del ZIP falla en la aplicación, abre el texto `CREAR-ZIP-MintStatSort-0.3-SelectorVisual.txt` entregado junto al ZIP, copia todo su contenido y pégalo en Windows PowerShell. Reconstruye el mismo ZIP en el Escritorio, comprueba su integridad y muestra la ruta. No instala el mod automáticamente ni necesita Internet.

Referencias técnicas: [Nice Bill Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671), [Harmony](https://harmony.pardeike.net/), [Dubs Mint Menus](https://github.com/Dubwise56/Dubs-Mint-Menus), [referencias RimWorld](https://www.nuget.org/packages/Krafs.Rimworld.Ref/1.6.4871).
