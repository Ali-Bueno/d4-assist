**Diablo 4 Assist 0.1.23** fixes the guide in act 3's Orbei Monastery and Guulrahn's prison. Thanks to the player who sent the act 3 notes and log!

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### Fixed
- **Orbei Monastery, the dying monk.** The step to speak with him belongs to a smaller quest the main one waits on, and the program did not follow that smaller quest, so the guide only got near him. It also placed him in the wrong spot, because the game places him from inside the tunnel's own layout. Now the guide follows the smaller quest and leads to the monk where he lies. The abbot's note in the monastery is placed the same way now.
- **Guulrahn's prison, the hiding prisoners.** The prisoners sitting in the jail cells are scenery that cannot be freed, but the guide counted them as prisoners to free and visited them one by one before leading into the jail. They are no longer guided to. Prisoners a dungeon lets you free are guided to as before.
- **Guulrahn's prison, after freeing Oyun.** The guide kept leading back to the cell gate already opened, instead of into the abandoned wing. The step you are standing at when the quest moves on now counts as done and does not pull you back. The scene in the city's marketplace is also guided to only in the marketplace, not in the prison's tunnels.

### Still to look at
- Elias's palace: the guide sometimes does not lead through an opening to a new area until you have gone through it.

None of this has been tested in the game yet. If something sounds wrong, or to help with Elias's palace, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.23 arregla el guía en el monasterio de Orbei y en la prisión de Guulrahn (acto 3). ¡Gracias al jugador que mandó sus notas y el log! Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Arreglado:
- **Monasterio de Orbei, el monje moribundo.** Hablar con él es un paso de una misión menor de la que depende la principal, y el programa no la seguía, así que el guía solo te acercaba. Además lo situaba en un sitio equivocado. Ahora sigue esa misión y lleva hasta el monje. La nota del abad del monasterio también queda en su sitio.
- **Prisión de Guulrahn, los prisioneros escondidos.** Los prisioneros de las celdas son decorado y no se pueden liberar, pero el guía los visitaba uno a uno antes de llevar a la cárcel. Ya no guía hacia ellos. Los prisioneros que una mazmorra sí deja liberar se guían como antes.
- **Prisión de Guulrahn, después de liberar a Oyun.** El guía volvía a la reja de la celda ya abierta en vez de llevar al ala abandonada. Ahora el paso junto al que estás cuando la misión avanza queda hecho y no tira de ti hacia atrás. La escena del mercado de la ciudad se guía solo en el mercado, no en los túneles de la prisión.

Pendiente: en el palacio de Elias el guía a veces no lleva por una abertura a una zona nueva hasta que la cruzas.

Nada de esto se ha probado aún en el juego. Si algo suena mal, o para ayudar con el palacio de Elias, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
