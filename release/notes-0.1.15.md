**Diablo 4 Assist 0.1.15** fixes quest objects that stayed silent when the quest wasn't tracked.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### Fixed
- In Wrack & Ruin (Hawezar), the eye sockets at the Engraved Stone Door never played the quest sound. The quest hands its steps over to two hidden quests (one per eye), and the program only followed those when you had the quest tracked. Now it follows them for any quest you have in progress, tracked or not, and a quest it found near you stays followed when you walk away from its objects. Other quests that hand over like this benefit too.

The sockets sound when you're close to them, like the other quest objects. Nothing leads you to them from far away in the open world yet.

This change hasn't been tested in the game yet. If a socket stays silent while you hold its eye, please run the program with `--debug`, stand at the stone door, and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.15 arregla objetos de misión que no sonaban si no tenías la misión en seguimiento. Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Arreglado: en Wrack & Ruin (Hawezar), las cuencas de los ojos de la puerta de piedra grabada nunca hacían el sonido de misión. La misión pasa sus pasos a dos misiones ocultas (una por ojo), y el programa solo las seguía si tenías la misión en seguimiento. Ahora las sigue con cualquier misión en curso, en seguimiento o no, y una misión que encontró cerca de ti se sigue aunque te alejes de sus objetos. Otras misiones que pasan sus pasos así también se benefician.

Las cuencas suenan cuando estás cerca, como los demás objetos de misión. En el mundo abierto todavía nada te lleva hasta ellas desde lejos.

Este cambio aún no se ha probado en el juego. Si una cuenca no suena mientras llevas su ojo, ejecuta el programa con `--debug`, ponte junto a la puerta de piedra y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
