**Diablo 4 Assist 0.1.30** makes the events list put the cursor on what you pick even when it lies right next to you, and keeps the program from losing the game's menus on slower machines.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### Fixed
- **Events list (Shift+F2 on the map), the cursor going back to you**: a tortured gift a few steps away lay almost under your own icon on the map, so the game took your icon instead of the gift. The program now zooms the map in further until what you picked is clear of your icon. It also waits for the map to finish zooming after each turn of the wheel before it places the cursor: on a handheld the map zooms more slowly, and the cursor could land where the gift was about to be rather than where it was. The same goes for the treasure goblins and the waypoints.
- **"Zoom in further" when the program should zoom by itself**: the same slow zoom could make the program give up before the map got there. If it still gives up, the log now says why.
- **The game's menus not found after a while** (the warning that the menus can't be read, with sounds then playing in menus): the program looks for the menus in the game's memory again on every world change and whenever a menu stays open, and on a handheld that search got slower and slower until it never finished. It now looks first where it found them last time, which takes a moment instead of a minute, and a search that a world change made useless is stopped at once.

### Known issue
- **ROG Xbox Ally in the Xbox full screen experience**: Windows doesn't pass the handheld's own buttons to other programs there, and the new way 0.1.29 tried doesn't get them either, so left on its D-pad does nothing. An external controller works, and so does the handheld in desktop mode. Otherwise, map one of its back buttons to Ctrl+Alt+Shift+G (same as left on the D-pad), and another to Ctrl+Alt+Shift+E if you want to switch the guide between objectives and exploring.

### Still to look at
- None of this has been tried in the game yet. If the cursor still goes back to you, run the program with `--debug`, try it again and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder before you start another session (only the last two are kept), and tell me whether you touched a stick or a button right after pressing Enter.

---

**En español:** la 0.1.30 hace que la lista de eventos ponga el cursor sobre lo que eliges aunque esté justo a tu lado, y evita que el programa pierda los menús del juego en equipos más lentos. Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Arreglado:
- **Lista de eventos (Shift+F2 en el mapa), el cursor volvía a ti**: un regalo torturado a pocos pasos quedaba casi debajo de tu propio icono en el mapa, y el juego tomaba tu icono en vez del regalo. Ahora el programa acerca más el mapa hasta que lo elegido queda separado de tu icono, y espera a que el mapa termine de hacer zoom tras cada giro de la rueda antes de poner el cursor: en una consola portátil el zoom va más lento y el cursor podía caer donde iba a estar el regalo y no donde estaba. Lo mismo vale para los goblins del tesoro y los puntos de viaje.
- **"Acércate más" cuando el programa debía hacerlo solo**: ese zoom lento podía hacer que el programa se rindiera antes de llegar. Si aún se rinde, el log ahora dice por qué.
- **Los menús del juego dejaban de encontrarse** (el aviso de que no se pueden leer los menús, y los sonidos sonando dentro de los menús): el programa vuelve a buscar los menús en la memoria del juego en cada cambio de mundo y cuando un menú sigue abierto, y en una portátil esa búsqueda se volvía cada vez más lenta hasta no terminar nunca. Ahora mira primero donde los encontró la última vez, lo que tarda un momento en vez de un minuto, y una búsqueda que un cambio de mundo dejó sin sentido se detiene al instante.

Problema conocido: en un ROG Xbox Ally en la pantalla completa de Xbox, Windows no pasa los botones propios de la consola a otros programas, y la vía nueva que probó la 0.1.29 tampoco los recibe, así que la cruceta izquierda no hace nada. Un mando externo funciona, y la consola en modo escritorio también. Si no, asigna uno de sus botones traseros a Ctrl+Alt+Shift+G (lo mismo que la cruceta izquierda), y otro a Ctrl+Alt+Shift+E si quieres cambiar la guía entre objetivos y exploración.

Pendiente: nada de esto se ha probado aún en el juego. Si el cursor sigue volviendo a ti, ejecuta el programa con `--debug`, vuelve a probarlo y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa antes de empezar otra sesión (solo se guardan las dos últimas), y dime si tocaste un stick o un botón justo después de pulsar Enter.
