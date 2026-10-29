**Diablo 4 Assist 0.1.20** fixes two act 2 dungeons where the dungeon guide pointed the wrong way: the Weeping Cairns and the Eldhaime Barracks. Thanks to the player who wrote up their act 2 run!

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

If you're still on 0.1.18, this also brings 0.1.19's fixes for the act 1 campaign dungeons and the switch between objectives and exploring (hold left on the D-pad for a second, or Ctrl+Alt+Shift+E).

### Fixed
- **The Weeping Cairns.** At the sealed tomb gate the guide stayed on Yorin, though the gate needs a ward stone from deeper in the dungeon. Now, when the quest wants you to bring something there first, walking away from the gate lets the guide explore; once the stone is heard it leads to it, and once you pick it up it leads you back to the gate.
- **The Eldhaime Barracks.** For the step down the climb, the guide led back to the dungeon entrance and said the objective was below you. The game files that step's spot under the old name of the room with the climb, and the program placed it in the wrong spot. It now finds that room and leads there.

None of this has been tested in the game yet. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.20 corrige dos mazmorras del acto 2 en las que la guía apuntaba mal: los Weeping Cairns y los barracones de Eldhaime. ¡Gracias al jugador que nos contó su partida del acto 2! Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior. Si aún tienes la 0.1.18, también trae los arreglos de la 0.1.19 para las mazmorras de campaña del acto 1 y el cambio entre objetivos y exploración (mantén la cruceta izquierda un segundo, o Ctrl+Alt+Shift+E).

Arreglado: en la puerta sellada de la tumba de los Weeping Cairns la guía se quedaba en Yorin, aunque la puerta necesita una piedra de más adentro de la mazmorra. Ahora, cuando la misión quiere que lleves algo allí antes, al alejarte de la puerta la guía explora; cuando suena la piedra te lleva a ella, y al cogerla te devuelve a la puerta. En los barracones de Eldhaime, en el paso de bajar por la escalada, la guía te llevaba a la entrada y decía que el objetivo estaba debajo: el juego guarda el punto de ese paso con el nombre antiguo de la sala de la escalada y el programa lo situaba en otro sitio. Ahora encuentra esa sala y te lleva allí.

Nada de esto se ha probado aún en el juego. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
