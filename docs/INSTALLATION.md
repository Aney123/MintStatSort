# Instalación

El paquete estable `Mod` es para RimWorld 1.6 y necesita Harmony. Nice Bill Tab,
Dubs Mint Menus y Combat Extended son opcionales y se cargan antes de MintStatSort.

1. Cierra RimWorld.
2. En Windows ejecuta Instalar-mod.cmd. Si no detecta Steam, indica la carpeta
   de RimWorld que contiene Mods. Puedes pasar `-RimWorldPath "RUTA"` al script
   Windows/Instalar-mod.ps1 directamente.
3. Reinicia con Harmony y MintStatSort activos. Selecciona el idioma deseado.

La instalación usa una carpeta temporal, respalda el paquete anterior en
ModBackups y conserva About/PublishedFileId.txt. Para instalación manual, copia
solo `Mod` a Mods/MintStatSort; conserva tu ID si vas a actualizar Workshop.
Los perfiles y preferencias personales viven en los datos del juego, no en GitHub.

La publicación en Steam se inicia por separado desde el menú de mods del juego;
instalar este respaldo no publica ni actualiza el artículo. La galería se sube
manualmente. Se mantiene la portada horizontal y la descripción completa EN/ES.

RimWorld 1.7: no uses este instalador estable para declarar soporte de 1.7.
La adaptación conservada necesita su propia validación; consulta Experimental.
