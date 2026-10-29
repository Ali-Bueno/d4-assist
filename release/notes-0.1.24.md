**Diablo 4 Assist 0.1.24** guides through the Lord of Hatred campaign, helps in the Horadric Vault guardian's fight, and adds a helltide list to the map. Thanks to DrCaotica, who played the whole campaign to the credits with the program and made these fixes!

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### New
- **Helltide list (Shift+F2 on the world map).** Says where the helltide is right now, from anywhere, and how many minutes it has left. Enter on it lists its objects (tortured gifts, altars, spires, the doomsayer) and the bounties up in its area, nearest first. Enter on one puts the mouse cursor on it, zooming the map in when needed.
- **F2 says your helltide cinders and Baneful Hearts.** It can be turned off in the settings.
- **Attack warnings in boss and elite fights.** An attack the game marks on the ground before it lands is spoken with a way to dodge, and plays a sound where the attack is, moving with it. The sound is a temporary one for now; its volume is the "Boss attack warnings" sound setting.
- **Waypoint list (Shift+F1).** Each waypoint says its region after its name, and the list also appears on your braille display. Wejinhani's waypoint, hidden under the map's tabs, can be chosen now.

### Fixed
(Place names are from the German game.)
- **Dungeons where you must kill every monster.** Once everything was explored the guide headed for the exit. It now leads to the monsters the map shows and says how many are left.
- **"Die Leere".** Two rooms that look the same no longer swap their quest places. The route also uses slides and climbs.
- **The explored map** is kept after a death that puts you in a new copy of the dungeon, and after a trip to town.
- **Doors the game keeps shut** (Tunnels of Hefaetrust): when the goal lies past one, the guide leads to the door, says the way is still blocked, and waits there.
- **A quest place you have reached** stays reached while its step lasts, instead of calling you back.
- **"Der versiegelte Flügel".** The steps around Tal Rasha's seal are led to in order, not the seal at once.
- **When the next step is in another area,** the guide leads back to the portal you came through.
- **Hatred's spires and Skartara's mindscapes.** Quest places that were a little off are corrected, and a killed objective monster counts as done.
- **Bosses that turn hostile only in the fight** now get the boss sound.
- **The Horadric Vault guardian.** The guide leads to the vessel its barrier asks for (Zoltun Kulle, Tal Rasha, Fahd or Cain) and says its name. Once the fight starts only that vessel stays audible, and using it is announced. Between barriers the guide leads to the guardian, not to the locked door.

### Still to look at
- The attack sound is temporary, and the spoken dodge stays on next to it. Not covered yet in the guardian's fight: the disk wave, spirit bursts, chakrams and lobbed orbs.
- Locked doors that only show as a map icon are not seen, and big destroyable objects (like flesh webbing) can block a route.
- Elias's palace: the guide sometimes does not lead through an opening to a new area until you have gone through it.

Most of this was played in the game. Not tested yet: how it works together with 0.1.23's act 3 fixes (the smaller quest in Orbei is now led to step by step), and a quest's step remembered after restarting the program. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.24 guía por la campaña de Lord of Hatred, ayuda en la pelea contra el guardián de la Cámara Horádrica y añade una lista de mareas infernales al mapa. ¡Gracias a DrCaotica, que jugó toda la campaña hasta los créditos con el programa e hizo estos arreglos! Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Nuevo:
- **Lista de mareas infernales (Mayúsculas+F2 en el mapa).** Dice dónde está la marea ahora, desde cualquier sitio, y cuántos minutos le quedan. Enter lista sus objetos y las recompensas activas en su zona; Enter en uno pone el cursor encima, acercando el mapa si hace falta.
- **F2 dice tus ascuas y corazones funestos** de la marea. Se puede desactivar en los ajustes.
- **Avisos de ataques en peleas contra jefes y élites.** Un ataque que el juego marca en el suelo se anuncia con hacia dónde esquivar, y suena donde está, moviéndose con él. El sonido es provisional; su volumen es el ajuste de sonido "Aviso de ataques de jefe".
- **Lista de puntos de ruta (Mayúsculas+F1).** Cada punto dice su región y la lista sale también en la línea braille. El punto de Wejinhani, que quedaba bajo las pestañas del mapa, ya se puede elegir.

Arreglado: las mazmorras de matar a todos los monstruos llevan a los que quedan; dos salas iguales ya no se cambian sus lugares de misión; la ruta usa toboganes y escaladas; el mapa explorado se conserva tras morir o ir a la ciudad; las puertas que el juego mantiene cerradas se guían y se avisa de que el paso sigue bloqueado; los pasos alrededor del sello de Tal Rasha van en orden; cuando el siguiente paso está en otra zona, el guía lleva al portal por el que entraste; los lugares de misión algo desplazados de las agujas del Odio y de los paisajes mentales de Skartara se corrigen; y en la pelea contra el guardián horádrico se guía a la vasija que pide su barrera y se dice su nombre.

Pendiente: el sonido de ataque es provisional; las puertas cerradas que solo son un icono del mapa no se ven; y en el palacio de Elias el guía a veces no lleva por una abertura hasta que la cruzas.

Casi todo se probó en el juego. Falta probar cómo funciona junto a los arreglos del acto 3 de la 0.1.23 y que un paso de misión se recuerde al reiniciar el programa. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
