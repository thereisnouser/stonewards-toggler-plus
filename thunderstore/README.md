# Stonewards TogglerPlus

**Choose which rings and tomes can appear in your Stonewards runs, globally or for individual character classes.** TogglerPlus extends the game's built-in Toggler without replacing its upgrade shop.

## ✨ Features

- **Default upgrades** — toggle eligible default rings and tomes alongside the normal unlockable upgrades.
- **ALL and class overrides** — set global availability, then explicitly allow or block upgrades for individual classes, even when the global setting differs.
- **Separate category controls** — Rings and Tomes have independent class selectors and saved rules.
- **Class-aware ring list** — a class tab shows only rings eligible for that class; **ALL** shows every ring. Tome cards remain visible across class tabs.
- **Native-style interface** — original upgrade cards, class icons and stepped pixel buttons, with no extra hotkeys.
- **Saved choices** — global settings use Stonewards' original blacklist; per-class settings are stored in your BepInEx profile.

## 🎮 How to use

1. Purchase the in-game **Toggler** upgrade through normal progression.
2. Open the upgrade/unlock shop and choose **Rings** or **Tomes**.
3. Select **ALL** to enable or disable an upgrade globally.
4. Select a class to explicitly enable or disable that upgrade for the class.

**Example:** Disable a ring under **ALL**, then enable it for **Miner**. Miner can receive the ring while classes without an override still follow the global ban.

Rules affect future local-player reward choices. They do not grant upgrades, bypass class restrictions or remove rewards already obtained.

**Before uninstalling:** Re-enable globally disabled default upgrades you still want to receive. Removing the mod does not clear Stonewards' saved blacklist.

## ⚙️ Configuration

Manage availability with the game's Toggler checkboxes. Per-class ring and tome inclusions/exclusions are saved in your active profile at:

`BepInEx/config/com.thereisnouser.stonewards.togglerplus.cfg`

**ALL** continues to use the game's saved global blacklist. Per-class settings are local to the selected BepInEx profile.

## 📦 Installation

Install **TogglerPlus** using **r2modman** or another compatible Thunderstore mod manager, then launch **Stonewards** through the manager. The package declares **BepInExPack 5** as a dependency.

## 🔗 Links

[GitHub](https://github.com/thereisnouser/stonewards-toggler-plus) · [Report an issue](https://github.com/thereisnouser/stonewards-toggler-plus/issues) · [Changelog](https://github.com/thereisnouser/stonewards-toggler-plus/blob/main/thunderstore/CHANGELOG.md)

---

*Made by thereisnouser*
