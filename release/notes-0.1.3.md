**Diablo 4 Assist 0.1.3** finds the game in more places, and lets you choose its folder when it can't.

**Download** `D4Assist.zip`, unzip it anywhere and run `D4Assist.exe`. Updating from an earlier version: close the program and copy only the new `D4Assist.exe` over the old one. The zip's `d4assist.json` would reset your settings.

### Changes
- The Steam version works: a Steam player confirmed it with 0.1.2.
- If Battle.net, Steam or the running game don't say where the game is, the program looks for a `Diablo IV` folder up to three levels deep on every local drive, for example `E:\Games\Blizzard\Diablo IV`.
- If it still can't find it, it asks you to choose the Diablo IV folder (the one with `Diablo IV.exe` in it). You can also choose the folder that contains it. It is saved in `d4assist.json`, so you only do this once.
- Settings, General tab: a new **Diablo IV folder** field with a **Browse** button to choose another folder.
- Opening the program a second time by mistake says it's already running, now in your Windows language, and the second copy closes.
- The readme explains the radar and object sounds in more detail: the two pips of an unexplored exit, the open-floor breath rising or falling, which sound goes with what, and what D-pad left does.
- The zip's `d4assist.json` now has the `InstallPath` line, for anyone who prefers to write the folder by hand (use forward slashes: `"InstallPath": "E:/Games/Diablo IV"`).

---

**En español:** la 0.1.3 encuentra el juego en más sitios. La versión de Steam funciona (lo confirmó un jugador con la 0.1.2). Si Battle.net, Steam o el juego abierto no dicen dónde está, busca una carpeta `Diablo IV` hasta tres niveles de profundidad en todos los discos. Si aun así no la encuentra, te pide que elijas la carpeta de Diablo IV (la que tiene `Diablo IV.exe`) y la recuerda. En la configuración, pestaña General, hay un campo **Carpeta de Diablo IV** con un botón **Examinar** para cambiarla. Si abres el programa dos veces por error, avisa de que ya está en marcha (ahora en el idioma de tu Windows) y la segunda copia se cierra. El readme explica mejor los sonidos del radar y de los objetos. Para actualizar sin perder tus ajustes, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.
