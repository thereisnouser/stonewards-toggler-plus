# Stonewards Toggler Plus

<img src="thunderstore/icon.png" alt="Toggler Plus icon" width="96" />

A BepInEx 5 mod for **Stonewards** that extends the game's built-in **Toggler** to include **default rings and tomes**. The additional entries are created using Stonewards' own `UnlockUpgradeButton` component and `unlockableButtonTemplate` UXML, not a separate custom UI.

> **Current development: v0.2.0 — local test required.** The working v0.1.0 sources and Thunderstore package assets are preserved on the [`release/0.1.0`](https://github.com/thereisnouser/stonewards-toggler-plus/tree/release/0.1.0) branch.

## What it does

- Adds eligible default (non-unlockable, non-hidden) rings and tomes to the corresponding original shop sections, where the original UI does not already contain them.
- Makes the existing checkbox available on these cards **only when the in-game Toggler upgrade has been purchased**.
- Calls the game's own `UpgradeManager.ToggleUpgradeAvailability`; disabled upgrades are stored in the native `ShopData` blacklist. No new save format, custom config, or direct save-file editing.
- Preserves normal unlockable upgrade buttons and the game's own reward-pool filtering.
- Leaves hidden upgrades excluded. Filters eligibility through `UpgradeManager.GetAvailableUpgrades` to avoid showing unavailable items.
- Has safety checks and writes diagnostic messages to the BepInEx log if the game UI/API differs.
- Adds a **per-class filter** above the in-game ring list: `All classes` keeps the vanilla global blacklist, while individual class tabs store additional ring bans for those classes.
- Class-specific bans are applied to the **local player's level-up ring pool**; they do not affect tome drops or the global ring blacklist.
- Class rules are stored in the plugin's BepInEx config, separate from game save data and Steam Cloud.

**Known limitation:** The UI integration locates each category's original grid by an existing native ring/tome card. If a category has no original card available as an anchor, the mod logs a warning and leaves that section unchanged rather than placing cards in the wrong container. This is to be tested.

## Requirements

- Stonewards (build matching the inspected game methods).
- BepInEx 5 installed through r2modman.
- .NET SDK for compiling the mod.

Keep the r2modman profile name identical to this repo: `stonewards-toggler-plus`.

## Build and install (Windows PowerShell)

Open PowerShell in the cloned repository and run:

```powershell
git pull --ff-only

powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir "F:\Steam\steamapps\common\Stonewards" `
  -ProfileDir "C:\Users\user\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\stonewards-toggler-plus"
```

Launch Stonewards using that r2modman profile. The script installs:

`BepInEx\plugins\StonewardsTogglerPlus\StonewardsTogglerPlus.dll`

The module should print `Toggler Plus initialized` in `BepInEx\LogOutput.log`. When the shop opens, it logs counts of new `LEVELUP` and `PICKUP` cards.

If you have not purchased the in-game Toggler upgrade, purchase it first. The mod does not grant it for free.

## Per-class ring toggles (0.2.0 test build)

1. Make sure the original game's **Toggler** upgrade is purchased.
2. Open the **Rings** tab in the game's original shop.
3. Use the new **All classes / [class name]** selector displayed near the ring list.
4. In **All classes**, toggles edit Stonewards' normal global blacklist.
5. Switch to a character class and disable a ring only for that class. Other classes can still receive it if it is globally enabled.
6. Global bans override per-class settings. To allow a ring for some classes, enable it in **All classes** first.
7. Start a new run using that class to verify that the disabled ring is absent from level-up choices. Check the other class separately, then restart the game and check persistence.

Configuration file: `BepInEx/config/com.thereisnouser.stonewards.togglerplus.cfg` inside the r2modman profile. These class rules are **local**; they are not automatically synchronized between PCs and should not be assumed to apply to other multiplayer clients.

**Test status:** v0.2.0 has not yet been compiled and checked in the user's game build. Do not publish this version to Thunderstore before verification.

## Packaging the stable v0.1.0 release

The published/stable release packaging files live on the protected `release/0.1.0` branch. Switch to that branch before creating a 0.1.0 ZIP:

```powershell
git switch release/0.1.0
git pull --ff-only

powershell -ExecutionPolicy Bypass -File .\package-thunderstore.ps1 `
  -GameDir "F:\Steam\steamapps\common\Stonewards" `
  -ProfileDir "C:\Users\user\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\stonewards-toggler-plus"
```

After compiling the **release/0.1.0** branch, the script creates `dist/TogglerPlus-0.1.0.zip`. This archive contains `manifest.json`, `README.md`, `CHANGELOG.md`, `icon.png` (256x256), and `plugins/StonewardsTogglerPlus.dll`.

Upload the resulting stable ZIP to the Stonewards community on [Thunderstore](https://thunderstore.io/c/stonewards/). Select the correct author/team account. Publishing requires a Thunderstore account and cannot be done by this local script.

Optional GitHub release, using GitHub CLI:

```powershell
gh release create v0.1.0 .\dist\TogglerPlus-0.1.0.zip `
  --title "Toggler Plus v0.1.0" `
  --notes "First release: adds default rings and tomes to the vanilla Stonewards Toggler."
```

## Test checklist

1. Open the original upgrade/unlock shop.
2. Confirm default rings and tomes are displayed in their respective sections, styled like standard entries.
3. Verify checkbox visibility when the built-in Toggler upgrade is purchased; check its hidden state before purchase.
4. Disable one default ring and one default tome. Close/reopen the shop, then restart the game and confirm both remain disabled.
5. Enter a **new** run (or refresh the upgrade draft); verify neither item is offered in its relevant reward pool.
6. Re-enable both, then verify they are eligible for rewards again.
7. Confirm normal unlockable upgrades and selection details still work.

If the cards are missing, or an error occurs, share the build output and the Toggler Plus lines from `BepInEx\LogOutput.log`. Disable the mod if it interferes with the UI.

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1 `
  -GameDir "F:\Steam\steamapps\common\Stonewards" `
  -ProfileDir "C:\Users\user\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\stonewards-toggler-plus"
```

Uninstalling removes the mod DLL but **does not clear blacklist selections** already saved by the game. To restore default availability, re-enable those items in the in-game Toggler before uninstalling.

## Developer notes

The original game code already filters blacklisted upgrades in:

- `RogueLikeUpgradeManager.CreateValidLevelupDraftPool()`
- `RogueLikeUpgradeManager.CreateValidPickupDraftPool()`

And the standard UI toggle invokes `UpgradeManager.ToggleUpgradeAvailability(ID)`, which updates `ShopData.blacklistedLevelupUpgradeIDs` / `ShopData.blacklistedPickupUpgradeIDs`.

Source files:

- `src/TogglerPlusPlugin.cs` — core Toggler integration (default build target).
- `src/ClassRingRules.cs` — per-class config, selector and ring reward-pool patch (v0.2.0).
- `src/DiagnosticsPlugin.cs` — optional read-only runtime inspector.
- `inspect-game.ps1` — Mono.Cecil IL inspector. Run after a game update to verify the integration points.
- `StonewardsTogglerDiagnostics.csproj` — optional diagnostic project; isolated from the actual mod build.

Generated IL reports, game binaries, and save files are not committed.

## License

No game assets or game binaries are distributed in this repository.
