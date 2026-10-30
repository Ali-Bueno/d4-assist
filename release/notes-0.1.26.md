**Diablo 4 Assist 0.1.26** makes the radar and the room announcements agree with where you can actually walk, from a tester's report in the Corrosive Swamp.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### Fixed
- **Exits you can't walk to** (the Corrosive Swamp, around Tysa): the room announcements no longer name exits on patches of floor you can't reach, and the radar no longer plays the exit tone toward them. 0.1.25 fixed this for the guide; now the radar and the announcements agree with it.
- **Edges with nothing past them**: where a part of the map has no way out on one side, the radar sometimes played the exit tone into nothing. It plays a wall there now.
- **Desks, shelves and barrels**: the radar hears furniture you can break as a wall until you break it, the same furniture the guide's route walks around.

### Still to look at
- The dungeon after putting both serpent's eyes back: its ladders. The guide's floor map merges levels at different heights, so it may point across a drop instead of to a ladder.
- None of this has been played in the game yet. If the barrels make the radar too busy, tell me. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.26 hace que el radar y los anuncios de las salas coincidan con por dónde se puede andar de verdad, a partir del informe de un tester en el Pantano Corrosivo. Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Arreglado:
- **Salidas a las que no se llega** (el Pantano Corrosivo, junto a Tysa): los anuncios de las salas ya no nombran salidas en trozos de suelo a los que no se llega andando, y el radar ya no toca el tono de salida hacia ellas. La 0.1.25 lo arregló para el guía; ahora el radar y los anuncios coinciden con él.
- **Bordes sin nada detrás**: donde una parte del mapa no tiene salida por un lado, el radar a veces tocaba el tono de salida hacia la nada. Ahora suena pared.
- **Pupitres, estanterías y barriles**: el radar oye como pared los muebles que se pueden romper hasta que los rompes, los mismos que la ruta del guía rodea.

Pendiente: las escaleras de la mazmorra tras devolver los dos ojos de la serpiente (el mapa del suelo mezcla niveles a distinta altura y puede apuntar a través de un desnivel). Nada de esto se ha probado aún en el juego. Si los barriles hacen que el radar suene demasiado, dímelo. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
