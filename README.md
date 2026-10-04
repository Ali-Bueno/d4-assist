# Diablo 4 Assist

This is a companion for Diablo 4 that adds navigation cues for dungeons, quests, objects, characters and boss
fights. It runs next to the game, talks through your screen reader (NVDA or JAWS) and plays sounds that come from
wherever things are around you.

## About the ban risk

To know where things are, the program reads the game's memory. It only reads, it never writes anything to the game or
injects anything into it. Still, Blizzard could see it as against their rules, so there's a risk of getting your
account banned. I play with it on my own account, but you decide. Use it at your own risk.

## It doesn't play for you

And I mean it: it never moves your character. No autowalk, no "take me there", no aiming or fighting for you. That
would be against the rules and it would look like cheating, and honestly that's not the point. The idea is just to give
you the info a sighted player gets by looking at the screen and the minimap, and you play the game yourself.

Same thing with monsters and loot: they don't make any sound. The only exception is the monsters a dungeon's objective
asks you to kill, because the game marks those for everyone anyway.

## How to start

Grab the zip from [Releases](../../releases), unzip it anywhere, open `D4Assist.exe` and run the game. That's it. It says
"Diablo 4 Assist active" and stays in the system tray, next to the clock. To close it, go to its tray icon and choose
Exit.

It finds the game through Battle.net, Steam or the running game, and else looks for a "Diablo IV" folder on your
drives. If it still can't find it, it asks you to choose the Diablo IV folder (the one with `Diablo IV.exe` in it) and
remembers it. You can change that folder later in the settings, General tab, with the Browse button.

It checks for new versions when it starts. If there is one, it tells you and asks if you want to update; say yes and it
updates itself and restarts, keeping your settings. You can turn that off in the settings, General tab, or check by hand
from the tray icon, with Check for updates.

It talks in the same language your game is in (13 languages). Only English and Spanish were written by me; the rest
are machine translations, so if something sounds weird in your language, let me know.

## What it does

All the directions are the ones from your stick, on the screen: up, down, left, right and the diagonals. If it says an
exit is up-left, push the stick up-left and you're going there. If you'd rather hear north, east and so on, there's a
check box for that in the settings: north is up, so up-left is northwest. And the sounds come from where things are:
something on your right plays on your right, and it gets louder as you get closer.

**Rooms.** When you walk into a junction, a big room or a dead end, it tells you what it is and where the exits are,
like "Junction. Exits: up, right unexplored." The ones you haven't been through yet are marked as unexplored.

**Radar.** Short sounds in four directions (up, down, left, right) that tell you what's ahead if you keep walking that
way. Left and right come from their side; up and down come from the middle, and down sounds a bit lower. It only plays
when something changes.

- A bright tone is an exit. Two quick pips right after it mean the exit leads somewhere you haven't been yet.
- A dull knock is a wall, and two knocks a closed door.
- A soft breath is open floor: it rises for up, falls for down and stays flat for left and right.

One thing: corridors in this game go diagonal a lot, so a corridor right between two directions can sound like a wall
until you turn a bit toward it.

**Guide.** Press left on the D-pad (or Ctrl+Alt+Shift+G on the keyboard) and a ping starts leading you, following the
actual path, to the nearest place you haven't explored. It pings faster as you get closer and plays three notes when you
get there. Press it again to turn it off. When you turn it on, it tells you where it's taking you. It goes first to
where your quest wants you (even if you don't have the quest tracked), then to objectives you already heard and haven't
done, then to unexplored stuff, and when everything's explored, to a portal. It's on by default in the Undercity and the
Pit, and off in normal dungeons.

**Sounds on things.** They repeat from where the thing is, louder as you get closer:

- The quest sound (Ctrl+Alt+Shift+6 plays it) is for what your quest or the dungeon wants from you: levers, things you
  have to carry and where they go, quest chests, stuff your quest asks you to use or break, the person your quest asks you
  to talk to, and the monsters the dungeon asks you to kill.
- Portals, including the Undercity's warp pads, have their own sound so you can find the way out.
- Doors, chests, bodies you can search, books and notes, and spots to look at have their own sounds too. Prisoners play
  the corpse sound.
- Characters you can talk to get a little wooden tap, the two nearest at once (the settings let you pick 1 to 4).
  Companions who follow you in a quest stay quiet.

Everything goes quiet once you've used it, except the spots to look at. In the settings each of these sounds has its own
switch and volume.

**Boss fights.** Safe spots you have to stand in, like Donan's or Vigo's barriers, play their own sound and go quiet once
you're inside. The Undercity braziers have their own sound too. There are also sounds for Mephisto's skill orbs,
Akarat's lights and the Horadric Guardian's vessels, but I haven't tested those yet.

**Barriers.** Some dungeons block a path with a wall until you kill someone. While the wall is there, the radar hears it
as a wall and the guide doesn't send you behind it. Once it opens, the guide counts with that part again.

**Silence.** All the sounds stop during conversations, cutscenes, game menus and when you switch to another window.

**Warnings.** If a game update breaks something, it tells you what stopped working instead of just going silent.

## Where it works

In every kind of dungeon you get everything: normal ones, campaign, expansion, Nightmare, the Undercity and the Pit. In
fixed story areas like Tristram or the hells you get the radar and the sounds, but no room announcements. In the open
world just the nearby sounds, because the game's navigation assist already does a good job there.

## Settings and shortcuts

Ctrl+Alt+Shift+C opens the settings from anywhere (also from the tray icon). You can turn off any sound, change its
volume, and change how fast the repeating ones repeat, by group or one by one. The master volume starts at 50%. The
General tab has the Diablo IV folder and a Browse button to choose another one.

Ctrl+Alt+Shift plus a number from 1 to 8 plays each sound with its name, so you can learn them: exit, open floor, wall,
door, unexplored exit, quest sound, guide ping and arrival.

Left on the D-pad turns the guide on and off (Xbox controllers, and PlayStation's DualSense or DualShock 4). In the
desert sandstorm quest it mutes and unmutes the ping that leads you between shelters. While a game menu is open the
D-pad does nothing, because there it belongs to the game. On the keyboard, Ctrl+Alt+Shift+G does the same as left on the
D-pad, also in menus.

## Known issues

If you open the program in the middle of a dungeon, the first room it announces is called the entrance even if it isn't.
I've tried to give a sound to everything that needs one, but some things might still be missing it. If you find one, let
me know.

## If something's not working

The program saves a log next to it, `d4assist-log.txt` (and the one before as `d4assist-log.old.txt`). Close the
program, open an [issue](../../issues), tell me where you were in the game and attach both files.

Speech goes through [PRISM](https://github.com/ethindp/prism) and the game data definitions come from
[d4data](https://github.com/blizzhackers/d4data). The code is private because it reads the game's memory; the builds are
published here.
