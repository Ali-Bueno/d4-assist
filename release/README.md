# Diablo 4 Assist

Diablo 4 Assist is a companion program for blind and low-vision players of Diablo IV. The game already reads its menus
and text through a screen reader, and its navigation assist helps in the open world. What it doesn't tell you is where
you are inside a dungeon: where the exits are, which way you haven't been yet, where the lever or the prisoner is, where
the portal out is. That is the gap this program fills. It runs next to the game, speaks through NVDA or JAWS, and plays
sounds that come from the direction of things around you.

You still play the game yourself. More on that below, because it matters.

Version 0.1.0 is made for Diablo IV 3.2.2. It has been played start to finish through the act 1 campaign, the Undercity
and the Pit. Later acts and the expansion will have quest objects or boss mechanics that don't make a sound yet; if you
run into one, please report it.

## What it won't do

It will never walk for you. There is no autopilot, no "take me there" button, no aiming, fighting or picking things up.
Your character only moves when you move it. An autopilot would break Blizzard's rules, and it would rightly be seen as
cheating. The point here is the opposite: to give you the information a sighted player gets from looking at the screen
and the minimap, so you can play the same game with your own hands.

For the same reason it doesn't reveal what a sighted player couldn't see. Monsters make no sound, with one exception:
the ones a dungeon's objective tells you to kill, which the game itself marks for everyone. Loot gets no sound either.

## A word about risk

To know where things are, the program reads the game's memory while you play. It only reads: it never writes anything
to the game, never injects code, and never touches the game's files. Even so, Blizzard could consider this against its
terms of service, and that could mean action against your account. Please keep that in mind and use it at your own risk.

## Getting started

You need Windows 10 or 11 (64-bit), Diablo IV installed through Battle.net, and NVDA or JAWS running. An Xbox
controller, DualSense or DualShock 4 is needed only for one button (D-pad left, for the guide). Nothing else to install;
the program carries everything it needs, and uses about 1 GB of memory on top of the game.

Download `D4Assist-0.1.0.zip` from the [Releases](../../releases) page and unzip it wherever you like. Run `D4Assist.exe`
before or after starting the game. It says "Diablo 4 Assist active" and then sits quietly in the notification area next
to the clock. There's no window. To close it, open its icon there (Windows+B, then look for "Diablo 4 Assist", maybe
under "Show hidden icons") and choose Exit.

It speaks the game's text language, whichever of the 13 it is: English, Spanish (Spain and Latin America), French,
German, Italian, Polish, Portuguese (Brazil), Russian, Turkish, Japanese, Korean, and Chinese, Simplified and
Traditional. Your screen reader needs a voice for that language. Everything except English and Spanish was translated by
machine, so corrections from native speakers are very welcome.

## How it works

Everything is described in terms of your controller's stick: up, down, left and right are directions on the screen,
the way you push the stick to go there. When the program says an exit is "up-left", pushing the stick up and to the
left takes you toward it.

The sounds are positional. Something to your right plays in your right ear, and things get louder as you get closer.
After a while you stop thinking about it and just walk toward the sound.

### Room announcements

When you walk into a junction, a big room or a dead end, it tells you what the place is and where its exits are:
"Junction. Exits: up, right unexplored." Exits you haven't been through yet are marked as unexplored, so you always know
which way is new. Corridors stay quiet. The first room you enter is announced as the entrance.

### The radar

The radar answers the question "what's in front of me if I keep going this way?" in four directions: up, down, left and
right. It speaks in short sounds, and only when something changes:

- a bright tone means an exit to another room or to unexplored ground;
- a dull knock is a wall;
- two knocks are a closed door (once you open it, it sounds as an exit);
- a soft breath is open floor, rising when it's above you on the screen and falling when it's below.

Dungeon corridors in Diablo IV often run diagonally, and the radar listens straight along the four directions, so a
corridor exactly between two of them can sound like a wall on both sides until you turn a bit toward it. The radar is
a port of the one in the author's Diablo II Resurrected mod, so if you know that one, you already know this.

### The exploration guide

Press D-pad left and a soft ping starts leading you, along the actual path, to the nearest part of the dungeon you
haven't seen. The closer you get, the faster it pings, and three notes tell you that you've arrived. Then it picks the
next spot. Press D-pad left again to turn it off. The button does nothing in the game itself, and while the inventory,
the map or any other menu is open the program leaves it alone.

When you switch it on, it says where it's taking you, and it has an order of priorities. First the place your quest
sends you, even if you don't have that quest tracked in the journal: if the quest wants you to follow someone or reach a
spot, the guide leads there and waits quietly until the quest moves on. Then objectives you've already heard and haven't
done yet, like a prisoner, a lever or an altar. Then unexplored ground. When there's nothing left to explore, it leads
you to a portal you've heard but haven't used, or back to the one you came through.

It remembers what you explored even when you jump between floors of the same dungeon. It starts switched on in the
Undercity and the Pit, where time matters, and off in ordinary dungeons. In the Undercity it switches itself off in the
boss room, and if the exit pad hasn't shown up after everything is explored, it says so and takes you back through the
dead ends where the pad might be hiding.

### Sounds on things

Portals loop a sound so you can find your way out: the dungeon's exit, the portals between floors and the Undercity's
next-floor pad. You hear a portal from about 60 meters away.

Objectives loop the quest sound: levers, items you have to carry and the pedestals they go on, quest chests, prisoners
(who play a corpse sound instead), the objects your current quest asks you to use, structures a quest asks you to break,
and the monsters the dungeon's objective tells you to kill. Each one falls silent once it's done. Objects the quest
doesn't let you use yet stay quiet until it does.

Doors, chests and things to read loop the same sounds as in the Diablo II mod. A door sounds like wood, metal or stone,
a chest or weapon rack like a chest, a book, note or sign like a scroll, and a body you can search like a corpse. You hear
the two nearest doors and the three nearest of the rest, from about 18 meters, and they stop once you've used them.
Places you can "look at" sound like a scroll and keep sounding, since you can look again.

Characters you can talk to get a soft wooden tap, from about 15 meters: faster as you get closer, left or right
depending on where they stand, and lower in pitch when they're below you on the screen. Vendors and quest companions tap
too; mercenaries and companions who are just following you don't.

### Boss fights

Some fights ask you to stand in a safe spot: Donan's barrier in hell, Lilith's in Skovos, Vigo's dome against Lilith's
Lament, or a light you have to stay inside. Those play their own sound from afar and go quiet once you're in. The
Undercity's spirit braziers, which raise your attunement, also have their own sound until you use them.

There are also sounds, not yet tested in play, for the skill orbs Mephisto steals from you (with a spoken note when he
takes them and when you've got them all back), Akarat's lights in the fight against the Harbinger of Hatred, and the
sigil vessels against the Horadric Guardian.

### Dungeon barriers

Some dungeons close a passage with a wall until you do something, usually kill a particular enemy. While that wall
stands, the radar hears it as a wall and the guide won't send you to what's behind it; it explores the rest and leads you
to the objective instead. When the game removes the wall and you pass nearby, that side opens up again for the guide.

### Quiet when it should be

All sounds pause during conversations, in cutscenes (video and in-game scenes alike), while any game menu is open, and
when you switch away from the game window. Loose remarks from bosses and your own character's lines don't count as
conversations.

### Desert shelters

In the campaign quest where you cross the desert with Meshif and have to shelter from sandstorms, the guide's ping leads
you shelter to shelter and tells you which one you've reached. This one hasn't been tested in play yet.

### Where it works

In every kind of dungeon (ordinary, campaign, expansion and Nightmare dungeons, the Undercity and the Pit) you get
everything. In fixed story areas like the Darkened Way, Tristram or the hells you get the radar and the sounds on
things, but no room announcements. In the open world only the nearby sounds and the desert shelters work; there, the
game's own navigation assist does the job.

### When something stops working

Every game update can change how the game keeps things in memory. The program looks for what it needs each time it
starts, so small updates usually don't matter. If it can't read something, it tells you once, with what no longer
works, for example: "Warning: can't read the rooms, no room announcements or radar. Diablo IV may have been updated.
Please send the log." The rest keeps working. If the program itself crashes, it says so too.

## Settings and shortcuts

Ctrl+Alt+Shift+C opens the settings window from anywhere, even with the game in front; you can also open it from the
icon in the notification area. It's made of standard Windows controls that screen readers handle well. Ctrl+Tab moves
between tabs and Tab through the options. Every sound can be switched off, made louder or quieter, and, if it repeats,
sped up or slowed down, either a whole group at once (radar, objects, quest and portals, guide and characters) or one by
one. With "Preview" checked you hear each sound as you change it. Sound changes apply right away. The General tab holds
the language, radar orientation and which features run; those take effect when the program restarts, and it offers to
restart for you. The master volume starts at 50.

Ctrl+Alt+Shift with a number from 1 to 8 says the name of a sound and plays it, so you can learn them in peace: 1 exit,
2 open floor, 3 wall, 4 door, 5 unexplored exit, 6 quest sound, 7 guide ping, 8 arrival.

The notification-area icon also has "Restart assistant", which keeps what it remembered about the dungeon you're in.

## Known issues

If you start the program in the middle of a dungeon, the first room it announces is called the entrance even if it
isn't. In fixed story areas, a ledge above a lower floor can sound like open floor. And as said above, from act 2 on
you'll meet things that don't make a sound yet.

## Reporting problems

Each time it runs, the program writes a log next to itself, `d4assist-log.txt`, and keeps the previous one as
`d4assist-log.old.txt`. If something doesn't sound, or you hear a warning, close the program from its icon, open an
[issue](../../issues), say where you were in the game, and attach both files. The log also says which version you're
using.

## About the source code

The source code is private, because the program reads the game's memory. Builds are published here.

## Thanks

Speech goes through [PRISM](https://github.com/ethindp/prism). Game data definitions come from
[d4data](https://github.com/blizzhackers/d4data). The radar and many of the sounds come from the author's accessibility
mod for Diablo II Resurrected.
