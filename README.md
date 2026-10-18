# Stonewards TogglerPlus

**Choose which rings and tomes can appear in your Stonewards runs, globally or for individual character classes.** TogglerPlus expands the game's built-in Toggler while keeping its original upgrade cards and controls.

## ✨ Features

- **Default rings and tomes** — include eligible default upgrades in the game's existing Toggler, not just unlockable ones.
- **Global controls** — use **All classes** to enable or disable an upgrade for everyone through the original saved Toggler blacklist.
- **Per-class overrides** — explicitly allow or block rings and tomes for a selected character class, independently of the global setting.
- **Independent categories** — Rings and Tomes have their own class selectors and override rules.
- **Class-appropriate rings** — in a class tab, hide rings the game marks unavailable for that class; `All classes` still shows every ring.
- **Persistent choices** — global settings use Stonewards' existing save data; per-class settings are saved in your BepInEx profile.
- **Native-style interface** — pixel-stepped class buttons and the game's own upgrade cards, with no extra hotkeys or standalone menus.

## 🎮 How to use

1. Purchase the in-game **Toggler** upgrade through normal progression. TogglerPlus does not unlock it for free.
2. Open Stonewards' upgrade/unlock shop and select **Rings** or **Tomes**.
3. Choose **All classes** to change the default availability of an item using its existing checkbox.
4. Select a specific class to set an exception for that class. Each checkbox click saves an explicit **allowed** or **blocked** decision.
5. Switch between classes or categories as needed. Unmodified class entries inherit the global setting.
6. Start a new run or generate new reward choices to apply the updated availability rules.

**Example:** Disable a ring under **All classes**, then select **Miner** and enable the same ring. That ring is allowed for Miner while remaining disabled for classes without an explicit override. The reverse also works: leave a ring enabled globally, then disable it for Miner.

Class overrides are applied to **future local-player reward-pool generation**. They do not grant an upgrade, bypass normal class eligibility rules, or remove something already obtained.

**Ring list filtering:** Stonewards' own `RogueLikeUpgradeSO.IsClassValid(PlayerClass)` determines which ring cards are visible for the selected class. Universal rings remain visible, while other classes' exclusive rings are hidden. Switching back to `All classes` restores the complete list. Hidden cards retain their existing settings. Tome visibility is unchanged.

## ⚙️ Configuration

Most settings are controlled directly through the in-game Toggler.

- **All classes:** Stonewards' native global upgrade blacklist, saved by the game.
- **Individual classes:** independent ring and tome allow/deny overrides, saved in the local BepInEx configuration.

Per-class settings are stored in your active mod-manager profile:

`BepInEx/config/com.thereisnouser.stonewards.togglerplus.cfg`

| Configuration section | Purpose |
| --- | --- |
| `Per-class ring inclusions` | Rings explicitly allowed for a class, even when globally blocked |
| `Per-class ring exclusions` | Rings explicitly blocked for a class |
| `Per-class tome inclusions` | Tomes explicitly allowed for a class, even when globally blocked |
| `Per-class tome exclusions` | Tomes explicitly blocked for a class |

The mod saves these class rules as upgrade IDs. The buttons in the game are the intended way to edit them; manual configuration editing is not required. Per-class settings belong to the selected BepInEx profile, not to Stonewards' normal global Toggler blacklist.

## 📦 Installation

Install **TogglerPlus** in the Stonewards community using **r2modman** or another compatible Thunderstore mod manager, then launch the game through that profile. The package declares **BepInExPack 5** as a dependency.

For local builds, use a profile named `stonewards-toggler-plus` to match this repository. See **Development** for the existing build and packaging commands.

## 🔧 Compatibility

- **Game:** Stonewards; developed against the game's existing Toggler and upgrade-draft interfaces. The new 0.2.0 class-filtering behavior requires in-game verification.
- **Mod loader:** BepInEx 5 with Harmony (Thunderstore dependency: `BepInEx-BepInExPack-5.4.2305`).
- **Multiplayer:** Class overrides target the local player's reward draft. Synchronization and host/client behavior have not been independently verified.

## ⚠️ Known limitations

- **New in 0.2.0:** Class-based exceptions are implemented in source but require gameplay testing before a public release.
- The plugin needs an existing native card in each category to identify the correct Rings/Tomes grid. If the grid cannot be identified, it leaves that section unchanged.
- Explicitly allowing an upgrade overrides only the relevant blacklist decision. Other game restrictions still apply, so it does not guarantee that an upgrade will be offered.
- New rules affect future reward generation, not rewards already offered or collected.
- **Before uninstalling**, re-enable any globally disabled default rings or tomes you want available again. Removing the mod does not clear the game's saved blacklist.
- Per-class settings are stored locally in your BepInEx profile. They are not automatically synchronized across other profiles, computers, or multiplayer clients.

## 🛠️ Development

TogglerPlus is a BepInEx 5/Harmony plugin targeting `netstandard2.1`. It extends `UnlockableMenu` using the native `UnlockUpgradeButton` component and existing UXML templates. The global Toggler continues to write the game's `ShopData` blacklist. For per-class decisions, the plugin changes the blacklist result only while building the relevant local player's ring or tome reward draft.

The release build includes `src/TogglerPlusPlugin.cs` and `src/ClassRingRules.cs`. Optional diagnostics and IL inspection utilities are separate developer tools.

### Building

You need a local Stonewards installation, the .NET SDK, PowerShell, and a BepInEx 5 mod-manager profile. Close the game before installing a rebuilt DLL.

```powershell
$gameDir = Read-Host "Stonewards installation folder"
$profileDir = Join-Path $env:APPDATA "r2modmanPlus-local\Stonewards\profiles\stonewards-toggler-plus"

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

To compile and create the Thunderstore upload ZIP from the same release sources:

```powershell
powershell -ExecutionPolicy Bypass -File .\package-thunderstore.ps1 `
  -GameDir $gameDir `
  -ProfileDir $profileDir
```

The packager checks version consistency, compiled DLL metadata, the 256×256 icon, and required archive entries. Output: `dist/TogglerPlus-0.2.0.zip`. It does **not** publish the mod.

## 🔗 Links

[Report an issue](https://github.com/thereisnouser/stonewards-toggler-plus/issues) · [Changelog](thunderstore/CHANGELOG.md) · [Thunderstore description](thunderstore/README.md)

---

*Made by thereisnouser*
