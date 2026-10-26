**Diablo 4 Assist 0.1.18** adds a sound at the waypoints you haven't activated yet, makes Raheir in the Den heard, and leaves your controller and keys alone while you play something else. Thanks to the player who sent the log from "The Hand Remembers the Blade", and to the one who heard the D-pad speak in another game!

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### New
- **Waypoints you haven't activated.** When you get near one, it plays the game's own "waypoint discovered" sound from where it stands, until you activate it. It has its own switch and volume in the settings, in the quest group.

### Fixed
- **Raheir in the Den.** At "Speak with Raheir in the Den" nothing sounded: the character tap left him out along with the hired mercenaries, and the quest step doesn't name him. He taps now, and a character whose conversation belongs to a quest you're on gets the quest sound even when the quest doesn't name them. The log now says which quest each character's conversation belongs to.
- **Playing something else.** With the program open while you played another game, left on the D-pad said "the guide only works in dungeons", even with Diablo IV closed. Now, while Diablo IV isn't the window in front, the D-pad and the program's keys do nothing and are left to the other programs; only Ctrl+Alt+Shift+C (settings) and Ctrl+Alt+Shift+Q (exit) work everywhere. The waypoint list closes if you switch windows.

None of this has been tested in the game yet. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.18 añade un sonido en los waypoints que aún no has activado, hace que Raheir se oiga en la Guarida y deja en paz tu mando y tus teclas mientras juegas a otra cosa. ¡Gracias al jugador que mandó el log de la misión de Raheir y a quien oyó hablar a la cruceta en otro juego! Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Nuevo: al acercarte a un waypoint que no has activado suena el propio sonido de "waypoint descubierto" del juego desde donde está, hasta que lo actives. Tiene su interruptor y su volumen en la configuración, en el grupo de misión.

Arreglado: en el objetivo de hablar con Raheir en la Guarida no sonaba nada; el toque de personaje lo dejaba fuera junto a los mercenarios contratados, y el paso de la misión no lo nombra. Ahora suena, y un personaje cuya conversación es de una misión que llevas recibe el sonido de misión aunque la misión no lo nombre. Con el programa abierto mientras jugabas a otra cosa, la cruceta izquierda decía "la guía solo funciona en mazmorras", incluso con Diablo IV cerrado. Ahora, mientras Diablo IV no es la ventana de delante, la cruceta y los atajos del programa no hacen nada y quedan libres para otros programas; solo Ctrl+Alt+Shift+C (configuración) y Ctrl+Alt+Shift+Q (salir) funcionan siempre. La lista de waypoints se cierra si cambias de ventana.

Nada de esto se ha probado aún en el juego. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
