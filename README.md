# Diablo 4 Assist

An accessibility companion for **blind and low-vision players of Diablo IV**. It runs next to the game and speaks
through your screen reader (NVDA or JAWS), adding spoken room announcements and positional sounds so you can find your
way through dungeons without seeing the screen. It works alongside the game's own third-party screen reader support and
its navigation assist for the open world.

> **Risk warning.** The program reads the game's memory while you play. It only reads: it never writes to the game, never
> injects code, never moves your character and never reveals enemies or loot. Even so, Blizzard could consider it
> against its terms of service, and there is a risk of action against your account. **Use it at your own risk.**

**Current version: 0.1.0** (first release, October 2026), made for Diablo IV 3.2.2. Tested start to finish in the act 1
campaign, the Undercity and the Pit. Later acts and the expansion may still have quest objects or boss mechanics with no
sound yet: please report them (see [Reporting problems](#reporting-problems)).

## What it does

- **Room announcements.** On entering junctions, large rooms and dead ends it says the exits as controller-stick
  directions ("Junction. Exits: up, right unexplored.").
- **Audio radar.** Short sounds up, down, left and right: a bright tone for an exit, a dull knock for a wall, two knocks
  for a closed door, a soft breath for open floor. A port of the author's Diablo II Resurrected radar.
- **Exploration guide.** A ping that leads you along the path to the nearest unexplored area, faster as you get closer,
  with an arrival chime. It first leads to objectives you have already heard and not done, and to the place your quest
  sends you. With nothing left, it leads to a portal. D-pad left turns it on and off.
- **Objective sounds.** A looping quest sound on levers, carried items and their pedestals, quest chests, prisoners, the
  quest objects you can use, structures a quest asks you to break, and the monsters a dungeon's objective asks you to
  kill. Other monsters never sound.
- **Dungeon barriers.** A wall that closes a passage until you complete an objective reads as a wall on the radar, and
  the guide does not lead you behind it until it opens.
- **Portals and exits.** A loop at every portal in a dungeon: the exit, the portals between floors, the Undercity pad.
- **Doors, chests and readables.** Looping sounds for doors (wood, metal, stone), chests, books and notes, and bodies to
  search; they stop once used.
- **Characters.** A soft tap on the nearest character you can talk to, faster as you approach.
- **Boss fights.** Sounds for protective barriers to stand in, and (not tested in play yet) Mephisto's stolen-skill orbs,
  Akarat's lights and the Horadric Guardian's vessels.
- **Silence when it matters.** All sounds pause during conversations, cutscenes, in-engine cinematics, game menus, and
  when the game is not the active window.
- **Warnings instead of silence.** If a game update stops the program from reading part of the game, it says so once,
  with what no longer works.
- **Settings window.** From the notification-area icon (or Ctrl+Alt+Shift+C): every sound can be switched off, levelled and
  sped up or slowed down, with a live preview. Standard Windows controls that screen readers read.

It speaks the game's text language: English, Spanish (Spain and Latin America), French, German, Italian, Polish,
Portuguese (Brazil), Russian, Turkish, Japanese, Korean and Chinese (Simplified and Traditional); the settings window can
force another. Your screen reader needs a voice for that language. Translations other than English and Spanish are
machine-made: corrections from native speakers are welcome.

Everything works in every dungeon type: ordinary, campaign, expansion and Nightmare dungeons, the Undercity and the Pit.
In fixed story areas (the Darkened Way, Tristram, the hells) the radar and objective sounds run without room
announcements. In the open world only nearby objectives and the desert sandstorm shelter guide (not tested in play yet) run; the
game's own navigation assist covers the rest.

## Requirements

- Windows 10 or 11, 64-bit. Nothing else to install: .NET is included in the program.
- Diablo IV installed through Battle.net (the program finds it on its own).
- NVDA or JAWS running (tested with NVDA).
- An Xbox controller, DualSense or DualShock 4, wired or Bluetooth, for D-pad left (everything else works without one).
- About 1 GB of free RAM on top of the game.

## Install and use

1. Download `D4Assist-<version>.zip` from the [Releases](../../releases) page and unzip it anywhere.
2. Run `D4Assist.exe`, before or after starting the game. It says "Diablo 4 Assist active" (in the game's
   language) and stays in the notification area, next to the clock; there is no window.
3. Play normally.
4. To close it, choose "Exit" from its notification-area icon menu (Windows+B, then the icon "Diablo 4 Assist";
   it may be under "Show hidden icons").

The full guide is `README.txt` inside the zip.

### Keys

| Key | Action |
|---|---|
| D-pad left | Exploration guide on/off (ignored while a game menu is open) |
| Ctrl+Alt+Shift+C | Settings window |
| Ctrl+Alt+Shift+1 … 8 | Hear each sound with its name: exit, open floor, wall, door, unexplored exit, quest sound, guide ping, arrival |

## Known issues

- Started in the middle of a dungeon, the first room it announces is called the entrance.
- The radar listens in four screen directions. Dungeon corridors often run diagonally: a corridor exactly between two
  directions reads as a wall on both until you line up with it.
- In fixed story areas, a ledge above a lower floor can read as open floor.
- From act 2 on and in the expansion, some quest objects or boss mechanics may not sound yet.

## Reporting problems

Each run writes `d4assist-log.txt` next to the program (the previous run is kept as `d4assist-log.old.txt`). If
something does not sound, or you hear a warning, close the program from its icon and attach both files to a new
[issue](../../issues), saying where you were in the game. The log's second line says which version you use.

## About the source code

The source code is private, because the program reads the game's memory. Builds are published here.

## Credits

- Screen reader output through [PRISM](https://github.com/ethindp/prism) (the Prismatoid .NET package).
- Game data definitions from [d4data](https://github.com/blizzhackers/d4data).
- Radar sounds and design from the author's accessibility mod for Diablo II Resurrected.
