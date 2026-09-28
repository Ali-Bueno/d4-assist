# Builds prism_bridge.dll (the in-process Lua C module that bridges UE4SS Lua to
# PRISM) and deploys it + the PRISM runtime DLLs into the mod's Scripts/ folder.
#
# Self-contained: compiles Lua 5.4.4 (vendored in libs/lua54/src, the exact version
# UE4SS bundles) into lua54.lib, then links prism_bridge against it. The bridge is a
# plain Lua C module (uses lua_State, not UE4SS's C++ mod SDK), so a UE4SS update
# needs no rebuild as long as the bundled Lua stays 5.4.x.
#
# TEMPLATE NOTE: this deploys to <template>/mod/Scripts. In a real project, either
# copy mod/Scripts into your game's Mods/<ModName>/Scripts, or junction it:
#   mklink /J "<game>\...\Mods\<ModName>\Scripts" "<repo>\mod\Scripts"
#
# Usage:  pwsh -File src\prism_bridge\build.ps1

$ErrorActionPreference = "Stop"
$proj = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)  # ...\template
$lua = "$proj\libs\lua54"
$prismInc = "$proj\libs\prism\include"
$prismBin = "$proj\libs\prism\bin"
$scripts = "$proj\mod\Scripts"   # copy/junction this into the game's Mods/<ModName>/Scripts
$tmp = "$env:TEMP\ue4ss_bridge_build"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

# Locate the MSVC dev environment (BuildTools or any VS with C++ tools).
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsRoot = & $vswhere -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -latest -property installationPath
if (-not $vsRoot) { throw "MSVC C++ tools not found (install VS Build Tools 'Desktop development with C++')." }
$vcvars = "$vsRoot\VC\Auxiliary\Build\vcvars64.bat"

# 1) Build lua54.lib (only if missing).
if (-not (Test-Path "$lua\lua54.lib")) {
    Write-Host "Building lua54.lib from vendored Lua 5.4.4..."
    $luaObjs = "$tmp\lua_obj"; New-Item -ItemType Directory -Force -Path $luaObjs | Out-Null
    $luaSrcs = Get-ChildItem "$lua\src\*.c" | Where-Object { $_.Name -notin @('lua.c','luac.c') } |
        ForEach-Object { "`"$($_.FullName)`"" }
    $c1 = "call `"$vcvars`" >nul 2>&1 && cl /nologo /c /O2 /MD /W0 /D_CRT_SECURE_NO_WARNINGS /TC /I`"$lua\include`" /Fo:`"$luaObjs\\`" $($luaSrcs -join ' ') && lib /nologo /out:`"$lua\lua54.lib`" `"$luaObjs\*.obj`""
    cmd /c $c1 | Out-Null
    if (-not (Test-Path "$lua\lua54.lib")) { throw "lua54.lib build failed" }
}

# 2) Build prism_bridge.dll.
Write-Host "Building prism_bridge.dll..."
$c2 = "call `"$vcvars`" >nul 2>&1 && cl /nologo /O2 /MD /TC /std:c17 /I`"$lua\include`" /I`"$prismInc`" `"$PSScriptRoot\prism_bridge.c`" /Fo:`"$tmp\prism_bridge.obj`" /LD /Fe:`"$tmp\prism_bridge.dll`" /link `"$lua\lua54.lib`" user32.lib"
cmd /c $c2 | Select-Object -Last 2
if (-not (Test-Path "$tmp\prism_bridge.dll")) { throw "prism_bridge.dll build failed" }

# 3) Deploy module + PRISM runtime into the mod's Scripts/ folder.
New-Item -ItemType Directory -Force -Path $scripts | Out-Null
Copy-Item "$tmp\prism_bridge.dll" "$scripts\prism_bridge.dll" -Force
Copy-Item "$prismBin\prism.dll" "$scripts\prism.dll" -Force
Copy-Item "$prismBin\tolk.dll" "$scripts\tolk.dll" -Force
Write-Host "Deployed to $scripts :"
Get-ChildItem $scripts | Select-Object Name, Length | Format-Table -AutoSize
