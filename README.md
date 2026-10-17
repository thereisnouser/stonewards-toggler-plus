# Stonewards TogglerPlus

**Choose which rings and tomes can appear in your Stonewards runs — globally or per character class.** TogglerPlus extends the game's built-in Toggler to cover regular starting upgrades while keeping the original shop interface and persistence system.

## ✨ Features

- **Default rings and tomes** — add eligible default (non-unlockable, non-hidden) upgrades to their corresponding shop categories.
- **Original Toggler controls** — use the game's own upgrade cards and checkboxes instead of a separate mod menu.
- **Vanilla progression** — checkboxes become available only after you purchase the in-game Toggler upgrade.
- **Saved availability choices** — toggled upgrades use Stonewards' existing persistent blacklist and are filtered from newly generated reward selections.
- **Native UI integration** — existing unlockable upgrades remain available; the mod avoids duplicate cards and does not expose hidden upgrades.

## 💍 Per-class ring exclusions (0.2.0 test build)

The **Rings** shop tab now includes a small selector:

- **All classes:** keep Stonewards' normal global upgrade toggles. A ring disabled here is disabled for every class.
- **Individual class:** change additional ring bans for that class alone. These changes do not modify the global blacklist.
- **Tomes:** continue using the original global Toggler controls.

Class-specific settings are kept in the profile's BepInEx configuration file `com.thereisnouser.stonewards.togglerplus.cfg`. They are local to the profile; do not assume they synchronize via Steam Cloud or automatically apply to other players in multiplayer.

**Testing:** Version 0.2.0 restores the prior experimental implementation. Compilation and in-game UI behavior still need verification. The 0.1.1 source state is preserved at [backup/pre-class-filters-0.1.1](https://github.com/thereisnouser/stonewards-toggler-plus/tree/backup/pre-class-filters-0.1.1).

## 🎮 How to use

1. Unlock the **Toggler** through normal Stonewards progression. The mod does **not** unlock it for free.
2. Open the game's original upgrade/unlock shop.
3. Find eligible default **rings** and **tomes** alongside the normal entries in their respective categories.
4. Use the original Toggler checkboxes to disable upgrades you do not want to receive.
5. Start a new run or generate a new upgrade choice to see the updated availability. Re-enable an upgrade to make it eligible again under the game's normal rules.

The mod uses the original shop interface. In v0.2.0, rings also have a class selector; no new hotkey is needed.

**Before uninstalling:** re-enable any default rings or tomes you want to be able to receive again. Removing the plugin does not clear the blacklist saved by the game.

## 📦 Installation

Install **TogglerPlus** from the Stonewards Thunderstore community using **r2modman** or another compatible mod manager once the package is published. Launch the game using your modded profile. The package declares **BepInExPack 5** as a dependency.

The local r2modman development profile in this repository is named `stonewards-toggler-plus`.

For a source build, follow the commands in the [Development](#-development) section below.

## 🔧 Compatibility

- **Game:** Stonewards, using the upgrade shop and Toggler APIs examined during development. Compatibility after game changes has not been confirmed.
- **Mod loader:** BepInEx 5 (Thunderstore dependency: `BepInEx-BepInExPack-5.4.2305`).
- **Multiplayer:** Global toggles use the game's existing availability data. Per-class rules filter the local player's ring choices; multiplayer behavior has not yet been separately verified.

## ⚠️ Known limitations

- A category must have an existing native ring/tome upgrade card for the mod to identify the correct grid. If none is available, the mod skips that category instead of inserting cards into an uncertain location.
- Disabling an upgrade affects **future reward selections**, not upgrades already offered or obtained in an ongoing run.
- **Saved blacklist entries persist after uninstall.** Re-enable your defaults in the Toggler before removing the mod.
- Changes to the game's upgrade shop or Toggler APIs may require an update.

## 🛠️ Development

TogglerPlus is a BepInEx 5 / Harmony plugin targeting `netstandard2.1`. It integrates with `UnlockableMenu`, reuses the game's `UnlockUpgradeButton` and UXML template, and enables the original checkboxes when the purchased Toggler level permits it. The game's own `UpgradeManager.ToggleUpgradeAvailability` and `ShopData` blacklist handle global saved choices. Per-class ring exclusions are stored separately in the BepInEx plugin config.

The 0.2.0 test build compiles `src/TogglerPlusPlugin.cs` and `src/ClassRingRules.cs`. The optional diagnostics plugin and IL inspector are development utilities, **not** extra released TogglerPlus features.

### Building

You need Stonewards, .NET SDK, Windows PowerShell, and a BepInEx 5 profile. Close the game before installing the plugin.

```powershell
$gameDir = "C:\path\to\Stonewards"
$profileDir = Join-Path $env:APPDATA "r2modmanPlus-local\Stonewards\profiles\stonewards-toggler-plus"

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

To compile and create a **stable Thunderstore archive**, switch back to the appropriate stable source branch first. The main branch is currently a test build; do not package it as version 0.1.1 until its manifest/changelog match:

```powershell
powershell -ExecutionPolicy Bypass -File .\package-thunderstore.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

The resulting package is written to `dist/TogglerPlus-<version>.zip` and includes the compiled plugin, manifest, dedicated Thunderstore README, changelog, and icon. The script does **not** publish to Thunderstore.

## 🔗 Links

[Thunderstore description](thunderstore/README.md) · [Changelog](thunderstore/CHANGELOG.md) · [Report an issue](https://github.com/thereisnouser/stonewards-toggler-plus/issues)

---

*Made by thereisnouser*
