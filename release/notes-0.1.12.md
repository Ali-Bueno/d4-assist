**Diablo 4 Assist 0.1.12** gives bosses, unique monsters and healing wells their own sounds, and should get the guide to the boss room.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### New
- Bosses growl with a metallic ring, and unique monsters growl with chattering teeth, everywhere: dungeons, the open world, Helltide, with a quest or without one. You hear the nearest of each, faster the closer it is. A boss that nothing can hurt for now goes quiet, so whatever you must kill meanwhile stands out. A boss the dungeon asks you to kill now plays this sound instead of the quest sound. Elite packs don't have a sound yet. The sounds come from the Diablo II: Resurrected access mod.
- Healing wells have their own sound, the drinking sound, and go quiet once you use them. You hear one once the game has loaded it, which is when you're fairly close.
- In the settings, the Characters group has "Bosses" and "Elites and uniques", and the Objects group has "Healing wells", each with its own switch and volume.

### Fixed
- The guide could fail to reach the boss room and send you back to the dungeon's portal instead. The likely cause: the boss door opens by itself when you finish the objective, usually while you are far from it, and the guide kept taking it as closed. Now, when the dungeon's quest moves on, the guide counts again with the doors and walls that may have opened. Meanwhile, turning the guide off and on again also helps.
- Left on the D-pad outside a dungeon did nothing, so it seemed broken. Now it tells you the guide only works in dungeons.
- The log now says which controller the program found and whether it found the game's menus, so reports about the controller or about sounds going quiet are easier to solve.

These changes are new, so tell me how they go, especially at a boss (the Helltide Diablo fight included) and in a dungeon where the guide didn't reach the boss room. If something goes wrong, send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder and the dungeon's name.

---

**En español:** con la 0.1.12, los jefes, los monstruos únicos y los pozos de curación tienen su propio sonido, y la guía debería llegar a la sala del jefe. Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Nuevo: los jefes gruñen con un timbre metálico y los monstruos únicos gruñen con castañeteo de dientes, en todas partes: mazmorras, mundo abierto, Marea Infernal, con misión o sin ella. Oyes el más cercano de cada tipo, más rápido cuanto más cerca está. Un jefe al que de momento nada puede herir se calla, para que destaque lo que tienes que matar mientras tanto. Un jefe que la mazmorra te pide matar suena así en lugar de con el sonido de misión. Los grupos de élite todavía no tienen sonido. Los sonidos vienen del mod de accesibilidad de Diablo II: Resurrected. Los pozos de curación tienen su sonido, el de beber, y se callan al usarlos; suenan cuando el juego ya los ha cargado, es decir, cuando estás bastante cerca. En los ajustes, el grupo Personajes tiene "Jefes" y "Élites y únicos", y el grupo Objetos tiene "Pozos de curación", cada uno con su interruptor y su volumen.

Arreglado: la guía podía no llegar a la sala del jefe y mandarte de vuelta al portal de la mazmorra. La causa probable: la puerta del jefe se abre sola al terminar el objetivo, normalmente con el jugador lejos, y la guía la seguía dando por cerrada. Ahora, cuando la misión de la mazmorra avanza, la guía vuelve a contar con las puertas y paredes que se hayan podido abrir. Mientras tanto, apagar y volver a encender la guía también ayuda. Izquierda en la cruceta fuera de una mazmorra no hacía nada y parecía estropeado; ahora te dice que la guía solo funciona en mazmorras. El log apunta ahora qué mando encontró el programa y si encontró los menús del juego.

Estos cambios son nuevos, así que contadme qué tal, sobre todo con un jefe (también el combate contra Diablo en la Marea Infernal) y en una mazmorra donde la guía no llegaba a la sala del jefe. Si algo va mal, mandadme `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa y el nombre de la mazmorra.
