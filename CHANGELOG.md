MintStatSort 0.6.10

- Explicación de puntuaciones y cuatro perfiles predefinidos.
- Información de requisitos de las recetas.
- Estimación opcional de ingredientes sustitutos compatibles.
- Munición seleccionable y DPS sostenido teórico con Combat Extended.
- Interfaz compacta y atajos de teclado.
- Traducción completa al español latinoamericano.

MintStatSort 0.6.9

Novedades y mejoras
- Las actualizaciones desde RimWorld incluyen automáticamente la descripción del mod, además de las notas de cambios.
- Portada principal adaptada al formato horizontal de Steam Workshop, con un título más legible en la vista previa.

MintStatSort 0.6.8

Novedades
- Fija hasta tres recetas y consulta sus estadísticas una al lado de otra. Las fijadas conservan su lugar al cambiar búsqueda o categorías y se descartan al cerrar el comparador.
- Estima ingredientes para entre 1 y 999 ejecuciones de la receta base. Actualiza necesarios y faltantes sin modificar cantidades de proyectos ni consumir materiales.
- Renombra, duplica, exporta e importa perfiles XML desde el menú de la cabecera. Importar crea una copia con nombre único; duplicar conserva ajustes independientes.
- Personaliza orden, ancho y visibilidad de columnas. Ocultar una columna mantiene su criterio, importancia y límites.
- Nuevo filtro Sin faltantes estimados, basado en referencias compatibles, material de comparación y ejecuciones. Prepara los resultados por etapas y muestra su progreso.
- Panel de diagnóstico accesible desde el comparador y los ajustes del mod: integraciones, versión y mods activos; permite copiar o guardar un informe local.

Conservación y rendimiento
- Las cantidades, el filtro de faltantes y las columnas se guardan en perfiles y preferencias por mesa. Los ajustes anteriores usan una ejecución y mantienen visibles sus columnas.
- Comprobación gradual de existencias en el hilo del juego. Reutiliza las referencias de ingredientes y publica cada pasada completa, sin escanear inventario al dibujar.
- El filtro es una estimación por receta: no suma sustitutos, no reserva existencias entre recetas y no comprueba acceso de colonos, investigación, habilidades ni restricciones de una orden. Los datos desconocidos quedan fuera una vez completada la pasada.
- Conserva selección múltiple de categorías, filtros independientes, límites, importancia, calidad y la acción nativa Añadir proyecto.
- Harmony sigue siendo obligatorio; Nice Bill Tab, Dubs Mint Menus y Combat Extended son opcionales.

MintStatSort 0.6.7

Novedades y mejoras
- Nueva distribución de la interfaz: perfiles en la cabecera, controles de comparación explícitos y selección de receta más visible.
- Selección múltiple de categorías: combina Armaduras y Prendas, o varias categorías del menú Más. Cada selección tiene su propio filtro eliminable.
- Ordenar por nombre, pulsar Producto / receta o elegir Orden original conserva estadísticas, importancia y límites.
- Estadísticas activas primero, buscador para añadir otras, importancia de 1 a 5, preferencia por mayor/menor y acceso separado a límites.
- Encabezados de estadísticas en varias líneas y desplazamiento horizontal para comparaciones amplias.
- Resumen inferior automático de ingredientes de la receta seleccionada, con necesarios, almacenados y faltantes; Ver ingredientes abre el detalle.
- Todas quita solo categorías; Limpiar filtros quita restricciones sin borrar los criterios; Restablecer comparación recupera los valores iniciales.
- La puntuación indica que es relativa a las recetas y criterios comparados.

Conservación y compatibilidad
- Mantiene perfiles y preferencias por mesa, y migra la categoría única de versiones anteriores.
- Cálculos graduales en el hilo del juego; el resumen de ingredientes no escanea el inventario al dibujar.
- Mantiene Añadir proyecto independiente de Aplicar comparación y conserva los callbacks originales de RimWorld.
- Requiere Harmony. Nice Bill Tab, Dubs Mint Menus y Combat Extended siguen siendo opcionales.
- El instalador conserva el ID de Workshop y crea una copia de seguridad.

MintStatSort 0.6.6
Ingredientes de recetas y resumen de filtros

- Nuevo panel de ingredientes para la receta seleccionada: cantidades necesarias, almacenadas y faltantes para una ejecución de la receta base.
- Selección de una referencia por ingrediente cuando la receta admite sustitutos. La selección del panel no modifica materiales de comparación, órdenes ni inventario real.
- Los recuentos compatibles de almacenes se preparan por etapas, se actualizan periódicamente y pueden refrescarse manualmente. Los requisitos solapados no reutilizan las mismas unidades de una pila en la vista previa.
- Las cantidades utilizan el cálculo del juego para la referencia elegida, incluyendo recetas basadas en nutrición y redondeo a unidades completas.
- Las recetas sin ingredientes tienen una vista vacía clara. Sin mapa o para recetas de pilas completas se muestran los datos no estimables como —.
- Nueva barra de filtros activos para búsqueda, categoría, favoritas y cada mínimo/máximo. Cada botón quita solamente ese filtro.
- Quitar todos elimina los filtros de recetas conservando criterios de comparación, importancia, material, calidad y datos guardados.
- Estas acciones conservan la vista previa hasta aplicar y reutilizan las estadísticas ya calculadas. Se mantienen las acciones nativas y las integraciones opcionales.

El panel estima existencias almacenadas para las referencias elegidas; no suma sustitutos ni comprueba acceso de colonos, reservas o restricciones de una orden concreta. No garantiza que una receta pueda fabricarse.
Requiere Harmony; Nice Bill Tab, Dubs Mint Menus y Combat Extended son opcionales.


MintStatSort 0.6.5
Límites numéricos e importancia de características

- Añadidos mínimos y máximos opcionales por característica, con límites incluidos. Puedes combinar peso máximo, daño mínimo y protección mínima.
- Añadida importancia de 1 a 5 por criterio para comparaciones con varias características. El valor inicial 1 conserva la comparación anterior; la puntuación sigue favoreciendo el equilibrio.
- Nuevo botón × junto a cada característica para configurar importancia y límites. Un * indica que hay un límite activo.
- Los perfiles y las comparaciones por tipo de mesa guardan también los límites y la importancia. Los ajustes anteriores se cargan sin límites y con importancia 1.
- Los filtros reutilizan las estadísticas preparadas. Las recetas pendientes permanecen visibles mientras se calculan; las recetas sin datos para un límite activo se ocultan al terminar.
- Se aceptan coma y punto decimal; se rechazan valores no finitos y mínimos superiores al máximo. Vaciar un campo elimina ese límite.
- Los límites porcentuales se introducen como porcentajes (60 para 60%). CE usa mm RHA y MPa donde corresponde. Las características con formato especial muestran una referencia numérica base en su descripción.
- Nice Bill Tab y Dubs Mint Menus respetan los límites y la importancia. Los menús y las variantes conservan sus acciones originales.

Requiere Harmony; Nice Bill Tab, Dubs Mint Menus y Combat Extended son opcionales.


MintStatSort 0.6.4
Perfiles de comparación y recetas favoritas

- Añadidos perfiles con nombre para guardar categoría, características y sus direcciones, material, calidad y filtro de favoritas.
- Cada tipo de mesa recuerda su última comparación aplicada, de forma independiente. Cargar un perfil conserva la vista previa hasta pulsar Aplicar o Guardar filtros.
- Añadidas estrellas para marcar recetas favoritas y un filtro junto a la búsqueda. Se conservan las variantes y acciones originales de las recetas.
- Los favoritos y los perfiles guardados se conservan al cerrar y al reiniciar RimWorld. Restablecer los filtros no los elimina.
- Actualizar o eliminar un perfil conserva las comparaciones ya aplicadas a otras mesas.
- Al cargar una categoría no disponible en la mesa actual, se vuelve a Todas para evitar una lista vacía.
- Se reutilizan las estadísticas preparadas cuando no cambian el material ni la calidad. Los menús opcionales también respetan las favoritas y la comparación por tipo de mesa.

Requiere Harmony. Nice Bill Tab, Dubs Mint Menus y Combat Extended siguen siendo opcionales.


MintStatSort 0.6.3
Calidad de comparación y notas de Workshop

- Añadido un selector de calidad de referencia, de Horrible a Legendaria, para comparar armas, armaduras y otros objetos con calidad.
- La calidad elegida se guarda al aplicar los filtros; Restablecer vuelve a Normal.
- Las estadísticas se recalculan gradualmente al cambiar de calidad. Se descartan las lecturas pendientes de la calidad anterior.
- Los productos sin calidad mantienen sus valores habituales. El selector no cambia la calidad de fabricación ni las órdenes existentes.
- Al publicar o actualizar MintStatSort desde el menú de mods de RimWorld, se envían automáticamente las notas incluidas para esa versión.
- Esta integración conserva el envío iniciado por el usuario y no modifica las notas de otros mods.

Requiere Harmony. Nice Bill Tab, Dubs Mint Menus y Combat Extended siguen siendo opcionales.


## 0.6.2 — rendimiento y compatibilidad

- Apertura del comparador sin calcular todas las estadísticas antes de mostrar la ventana.
- Descubrimiento de características, comparación y recuento de materiales repartidos entre fotogramas, con avisos de preparación.
- Reutilización de mediciones al buscar y cambiar filtros; cancelación del trabajo anterior al cambiar de contexto o cerrar.
- Detección de Combat Extended y acceso a sus campos con caché, incluida su ausencia.
- Actualización de materiales mediante tiempo real, también con el juego pausado y Creative Mode activo.
- El instalador Workshop mantiene el identificador del mod y conserva el ID de la publicación existente.


[h1]MintStatSort 0.6.1[/h1]
[list]
[*]Botón Añadir proyecto visible en el comparador del menú normal de RimWorld.
[*]La receta seleccionada se añade mediante la acción original del juego, conservando sus comprobaciones de disponibilidad.
[*]Guardar filtros se distingue claramente de la acción que añade un proyecto.
[*]Nice Bill Tab y Dubs Mint Menus siguen siendo opcionales: el selector también está integrado en el menú normal.
[*]Incluye la integración opcional con Combat Extended introducida en 0.6.0: penetración cortante y contundente, cargador, recarga, unidades de armadura y munición de referencia.
[/list]
