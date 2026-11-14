**Diablo 4 Assist 0.1.35** fixes the guide at the start of the Darkened Holt, and makes it stop sending you back to a ladder it can't get you up in the Necropolis of the First Born.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy the new `D4Assist.exe` over the old one (and the `wine` folder, if you play on Linux); the zip's `d4assist.json` would reset your settings.

### Fixed
- **Darkened Holt**: at the start, the guide led towards a place you can't walk to, and kept doing so until you walked off or switched the guide off and on. It took a small gap at the edge of the first clearing for a way on. It no longer does.
- **Necropolis of the First Born, the tumor off the Demonic Memorial**: the guide kept leading to the foot of a ladder wrapped in a flesh web that opens only once that tumor is dead, so you stood right below the tumor with no way up. Now, when the guide has sent you to a ladder or a jump with something standing on it, and you walk about ten meters away without having got across, it gives up on it and leads you round another way. Switching the guide off and on makes it try that ladder again.

### Good to know
- **The Darkened Way fix from 0.1.34** is confirmed by a tester who played through it.

### Still to look at
- **The Necropolis**: the first time, the guide may still lead you to that ladder. Walk away from it and the guide takes the other way. This was checked with tests and a tester's log, not by playing it again. If you replay it, run the program with `--debug` and send me `d4assist-log.txt`.
- **Other players' note** (0.1.34, off by default): still not heard with a real player near.
- The radar still reads the blocked staircase in the Darkened Way as open.

---

**En español:** la 0.1.35 arregla la guía al principio del Darkened Holt (el bosque del acto 1) y hace que deje de mandarte una y otra vez a una escalera por la que no puede subirte en la Necrópolis del Primogénito.

**Actualizar:** desde la 0.1.7 o posterior, el programa ofrece esta versión solo al arrancar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia el nuevo `D4Assist.exe` encima del viejo (y la carpeta `wine`, si juegas en Linux); el `d4assist.json` del zip te reiniciaría la configuración.

Arreglado:
- **Darkened Holt**: al principio, la guía llevaba hacia un sitio al que no se puede caminar, y seguía haciéndolo hasta que te alejabas o apagabas y encendías la guía. Tomaba por una salida un hueco pequeño en el borde del primer claro. Ya no.
- **Necrópolis del Primogénito, el tumor junto al memorial demoníaco**: la guía llevaba una y otra vez al pie de una escalera envuelta en una telaraña de carne que solo se abre cuando ese tumor muere, así que te quedabas justo debajo del tumor sin forma de subir. Ahora, cuando la guía te ha mandado a una escalera o a un salto con algo encima y te alejas unos diez metros sin haber cruzado, renuncia a ese paso y te lleva rodeando por otro camino. Apagar y encender la guía hace que vuelva a intentar esa escalera.

Conviene saber:
- **El arreglo de la Vía Oscura de la 0.1.34** está confirmado por un tester que la jugó entera.

Pendiente:
- **La Necrópolis**: la primera vez, la guía todavía puede llevarte a esa escalera. Aléjate de ella y la guía toma el otro camino. Esto está comprobado con pruebas y con el log de un tester, no jugándolo otra vez. Si la repites, ejecuta el programa con `--debug` y mándame `d4assist-log.txt`.
- **La nota de otros jugadores** (0.1.34, desactivada por defecto): sigue sin oírse con un jugador real cerca.
- El radar sigue leyendo como abierta la escalera cerrada de la Vía Oscura.
