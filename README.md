# Stonewards Toggler Plus

An in-progress BepInEx mod for **Stonewards**, intended to extend the in-game **Toggler** so players can disable **default rings and tomes** as well as unlockable upgrades.

> **Status: research / diagnostics only.** This repository does **not** contain a working Toggler gameplay patch yet. The initial code inspects the installed game's current assembly and loaded upgrade assets so that the final patch can target verified APIs instead of guessing.

## Intended functionality

- Expose initially available (default) rings and tomes in the existing Toggler interface.
- Keep ordinary unlockable-upgrade toggling intact.
- Persist disabled defaults without modifying Steam Cloud progression.
- Apply the exclusions when generating new rewards, not to already selected upgrades.
- Respect character and multiplayer rules, and avoid empty-pool failures.

These are **planned** features, not implemented ones.

## Inspect the game (no build required)

Run from the repository directory in **Windows PowerShell**:

```powershell
powershell -ExecutionPolicy Bypass -File .\inspect-game.ps1 `
  -GameDir "F:\Steam\steamapps\common\Stonewards" `
  -ProfileDir "C:\Users\user\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\stonewards"
```

The inspector reads `Stonewards_Data\Managed\Assembly-CSharp.dll` using `Mono.Cecil.dll` from BepInEx and creates:

| Output file | Purpose |
| --- | --- |
| `out/01-types-and-methods.txt` | Game types, fields, and method signatures |
| `out/02-relevant-method-il.txt` | Relevant IL instructions |
| `out/03-calls-and-references.txt` | References to the Toggler/upgrade pathways |

Neither this script nor these generated reports change game files or save data. If the script cannot find `Mono.Cecil.dll`, verify that BepInEx 5 is installed and enabled in the selected profile.

## Optional: loaded upgrade diagnostics

Requires the **.NET SDK** and a BepInEx 5 profile. Build and install the read-only diagnostic plugin:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-and-install.ps1 `
  -GameDir "F:\Steam\steamapps\common\Stonewards" `
  -ProfileDir "C:\Users\user\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\stonewards"
```

Launch Stonewards. When upgrade assets have loaded, the plugin writes `BepInEx\config\StonewardsTogglerDiagnostics.upgrades.txt` inside the selected game/profile location. This report is a **partial runtime snapshot** if some assets have not yet loaded.

Remove diagnostics afterward:

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1 `
  -GameDir "F:\Steam\steamapps\common\Stonewards" `
  -ProfileDir "C:\Users\user\AppData\Roaming\r2modmanPlus-local\Stonewards\profiles\stonewards"
```

Uninstalling the diagnostic plugin does not delete the reports.

## Next development step

Inspect the reports, identify the actual vanilla Toggler rendering, persistence, and reward-filtering methods, then implement and test a Harmony patch against the current build. Do not publish this repository as a usable mod before that is done.

Game assemblies, BepInEx binaries, local reports, and save files are intentionally excluded from version control.
