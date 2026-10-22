**Diablo 4 Assist 0.1.16** adds a waypoint list on the world map and a guide to quest places in the open world, and fixes many places where the guide or the quest sounds failed. Most of this work is by DrCaotica, who tested it in the game with a controller. The source code is now public, in this same repository, under the MIT license.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### New
- **Waypoint list.** With the world map open, Shift+F1 lists the waypoints you have activated in this world, nearest first. Up and down read them (name, distance and direction), Enter puts the mouse cursor on the chosen waypoint, and Escape closes the list. This is the first time the program sends anything to the game: it only moves the cursor and never clicks. You can turn the list off in the settings.
- **Quest places in the open world.** Outside dungeons, D-pad left leads to the place of your current quest step that the map marks, starting with the game's own quest marker. It says the quest's name, the distance and which of how many places it leads to. The ping follows a path around obstacles, not a straight line. Press again for the next place; the last press turns the guide off.
- **Two more campaign routes:** the blood-petal trail in the Philios quest, from Lorath to the waterfall shrine, and the pentagram in the warlock class quest, traced point by point and part by part.
- Ctrl+Alt+Shift+Q closes the program at once.

### Fixed
- The guide uses jump points where a route needs them, leads into the boss room once nothing is left to explore, keeps to the middle of walkways, leaves big rooms through the right openings, and no longer points back while you stand in a doorway or walks you into a wall between stray patches of floor.
- Quest steps the guide couldn't reach before now lead: steps that name a group of objects, places across a rising bridge, seasonal dungeons such as the Halls of the Blind, and a quest in the Askari dungeon where the guide stopped far from the trigger.
- In some story areas whose name has a space in it, the program stayed silent: no sounds and no guide.
- Rathma's soul split: the quest sound plays on your body, so you can find your way back to it.
- Inarius fight: the boss no longer goes silent when he leaps, and the guide leads to his chains once they are free and back to him while you carry one.
- The program remembers quest progress between runs, in `d4assist-quests.json` next to it.
- The program keeps running when a read from the game fails or the world changes, and an update without a checksum is refused.

The quest marker variants, the "which of how many" count and the remembered quest progress haven't been tested in the game yet. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.16 añade una lista de waypoints en el mapa del mundo y una guía a los lugares de misión en el mundo abierto, y arregla muchos sitios donde fallaban la guía o los sonidos de misión. Casi todo este trabajo es de DrCaotica, que lo probó en el juego con mando. El código fuente ya es público, en este mismo repositorio, con licencia MIT. Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Nuevo: con el mapa del mundo abierto, Shift+F1 lista los waypoints que has activado en este mundo, del más cercano al más lejano. Arriba y abajo los leen (nombre, distancia y dirección), Enter pone el cursor del ratón sobre el waypoint elegido y Escape cierra la lista. Es la primera vez que el programa envía algo al juego: solo mueve el cursor, nunca hace clic. Puedes desactivar la lista en los ajustes. Fuera de las mazmorras, la cruceta izquierda lleva al lugar del paso actual de tu misión que marca el mapa, empezando por el marcador de misión del propio juego, siguiendo un camino que rodea los obstáculos. Dos rutas de campaña nuevas: el rastro de pétalos de sangre de la misión de Philios y el pentagrama de la misión de clase del brujo. Ctrl+Alt+Shift+Q cierra el programa al instante.

Arreglado: la guía usa los puntos de salto, entra en la sala del jefe cuando no queda nada por explorar, va por el centro de las pasarelas y sale bien de las salas grandes. Pasos de misión que antes no se alcanzaban ahora sí llevan, también en algunas mazmorras de temporada. En algunas zonas de historia con un espacio en el nombre el programa se quedaba en silencio. En la separación del alma de Rathma, el sonido de misión suena en tu cuerpo. En la pelea con Inarius, el jefe ya no se calla cuando salta y la guía lleva a sus cadenas. El programa recuerda el progreso de las misiones entre ejecuciones.

Algunas partes aún no se han probado en el juego. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
