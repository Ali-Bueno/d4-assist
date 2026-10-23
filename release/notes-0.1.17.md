**Diablo 4 Assist 0.1.17** brings the quest guide to story areas such as the hells, and lets its path cross jumps and slides. A player asked for it in The Blind Eye, in the Burning Hells; thanks for the log!

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### New
- **Quest places in story areas.** In fixed story areas such as the hells, the Darkened Way or Tristram, D-pad left now works as in the open world: it leads to the places your quest marks on the map and says the quest's name and the distance; press again for the next place, and after the last one it turns off. Before, it only said that the guide works in dungeons.
- **Jumps and slides on the way.** The path to a quest place, in story areas and in the open world, now goes over jumps, duck-unders and slides instead of giving up and leading straight at the place. A slide is only taken downhill. The program doesn't tell you to jump: the ping leads you to the edge, and you use the jump yourself.

### Fixed
- The mural in the Sightless Eye, which the game copies for a cutscene, is no longer taken as locked and open by turns.

Where the game moves you itself (a teleport, a cutscene), the guide still points straight at the place. None of this has been tested in the game yet. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.17 lleva la guía de misión a las zonas de historia como los infiernos, y su camino ya cruza saltos y toboganes. Lo pidió un jugador en El Ojo Ciego, en los Infiernos Ardientes; ¡gracias por el log! Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Nuevo: en las zonas de historia fijas, como los infiernos, la Senda Oscura o Tristram, la cruceta izquierda funciona como en el mundo abierto: lleva a los lugares que tu misión marca en el mapa y dice el nombre de la misión y la distancia; pulsa otra vez para el siguiente lugar, y tras el último se apaga. El camino, en las zonas de historia y en el mundo abierto, ahora pasa por saltos, pasos agachado y toboganes (el tobogán solo cuesta abajo). El programa no te avisa de que saltes: el pitido te lleva al borde y el salto lo haces tú.

Arreglado: el mural del Ojo Ciego, que el juego copia para una cinemática, ya no se toma como cerrado y abierto por turnos.

Donde el juego te mueve solo (un teletransporte, una cinemática), la guía sigue apuntando en línea recta. Nada de esto se ha probado aún en el juego. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
