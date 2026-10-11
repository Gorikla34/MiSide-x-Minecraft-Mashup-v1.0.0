# MiSide x Minecraft Mashup

Play as a Minecraft-style builder in Mita's house (MiSide Demo is the host game).

## What a player gets (design)
- Minecraft-style hotbar and inventory: Dirt, Stone, Wood, Glass, TNT.
- Place and remove blocks, light TNT. TNT only breaks blocks you placed; the house stays intact.
- Mita lives as usual but notices what you do and reacts to building and explosions.
- Cozy and funny mood. Singleplayer.

Controls: E inventory, 1-5 or mouse wheel select, right mouse place, left mouse remove, F light TNT.

## Status: SOURCE ONLY, UNTESTED
- `src/Plugin.cs` and `src/MiSideMinecraft.CI.csproj` were written without the game files and have never been run. GitHub Actions builds them and publishes a zip under Releases.
- MiSide Demo is a Unity IL2CPP game (GameAssembly.dll present), so the plugin targets BepInEx 6 IL2CPP. Not yet run in the game.
- Mita's reaction is a placeholder (she turns to face you); the real animations and voice lines need the game's own components.
- Not ready to publish. Still missing: a build, a working install recipe, a real gameplay screenshot, and a passing test in Melty.
- `gemini-code-1791696974148.json` is an earlier draft description and does not match a real build.

## Next steps (on a PC with MiSide Demo and Melty)
1. Install BepInEx 6 (Unity IL2CPP, x64) into the game folder, run the game once so it generates its files, then close it.
2. Fix compile errors, test in game, hook real Mita reactions.
3. Package `MiSideMinecraft.dll`, then validate, upload, test and publish with Melty.
