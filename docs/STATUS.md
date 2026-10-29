# Estado y pendientes — 28 de octubre de 2026

## Identificación verificada

Fuente estable: 0.6.10 en .csproj y About.xml. La DLL local, el instalador de
0.6.10 y la base de la instalación tienen SHA-256
`2e428a460fdf0a7da3ba56073899780fe97d2951b40a9c60884cd325ebbbc31c`.
El GitHub main consultado aún declara 0.3.0. La revisión de copias no se basa
solo en nombres ni fechas: cotejó fuentes, metadatos y hashes.

Además existe un overlay instalado de 1.7 con las mismas funciones base y un
cambio local de ComparisonWindow: usa FirstOrDefault y un criterio de respaldo
cuando una columna visible no es un criterio activo. Se conserva íntegro en
Experimental/RimWorld17. Las cuatro candidatas localizadas tienen las mismas
fuentes; sus binarios difieren. No se inventa un número de versión nuevo.

## Validación y límites de evidencia

Las evidencias históricas del 11 de octubre de 2026 registran 363 controles de
motor sin CE, 670 con CE y la matriz de mods, y 23 de interfaz/teclado, cero fallos.
No se presentan como pruebas actuales ni como compatibilidad universal.
El entorno instalado ahora es RimWorld 1.7.4990 rev1069 (Waylith/.NET 10).
No se inició ni se cambió el juego durante este respaldo. El port 1.7 tenía una
marca «CANDIDATA-SIN-VALIDAR» y advertencias de referencias .NET 8/10; no hay
evidencia suficiente para declararlo estable o listo para Workshop.
Los resultados de compilación y pruebas de esta copia se detallan en
BACKUP-REVIEW.md.

## Pendientes y problemas conocidos

- Validar el port 1.7 en juego y revisar contratos Harmony, Nice, Dubs y CE.
- Revisar antes de integrar en 1.6 la corrección de columnas presente solo en el
  port 1.7. En la fuente 1.6, First puede fallar si una columna no tiene criterio.
  Se preserva el código original; este respaldo no modifica el comportamiento.
- Preparar un arnés de motor autónomo sin las partidas/instalaciones privadas del
  laboratorio original. La fuente del arnés está incluida; los escenarios no.
- Ingredientes y sustitutos son estimaciones conservadoras: no resuelven todas
  las asignaciones posibles, no reservan entre recetas ni verifican acceso real,
  investigación, habilidades o restricciones de cada proyecto.
- Las puntuaciones son relativas; los datos desconocidos no dan un total válido.
- DPS sostenido de CE es teórico: no incluye precisión, apuntado, armadura ni
  condiciones del colono. No garantiza daño real.
- No se ha probado cada combinación/version de mods ni multijugador.
- Las capturas y promociones incluidas provienen de 0.6.9; no demuestran visualmente
  las funciones nuevas de 0.6.10 ni el soporte 1.7.
- No se verificó el contenido publicado actualmente en Steam ni el estado de
  ramas privadas/no accesibles distintas de las consultadas.
