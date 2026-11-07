**Diablo 4 Assist 0.1.32** uses far less CPU and memory, in the open world above all. A player saw the processor at 100% with the open world option on, and the program was part of the cause. This version is meant to run well on handhelds too.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### Fixed
- **Processor at 100% in the open world**: the program kept searching the game's memory again for the map's list of markers, though the list never moves, and its searches read the memory the game shares with the graphics card, by far the slowest to read. Now the list is searched once per game session, and that memory is left out. On the test PC, in the open world, the program takes 0.2 to 0.3% of the processor, and reading the map's events the first time takes 1.5 seconds instead of about 10.
- **The game's memory growing**: those searches made Windows give the game real memory for parts it wasn't using, up to 5 GB more on the test PC. They now skip those parts.

### Changed
- **The program itself uses much less memory**: about 335 MB after starting instead of 860, and about 560 MB after a few minutes of play. It no longer keeps the names of every game file and every text of your language in memory; it reads the ones it needs when it needs them.
- **Less work while nothing happens**: it wakes up far less often when there is nothing to play or read, and starts a little faster.
- **Controllers**: the D-pad is read about 30 times a second instead of 60, and a controller you plug in can take up to 2 seconds to be noticed.
- **`D4Assist.exe` takes 122 MB on disk instead of 52**, which saves memory while it runs. The download stays the same size.
- **The program asks Windows for one more right on the game**, to know which memory is in use. It still only reads: it never writes anything to the game.
- **Every log now has a `CPU:` line** each minute of play, with the share of the machine the program took.

### Known issues
- **With very little free memory**, a menu you rarely open may not silence the sounds until the next loading screen.
- **Helltides in Nahantu and Skovos from afar**: they only appear in the list once the map marks their objects, so usually when you are already in that region.
- **ROG Xbox Ally in the Xbox full screen experience**: a tester confirms that external controllers and pads in Xbox input mode work there. The handheld's own controls still don't: the handheld in desktop mode works, or map one of its back buttons to Ctrl+Alt+Shift+G (same as left on the D-pad), and another to Ctrl+Alt+Shift+E if you want to switch the guide between objectives and exploring.

### Still to look at
- This was tried in the game on one PC with integrated graphics, in the open world, and Shift+F2 on the map worked. It was not tried in a helltide, on a handheld or with a separate graphics card. If you can, send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder before you start another session (only the last two are kept) after:
  - a helltide, with the open world option on and Shift+F2 on the map;
  - any session on a handheld, or on a PC with a separate graphics card.
- Please tell me if left on the D-pad misses a quick press, or if a controller isn't noticed.

---

**En español:** la 0.1.32 usa mucha menos CPU y memoria, sobre todo en el mundo abierto. Un jugador vio el procesador al 100% con la opción de mundo abierto activada, y el programa era parte de la causa. Esta versión busca funcionar bien también en handhelds.

Arreglado:
- **Procesador al 100% en el mundo abierto**: el programa volvía a buscar una y otra vez en la memoria del juego la lista de marcadores del mapa, aunque esa lista nunca se mueve, y sus búsquedas leían la memoria que el juego comparte con la tarjeta gráfica, con diferencia la más lenta de leer. Ahora la lista se busca una vez por sesión de juego y esa memoria se deja fuera. En el PC de prueba, en el mundo abierto, el programa usa entre el 0,2 y el 0,3% del procesador, y leer los eventos del mapa la primera vez tarda 1,5 segundos en vez de unos 10.
- **La memoria del juego creciendo**: esas búsquedas hacían que Windows diera memoria real al juego para partes que no usaba, hasta 5 GB más en el PC de prueba. Ahora se saltan esas partes.

Cambiado:
- **El propio programa usa mucha menos memoria**: unos 335 MB tras arrancar en vez de 860, y unos 560 MB tras unos minutos de juego. Ya no guarda en memoria los nombres de todos los archivos del juego ni todos los textos de tu idioma; lee los que necesita cuando los necesita.
- **Menos trabajo cuando no pasa nada**: se despierta muchas menos veces cuando no hay nada que sonar ni leer, y arranca algo más rápido.
- **Mandos**: la cruceta se lee unas 30 veces por segundo en vez de 60, y un mando que conectes puede tardar hasta 2 segundos en detectarse.
- **`D4Assist.exe` ocupa 122 MB en disco en vez de 52**, lo que ahorra memoria mientras funciona. La descarga sigue pesando lo mismo.
- **El programa pide a Windows un permiso más sobre el juego**, para saber qué memoria está en uso. Sigue solo leyendo: nunca escribe nada en el juego.
- **Cada log tiene ahora una línea `CPU:`** por minuto de juego, con la parte de la máquina que usó el programa.

Problemas conocidos:
- **Con muy poca memoria libre**, un menú que abras pocas veces puede no silenciar los sonidos hasta la siguiente pantalla de carga.
- **Helltides de Nahantu y Skovos desde lejos**: solo aparecen en la lista cuando el mapa marca sus objetos, así que normalmente cuando ya estás en esa región.
- **ROG Xbox Ally en la pantalla completa de Xbox**: un tester confirma que ahí funcionan los mandos externos y los pads en modo de entrada Xbox. Los controles propios de la consola siguen sin funcionar: la consola en modo escritorio sí funciona, o asigna uno de sus botones traseros a Ctrl+Alt+Shift+G (lo mismo que la cruceta izquierda), y otro a Ctrl+Alt+Shift+E si quieres cambiar la guía entre objetivos y exploración.

Pendiente: se probó en el juego en un PC con gráfica integrada, en el mundo abierto, y Shift+F2 en el mapa funcionó. No se ha probado en una helltide, en una handheld ni con tarjeta gráfica aparte. Si puedes, mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa antes de empezar otra sesión (solo se guardan los dos últimos) después de:
- una helltide, con la opción de mundo abierto activada y Shift+F2 en el mapa;
- cualquier sesión en una handheld, o en un PC con tarjeta gráfica aparte.

Y dime si la cruceta izquierda se salta alguna pulsación rápida, o si no detecta algún mando.
