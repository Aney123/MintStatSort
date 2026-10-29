# MintStatSort

Development backup reviewed on October 28, 2026. Stable 0.6.10 targets RimWorld
1.6. A local, unvalidated 1.7 adaptation is preserved separately under
`Experimental/RimWorld17`; it is not a confirmed 1.7 release.

Search, compare and sort workbench recipes. Includes multiple categories,
weighted criteria, numeric limits, quality/material references, pinned recipes,
profiles, ingredient estimates, optional CE ammunition and theoretical sustained
DPS, compact panels and keyboard shortcuts. English, Spanish and Latin American
Spanish. Harmony is required; Nice Bill Tab, Dubs Mint Menus and CE are optional.

With .NET SDK 8.0.425+ in the 8.x series, run:
```
dotnet restore Source/MintStatSort.csproj --locked-mode
dotnet build Source/MintStatSort.csproj -c Release --no-restore
```
The own mod DLL is included in `Mod/Assemblies`; no game/third-party DLL is bundled.
For RimWorld 1.6, close the game and run `Instalar-mod.cmd`, or copy `Mod` to the
game's Mods/MintStatSort folder. Enable Harmony first. Publishing to Workshop is
a separate, user-initiated action.

See [README.md](README.md), [DEVELOPING.md](DEVELOPING.md),
[status](docs/STATUS.md) and [NOTICE.md](NOTICE.md).
