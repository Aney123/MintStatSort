# Mint Stat Sort

Sort crafting recipes by damage, armor protection and other stats in **RimWorld 1.6**, with **Nice Bill Tab** or **Dubs Mint Menus**. Includes English and Spanish UI translations.

**[Download Mint Stat Sort 0.3 for Windows](https://github.com/Aney123/MintStatSort/releases/download/v0.3.0/MintStatSort-0.3-SelectorVisual.zip)** · [All releases](https://github.com/Aney123/MintStatSort/releases) · [Español](README.md)

## Features

- Select multiple goals, such as sharp and blunt melee damage, or higher protection and lower mass.
- Rank recipes by balance across all selected goals, with equal importance for each goal.
- Browse grouped stats, recommended stats, visual categories and material icons.
- Show material quantities stored in stockpiles and shelves on the current map.
- Keep the crafting menu's own recipe cards and actions. Existing bills retain their priority.

## Requirements and installation

Requires **RimWorld 1.6**, [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077), and at least one of [Nice Bill Tab](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671) or [Dubs Mint Menus](https://steamcommunity.com/sharedfiles/filedetails/?id=1446523594).

1. Download the mod ZIP above, close RimWorld and extract the complete archive.
2. Run `Instalar-mod.cmd` on Windows. It detects Steam installations and can ask for the game folder.
3. Enable Harmony, your crafting menu and Mint Stat Sort, in that order, then restart the game.

For manual installation, copy the archive's `Mod` folder into `RimWorld/Mods/MintStatSort`, so `About`, `Assemblies` and `Languages` sit directly inside the installed mod folder. Remove older copies from `Mods`. The installer backs them up outside `Mods`, under `RimWorld/ModBackups`.

The mod DLL is included. No compilation or online service is needed to play.

## Usage

Open a crafting bench, select a category, then click **Characteristics…**. Check one or more stats and select whether higher or lower values are better. Apply the selection to rank available recipes.

A single stat displays its actual reference value; several stats produce a relative score among the visible, comparable recipes. Reference values use **Normal quality**. The material selection is a comparison setting and does not change bill ingredients. Stored quantities do not account for reservations or guarantee that a recipe can be crafted.

## Validation and limitations

Built against RimWorld **1.6.4871** reference assemblies. Automated checks cover ranking, storage quantities, Nice Bill Tab/Dubs Mint Menus integration signatures and installer syntax. Full in-game UI testing has not been performed in the development environment.

Nice Bill Tab - Expansion, multiplayer patches, Combat Extended and custom combat/storage systems may need additional integration. Missing values remain at the end of the list. The Spanish [full guide](LEEME.md) documents scoring and compatibility in more detail.

Report problems in [Issues](https://github.com/Aney123/MintStatSort/issues), including your RimWorld version, crafting menu, reproduction steps and `Player.log` when relevant.

Build instructions are in [DEVELOPING.md](DEVELOPING.md). The original source is published for viewing and downloading; no general modification or redistribution license has been granted. See [NOTICE.md](NOTICE.md).
