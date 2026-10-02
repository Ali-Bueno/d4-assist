**Diablo 4 Assist 0.1.2** reads the game files of the **Steam version** of Diablo IV.

0.1.1 found a Steam install but couldn't read it: Steam stores the game files in a different format from Battle.net. 0.1.2 reads that format. It was written from two tools that read real Steam copies and tested on a Steam-format copy of real game files, but **nobody has tried it on a real Steam install yet**.

**Download** `D4Assist.zip`, unzip it anywhere and run `D4Assist.exe`. Updating from an earlier version: close the program and copy only the new `D4Assist.exe` over the old one. The zip's `d4assist.json` would reset your settings.

### Changes
- Reads the Steam version's game files.
- If the game's program file is not in the install folder, the program finds it through the running game.
- The log (`d4assist-log.txt`) reports each step of reading the Steam files, on lines starting with `Steam storage:`.
- Nothing changes for Battle.net players.

### Steam players
Please try it and open an [issue](https://github.com/Ali-Bueno/d4-assist/issues) with `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder, whether it works or not. If it fails, the log says at which step.

---

**En español:** la 0.1.2 lee los archivos del juego de la **versión de Steam**. La 0.1.1 encontraba la carpeta de Steam pero no podía leerla, porque Steam guarda los archivos en otro formato. Todavía no se ha probado con una instalación real de Steam: si la tienes, pruébala y abre un [issue](https://github.com/Ali-Bueno/d4-assist/issues) con `d4assist-log.txt` y `d4assist-log.old.txt` adjuntos, funcione o no. Para actualizar sin perder tus ajustes, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior. Para quien juega en Battle.net no cambia nada.
