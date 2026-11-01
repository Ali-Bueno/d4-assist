**Diablo 4 Assist 0.1.27** fixes what testers ran into in Vessel of Hatred and in a replay of act 1, and adds a guide to dungeon entrances in the open world.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### New
- **Dungeon entrances in the open world**: left on the D-pad, after your quest places, now also leads to the entrances of dungeons and cellars within 150 meters, along a path the app finds ("Entrance: Sunken Ruins, 40 meters."). Handy when the game's navigation assist says you've arrived but the door is below a ledge or behind a rock.

### Fixed
- **Necropolis of the Firstborn tumors**: the guide said you had arrived about 3.5 meters from a tumor, with its own pieces or a flesh web still in between. It now keeps leading until you are right next to it, and plays the arrival sound only there.
- **The Darkened Way, after the gate**: on the way down to the ruined city the path went straight through invisible doors that stay shut. Paths in story areas and the open world now go around doors that are still locked.
- **Durance of Hate, second visit**: the guide led through the tubes that block the old tunnel. Structures like these, which no quest asks you to break, are now walked around like breakable furniture, and the radar hears them as a wall until they break.
- **Tomb of Akarat, the braziers to light**: they lost their quest sound once you used one. They now keep sounding while the quest still needs them, so you can find all four.
- **Controllers that do nothing (ROG Xbox Ally)**: the log now says why a press of left on the D-pad was ignored, and which buttons each controller reports first. If the D-pad does nothing for you, please send me `d4assist-log.txt` after pressing it in a dungeon.

### Still to look at
- **Dedicant's Cache**: the guide stays on the door Neyrelle opens from the other side. I need a log with `--debug` from there to see why.
- **The braziers**: the app can't yet tell a lit brazier from an unlit one. A `--debug` log while lighting them would show it.
- **Ledges over lower floors** (the Necropolis overlook, the Snake Den's ladders): the guide's floor map merges levels at different heights, so it may point across a drop.
- None of this has been played in the game yet. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder, before starting another session (only the last two are kept).

---

**En español:** la 0.1.27 arregla lo que los testers encontraron en Vessel of Hatred y al repetir el acto 1, y añade una guía a las entradas de mazmorras en el mundo abierto. Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Nuevo:
- **Entradas de mazmorras en el mundo abierto**: la cruceta izquierda, después de los lugares de tus misiones, lleva también a las entradas de mazmorras y sótanos a menos de 150 metros, por un camino que calcula el programa ("Entrada: Ruinas hundidas, 40 metros."). Útil cuando la asistencia de navegación del juego dice que has llegado pero la puerta está bajo un saliente o detrás de una roca.

Arreglado:
- **Tumores de la Necrópolis del Primogénito**: la guía decía que habías llegado a unos 3,5 metros del tumor, con sus propios trozos o una telaraña de carne en medio. Ahora sigue guiando hasta que estás pegado a él, y el sonido de llegada suena solo ahí.
- **El Camino Oscuro, después de la verja**: bajando hacia la ciudad en ruinas, el camino iba recto por puertas invisibles que siguen cerradas. Los caminos en zonas de historia y en el mundo abierto ahora rodean las puertas que siguen bloqueadas.
- **Durance of Hate, segunda visita**: la guía llevaba a través de los tubos que tapan el túnel viejo. Las estructuras así, que ninguna misión pide romper, ahora se rodean como los muebles rompibles, y el radar las oye como pared hasta que se rompen.
- **Tumba de Akarat, los braseros que hay que encender**: perdían el sonido de misión al usar uno. Ahora siguen sonando mientras la misión los necesite, para que encuentres los cuatro.
- **Mandos que no hacen nada (ROG Xbox Ally)**: el log ahora dice por qué se ignoró una pulsación de la cruceta izquierda y qué botones envía primero cada mando. Si la cruceta no te hace nada, mándame `d4assist-log.txt` después de pulsarla en una mazmorra.

Pendiente: la puerta que Neyrelle abre desde el otro lado en el Dedicant's Cache (necesito un log con `--debug` de ahí); distinguir un brasero encendido de uno apagado; los salientes sobre pisos más bajos (el mirador de la Necrópolis, las escaleras del Snake Den). Nada de esto se ha probado aún en el juego. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa, antes de empezar otra sesión (solo se guardan las dos últimas).
