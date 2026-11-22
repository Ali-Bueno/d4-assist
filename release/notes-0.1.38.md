**Diablo 4 Assist 0.1.38** adds a guide for strongholds and steadier paths in the open world, and fixes the guide after the last Elias fight in the Foul Undercrypts (act 5). The stronghold guide and the open-world paths are DrCaotica's work.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy the new `D4Assist.exe` over the old one (and the `wine` folder, if you play on Linux); the zip's `d4assist.json` would reset your settings.

### New
- **Stronghold guide**: inside a stronghold whose quest is in progress, left on the D-pad leads like in a dungeon. It goes to each place the step marks, plays the arrival sound at each and moves on to the next by itself. When the step marks nothing left, it explores. In a boss fight it keeps leading to the boss while the map marks it. It turns off when you leave or the stronghold is freed.
- **Map menu, Strongholds**: freed strongholds are left out, and the cursor lands on the stronghold's map icon. A stronghold chosen there hands over to the stronghold guide once you are inside.
- **Open-world paths**: the path goes round crates and stands, no longer cuts through wall corners, and no longer changes every few seconds. If you stay stuck on it (stick pushed, hardly moving), that spot counts as blocked: the program says so and finds another way. It remembers the spot for 60 days and frees it once you walk through. Standing still to read or fight never counts as stuck.
- **More quest places**: the D-pad left list also offers map markers that a quest names in a group, like the cave priestess in the Altar of Ruin. The places you have already reached in a dungeon are kept when the program restarts.

### Fixed
- **The Foul Undercrypts, after the last Elias fight**: the guide took you back to the dungeon's entrance before leading you after Tysa. A place that only a step the game hides names now leads only after the places of the step it shows.

### Still to look at
- The Foul Undercrypts fix was checked with tests and a tester's log, not by playing it again. Several parts of the stronghold guide and the open-world paths weren't played either: the boss staying the target, exploring a stronghold, and blocked spots and reached places after a restart. If something goes wrong, run the program with `--debug` and send me `d4assist-log.txt`.
- On the act 5 lag a tester reported: the log shows the program used 1–2 % of the machine, so the lag didn't come from it.

---

**En español:** la 0.1.38 añade una guía para los bastiones y caminos más estables en el mundo abierto, y arregla la guía tras la última pelea con Elias en las Foul Undercrypts (acto 5). La guía de bastiones y los caminos del mundo abierto son trabajo de DrCaotica.

**Actualizar:** desde la 0.1.7 o posterior, el programa ofrece esta versión solo al arrancar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia el nuevo `D4Assist.exe` encima del viejo (y la carpeta `wine`, si juegas en Linux); el `d4assist.json` del zip te reiniciaría la configuración.

Nuevo:
- **Guía de bastiones**: dentro de un bastión con su misión en curso, izquierda en la cruceta guía como en una mazmorra. Va a cada lugar que marca el paso, suena la llegada en cada uno y pasa sola al siguiente. Cuando el paso ya no marca nada, explora. En una pelea con un jefe sigue llevando al jefe mientras el mapa lo marca. Se apaga al salir o al liberar el bastión.
- **Menú del mapa, Bastiones**: los bastiones liberados ya no salen, y el cursor cae en el icono del bastión en el mapa. Un bastión elegido ahí pasa a la guía de bastiones en cuanto entras.
- **Caminos del mundo abierto**: el camino rodea cajas y puestos, ya no corta esquinas de muros y ya no cambia cada pocos segundos. Si te quedas atascado en él (stick empujado, casi sin moverte), ese sitio cuenta como bloqueado: el programa lo dice y busca otro camino. Recuerda el sitio 60 días y lo libera cuando pasas por él. Quedarte quieto para leer o pelear nunca cuenta como atasco.
- **Más lugares de misión**: la lista de izquierda en la cruceta también ofrece marcadores del mapa que una misión nombra en grupo, como la sacerdotisa de la cueva en el Altar of Ruin. Los lugares que ya alcanzaste en una mazmorra se conservan al reiniciar el programa.

Arreglado:
- **Foul Undercrypts, tras la última pelea con Elias**: la guía te llevaba de vuelta a la entrada de la mazmorra antes de llevarte detrás de Tysa. Un lugar que solo nombra un paso que el juego oculta ahora guía solo después de los lugares del paso que muestra.

Pendiente:
- El arreglo de las Foul Undercrypts está comprobado con pruebas y el log de un tester, no jugándolo otra vez. Varias partes de la guía de bastiones y de los caminos del mundo abierto tampoco se han jugado: que el jefe siga siendo el objetivo, explorar un bastión, y los sitios bloqueados y los lugares alcanzados tras reiniciar. Si algo falla, ejecuta el programa con `--debug` y mándame `d4assist-log.txt`.
- Sobre el lag en el acto 5 que contó un tester: el log muestra que el programa usaba un 1–2 % del equipo, así que el lag no venía de él.
