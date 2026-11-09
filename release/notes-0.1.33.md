**Diablo 4 Assist 0.1.33** adds a map menu with dungeons, cellars, strongholds and boss lairs, leads you to the place you choose there and through legions, makes the open-world guide lighter, and can talk on Linux through Proton. The map menu, the chosen-place guide, the legion guides and the lighter guide are DrCaotica's work.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy the new `D4Assist.exe` over the old one (and the `wine` folder, if you play on Linux); the zip's `d4assist.json` would reset your settings. This version adds that `wine` folder next to `D4Assist.exe`; on Windows it isn't used.

### New
- **Map menu**: with the world map open, Shift+F1 now opens a menu instead of the waypoint list: Waypoints, Dungeons, Cellars, Strongholds, and Boss lairs when you play on a Torment tier. Up and down choose, Enter opens a list, Escape goes back. Every list is nearest first. Enter puts the mouse cursor on the place and zooms the map out if needed; a place too far for the map puts the cursor on the activated waypoint nearest to it.
- **The guide to the place you chose**: a dungeon, cellar or stronghold chosen with Enter is where left on the D-pad leads first, from up to 1500 meters away, until you reach it.
- **Legions**: inside a legion, left on the D-pad leads to what the map marks: the boss, the structures and the captains, then the reward chest, and it says when nothing is left. In defense legions (Skovos, Chakhir) it leads first to the side under attack and says which.
- **Linux (Steam with Proton)**: the program can now talk through Linux's own speech, with Orca or Cthulhu running. The README says how to start it. Cthulhu's Wine Access (its NVDA stand-in) no longer works with it.

### Changed
- **Lighter guide in the open world**: in a helltide with the guide on, the program went from about 15% of one processor core to 6-8% on the test PC. Path searches no longer pile up after a teleport, and once you're 150 meters or more off the path, the guide stops.
- **Screen reader library updated** (PRISM 0.18.3). On Windows nothing should change; tell me if your screen reader stops talking.
- **Updates only rewrite the files that changed.**

### Fixed
- **Left on the D-pad in the difficulty statue's menu** started the guide. Now it does nothing there.

### Known issues
- **Cellars**: the list shows every cellar, also the ones the game's map doesn't show you yet (it shows a cellar only once you've been near it).
- **Strongholds**: all of them are listed, freed or not.
- **With very little free memory**, a menu you rarely open may not silence the sounds until the next loading screen.
- **Helltides in Nahantu and Skovos from afar**: they only appear in the list once the map marks their objects, so usually when you are already in that region.
- **ROG Xbox Ally in the Xbox full screen experience**: the handheld's own controls don't reach the program. Use it in desktop mode, or map one of its back buttons to Ctrl+Alt+Shift+G (same as left on the D-pad), and another to Ctrl+Alt+Shift+E if you want to switch the guide between objectives and exploring.

### Still to look at
- **Legions** weren't tried in the game. Tell me whether the guide moves on when a structure or a captain falls, and, in the Skovos and Chakhir defenses, whether it leads to the side under attack.
- **Linux** wasn't tried by me. If you play there, tell me if it talks, and send me `d4assist-log.txt` from the program's folder if it doesn't.
- The requests of 0.1.32 still stand: logs from a session on a handheld, or on a PC with a separate graphics card.

---

**En español:** la 0.1.33 añade un menú del mapa con mazmorras, sótanos, fortalezas y guaridas de jefes, te guía al sitio que elijas ahí y por las legiones, aligera la guía del mundo abierto y puede hablar en Linux con Proton. El menú del mapa, la guía al sitio elegido, la de las legiones y la guía más ligera son trabajo de DrCaotica.

**Actualizar:** desde la 0.1.7 o posterior, el programa ofrece esta versión solo al arrancar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia el nuevo `D4Assist.exe` encima del viejo (y la carpeta `wine`, si juegas en Linux); el `d4assist.json` del zip te reiniciaría la configuración. Esta versión añade esa carpeta `wine` junto a `D4Assist.exe`; en Windows no se usa.

Nuevo:
- **Menú del mapa**: con el mapa del mundo abierto, Shift+F1 abre ahora un menú en vez de la lista de puntos de ruta: Puntos de ruta, Mazmorras, Sótanos, Fortalezas, y Guaridas de jefes cuando juegas en un nivel de Tormento. Arriba y abajo eligen, Enter abre una lista y Escape vuelve atrás. Todas las listas van de más cerca a más lejos. Enter pone el cursor del ratón sobre el sitio y aleja el mapa si hace falta; si el sitio queda demasiado lejos para el mapa, pone el cursor en el punto de ruta activado más cercano a él.
- **La guía al sitio que elegiste**: una mazmorra, un sótano o una fortaleza elegidos con Enter es adonde lleva primero la cruceta izquierda, desde hasta 1500 metros, hasta que llegues.
- **Legiones**: dentro de una legión, la cruceta izquierda lleva a lo que marca el mapa: el jefe, las estructuras y los capitanes, luego el cofre de recompensa, y avisa cuando no queda nada. En las legiones de defensa (Skovos, Chakhir) lleva primero al lado atacado y dice cuál.
- **Linux (Steam con Proton)**: el programa puede hablar ahora con la voz del propio Linux, con Orca o Cthulhu en marcha. El README explica cómo arrancarlo. El Wine Access de Cthulhu (su sustituto de NVDA) ya no funciona con él.

Cambiado:
- **Guía más ligera en el mundo abierto**: en una helltide con la guía activa, el programa pasó de un 15% de un núcleo del procesador a un 6-8% en el PC de prueba. Las búsquedas de camino ya no se amontonan tras un teletransporte, y si te alejas 150 metros o más del camino, la guía se para.
- **Librería del lector de pantalla actualizada** (PRISM 0.18.3). En Windows no debería cambiar nada; dime si tu lector de pantalla deja de hablar.
- **Las actualizaciones solo reescriben los archivos que cambian.**

Arreglado:
- **La cruceta izquierda en el menú de la estatua de dificultad** activaba la guía. Ahora ahí no hace nada.

Problemas conocidos:
- **Sótanos**: la lista muestra todos, también los que el mapa del juego aún no te enseña (solo los enseña cuando has pasado cerca).
- **Fortalezas**: salen todas, liberadas o no.
- **Con muy poca memoria libre**, un menú que abras pocas veces puede no silenciar los sonidos hasta la siguiente pantalla de carga.
- **Helltides de Nahantu y Skovos desde lejos**: solo aparecen en la lista cuando el mapa marca sus objetos, así que normalmente cuando ya estás en esa región.
- **ROG Xbox Ally en la pantalla completa de Xbox**: los controles propios de la consola no llegan al programa. Úsala en modo escritorio, o asigna uno de sus botones traseros a Ctrl+Alt+Shift+G (lo mismo que la cruceta izquierda), y otro a Ctrl+Alt+Shift+E si quieres cambiar la guía entre objetivos y exploración.

Pendiente:
- **Las legiones** no se han probado en el juego. Dime si la guía pasa a lo siguiente cuando cae una estructura o un capitán y, en las defensas de Skovos y Chakhir, si lleva al lado atacado.
- **Linux** no lo he probado yo. Si juegas ahí, dime si habla, y si no, mándame `d4assist-log.txt` de la carpeta del programa.
- Sigue en pie lo de la 0.1.32: logs de una sesión en una handheld, o en un PC con tarjeta gráfica aparte.
