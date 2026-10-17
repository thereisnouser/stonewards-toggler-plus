# Stonewards Toggler Plus

A BepInEx 5 mod for **Stonewards** that extends the game's built-in **Toggler** to include **default rings and tomes**. The additional entries are created using Stonewards' own `UnlockUpgradeButton` component and `unlockableButtonTemplate` UXML, not a separate custom UI.

> **Status: 0.2.0 — per-class ring test build.** The earlier default-ring/tome toggling was tested by the project owner. New class-based rules still require a local compilation and in-game test.

## What it does

- Adds eligible default (non-unlockable, non-hidden) rings and tomes to the corresponding original shop sections, where the original UI does not already contain them.
- Makes the existing checkbox available on these cards **only when the in-game Toggler upgrade has been purchased**.
- Calls the game's own `UpgradeManager.ToggleUpgradeAvailability`; disabled upgrades are stored in the native `ShopData` blacklist. No new save format, custom config, or direct save-file editing.
- Preserves normal unlockable upgrade buttons and the game's own reward-pool filtering.
- Leaves hidden upgrades excluded. Filters eligibility through `UpgradeManager.GetAvailableUpgrades` to avoid showing unavailable items.
- Has safety checks and writes diagnostic messages to the BepInEx log if the game UI/API differs.
- Adds a compact class selector above the original ring list: **All classes** plus playable character classes.
- In **All classes**, a ring's native checkbox edits Stonewards' global blacklist, as before.
- In a **class tab**, ring checkboxes control additional bans **for that class only**, without changing the global blacklist.
- The game's existing class restrictions still apply; global bans always override class-specific settings.
- During level-up draft generation, the mod removes class-banned rings using the local player's `playerClass`.
- Class-specific ring rules persist to `BepInEx/config/com.thereisnouser.stonewards.togglerplus.cfg` in the selected r2modman profile. These are local mod settings and are **not** Steam Cloud synced.
- Tomes continue to use the original **global** toggle only.

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

## Test checklist

1. Open the original upgrade/unlock shop.
2. Confirm default rings and tomes are displayed in their respective sections, styled like standard entries.
3. Verify checkbox visibility when the built-in Toggler upgrade is purchased; check its hidden state before purchase.
4. Disable one default ring and one default tome. Close/reopen the shop, then restart the game and confirm both remain disabled.
5. Enter a **new** run (or refresh the upgrade draft); verify neither item is offered in its relevant reward pool.
6. Re-enable both, then verify they are eligible for rewards again.
7. Confirm normal unlockable upgrades and selection details still work.
8. Select a character tab (for example, Miner) and disable a ring that is globally enabled.
9. Return to **All classes**: the ring should remain enabled globally.
10. Switch back to the character tab: the ring should remain disabled for that class.
11. Start a run as that class and reroll several level-up drafts. The ring must not appear.
12. Start a run as a different class; if the ring is valid for that class, it should remain eligible.
13. Restart the game and verify the class-specific setting is retained. Confirm that disabling a tome still applies globally.

**Note:** A globally disabled ring remains disabled in every class, even if a class tab shows it as individually enabled. To allow a ring in some classes, first enable it in **All classes**, then disable it only in the unwanted class tabs.

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

- `src/TogglerPlusPlugin.cs` — actual test mod (default build target).
- `src/ClassRingRules.cs` — class selector, class-specific config, and local level-up draft filtering.
- `src/DiagnosticsPlugin.cs` — optional read-only runtime inspector.
- `inspect-game.ps1` — Mono.Cecil IL inspector. Run after a game update to verify the integration points.
- `StonewardsTogglerDiagnostics.csproj` — optional diagnostic project; isolated from the actual mod build.

Generated IL reports, game binaries, and save files are not committed.

## License

No game assets or game binaries are distributed in this repository.
