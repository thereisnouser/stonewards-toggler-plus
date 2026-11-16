# Stonewards TogglerPlus

**Choose which rings and tomes can appear in your Stonewards runs, globally or for individual character classes.** TogglerPlus extends the game's built-in Toggler without replacing its upgrade shop.

## ✨ Features

- **Default upgrades** — manage eligible default rings and tomes alongside normally unlockable upgrades in the original Toggler.
- **Global and per-class rules** — use **ALL** for the game's global availability setting, then allow or block individual upgrades for specific classes, including exceptions to global bans.
- **Separate Rings and Tomes filters** — each category has its own class selector and independently saved overrides.
- **Class-aware ring list** — class tabs hide rings the selected class cannot receive; **ALL** shows the complete list. Tome cards are not filtered by class.
- **Game-style controls** — original upgrade cards, class icons, pixel-stepped filter buttons and readable labels, with no new hotkeys.
- **Persistent choices** — global settings use Stonewards' saved blacklist; class overrides are stored in your BepInEx profile. The mod also avoids repeated player-class lookups during reward-pool generation.

## 🎮 How to use

1. Purchase Stonewards' **Toggler** upgrade through normal progression.
2. Open the game's upgrade/unlock shop and select **Rings** or **Tomes**.
3. Select **ALL** to enable or disable an upgrade for all classes by default.
4. Select a class and use the upgrade's checkbox to explicitly allow or block it for that class. Classes without an override follow the **ALL** setting.

For example, disable a ring under **ALL**, switch to **Miner**, and enable that ring. Miner can then receive it while other classes without an override still follow the global ban. You can also leave an upgrade enabled globally and block it for a specific class.

Changes affect **future reward choices** for the local player. They do not grant upgrades, bypass class eligibility rules or remove already acquired rewards. Switching class tabs changes which ring cards are displayed, not their saved settings.

**Before uninstalling:** Re-enable any globally disabled default upgrades that you want available without the mod. Uninstalling TogglerPlus does not clear the blacklist saved by Stonewards.

## ⚙️ Configuration

Use the in-game Toggler checkboxes to change the rules. The **ALL** tab updates the game's existing global blacklist; per-class decisions are saved in your active mod-manager profile:

`BepInEx/config/com.thereisnouser.stonewards.togglerplus.cfg`

| Configuration section | Purpose |
| --- | --- |
| `Per-class ring inclusions` | Rings explicitly allowed for a class, even if globally blocked |
| `Per-class ring exclusions` | Rings explicitly blocked for a class |
| `Per-class tome inclusions` | Tomes explicitly allowed for a class, even if globally blocked |
| `Per-class tome exclusions` | Tomes explicitly blocked for a class |

These settings contain upgrade IDs and are specific to the selected BepInEx profile. You normally do not need to edit the file manually.

## 📦 Installation

Install [TogglerPlus on Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/TogglerPlus/) using **r2modman** or another compatible Thunderstore mod manager, then launch **Stonewards** through that profile. The package declares **BepInExPack 5** as a dependency.

## 🛠️ Development

TogglerPlus is a BepInEx 5/Harmony plugin targeting `netstandard2.1`. It adds default-upgrade cards to `UnlockableMenu` using the game's `UnlockUpgradeButton` and UXML assets. The global blacklist remains managed by Stonewards; class-specific overrides adjust blacklist checks when the local player's ring or tome reward pool is generated.

### Building

You need the .NET SDK, PowerShell, a local Stonewards installation, and BepInEx 5 files (for example, in an r2modman profile). The release build includes `src/TogglerPlusPlugin.cs` and `src/ClassRingRules.cs`; the diagnostics project is separate.

Close the game before installing a rebuilt DLL. From the repository root, run:

```powershell
$gameDir = Read-Host "Stonewards installation folder"
$profileDir = Join-Path $env:APPDATA "r2modmanPlus-local\Stonewards\profiles\stonewards-toggler-plus"

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

To build and package the same release sources for Thunderstore:

```powershell
powershell -ExecutionPolicy Bypass -File .\package-thunderstore.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

The packager validates the manifest, plugin and assembly versions, the icon dimensions, and required ZIP entries. It creates `dist/TogglerPlus-0.2.2.zip` but does not upload it.

## 🔗 Links

[Thunderstore](https://thunderstore.io/c/stonewards/p/thereisnouser/TogglerPlus/) · [Report an issue](https://github.com/thereisnouser/stonewards-toggler-plus/issues) · [Changelog](thunderstore/CHANGELOG.md)

---

*Made by thereisnouser*
