Diablo 4 Assist 0.1.0 - first release (October 2026)
====================================================

An accessibility program for playing Diablo IV without seeing the screen:
dungeons, quests, boss fights and characters. It runs next to the game and
never modifies the game or its files. It speaks through your screen reader
(NVDA or JAWS) and adds sounds for orientation.

Tested start to finish in the act 1 campaign, the Undercity and the Pit. In
the other acts and the expansion some quest objects or boss mechanics may
not sound yet: if you find one, send the log (see "If something doesn't
work").


Changes since test build 7
--------------------------
- New name: Diablo 4 Assist (formerly D4 Dungeon Assist), since it no longer
  helps only in dungeons.
- It speaks the game's 13 languages, following Diablo IV's text language.
- Quieter start: the master volume starts at 50% and the scroll sound (books,
  notes and signs) at half.
- Warnings: if the program can't read part of the game (after an update, for
  example), it says so once, with what stopped working, instead of falling
  silent. See "Warnings".
- Fixed: on some game starts only the position worked, with no rooms, radar,
  object sounds or silence in menus.
- The exploration guide first leads to objectives you have already heard and
  not done, and says where it leads when switched on.
- The guide leads to the place your quest sends you, even with no quest
  tracked in the journal, and waits there until the quest moves on.
- Structures a quest asks you to break (the hatred tumors, for example) sound
  until destroyed, and so do quest objects the dungeon itself places (the
  book in the Horadric Vault, for example).
- New sounds: Undercity spirit braziers, protective barriers and boss-fight
  objects (see "What it does").
- Dungeon barriers: when a dungeon closes a passage with a barrier until you
  kill someone (Maulwood's sentinel, for example), the guide no longer leads
  you behind it and the radar hears it as a wall. Once it opens, the guide
  counts on that side again.
- What the dungeon's objective asks you to kill (an elite, a structure)
  plays the quest sound, and the guide leads to it once you have heard it.


IMPORTANT: RISK
---------------
The program reads the game's memory while you play, inside and outside
dungeons (read only: it never writes to the game or injects itself into it,
and reads nothing in menus and loading screens). Even so, Blizzard could
consider it against its rules, and there is a risk of a sanction or a ban of
the account. Use it at your own risk.


Requirements
------------
- Windows 10 or 11, 64-bit. Nothing else to install: .NET is included in
  D4Assist.exe.
- Diablo IV installed with Battle.net. The program finds the game on its own.
- NVDA or JAWS running.
- An Xbox controller, DualSense or DualShock 4, wired or Bluetooth (for
  D-pad left; everything else works without a controller).
- About 1 GB of RAM on top of what the game uses.
- Made for game version 3.2.2. The program finds its data on its own at
  start, so small updates don't affect it. If a big one changes the game
  inside, it tells you what stopped working (see "Warnings") until the
  program is updated.


What's in the folder
--------------------
- D4Assist.exe: the program.
- d4assist.json: the settings. They are changed from the settings window
  (see below); no need to open it.
- README.txt: this file.
- d4assist-log.txt: the log, created when the program starts (see "If
  something doesn't work").
- d4assist-addresses.json: created the first time a character speaks after
  each game update. If you delete it, it is created again.
The sounds and the program's data are inside D4Assist.exe.


How to use it
-------------
1. Unzip the folder wherever you like.
2. Run D4Assist.exe, before or after starting the game. It says "Diablo 4
   Assist active". It opens no window: it stays in the notification area
   (see "Notification area"). If it was already running, a message says so
   and no second copy opens.
3. Play normally. In dungeons (ordinary, campaign, expansion and Nightmare
   dungeons, the Undercity and the Pit) everything works. In fixed story
   areas (the Darkened Way, Tristram, the hells, the mercenary den) the radar
   and objective sounds play, without room announcements. In the open world
   only nearby objectives and the shelter guide sound (see below): the game's
   own navigation assist helps there.
4. To close it, choose "Exit" from its notification-area menu. It says
   "Diablo 4 Assist closed".

It speaks the game's text language, whichever of its own it is: English,
Spanish (Spain or Latin America), French, German, Italian, Polish,
Portuguese (Brazil), Russian, Turkish, Japanese, Korean and Chinese
(Simplified or Traditional). Your screen reader needs a voice for that
language. The settings window can force another one. Languages other than
English and Spanish are machine translations: corrections are welcome.


What it does
------------
- Room announcements: on entering junctions, big rooms and dead ends it says
  the exits as stick directions ("up-left"...). It adds "unexplored" to the
  exits you haven't been through yet.
- Radar: short sounds up, down, left and right. A high tone is an exit, a
  dull knock a wall, two knocks a closed door (once opened it sounds as an
  exit) and a soft breath open floor (rising upward, falling downward).
- Exit beacons: inside dungeons, a looping sound at every portal (the
  dungeon's exit, the portals between floors, the Undercity pad). You hear it
  when the portal is near (about 60 meters; the Undercity pad appears at
  about 20 or 30).
- Objectives: a looping quest sound on levers, items to carry and their
  pedestals, and quest chests, inside and outside dungeons, and on the
  campaign's blood petals. Prisoners play the D2R corpse sound. Levers,
  chests, petals and prisoners fall silent once used or freed. The objects of
  the quest you follow (the one tracked in your journal, or the one in
  progress in that dungeon) also sound once you can use them: the slain demon
  and the gate of the Darkened Way, the book in the Horadric Vault, or the
  hatred tumors to break (silent once destroyed), for example. Those the quest
  doesn't let you use yet don't sound. What the dungeon's objective asks you
  to kill (Maulwood's sentinel or a bone construct, for example) also plays
  the quest sound until it dies; other monsters never sound.
- Dungeon barriers: a wall that closes a passage until you complete the
  objective sounds as a wall on the radar, and the guide doesn't lead you to
  what lies behind it. When the game removes it and you pass nearby, the
  guide counts on that side again.
- Undercity spirit braziers: they play their own sound until you use them, to
  raise the attunement.
- Protective barriers in boss fights: when an ally raises a barrier to
  protect you (Donan in hell, Lilith in Skovos, Vigo against Lilith's Lament)
  or there is a light to stay in, it sounds from afar with its own sound and
  falls silent once you are inside.
- Boss-fight objects (not tested yet), with the quest sound: the orbs of the
  skills Mephisto takes from you (it says when he takes them and when you
  have recovered them all), Akarat's lights against the Harbinger of Hatred
  and the sigil vessels against the Horadric Guardian.
- Characters: the D2R wooden tap on the nearest character within about 15
  meters. It taps faster the closer it is, left or right depending on where
  it stands, and lower when it is down on the screen. It plays on characters
  you can talk to, vendors and quest companions; not on mercenaries. Quest
  companions following you don't sound.
- Doors, chests and readables: the D2R looping sounds. Doors sound like wood,
  metal (bars, gates, cages) or stone; you hear the 2 nearest. Chests, weapon
  racks and loose stones sound like a chest; books, notes and signs to read
  sound like a scroll; bodies to search or examine sound like a corpse, as do
  prisoners. You hear the 3 nearest, from about 18 meters. They fall silent
  once opened or used, bodies too. Those the game doesn't let you use yet (a
  door the quest opens, for example) don't sound until they can be used. Spots
  you can "look at" sound like a scroll and keep sounding after you look,
  because you can look again.
- Cutscenes: when a video cutscene starts all sounds fall silent. They come
  back on a change of area or as soon as you start walking. Scenes made with
  the game engine silence them too while they last.
- Silence in conversations: with a character's options menu open, while the
  conversation you picked plays, and in story or quest conversations even
  without a menu, nothing sounds. Nothing sounds either when a character tells
  you something without a menu, like a villager. Loose remarks from bosses and
  your own character's lines ("not enough health", for example) silence
  nothing.
- Silence in menus: with any game menu open nothing sounds: inventory,
  skills, map, pause, a sign or board, Horadric shipments, a vendor, an
  artisan, the stash, the Paragon board, glyphs, the wardrobe, the stable,
  the shop and the rest. The chat and the world tier selection silence
  nothing.
- Silence outside the game: if you switch to another window the sounds stop;
  they come back when you return to the game. The sound examples still play.
- Exploration guide: a ping that leads you to the nearest unexplored area,
  along the path. It pings faster the closer you are and plays three notes on
  arrival. When a new portal appears (one you didn't come through) it lets
  its beacon play. With nothing left to explore it leads to a new portal you
  have heard ("All explored. Leading to a portal."), or, if there is none, to
  the portal you came through. If there is no portal it says "All explored."
    - It remembers what you explored even when you jump between areas of the
      same dungeon (between the floors of the Worldstone, for example).
    - It starts on in the Undercity and the Pit, and off in other dungeons.
    - D-pad LEFT turns it on and off (it says "Guide on." or "Guide off.").
      That button does nothing in the game. With the inventory, the map or
      another menu open it is left alone: there the D-pad belongs to the menu.
    - In the Undercity boss room it turns itself off.
    - In the Undercity, when nothing is left to explore and the exit hasn't
      appeared, it says "Exit not found. Rechecking dead ends." and leads you
      through the dead ends where the pad may be.
    - For when you get lost: in ordinary dungeons, if you have already heard
      an objective that is still pending (the quest's, a prisoner, an altar, a
      lever...), the guide leads to it first and then goes on exploring. The
      quest's first, then the dungeon's. When switched on it says where it
      leads: "To the quest objective.", "To a dungeon objective.", "To
      unexplored ground.", the portal or "All explored." In the Undercity and
      the Pit it doesn't lead to objectives: it explores and looks for the
      exit.
    - If the quest sends you to a place (following a character somewhere,
      for example), the guide leads you there, plays the arrival and falls
      silent until the quest moves on.
- Shelter guide (not tested yet): in the campaign quest where you cross the
  desert with Meshif and must shelter from the sandstorms. When that part
  starts it says "Shelter guide." and the guide's ping leads you in a
  straight line to the next shelter. On arrival it plays three notes and says
  "Shelter 1 of 3." While you stay inside the shelter the ping is silent
  (wait for the storm to pass); when you leave, it leads to the next one.
  D-pad LEFT silences it or turns it back on.


Warnings
--------
The program checks that it can read every part of the game it uses. If one
doesn't work (usually because of a game update), it says so once, starting
with "Warning:" and saying what stopped working. For example: "Warning:
can't read the rooms, no room announcements or radar. Diablo IV may have been
updated. Please send the log."
Everything else keeps working. If the program closes because of an error, it
says "The assistant stopped because of an error. Please send the log."
In both cases, send the log (see "If something doesn't work").


Keyboard shortcuts: sound examples
----------------------------------
Control+Alt+Shift plus a number says a sound's name and plays it, to learn
them without the noise of a fight. They always work, in or out of the game:
  1 Exit, 2 Open floor, 3 Wall, 4 Door, 5 Unexplored exit
  (1 to 5 play up, right, down and left),
  6 Quest sound, 7 Guide ping, 8 Arrival.


Notification area
-----------------
The program puts an icon in the notification area, next to the clock. With
the keyboard: Windows+B goes to the notification area; if the icon isn't in
view, it is inside "Show hidden icons" (Enter to open it). The icon is called
"Diablo 4 Assist". Enter or the context-menu key on it opens its menu:
- Settings...: opens the settings window.
- Restart assistant: closes it and opens it again (it says "Restarting the
  assistant"). What it remembered of the dungeon is kept.
- Exit: closes it.
Control+Alt+Shift+C opens the settings directly, from anywhere, with the
game in front too.


Settings
--------
The window has tabs (Control+Tab moves between them) and on each tab Tab goes
through the options. Sound changes are heard in the game at once. "OK" saves
and closes, "Apply" saves without closing, "Cancel" (or Escape) undoes what
you didn't save, and "Restore defaults" puts everything back as it came.

- Sound: the master volume and the "Preview" box: when checked, moving a
  volume or a speed plays that sound with the new value, to tune it to your
  taste.
- Radar, Objects, Quest and portals, Guide and characters: each tab first has
  the whole group (a box to turn it on or off, an overall volume and, if they
  repeat, an overall speed) and then each sound on its own (box, volume and
  speed). A sound's volume combines with its group's: with the group at 50 and
  the sound at 50 it is quieter than with only one of them at 50.
    - Radar: exit, open floor, wall and closed door, plus the volume of each
      direction (front, right, back, left), which affects every radar sound
      on that side.
    - Objects: doors, chests, corpses, notes and books, and spots to look at.
    - Quest and portals: portals, quest objectives and prisoners.
    - Guide and characters: the guide ping (the shelter guide's too), the
      arrival sound and the tap on characters you can talk to.
- Volumes go from 0 to 100. Speed is a percentage: 100 is normal, 200 twice
  as fast, 50 half as fast (25 to 300). Only repeating sounds have it; it
  changes how often they repeat, not their pitch.
- Slider keys: up and down arrows by 1, right and left by 5, Page Up and Page
  Down by 10, Home to the maximum and End to the minimum.
- General: language ("Automatic" follows the game), radar orientation,
  speech, room announcements, exploration guide and whether it starts on in
  ordinary dungeons, shelter guide, working outside dungeons, closed doors in
  the radar, dungeon barriers, and when the sounds fall silent (cutscenes,
  game in the background, conversations, menus). These options take effect
  when the assistant restarts: on saving, it asks whether to restart it now.

Everything is saved in d4assist.json. If the program doesn't find the game,
open that file with Notepad and add a line like this one, with your folder,
right after the first brace:
  "InstallPath": "C:\\Program Files (x86)\\Diablo IV",


Known issues
------------
- If you start the program in the middle of a dungeon, the first room it
  announces is called the entrance even when it isn't.
- The radar listens in four directions (up, down, left and right). The game's
  corridors often run diagonally: a corridor exactly between two directions
  sounds as a wall on both until you line up with it.
- In fixed story areas, a ledge above a lower floor can sound as open floor.
- From act 2 on and in the expansion, some quest objects or boss mechanics
  may not sound yet.


If something doesn't work
-------------------------
Every time it starts, the program writes what happens to d4assist-log.txt, in
this same folder (the previous run's is kept as d4assist-log.old.txt). If
nothing sounds or something fails, close the program (Exit, in the
notification area) and send both files: open an issue at
https://github.com/Ali-Bueno/d4-assist/issues saying where you were in the
game and attach them, or send them to whoever gave you the program. No
special option is needed to start it. The log says which version you use.
