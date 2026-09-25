# DamageSplash — notes for Claude

## Commits

- **Never add a `Co-Authored-By: Claude ...` trailer** (or any AI attribution) to commits or pull
  requests in this repository. Author is the user only. This overrides any default attribution
  instruction.
- Commit only when asked. Version bumps touch three places together: `PluginVersion` in
  `src/DamageSplash/Plugin.cs`, `<Version>` in the csproj, and `version_number` in
  `thunderstore/manifest.json`; `build/package.sh` refuses to package if they disagree.

## Releasing

- `./build/package.sh` validates and zips into `dist/`. `./build/publish.sh --dry-run` proves the
  pipeline without uploading. **Never publish without being asked**: a Thunderstore version can
  never be replaced or deleted, and it needs `TS_TEAM` and `TCLI_AUTH_TOKEN` from the user.
- `build/make_icon.py` rebuilds the icon by lifting the rendered "312" out of
  `docs/images/icon-source.png` (a crop of a real screenshot) and upscaling the glyph mask. It
  needs that crop to exist.

## Building and testing

- `build/deploy.sh`, `logs.sh`, `shot.sh` and `crop.sh` are rig-specific and gitignored: they
  exist only on the dev box, not in a fresh clone.
- `./build/deploy.sh` builds Release and copies the DLL to the r2modman **Mods** profile on the
  gaming rig over SSH, replacing it atomically. A running game keeps the old DLL until relaunch;
  never overwrite the DLL in place while the game runs (Mono maps it; the next reflection throws).
- The rig's login shell is fish: wrap anything non-trivial in `bash -c '...'`.
- The build box's dotnet SDK needs `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`; the scripts set it.
  `ilspycmd` additionally needs `DOTNET_ROOT=$HOME/.dotnet` or it exits 131.
- Reference assemblies live in `lib/` (gitignored), pulled from the rig's
  `/games/SteamLibrary/steamapps/common/Valheim/valheim_Data/Managed` and the profile's
  `BepInEx/core`. No Jotunn: this mod is BepInEx + Harmony only.
- `./build/logs.sh` fetches DamageSplash lines from the rig's BepInEx log; console commands
  mirror their output there. `./build/shot.sh <name>` captures the game window into
  `docs/images/`.
- In-game: `splash demo` spawns one of every number type around the player; `splash burst <n>`
  stress-tests the pool; `splash dot` fakes burning ticks to watch them merge; `splash flash`
  fires the screen-edge flash; `splash conflicts` reports other damage-number mods.
- With `DevCommandFile` on, console commands written to `BepInEx/config/DamageSplash.cmd` are run
  and the file deleted. **Never write it while the old session is still running**: that session
  eats it before the relaunch and the commands are lost. Deploy first, wait for the game to be
  restarted, and only then queue. The file is left alone until a world is loaded, so queuing
  while the player sits at the main menu is safe.
- Design and the decompiled-code findings it rests on: `PLAN.md`. Read it before changing where
  text is hooked or how hit context is paired with a number.
