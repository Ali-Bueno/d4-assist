**Diablo 4 Assist 0.1.34** fixes the guide in the Darkened Way, adds a key that says how much experience you need for the next level, and adds an optional sound for other players near you.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy the new `D4Assist.exe` over the old one (and the `wine` folder, if you play on Linux); the zip's `d4assist.json` would reset your settings.

### New
- **Experience**: Ctrl+Alt+Shift+X says your level and how much experience is left to the next one, for example "Level 20. 6546 experience to the next level." At the highest level it says your paragon level and what is left to the next. It works while Diablo IV is the window in front.
- **Other players (optional, off by default)**: a soft, slow note for the players near you, up to three at once, from about twice as far as the tap of the characters you can talk to. Turn it on in the settings, under Characters, "Other players". It was asked for to find party members, but for now it can't tell your party from any other player, so in a busy town it will sound for strangers too.

### Fixed
- **The Darkened Way, "In Her Wake"**: after you open the ancient gate, left on the D-pad led back east and down a staircase that the game keeps blocked with an invisible wall during that quest, and you got stuck there. The guide now knows about that wall and leads west through Casama instead, to the climb down in front of the Ancient City's door.

### Good to know
- **Holding left on the D-pad** (to switch the guide between objectives and exploring) has a keyboard key too: Ctrl+Alt+Shift+E. Ctrl+Alt+Shift+G is the short press.

### Still to look at
- **The Darkened Way fix** was checked against the game's data and a tester's log, not by playing through it again. If the guide still gets stuck there, send me `d4assist-log.txt` recorded with `--debug`.
- **Other players**: I found nobody near me while testing, so I haven't heard the note with a real player. If you turn it on, tell me whether it sounds for the players around you, and whether it ever sounds when you are alone.
- The radar still reads that blocked staircase in the Darkened Way as open.

---

**En español:** la 0.1.34 arregla la guía en la Vía Oscura, añade una tecla que dice cuánta experiencia te falta para el siguiente nivel y un sonido opcional para los jugadores que tengas cerca.

**Actualizar:** desde la 0.1.7 o posterior, el programa ofrece esta versión solo al arrancar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia el nuevo `D4Assist.exe` encima del viejo (y la carpeta `wine`, si juegas en Linux); el `d4assist.json` del zip te reiniciaría la configuración.

Nuevo:
- **Experiencia**: Ctrl+Alt+Shift+X dice tu nivel y cuánta experiencia falta para el siguiente, por ejemplo «Nivel 20. Faltan 6546 de experiencia para el siguiente nivel.» En el nivel máximo dice tu nivel de leyenda y lo que falta para el siguiente. Funciona con Diablo IV en primer plano.
- **Otros jugadores (opcional, desactivado por defecto)**: una nota suave y lenta para los jugadores que tengas cerca, hasta tres a la vez, desde más o menos el doble de lejos que el toque de los personajes con los que puedes hablar. Se activa en la configuración, en Personajes, «Otros jugadores». Se pidió para encontrar a los miembros del grupo, pero de momento no distingue a tu grupo de cualquier otro jugador, así que en una ciudad con gente sonará también con desconocidos.

Arreglado:
- **La Vía Oscura, «Tras sus pasos»**: después de abrir la puerta antigua, la cruceta izquierda llevaba de vuelta al este y por una escalera que el juego mantiene cerrada con un muro invisible durante esa misión, y te quedabas atascado ahí. La guía ya conoce ese muro y lleva por el oeste, a través de Casama, hasta la bajada que hay frente a la puerta de la Ciudad Antigua.

Conviene saber:
- **Mantener la cruceta izquierda** (para cambiar la guía entre objetivos y exploración) también tiene tecla: Ctrl+Alt+Shift+E. Ctrl+Alt+Shift+G es la pulsación corta.

Pendiente:
- **El arreglo de la Vía Oscura** está comprobado con los datos del juego y el log de un tester, no jugando otra vez esa parte. Si la guía se sigue atascando ahí, mándame `d4assist-log.txt` grabado con `--debug`.
- **Otros jugadores**: no encontré a nadie cerca mientras probaba, así que no he oído la nota con un jugador real. Si la activas, dime si suena con los jugadores que tengas alrededor y si alguna vez suena estando solo.
- El radar sigue leyendo como abierta esa escalera cerrada de la Vía Oscura.
