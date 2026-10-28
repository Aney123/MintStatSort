# Preferencias de publicación de MintStatSort

El usuario pidió el 9 de octubre de 2026 que cada actualización futura incluya
notas de cambios reales y que aparezcan automáticamente en la pestaña «Notas
sobre cambios» de Steam Workshop al actualizar el mod.

- Preparar notas en español para cada versión: versión, novedades, correcciones
  y mejoras que realmente incluya ese lanzamiento. No usar solo la fecha ni
  copiar el historial entero como nota de la nueva actualización.
- Preferencia expresa del usuario: las notas públicas deben describir solo
  novedades, mejoras y errores corregidos del mod para los jugadores. No incluir
  referencias a IA, asistentes, herramientas de generación, conversaciones,
  propuestas del usuario, auditorías, pruebas, escenarios de demostración ni
  relatos del trabajo realizado. Conservar evidencia técnica y prompts en
  documentos separados; no insertarlos en las notas. No repetir funciones
  conservadas como si fueran novedades ni inventar correcciones.
- Mantener las notas breves. Si un envío no introduce cambios reales en el
  funcionamiento del mod, usar únicamente «Sin cambios recientes.» después de
  la línea obligatoria MintStatSort VERSION. No rellenar notas con cambios de
  documentación, descripción o preparación del paquete. Mantener el historial
  de mejoras anteriores sin borrarlo ni repetirlo como novedad del envío actual.
- Mantener el historial en CHANGELOG.md y Publicacion-Workshop/notas-version-es.txt.
  Guardar el texto de la última versión en
  Mod/Publicacion-Workshop/notas-actualizacion-steam.txt e incluirlo en el
  instalador y en la carpeta instalada.
- Desde 0.6.3, WorkshopNotesIntegration conecta este texto al envío de notas
  iniciado desde el menú de mods de RimWorld, en Workshop.Upload y
  Workshop.OnItemCreated, conservando la llamada original SubmitItemUpdate.
  El TXT debe empezar con una línea exacta «MintStatSort VERSION», donde VERSION
  coincide con modVersion de About.xml. Después deben ir las notas de esa versión.
  Mantener MintStatSort activo y reiniciar RimWorld tras actualizar la DLL.
- La integración debe seleccionar las notas de la versión que se está subiendo,
  aplicarse solamente a mintstatsort.recipes y conservar el flujo original de
  publicación iniciado por el usuario. Probar la selección y el contrato del
  punto de integración sin subir una actualización de prueba a Steam.
- Conservar el PublishedFileId existente. No afirmar que la publicación
  automática de notas ya está implementada hasta verificar esa integración.

Esto es una preferencia para próximos lanzamientos, no una solicitud de publicar
o editar ahora las notas existentes en Steam.

## Paquete completo de Workshop (desde 0.6.9)

- El usuario pidió que título, descripción, portada y notas estén preparados en
  el instalador y en la carpeta instalada. Incluir la galería y las capturas
  originales en Publicacion-Workshop; su subida adicional es manual en Steam.
- Mantener publicacion-version.txt con la línea exacta MintStatSort VERSION y
  descripcion-workshop-es.txt con el BBCode del lanzamiento. La integración de
  SetWorkshopItemDataFrom actualiza la descripción solo para este paquete y
  manifiesto válido, preservando las llamadas nativas. Probar el contrato sin
  publicar en Steam. Reiniciar el juego tras instalar la DLL nueva.
- La descripción que se envía debe contener ambos idiomas completos: inglés
  primero y español después, según la preferencia expresa del usuario. No basta
  con entregar traducciones separadas. Mantener descripcion-workshop-bilingue.txt
  y su copia idéntica descripcion-workshop-es.txt (nombre histórico que lee la
  DLL 0.6.9); las versiones de un solo idioma son archivos auxiliares. Verificar
  el límite de 7999 bytes UTF-8 y sincronizar fuente, instalador y mod instalado.
  La descripción corta de About.xml también debe incluir inglés y español.
- El título procede de About.xml, la portada de About/Preview.png. Preparar
  Preview horizontal 16:9, sin barras y legible a tamaño de miniatura, por debajo
  de 1 MB. La portada cuadrada produjo márgenes negros en la columna derecha
  de Steam y el usuario pidió corregirla. Conservar versiones
  anteriores y el PublishedFileId de la instalación local.
- La opción nativa de publicación requiere Modo desarrollador: Opciones →
  General. Luego Mods → copia local → Avanzado… → Actualizar en la Workshop.
- No anunciar funcionalidades futuras como implementadas ni describir las
  estimaciones de ingredientes como garantías de fabricación. Documentar qué
  versiones se probaron y qué imágenes son ediciones promocionales.

## Idiomas (desde 0.6.10)

- Mantener traducciones completas en English, Spanish y SpanishLatin. El usuario
  pidió expresamente español latinoamericano. Conservar las mismas claves y
  parámetros de formato en los tres archivos; no sustituir castellano por latino.
- SpanishLatin usa «Añadir proyecto», según la traducción latinoamericana del
  juego instalado. Los nombres de productos de otros mods dependen de esos mods.
- Para las pruebas aisladas, langFolderName debe coincidir con el nombre completo
  del idioma instalado (SpanishLatin (Español(Latinoamérica))), no solo el alias.
- Ctrl+Enter debe guardar sin cerrar ni crear órdenes: closeOnAccept=false en
  el comparador evita que Window intercepte Enter antes del atajo.
