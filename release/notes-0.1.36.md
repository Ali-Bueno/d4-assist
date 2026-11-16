**Diablo 4 Assist 0.1.36** fixes the guide in four campaign dungeons of acts 2 and 3, and the helltide events menu's stopover when an object is far away.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy the new `D4Assist.exe` over the old one (and the `wine` folder, if you play on Linux); the zip's `d4assist.json` would reset your settings.

### Fixed
- **The Weeping Cairns**: after you place the first stone, the guide stayed on Yorin instead of taking you through the door that just opened. Now it leads on to find the next stone, and back to Yorin once you carry it.
- **The Orbei Monastery**: the guide stopped at piles of monk corpses where only one can be searched, and kept bringing you back to the others. Once you search one corpse of a pile, the others beside it no longer lead (switching the guide off and on brings them back).
- **Guulrahn, after freeing Oyun**: the guide led back to the locked cell gates instead of on through the weakened wall to the way out. It now leads on.
- **Elias's palace, the last fight**: just outside the room where you fight Elias, the guide turned you back towards unexplored rooms. The step's place was read at the dungeon's entrance; it now lies in Elias's room, so the guide takes you in.
- **Helltide objects too far for the map** (events menu, Shift+F2): the stopover offered could be the very waypoint you were standing at. The stopover is now the activated waypoint nearest to the object on the whole map, and never one that doesn't bring you clearly closer.

### New
- **A helltide object or a whisper chosen with Enter** is now also where D-pad left leads first, as the places of the map menu do. While it is more than 1500 meters away, D-pad left says how far it is and which way, instead of leading somewhere else.

### Still to look at
- All of the above was checked with tests, a tester's logs and the game's data, not by playing it again. If you play these dungeons, run the program with `--debug` and send me `d4assist-log.txt`.
- **The Orbei Monastery**: it's still unclear whether the other corpses of a pile can be searched at all.

---

**En español:** la 0.1.36 arregla la guía en cuatro mazmorras de la campaña de los actos 2 y 3, y la parada intermedia del menú de eventos del Helltide cuando un objeto está lejos.

**Actualizar:** desde la 0.1.7 o posterior, el programa ofrece esta versión solo al arrancar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia el nuevo `D4Assist.exe` encima del viejo (y la carpeta `wine`, si juegas en Linux); el `d4assist.json` del zip te reiniciaría la configuración.

Arreglado:
- **Weeping Cairns**: después de colocar la primera piedra, la guía se quedaba en Yorin en vez de llevarte por la puerta que se acababa de abrir. Ahora te lleva a buscar la siguiente piedra, y de vuelta a Yorin cuando la llevas.
- **Monasterio de Orbei**: la guía se paraba en montones de cadáveres de monjes donde solo se puede registrar uno, y te volvía a llevar a los otros. Al registrar un cadáver de un montón, los demás de al lado dejan de guiar (apagar y encender la guía los recupera).
- **Guulrahn, después de liberar a Oyun**: la guía volvía a las rejas cerradas de las celdas en vez de seguir por el muro agrietado hacia la salida. Ahora sigue adelante.
- **Palacio de Elias, el último combate**: justo fuera de la sala donde luchas contra Elias, la guía te hacía volver hacia salas sin explorar. El sitio del paso se leía en la entrada de la mazmorra; ahora está en la sala de Elias, y la guía te mete dentro.
- **Objetos del Helltide demasiado lejos para el mapa** (menú de eventos, Shift+F2): la parada intermedia podía ser el mismo punto de ruta en el que estabas. Ahora es el punto de ruta activado más cercano al objeto en todo el mapa, y nunca uno que no te acerque claramente.

Nuevo:
- **Un objeto del Helltide o un susurro elegido con Enter** es ahora también adonde lleva primero la cruceta izquierda, como los sitios del menú del mapa. Mientras esté a más de 1500 metros, la cruceta izquierda dice a qué distancia está y en qué dirección, en vez de llevarte a otro sitio.

Pendiente:
- Todo lo anterior está comprobado con pruebas, los logs de un tester y los datos del juego, no jugándolo otra vez. Si juegas estas mazmorras, ejecuta el programa con `--debug` y mándame `d4assist-log.txt`.
- **Monasterio de Orbei**: sigue sin estar claro si los otros cadáveres de un montón se pueden registrar.
