**Diablo 4 Assist 0.1.28** adds an events menu on the map and treasure goblins (by DrCaotica), and fixes what a tester ran into in act 1.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### New
- **Events menu on the world map (Shift+F2)**: three entries, Helltide (the list it opened before), Whispers and Events. Whispers are listed nearest first with their region, grim favors and minutes left; Events lists the coming legions and world bosses with the minutes to their start. Enter puts the mouse cursor on the entry; Escape goes back a level. Right after you open the map the list may say it is still reading: ask again a few seconds later.
- **Treasure goblins**: they now get the elite's sound, and Shift+F3 on the map lists the goblins the map shows near you; Enter puts the cursor on one.
- **War Plans command table**: it loops the quest sound.
- **Steps that only ask you to enter an area** (the Labyrinth's Summoner, Diablo's arena): the guide no longer says "all explored" there; it leads to who the map marks in that area, and over the triggers that start the fight.
- **Tower floors with two entrances** (the Countess's tower): the guide leads on to the next floor's portal instead of back to the one you came by.
- **A key another program uses**: if Shift+F1, Shift+F2 or Shift+F3 is taken by another program on your computer, the program uses the same key with Ctrl (Ctrl+Shift+F2) and says so once.

### Fixed
- **The Darkened Way, "In Her Wake", right after the gate**: switching the guide on there still led down the ramp the invisible doors shut. It now goes the long way round, by the climb down.
- **The difficulty statue**: in its menu, left on the D-pad switched the guide off and on and offered entrances and quests. That menu now counts as a menu: the D-pad is left to the game and the sounds pause.
- **The Darkened Way after the ruined city**: left on the D-pad first offered a quest marker that belongs to the ruined city's dungeon, 228 meters off in a straight line. Markers of another area are no longer offered.
- **Entering a dungeon**: the guide sometimes said "all explored" at the entrance while the dungeon's map was still loading.
- **Logs with `--debug`**: piles of barrels and crates wrote the same lines again every half second (most of a 15 MB log). They are written once now.
- **A game disconnect**: it no longer starts sounds for junk the game leaves in memory as it goes.

### ROG Xbox Ally
On one Ally the program saw the controller but none of its buttons for a whole prologue, and read the D-pad fine later in the same game. The difference we can see: the first time the program was started before the game, the second time with the game already running. If the D-pad does nothing for you, please try starting Diablo 4 Assist after the game, press left on the D-pad in a dungeon, and send me `d4assist-log.txt`: it now names the controller and says whether anything at all arrives from it.

### Still to look at
- None of this has been played in the game yet. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder, before starting another session (only the last two are kept).
- The names of whispers, grim favors and events are translated by hand outside Spanish, English and German; tell me if one sounds wrong in your language.

---

**En español:** la 0.1.28 añade un menú de eventos en el mapa y los duendes del tesoro (de DrCaotica), y arregla lo que un tester encontró en el acto 1. Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Nuevo:
- **Menú de eventos en el mapa del mundo (Shift+F2)**: tres entradas, Marea infernal (la lista de antes), Susurros y Eventos. Los susurros salen por distancia con su región, favores siniestros y minutos que les quedan; Eventos da las próximas legiones y jefes de mundo con los minutos que faltan para que empiecen. Intro pone el cursor del ratón en la entrada; Escape vuelve un nivel. Justo al abrir el mapa la lista puede decir que todavía está leyendo: vuelve a pedirla unos segundos después.
- **Duendes del tesoro**: tienen el sonido de élite, y Shift+F3 en el mapa da los duendes que el mapa muestra cerca; Intro pone el cursor en uno.
- **Mesa de mando de los Planes de Guerra**: suena con el sonido de misión.
- **Pasos que solo piden entrar en una zona** (el Invocador del Laberinto, la arena de Diablo): la guía ya no dice "todo explorado" ahí; lleva a quien el mapa marca en esa zona y por los activadores que empiezan el combate.
- **Pisos de torre con dos entradas** (la torre de la Condesa): la guía lleva al portal del piso siguiente en vez de volver al de llegada.
- **Una tecla que usa otro programa**: si otro programa de tu ordenador usa Shift+F1, Shift+F2 o Shift+F3, el programa usa la misma con Ctrl (Ctrl+Shift+F2) y lo dice una vez.

Arreglado:
- **El Camino Oscuro, "In Her Wake", justo después de la verja**: al encender la guía ahí seguía llevando rampa abajo por las puertas invisibles cerradas. Ahora da la vuelta larga, por la bajada escalando.
- **La estatua de la dificultad**: en su menú, la cruceta izquierda apagaba y encendía la guía y ofrecía entradas y misiones. Ese menú ahora cuenta como menú: la cruceta queda para el juego y los sonidos se pausan.
- **El Camino Oscuro después de la ciudad en ruinas**: la cruceta izquierda ofrecía primero un marcador de misión de la mazmorra de la ciudad, a 228 metros en línea recta. Ya no se ofrecen marcadores de otra zona.
- **Al entrar en una mazmorra**: la guía a veces decía "todo explorado" en la entrada mientras el mapa aún cargaba.
- **Logs con `--debug`**: las pilas de barriles y cajas escribían las mismas líneas cada medio segundo (casi todo un log de 15 MB). Ahora se escriben una vez.
- **Una desconexión del juego**: ya no arranca sonidos por la basura que el juego deja en memoria al irse.

ROG Xbox Ally: en un Ally el programa veía el mando pero ninguno de sus botones durante todo un prólogo, y leyó bien la cruceta más tarde en la misma partida. La diferencia visible: la primera vez el programa se abrió antes que el juego, la segunda con el juego ya abierto. Si la cruceta no te hace nada, prueba a abrir Diablo 4 Assist después del juego, pulsa la cruceta izquierda en una mazmorra y mándame `d4assist-log.txt`: ahora nombra el mando y dice si llega algo de él.

Pendiente: nada de esto se ha probado aún en el juego. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa, antes de empezar otra sesión (solo se guardan las dos últimas). Los nombres de susurros, favores y eventos están traducidos a mano fuera del español, inglés y alemán; dime si alguno suena mal en tu idioma.
