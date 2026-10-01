First release of **Diablo 4 Assist**, an accessibility companion for blind players of Diablo IV. Made for Diablo IV 3.2.2; tested start to finish in the act 1 campaign, the Undercity and the Pit.

> **Risk:** the program reads the game's memory (read only: it never writes to the game or injects anything). Blizzard could still consider it against its terms of service. Use it at your own risk.

**Download** `D4Assist-0.1.0.zip`, unzip it anywhere and run `D4Assist.exe`. It lives in the notification area. The full guide is `README.txt` inside the zip, and the [README](https://github.com/Ali-Bueno/d4-assist#readme) here.

### What it does
- Spoken room announcements with exits as stick directions.
- Audio radar (exit, wall, closed door, open floor) in four directions.
- Exploration guide (D-pad left): leads to unexplored ground, to objectives already heard, to the place the quest sends you, then to a portal.
- Looping sounds on objectives, portals, doors, chests, readables, characters, boss-fight barriers, and on what a dungeon's objective asks you to kill. Other monsters never sound.
- Dungeon barriers block the radar and the guide until they open.
- Sounds pause in conversations, cutscenes, menus and when the game is in the background.
- Spoken warning if a game update stops it from reading part of the game.
- Speaks the game's 13 text languages. Languages other than English and Spanish are machine translations: corrections from native speakers are welcome.

### Known issues
- Started mid-dungeon, the first room is called the entrance.
- Corridors exactly between two radar directions read as a wall until you line up with them.
- From act 2 on, some quest objects or boss mechanics may not sound yet. Not tested in play yet: dungeon barriers other than Maulwood's, Mephisto's orbs, Akarat's lights, the Horadric vessels, the desert shelter guide.

Report problems in [Issues](https://github.com/Ali-Bueno/d4-assist/issues), attaching `d4assist-log.txt` and `d4assist-log.old.txt` from the program's folder.

---

**En español:** primera versión de Diablo 4 Assist, un asistente de accesibilidad para jugar a Diablo IV sin ver la pantalla. Descarga `D4Assist-0.1.0.zip`, descomprímelo y abre `D4Assist.exe`. Las instrucciones completas están en `README.txt` (en inglés), dentro del zip. **Aviso:** lee la memoria del juego (solo lectura); úsalo bajo tu propia responsabilidad. Los problemas, en [Issues](https://github.com/Ali-Bueno/d4-assist/issues), con los dos archivos de registro adjuntos.

