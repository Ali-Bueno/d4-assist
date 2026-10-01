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

It talks in the same language your game is in (13 languages). Only English and Spanish were written by me; the rest
are machine translations, so if something sounds weird in your language, let me know.

## What it does

All the directions are the ones from your stick, on the screen: up, down, left, right and the diagonals. If it says an
exit is up-left, push the stick up-left and you're going there. And the sounds come from where things are: something on
your right plays on your right, and it gets louder as you get closer.

**Rooms.** When you walk into a junction, a big room or a dead end, it tells you what it is and where the exits are,
like "Junction. Exits: up, right unexplored." The ones you haven't been through yet are marked as unexplored.

**Radar.** Short sounds in four directions (up, down, left, right) that tell you what's ahead if you keep walking that
way. A bright tone is an exit, a dull knock is a wall, two knocks a closed door, and a soft breath is open floor. It only
plays when something changes. One thing: corridors in this game go diagonal a lot, so a corridor right between two directions can
sound like a wall until you turn a bit toward it.

**Guide.** Press left on the D-pad and a ping starts leading you, following the actual path, to the nearest place you
haven't explored. It pings faster as you get closer and plays three notes when you get there. Press it again to turn it
off. When you turn it on, it tells you where it's taking you. It goes first to where your quest wants you (even if you
don't have the quest tracked), then to objectives you already heard and haven't done, then to unexplored stuff, and when
everything's explored, to a portal. It's on by default in the Undercity and the Pit, and off in normal dungeons.

**Sounds on things.** Portals loop a sound so you can find the way out. Objectives play the quest sound: levers, things
you have to carry and where they go, quest chests, stuff your quest asks you to use or break, and what the dungeon asks
you to kill. Prisoners play a corpse sound. Doors, chests, books and notes, and bodies you can search have their own
sounds too. Characters you can talk to get a little wooden tap. Everything goes quiet once you've used it.

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
volume, and change how fast the repeating ones repeat, by group or one by one. The master volume starts at 50%.

Ctrl+Alt+Shift plus a number from 1 to 8 plays each sound with its name, so you can learn them: exit, open floor, wall,
door, unexplored exit, quest sound, guide ping and arrival.

Left on the D-pad turns the guide on and off.

## Known issues

If you open the program in the middle of a dungeon, the first room it announces is called the entrance even if it isn't.
And from act 2 on there will be things that don't make a sound yet. I tested everything through act 1, the Undercity and
the Pit.

## If something's not working

The program saves a log next to it, `d4assist-log.txt` (and the one before as `d4assist-log.old.txt`). Close the
program, open an [issue](../../issues), tell me where you were in the game and attach both files.

Speech goes through [PRISM](https://github.com/ethindp/prism) and the game data definitions come from
[d4data](https://github.com/blizzhackers/d4data). The code is private because it reads the game's memory; the builds are
published here.
