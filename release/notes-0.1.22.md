**Diablo 4 Assist 0.1.22** fixes the crash right after updating. Thanks to the player who sent both logs!

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

**This time only:** the update to 0.1.22 is still installed by your current version, which has the bug. When it offers 0.1.22 at start, wait about half a minute before choosing Install. If the program closes anyway, just open it again: 0.1.22 is already in place.

### Fixed
- **The crash after updating.** When the update was accepted while the program was still starting, the new files went in before it had finished loading. The part of the program that was still loading then failed, and the program closed instead of restarting. Now the update waits until the program has finished starting, then restarts as usual.

None of this has been tested in the game yet. If something sounds wrong, please run the program with `--debug` where it happens and send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** la 0.1.22 arregla el cierre justo después de actualizar. ¡Gracias al jugador que mandó los dos logs! Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Solo esta vez: la actualización a la 0.1.22 la instala todavía tu versión actual, que tiene el fallo. Cuando te la ofrezca al iniciar, espera como medio minuto antes de elegir Instalar. Si aun así el programa se cierra, ábrelo otra vez: la 0.1.22 ya está instalada.

Arreglado: si se aceptaba la actualización mientras el programa todavía estaba arrancando, los archivos nuevos se copiaban antes de que terminara de cargar. La parte que aún estaba cargando fallaba y el programa se cerraba en lugar de reiniciarse. Ahora la actualización espera a que el programa termine de arrancar y luego se reinicia como siempre.

Nada de esto se ha probado aún en el juego. Si algo suena mal, ejecuta el programa con `--debug` donde pase y mándame `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa.
