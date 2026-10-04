**Diablo 4 Assist 0.1.9** fixes sounds going silent after switching characters, and helps a lot in the big seasonal dungeons.

**Updating:** with 0.1.7 or 0.1.8, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### Fixed
- After switching characters, the program could think a menu was still open and keep every sound silent (radar, chests, everything) until you restarted it. It now checks the menus again on every loading screen, and a menu that seems open for more than 15 seconds gets checked again too, so a false silence fixes itself. This is most likely the "sounds stopped working" some of you reported.
- Quest companions who follow you (like the warrior in a seasonal dungeon) no longer sound like an objective.
- The objectives the program heard are no longer forgotten if it restarts in the middle of a dungeon.

### New
- The guide knows where your quest's objective is even when it's far away, if the game marks it on the map: a door or a lever at the other end of the dungeon. This one is new, so tell me how it goes.
- Seasonal dungeons with side dungeons inside (like the one in Travincal): the guide leads to the side dungeons' entrances you haven't used yet, even before you've seen them, and follows the steps of the dungeon's own quest (for example, the orb you have to destroy).
- Echo of Diablo: the gates it summons its clones from play the quest sound from far away, and the boss goes quiet while nothing can hurt it. Break the gates and it can be hit again.

---

**En español:** la 0.1.9 arregla los sonidos que se quedaban en silencio al cambiar de personaje, y ayuda mucho en las mazmorras grandes de temporada. Si tienes la 0.1.7 o la 0.1.8, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"); desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Arreglado: al cambiar de personaje, el programa podía creer que seguía abierto un menú y dejar todos los sonidos en silencio (radar, cofres, todo) hasta reiniciarlo; ahora vuelve a comprobar los menús en cada pantalla de carga, y si un menú parece abierto más de 15 segundos lo comprueba otra vez, así que un silencio falso se arregla solo. Seguramente era el "dejaron de funcionar los sonidos" que algunos contasteis. Los compañeros de misión que te siguen (como el guerrero de una mazmorra de temporada) ya no suenan como un objetivo. Los objetivos que el programa ya había oído no se olvidan si se reinicia en mitad de una mazmorra.

Nuevo: la guía sabe dónde está el objetivo de tu misión aunque esté lejos, si el juego lo marca en el mapa (una puerta o una palanca en la otra punta de la mazmorra); es nuevo, así que contadme qué tal. En las mazmorras de temporada con minimazmorras dentro (como la de Travincal), la guía lleva a las entradas que aún no has usado, aunque no las hayas visto, y sigue los pasos de la misión de la propia mazmorra (por ejemplo, el orbe que hay que destruir). Eco de Diablo: los portales de los que saca sus clones suenan con el sonido de misión desde lejos, y el jefe se calla mientras nada le hace daño; rompe los portales y se le puede volver a pegar.
