**Diablo 4 Assist 0.1.37** fixes the guide in the Corrosive Swamp (act 4) and in any dungeon whose step only the game's quest marker shows.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy the new `D4Assist.exe` over the old one (and the `wine` folder, if you play on Linux); the zip's `d4assist.json` would reset your settings.

### Fixed
- **The Corrosive Swamp, the Maggot Queen**: after exploring every area, the guide turned you back to the campfire instead of taking you to the Queen's hole. The game marks that step only with its own quest marker on the map, which the guide ignored in dungeons. In a dungeon the guide now leads to the game's quest markers too, also across ground you haven't explored yet.

### Still to look at
- This was checked with tests, a tester's log and the game's data, not by playing it again. If you play a dungeon like this, run the program with `--debug` and send me `d4assist-log.txt`. It's still unclear whether the game removes its marker as soon as the step is done.

---

**En español:** la 0.1.37 arregla la guía en la Ciénaga Corrosiva (acto 4) y en cualquier mazmorra cuyo paso solo muestra el marcador de misión del juego.

**Actualizar:** desde la 0.1.7 o posterior, el programa ofrece esta versión solo al arrancar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia el nuevo `D4Assist.exe` encima del viejo (y la carpeta `wine`, si juegas en Linux); el `d4assist.json` del zip te reiniciaría la configuración.

Arreglado:
- **Ciénaga Corrosiva, la Reina de los gusanos**: después de explorar todas las zonas, la guía te mandaba de vuelta a la hoguera en vez de llevarte al agujero de la Reina. El juego marca ese paso solo con su propio marcador de misión en el mapa, y en las mazmorras la guía lo ignoraba. Ahora, en una mazmorra, la guía también lleva a los marcadores de misión del juego, incluso por zonas que aún no has explorado.

Pendiente:
- Está comprobado con pruebas, el log de un tester y los datos del juego, no jugándolo otra vez. Si juegas una mazmorra así, ejecuta el programa con `--debug` y mándame `d4assist-log.txt`. Aún no está claro si el juego quita su marcador en cuanto el paso termina.
