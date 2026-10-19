# Stonewards TogglerPlus

**Control which rings and tomes can appear in your runs — for all classes or for individual character classes — using Stonewards' built-in Toggler.**

## ✨ Features

- **Default upgrades** — toggle eligible default rings and tomes alongside the game's ordinary unlockable upgrades.
- **ALL** — globally enable or disable an upgrade with the original Toggler checkbox.
- **Per-class exceptions** — allow or block an upgrade for a chosen class, even when the global setting says otherwise.
- **Separate Rings and Tomes controls** — each shop category has its own class selector and saved overrides.
- **Native-style interface** — uses the existing upgrade cards, class icon sprites and pixel-stepped buttons with larger text, without separate menus or hotkeys.
- **Class-aware ring list** — a class tab shows only rings eligible for that class; ALL shows the complete list.
- **Optimized reward drafting** — caches class lookups to reduce work whenever a new ring or tome reward pool is generated.
- **Saved choices** — global rules use the game's existing blacklist; class-specific rules use your local BepInEx configuration.

## 🎮 How to use

1. Purchase Stonewards' in-game **Toggler** upgrade through normal progression.
2. Open the original upgrade/unlock shop and switch to **Rings** or **Tomes**.
3. Choose **ALL** to edit the global defaults.
4. Choose a class to explicitly enable or disable a particular upgrade for that class.

**Example:** Disable a ring in **ALL**, then select **Miner** and enable it. Miner can receive that ring while other classes without overrides still follow the global ban.

Class-specific rules affect **future local-player reward choices**. They cannot force invalid rewards to appear or remove upgrades already acquired.

## ⚙️ Configuration

Use the in-game checkboxes for everyday changes. Class-specific inclusions and exclusions are saved separately for Rings and Tomes in your active mod-manager profile:

`BepInEx/config/com.thereisnouser.stonewards.togglerplus.cfg`

The **ALL** setting continues to use the game's saved Toggler blacklist. No additional hotkeys are required.

## 📦 Installation

Install **TogglerPlus** with **r2modman** or another compatible Thunderstore mod manager, then launch Stonewards through your modded profile. BepInExPack 5 is declared as a dependency.

## 🔧 Compatibility

- **Game:** Stonewards; uses the game's built-in Toggler and upgrade shop.
- **Mod loader:** BepInEx 5.
- **Multiplayer:** Class-specific filtering targets the local player's reward drafts; multiplayer synchronization has not been verified.

## ⚠️ Known limitations

Per-class rules are local to your BepInEx profile and have not been independently verified for multiplayer synchronization. An explicit class allowance overrides only the blacklist, not other game restrictions, and never guarantees a particular reward.

**Before uninstalling:** Re-enable globally blocked default upgrades if you want them available in the unmodded game. Uninstalling the plugin does not clear the game's saved blacklist.

## 🔗 Links

[GitHub](https://github.com/thereisnouser/stonewards-toggler-plus) · [Report an issue](https://github.com/thereisnouser/stonewards-toggler-plus/issues) · [Changelog](https://github.com/thereisnouser/stonewards-toggler-plus/blob/main/thunderstore/CHANGELOG.md)

---

*Made by thereisnouser*
