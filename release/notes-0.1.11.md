**Diablo 4 Assist 0.1.11** stops the guide from sending you to doors you can't open yet, and takes you through a door once it opens.

**Updating:** with 0.1.7 or newer, the program offers this version itself when it starts (or from the tray icon's menu, "Check for updates"). From an older version, close the program and copy only the new `D4Assist.exe` over the old one; the zip's `d4assist.json` would reset your settings.

### Fixed
- In dungeons with a locked door (find a key on a body, or kill some monsters to unseal it), the guide kept taking you to that door instead of to the parts you still had to explore. A door the game won't let you open yet is now a wall for the guide, and never its goal. If you stand at a closed door and walk away without it opening, the guide also takes it as locked, until your quest moves on (for example, you found the key) or you turn the guide off and on again.
- After you opened a locked door, or a sealed one opened, the guide sent you back to explore leftover corners of the area behind you instead of moving on. Now, once a door or a barrier opens, the guide takes you through it first, and only comes back for what you left behind when there's nothing new past it. That also leads you on toward the boss room.

These changes are new, so tell me how they go, especially in a dungeon with a key door or a sealed door. If something goes wrong, send me `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder and the dungeon's name.

---

**En español:** con la 0.1.11, la guía ya no te manda a puertas que todavía no puedes abrir, y te hace pasar por una puerta en cuanto se abre. Si tienes la 0.1.7 o posterior, el programa te ofrece esta versión solo al iniciar (o desde el menú del icono de la bandeja, "Buscar actualizaciones"). Desde una versión anterior, cierra el programa y copia solo el nuevo `D4Assist.exe` encima del anterior.

Arreglado: en mazmorras con una puerta cerrada con llave (buscar la llave en un cadáver, o matar a unos monstruos para quitar el sello), la guía te llevaba una y otra vez a esa puerta en vez de a las zonas que aún te faltaba explorar. Ahora una puerta que el juego todavía no te deja abrir es una pared para la guía, y nunca su destino. Si te quedas junto a una puerta cerrada y te alejas sin que se abra, la guía también la da por cerrada, hasta que tu misión avance (por ejemplo, encontraste la llave) o apagues y vuelvas a encender la guía. Además, después de abrir una puerta cerrada, o de que se abriera una sellada, la guía te mandaba atrás a explorar rincones de la zona anterior en vez de seguir adelante. Ahora, cuando se abre una puerta o una barrera, la guía te lleva primero a través de ella, y solo vuelve a lo que dejaste atrás cuando ya no queda nada nuevo al otro lado. Eso también te lleva hacia la sala del jefe.

Estos cambios son nuevos, así que contadme qué tal, sobre todo en una mazmorra con puerta de llave o puerta sellada. Si algo va mal, mandadme `d4assist-log.txt` y `d4assist-log.old.txt` de la carpeta del programa y el nombre de la mazmorra.
