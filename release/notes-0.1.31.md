**Diablo 4 Assist 0.1.31** keeps the guide at the fight when a quest step sends waves of monsters at you, stops it leading into solid rock, stops the D-pad from turning it on before you play, and lists helltides in Nahantu and Skovos once the map shows them.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### Fixed
- **Waves of monsters in a quest** (the Condemned Mines, at the door Neyrelle opens): a step that asks you to kill many monsters and names no place sent the guide off exploring in the middle of the fight, and after a death nothing led you back. The guide now stays at the last quest place you reached, and leads you back there if you die. If 20 seconds pass there with no monsters on the map, it moves on, because those monsters must be somewhere else; if they turn up there later, it comes back.
- **The guide leading into solid rock**: in the Condemned Mines it aimed at pieces of the map that only fill the gaps between rooms, so it tried to send you through spaces you couldn't walk. They no longer count as ground to explore.
- **Left on the D-pad before you play**: it now does nothing on the character select and creation screens, while loading and during cinematics, so it can't turn the guide on there.
- **The guide heading for a dungeon entrance as soon as you entered the game**: a press right after entering, before the program had read the map, could take a nearby entrance for your goal. Until that first read (30 seconds at most) it now says there is no quest place on the map.
- **Helltides in Nahantu and Skovos (Shift+F2 on the map)**: the list only knew the base game's regions. It now also shows any region where the map marks helltide objects, first in the list and with no minutes left, because the program can't tell yet when that helltide ends.

### Known issues
- **Helltides in Nahantu and Skovos from afar**: they only appear in the list once the map marks their objects, so usually when you are already in that region.
- **ROG Xbox Ally in the Xbox full screen experience**: a tester confirms that external controllers and pads in Xbox input mode work there. The handheld's own controls still don't: the handheld in desktop mode works, or map one of its back buttons to Ctrl+Alt+Shift+G (same as left on the D-pad), and another to Ctrl+Alt+Shift+E if you want to switch the guide between objectives and exploring.

### Still to look at
- None of this has been tried in the game yet. Please run the program with `--debug` and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder before you start another session (only the last two are kept), after any of these:
  - the door in the Condemned Mines, with a death if one happens;
  - left on the D-pad on the character creation screen, then entering the game;
  - Shift+F2 inside a helltide in Nahantu or Skovos, and once more from far away at another hour. This tells me whether the program can see those helltides from afar too.

---

**En español:** la 0.1.31 mantiene la guía en la pelea cuando un paso de misión te lanza oleadas de monstruos, evita que te lleve contra la roca, impide que la cruceta la encienda antes de jugar y muestra las helltides de Nahantu y Skovos cuando aparecen en el mapa. Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Arreglado:
- **Oleadas de monstruos en una misión** (las Condemned Mines, en la puerta que abre Neyrelle): un paso que pide matar muchos monstruos sin nombrar ningún lugar mandaba a la guía a explorar en plena pelea, y tras una muerte nada te llevaba de vuelta. Ahora la guía se queda en el último lugar de misión al que llegaste y te lleva de vuelta si mueres. Si pasan 20 segundos allí sin monstruos en el mapa, sigue adelante, porque esos monstruos estarán en otro sitio; si luego aparecen allí, vuelve.
- **La guía llevándote contra la roca**: en las Condemned Mines apuntaba a trozos del mapa que solo rellenan los huecos entre salas, así que intentaba mandarte por espacios que no se podían pisar. Ya no cuentan como terreno por explorar.
- **Cruceta izquierda antes de jugar**: ahora no hace nada en las pantallas de selección y creación de personaje, durante las cargas ni en las cinemáticas, así que ahí no puede encender la guía.
- **La guía yendo a la entrada de una mazmorra nada más entrar al juego**: una pulsación justo al entrar, antes de que el programa leyera el mapa, podía tomar una entrada cercana como objetivo. Hasta esa primera lectura (30 segundos como máximo) ahora dice que no hay lugar de misión en el mapa.
- **Helltides de Nahantu y Skovos (Shift+F2 en el mapa)**: la lista solo conocía las regiones del juego base. Ahora también muestra cualquier región donde el mapa marque objetos de helltide, al principio de la lista y sin minutos restantes, porque el programa aún no sabe cuándo termina esa helltide.

Problemas conocidos:
- **Helltides de Nahantu y Skovos desde lejos**: solo aparecen en la lista cuando el mapa marca sus objetos, así que normalmente cuando ya estás en esa región.
- **ROG Xbox Ally en la pantalla completa de Xbox**: un tester confirma que ahí funcionan los mandos externos y los pads en modo de entrada Xbox. Los controles propios de la consola siguen sin funcionar: la consola en modo escritorio sí funciona, o asigna uno de sus botones traseros a Ctrl+Alt+Shift+G (lo mismo que la cruceta izquierda), y otro a Ctrl+Alt+Shift+E si quieres cambiar la guía entre objetivos y exploración.

Pendiente: nada de esto se ha probado aún en el juego. Ejecuta el programa con `--debug` y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa antes de empezar otra sesión (solo se guardan las dos últimas), después de cualquiera de estas pruebas:
- la puerta de las Condemned Mines, con una muerte si se da;
- cruceta izquierda en la pantalla de creación de personaje, y luego entrar al juego;
- Shift+F2 dentro de una helltide de Nahantu o Skovos, y otra vez desde lejos a otra hora. Esto me dirá si el programa también puede ver esas helltides desde lejos.
