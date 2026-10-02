**Diablo 4 Assist 0.1.1** finds the game when it was installed through **Steam**, not only through Battle.net.

**Download** `D4Assist.zip`, unzip it anywhere and run `D4Assist.exe`. Updating from 0.1.0: close the program and copy only the new `D4Assist.exe` over the old one. The zip's `d4assist.json` would reset your settings.

### Changes
- The game is found through Battle.net, then through Steam (any Steam library, on any drive).
- If neither knows it, the program uses the folder of the running game: start Diablo IV first, then the program.
- The log (`d4assist-log.txt`) now says which game folder was used, on the `Game install:` line.

### Steam players
This is the first build that should work with the Steam version, and it has not been tried on one yet. If it fails, open an [issue](https://github.com/Ali-Bueno/d4-assist/issues) with `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.1 encuentra el juego instalado desde **Steam** (en cualquier biblioteca o disco), no solo desde Battle.net. Si no lo encuentra por ninguno de los dos, usa la carpeta del juego abierto: abre primero Diablo IV y luego el programa. Todavía no se ha probado con la versión de Steam: si falla, abre un [issue](https://github.com/Ali-Bueno/d4-assist/issues) con `d4assist-log.txt` y `d4assist-log.old.txt` adjuntos. Para actualizar desde la 0.1.0 sin perder tus ajustes, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.
