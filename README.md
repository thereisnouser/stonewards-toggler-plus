# Stonewards TogglerPlus

**Choose which default rings and tomes can appear in your Stonewards runs.** TogglerPlus extends the game's built-in Toggler to cover regular starting upgrades while keeping the original shop interface and persistence system.

## ✨ Features

- **Default rings and tomes** — add eligible default (non-unlockable, non-hidden) upgrades to their corresponding shop categories.
- **Original Toggler controls** — use the game's own upgrade cards and checkboxes instead of a separate mod menu.
- **Vanilla progression** — checkboxes become available only after you purchase the in-game Toggler upgrade.
- **Saved availability choices** — toggled upgrades use Stonewards' existing persistent blacklist and are filtered from newly generated reward selections.
- **Native UI integration** — existing unlockable upgrades remain available; the mod avoids duplicate cards and does not expose hidden upgrades.

## 🎮 How to use

1. Unlock the **Toggler** through normal Stonewards progression. The mod does **not** unlock it for free.
2. Open the game's original upgrade/unlock shop.
3. Find eligible default **rings** and **tomes** alongside the normal entries in their respective categories.
4. Use the original Toggler checkboxes to disable upgrades you do not want to receive.
5. Start a new run or generate a new upgrade choice to see the updated availability. Re-enable an upgrade to make it eligible again under the game's normal rules.

The mod works within the existing interface. There are **no additional hotkeys, panels, or mod-specific settings**.

**Before uninstalling:** re-enable any default rings or tomes you want to be able to receive again. Removing the plugin does not clear the blacklist saved by the game.

## 📦 Installation

Install **TogglerPlus** from the Stonewards Thunderstore community using **r2modman** or another compatible mod manager once the package is published. Launch the game using your modded profile. The package declares **BepInExPack 5** as a dependency.

The local r2modman development profile in this repository is named `stonewards-toggler-plus`.

For a source build, follow the commands in the [Development](#-development) section below.

## 🔧 Compatibility

- **Game:** Stonewards, using the upgrade shop and Toggler APIs examined during development. Compatibility after game changes has not been confirmed.
- **Mod loader:** BepInEx 5 (Thunderstore dependency: `BepInEx-BepInExPack-5.4.2305`).
- **Multiplayer:** The plugin changes the local upgrade shop and uses the game's existing availability data. Multiplayer behavior has not been separately verified.

## ⚠️ Known limitations

- A category must have an existing native ring/tome upgrade card for the mod to identify the correct grid. If none is available, the mod skips that category instead of inserting cards into an uncertain location.
- Disabling an upgrade affects **future reward selections**, not upgrades already offered or obtained in an ongoing run.
- **Saved blacklist entries persist after uninstall.** Re-enable your defaults in the Toggler before removing the mod.
- Changes to the game's upgrade shop or Toggler APIs may require an update.

## 🛠️ Development

TogglerPlus is a BepInEx 5 / Harmony plugin targeting `netstandard2.1`. It integrates with `UnlockableMenu`, reuses the game's `UnlockUpgradeButton` and UXML template, and enables the original checkboxes when the purchased Toggler level permits it. The game's own `UpgradeManager.ToggleUpgradeAvailability` and `ShopData` blacklist handle saved choices; the mod does not introduce a separate save format.

The repository's release build uses `src/TogglerPlusPlugin.cs`. The optional diagnostics plugin and IL inspector are development utilities, **not** extra released TogglerPlus features.

### Building

You need Stonewards, .NET SDK, Windows PowerShell, and a BepInEx 5 profile. Close the game before installing the plugin.

```powershell
$gameDir = "C:\path\to\Stonewards"
$profileDir = Join-Path $env:APPDATA "r2modmanPlus-local\Stonewards\profiles\stonewards-toggler-plus"

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

To compile and create a Thunderstore archive:

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
