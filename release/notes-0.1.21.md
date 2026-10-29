**Diablo 4 Assist 0.1.21** makes the dungeon guide lead straight to the Weeping Cairns' ward-stone altar. Thanks to the player who sent the logs from act 2!

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### Fixed
- **The Weeping Cairns' altar.** When the quest asks for the ward stone, the game's map already marks the altar that gives it, but the guide only used the map's marks for levers, doors and the like, so it had to search the dungeon. Now, when the quest names a structure you break by attacking it and the map marks it, the guide leads there. Then, as in 0.1.20, it leads to the stone once it's heard, and back to the gate once you pick it up.

### Still looking into
- **A crash after updating.** One player heard the program say it stopped because of an error right after it updated itself. I couldn't make it happen here. If it happens to you, please send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder without moving them out of it: copy them instead, because a log moved away while the program runs keeps the rest of that session.

None of this has been tested in the game yet. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.21 hace que la guía de mazmorras lleve directamente al altar de la piedra de los Weeping Cairns. ¡Gracias al jugador que mandó los logs del acto 2! Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Arreglado: cuando la misión pide la piedra, el mapa del juego ya marca el altar que la da, pero la guía solo usaba las marcas del mapa de palancas, puertas y similares, así que tenía que explorar la mazmorra para encontrarlo. Ahora, cuando la misión nombra una estructura que se rompe atacándola y el mapa la marca, la guía lleva allí. Después, como en la 0.1.20, lleva a la piedra cuando suena y de vuelta a la puerta al cogerla.

Investigando: un jugador oyó al programa decir que se había detenido por un error justo después de actualizarse. No he conseguido que pase aquí. Si te pasa, mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa sin sacarlos de ella: cópialos, porque un log movido mientras el programa sigue abierto se lleva el resto de esa sesión.

Nada de esto se ha probado aún en el juego. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
