**Diablo 4 Assist 0.1.39** keeps the program running when the screen reader fails to read something.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy the new `D4Assist.exe` over the old one (and the `wine` folder, if you play on Linux); the zip's `d4assist.json` would reset your settings.

### Fixed
- **The program closed when the screen reader failed** (ZDSR, F2 for the cinders and hearts): when a screen reader answered with an error, the program closed. Now the error goes to the log and the program keeps running. If the screen reader fails at speaking and showing braille together, the program asks it only to speak, plus braille where it has it.

### Still to look at
- This was checked with tests and the reporter's log, not with ZDSR. If you use ZDSR, tell me whether F2 now reads the cinders and hearts.

---

**En español:** la 0.1.39 hace que el programa siga funcionando cuando el lector de pantalla falla al leer algo.

**Actualizar:** desde la 0.1.7 o posterior, el programa ofrece esta versión solo al arrancar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia el nuevo `D4Assist.exe` encima del viejo (y la carpeta `wine`, si juegas en Linux); el `d4assist.json` del zip te reiniciaría la configuración.

Arreglado:
- **El programa se cerraba cuando fallaba el lector de pantalla** (ZDSR, F2 para las cenizas y los corazones): cuando un lector de pantalla respondía con un error, el programa se cerraba. Ahora el error va al log y el programa sigue funcionando. Si el lector falla al hablar y mostrar el braille a la vez, el programa le pide solo que hable, más el braille si lo tiene.

Pendiente:
- Está comprobado con pruebas y el log de quien lo reportó, no con ZDSR. Si usas ZDSR, dime si F2 ya lee las cenizas y los corazones.
