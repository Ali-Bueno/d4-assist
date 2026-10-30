**Diablo 4 Assist 0.1.25** fixes the dungeon guide in acts 4 and 5 of the campaign, from a tester's reports.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### Fixed
- **Chains of jumps** (the Temple of the Deathspeaker, on the way to the last two Echoes of Elias): the guide now leads across each jump of a chain instead of pointing straight at the goal. When two jumps stood close together, their ends were paired wrongly; the same mistake hid jumps in 46 places across the game.
- **Swamps without walls** (the Corrosive Swamp, around Tysa): the guide no longer sends you to "unexplored" ground on patches of floor you can't walk to.
- **Desks, shelves and barrels** (the Horadric Vault's Gallery, act 4): the guide's route walks around furniture you can break wherever there is a way around, and stops avoiding a piece once you break it. It can be turned off with `BreakablePropsAvoided` in `d4assist.json`.
- **Following Tysa** (the crypts under the keep, act 5): when a companion runs off, the guide no longer waits where they stood; it leads to where they were last heard, then explores on.
- **The serpent trail to the Tree of Whispers** (act 5): after burning the incense, D-pad left found no quest place because the quest's step was read wrong. The step is read again now, and until then every place the map marks for the quest is offered.

### Still to look at
- The dungeon after putting both serpent's eyes back: its ladders. The guide's floor map merges levels at different heights, so it may point across a drop instead of to a ladder.
- None of this has been played in the game yet. Which sign the game gives when you break furniture is not known yet. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.25 arregla el guía de mazmorras en los actos 4 y 5 de la campaña, a partir de los informes de un tester. Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Arreglado:
- **Saltos encadenados** (el Templo del Portavoz de la Muerte, camino de los dos últimos ecos de Elias): el guía lleva por cada salto de la cadena en vez de apuntar en línea recta al objetivo. Cuando dos saltos estaban muy juntos, sus extremos se emparejaban mal; el mismo fallo escondía saltos en 46 sitios del juego.
- **Pantanos sin paredes** (el Pantano Corrosivo, junto a Tysa): el guía ya no manda a zonas "sin explorar" en trozos de suelo a los que no se llega andando.
- **Pupitres, estanterías y barriles** (la Galería de la Cámara Horádrica, acto 4): la ruta del guía rodea los muebles que se pueden romper si hay camino alrededor, y deja de rodear uno en cuanto lo rompes. Se puede desactivar con `BreakablePropsAvoided` en `d4assist.json`.
- **Seguir a Tysa** (las criptas bajo la fortaleza, acto 5): cuando un acompañante sale corriendo, el guía ya no se queda donde estaba; lleva a donde se le oyó por última vez y luego sigue explorando.
- **El rastro de la serpiente hasta el Árbol de los Susurros** (acto 5): tras quemar el incienso, la cruceta izquierda no encontraba ningún lugar de misión porque el paso de la misión se leía mal. Ahora se vuelve a leer y, mientras tanto, se ofrecen todos los lugares que el mapa marca para la misión.

Pendiente: las escaleras de la mazmorra tras devolver los dos ojos de la serpiente (el mapa del suelo mezcla niveles a distinta altura y puede apuntar a través de un desnivel). Nada de esto se ha probado aún en el juego. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
