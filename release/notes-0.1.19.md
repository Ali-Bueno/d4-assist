**Diablo 4 Assist 0.1.19** stops the dungeon guide from leading you back the way you came in campaign dungeons, finds the way across ladders and jumps in more places, and adds a switch between leading to the quest and leading to the ground you haven't explored. Thanks to the player who sent the long report and the logs from act 1's dungeons!

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### New
- **Objectives or exploring.** Hold left on the D-pad for about a second (or press Ctrl+Alt+Shift+E) to switch the dungeon guide between leading to the quest objectives ("Guide: objectives.") and leading only to what you haven't explored yet ("Guide: exploring."). The choice lasts until you leave the dungeon; each new one starts on the objectives. To tell a press from a hold, a short press of left on the D-pad now acts when you let go of the button.

### Fixed
- **The guide leading back.** Some quest steps are invisible spots you only have to walk through, and the guide could take one you had already crossed for one still ahead. In the Cradle it led back outside each gate once it opened: the dungeon repeats the same tunnel, and the step's spot was placed in the tunnel behind you. In the Necropolis it went back to the central chamber after every tumor. Now a spot you have walked out of stays behind you, a spot the quest names where you have already been never leads, and they only come back once there is nothing left to explore.
- **The Necropolis tumors.** Three of the five stand on platforms you reach by a ladder or by ducking under something, and the guide didn't know about those, so it aimed straight at the wall. It now uses ladders and duck-unders as well as jumps.
- **The Darkened Way.** The guide couldn't find the way across this area's jump and ladder and pointed in a straight line. It now knows them, here and in every story area that has them.

### Still open
- In the Old Mines after Neyrelle opens the door, and in the Horadric Vault, the logs didn't show why the guide held back. These fixes may cover them; a `--debug` log would tell.
- On the Mourning Shore the path may run through a locked gate.
- Vhenard's sound stopping is probably on purpose: a boss goes quiet while it can't be hurt, and she can't between waves.

None of this has been tested in the game yet. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.19 evita que la guía de mazmorras te lleve hacia atrás en las mazmorras de campaña, encuentra el camino por escaleras y saltos en más sitios y añade un cambio entre ir a la misión e ir a lo que no has explorado. ¡Gracias al jugador que mandó el informe largo y los logs de las mazmorras del acto 1! Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Nuevo: mantén la cruceta izquierda un segundo (o pulsa Ctrl+Alt+Shift+E) para cambiar la guía entre los objetivos de la misión («Guía: objetivos.») y solo lo que no has explorado («Guía: exploración.»). Dura hasta que sales de la mazmorra; cada mazmorra nueva empieza en objetivos. Para distinguir pulsar de mantener, la pulsación corta de la cruceta izquierda ahora actúa al soltar el botón.

Arreglado: algunos pasos de misión son puntos invisibles que solo hay que cruzar, y la guía podía tomar uno ya cruzado por uno pendiente. En el Cradle te devolvía fuera de cada verja tras abrirse (la mazmorra repite el mismo túnel y el punto quedaba en el de detrás), y en la Necrópolis volvía a la cámara central tras cada tumor. Ahora un punto del que has salido queda atrás, uno que la misión nombra donde ya has estado nunca guía, y solo vuelven cuando no queda nada por explorar. Tres de los cinco tumores de la Necrópolis están en plataformas a las que se llega por escalera o agachándose; la guía no las conocía y apuntaba a la pared, y ahora las usa además de los saltos. En el Darkened Way la guía no encontraba el camino por su salto y su escalera; ahora los conoce, ahí y en cualquier zona de historia que los tenga.

Pendiente: en las Old Mines después de que Neyrelle abra la puerta y en la Horadric Vault los logs no mostraban por qué se quedaba atrás la guía; estos arreglos pueden cubrirlo, y un log con `--debug` lo diría. En la Mourning Shore el camino puede pasar por una verja cerrada. Que el sonido de Vhenard se pare es probablemente intencionado: un boss se calla mientras no se le puede dañar, y a ella no se puede entre oleadas.

Nada de esto se ha probado aún en el juego. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
