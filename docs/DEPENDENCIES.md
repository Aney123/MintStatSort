# Dependencias y terceros

| Uso | Paquete o producto | Versión declarada |
| --- | --- | --- |
| Referencias de compilación | Krafs.Rimworld.Ref (NuGet) | 1.6.4871 |
| API de parches | Lib.Harmony (NuGet) | 2.3.3 |
| Referencias .NET Framework | Microsoft.NETFramework.ReferenceAssemblies | 1.0.3 |
| Inspección de metadatos de pruebas | Mono.Cecil | 0.11.5 |
| Analizador PowerShell de pruebas | System.Management.Automation | 7.4.7 |
| Compilar y ejecutar pruebas | SDK .NET | 8.0.425, roll-forward 8.x |
| Jugar estable | RimWorld + Harmony | 1.6; Harmony compatible instalado por el usuario |

NuGet.Config usa exclusivamente nuget.org y no contiene credenciales. Los dos
packages.lock.json fijan dependencias transitivas y hashes de contenido. Los
paquetes se restauran; este repositorio no los redistribuye como DLL separadas.
Cada dependencia conserva sus condiciones. No se inventa una licencia nueva:
NOTICE.md mantiene la política existente del repositorio.

Nice Bill Tab, Dubs Mint Menus y CE se detectan opcionalmente. Sus DLL, el juego,
Unity, UnityStubs, Harmony, partidas y mods de compatibilidad no se incluyen.
Para compilar el port experimental se requieren referencias locales legítimas de
RimWorld 1.7 y Harmony 1.7; el script no las copia al proyecto.

Las capturas muestran la interfaz y recursos de RimWorld y posibles mods; son
material ilustrativo, no recursos extraídos para redistribuir el juego. La galería
contiene ediciones promocionales y una portada preparada para este mod. Se
conservan como material del proyecto sin afirmar derechos exclusivos sobre los
elementos de terceros. No se ha hecho una revisión jurídica de sus licencias.
