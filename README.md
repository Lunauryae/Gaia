# Gaia

> Full-featured FFXIV gardening plugin.

Gaia is a comprehensive FFXIV gardening plugin with automation, planning,
crossbreeding optimization, and status tracking. Distributed via custom
Dalamud repo for friends and the community who find it useful.

## Features

- **Farming automation** — fertilize, water, and harvest entire beds with a single click
- **Planting automation** — queue up seed+soil combinations across multiple beds
- **Crossbreeding optimizer** — plan neighbor layouts from a community crossbreed dataset
- **Status tracking** — live progress indicators, garden state, and per-character profiles
- **Smart fertilizer selection** — pick your fertilizer (Fishmeal, etc.) from a dropdown before applying
- **Configurable timing** — tune action delays to match your playstyle and keep the game happy
- **Character & location aware** — tracks class, level, and zone so multi-character setups just work

## Installation

1. Open FFXIV and type `/xlsettings`.
2. Go to the **Experimental** tab.
3. Scroll to **Custom Plugin Repositories**.
4. Paste the Gaia repo URL into an empty box: `https://raw.githubusercontent.com/Lunauryae/Gaia/main/repo.json`
5. Hit **+**, make sure Enable is checked, then **Save and Close**.
6. Open `/xlplugins`, search for **Gaia**, and install.
7. In game, type `/gaia` to open the main window.

## Releasing a new version (for the developer)

1. Bump the `<Version>` in `Gaia/Gaia.csproj` (e.g. `1.0.1.0` → `1.0.2.0`).
2. Update `AssemblyVersion` in `manifest.json` and `repo.json` to match.
   (`Gaia/Gaia.json` has no `AssemblyVersion` field — DalamudPackager
   injects it from the csproj `<Version>` at build time.)
3. Update `LastUpdate` in `repo.json` to the current Unix epoch (seconds).
   On Windows PowerShell: `[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()`.
   On bash: `date +%s`.
4. Commit and push to `main`.
5. Tag the commit with the version: `git tag v1.0.2 && git push --tags`.
6. GitHub Actions `release.yml` fires on tag push, builds, and attaches
   `latest.zip` to a new GitHub Release automatically.
7. Dalamud users see the update within ~6 hours (default repo cache TTL).
   To force a refresh: `/xlsettings` → Experimental → toggle the repo
   off and on, then **Save and Close**.

Future improvement: a `scripts/bump-version.ps1` that does steps 1–3
automatically given a target version.

## Credits

Crossbreed data from the FFXIV community crossbreed spreadsheet. Gardening
knowledge from FFXIV Gardening and community gardening data. Visual
crossbreed reference: FFXIV Gardening Cross Diagrams. Related work:
FFXIV-Crossbreed-Helper by nick75g.
