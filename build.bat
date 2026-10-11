@echo off
rem UNTESTED. Usage: build.bat "C:\path\to\MiSide Demo"
rem Builds the plugin against the game's BepInEx files and copies it into BepInEx\plugins.
if "%~1"=="" (
  echo Usage: build.bat "C:\path\to\MiSide Demo"
  echo The folder is the one that contains MiSide.exe and BepInEx.
  exit /b 1
)
dotnet build "%~dp0src\MiSideMinecraft.csproj" -c Release -o "%~dp0out" -p:GameDir="%~1"
if errorlevel 1 (
  echo BUILD FAILED. Copy the red error lines and send them to your helper.
  exit /b 1
)
if not exist "%~1\BepInEx\plugins" mkdir "%~1\BepInEx\plugins"
copy /Y "%~dp0out\MiSideMinecraft.dll" "%~1\BepInEx\plugins\MiSideMinecraft.dll"
echo DONE. Start the game.
