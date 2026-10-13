# MiSide x Minecraft Mashup

Play as a Minecraft-style builder in Mita's house (MiSide Demo is the host game).

## What a player gets (design)
- Real Minecraft look: block textures, hotbar and hearts are read from the player's own Minecraft Java install (nothing from Minecraft is shipped). Blocks: Dirt, Stone, Cobblestone, Oak Planks, Glass, Bricks, Sand, Stone Bricks, TNT.
- Place and remove blocks, light TNT. TNT only breaks blocks you placed; the house stays intact.
- Mita lives as usual but notices what you do and reacts to building and explosions.
- Cozy and funny mood. Singleplayer.

Controls: E block menu, 1-9 or mouse wheel select, right mouse place, left mouse break your own block, F light TNT.
Requires Minecraft Java started at least once with the official launcher (needs %APPDATA%\.minecraft\versions).

## Status: SOURCE ONLY, UNTESTED
- `src/Plugin.cs` and `src/MiSideMinecraft.csproj` were written without the game files and have never been run. IL2CPP mods cannot be built on GitHub (they need files BepInEx generates from the game), so build locally with `build.bat`.
- MiSide Demo is a Unity IL2CPP game (GameAssembly.dll present), so the plugin targets BepInEx 6 IL2CPP. Not yet run in the game.
- Mita's reaction is a placeholder (she turns to face you); the real animations and voice lines need the game's own components.
- Not ready to publish. Still missing: a build, a working install recipe, a real gameplay screenshot, and a passing test in Melty.
- `gemini-code-1791696974148.json` is an earlier draft description and does not match a real build.

## Next steps (on a PC with MiSide Demo and Melty)
1. Install BepInEx 6 (Unity IL2CPP, x64) into the game folder, run the game once so it generates its files, then close it.
2. Install the .NET 8 SDK, then run `build.bat "C:\path\to\MiSide Demo"`.
3. Test in game, fix errors, hook real Mita reactions.
4. Package `MiSideMinecraft.dll`, then validate, upload, test and publish with Melty.
