# Estructura

```
Source/                     Código estable C# net472 y proyecto .csproj
Tests/                      Pruebas de lógica, contratos y del instalador
Mod/About/                  Metadatos y portada, sin ID de publicación local
Mod/Assemblies/              Solo la DLL propia MintStatSort.dll
Mod/Languages/               English, Spanish, SpanishLatin
Mod/Publicacion-Workshop/    Descripciones, notas, guías y recursos de publicación
Windows/                    Instalador con respaldo y preservación de ID
Experimental/RimWorld17/     Snapshot separado y script de compilación del port local
QA/Runtime16/               Fuente portátil del arnés de pruebas aisladas
docs/                       Dependencias, estado e inventario de recuperación
```

Controls/ComparisonWindow gestionan la UI; RecipeSession y BudgetedWork preparan
datos por etapas. SortingCore normaliza y combina criterios; Metrics y
CombatExtendedIntegration leen estadísticas. IngredientInspector y las clases
Allocation estiman existencias. Settings/ComparisonPreferences y ProfileDocument
persisten ajustes. VanillaIntegration, NiceIntegration y MintIntegration
conectan el comparador con las acciones nativas. Workshop* limita la integración
de publicación a este packageId. El port 1.7 conserva un conjunto independiente
de fuentes y no cambia el Mod estable.
