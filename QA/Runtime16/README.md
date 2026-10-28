# Arnés aislado de RimWorld 1.6

Fuente propia preservada del laboratorio de 0.6.10. Necesita una copia legítima
de RimWorld 1.6, Harmony y el mod. No se incluyen dependencias, partidas, perfiles
de usuario, logs con rutas privadas ni una instalación del juego.

Compila con Build.ps1 proporcionando GamePath, HarmonyPath y ModAssembly.
Elige un perfil de datos de prueba independiente, crea la carpeta indicada por
MSS_PROBE_ROOT y un phase.txt con core o ce. El perfil necesita un escenario de
pruebas apropiado; no se ha reconstruido en este respaldo. El componente ejecuta
comprobaciones al cargar un mapa y puede crear objetos, modificarlo y cerrar el
proceso de prueba: nunca lo actives en una partida real. No está incluido en Mod.

La portabilidad del build se puede verificar sin ejecutar el juego. Los scripts
históricos Prepare-Phase/Run-Phase y sus rutas exactas quedan en el respaldo
privado, porque dependían de otros proyectos locales y junctions del juego.
