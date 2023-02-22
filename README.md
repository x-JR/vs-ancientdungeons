# Th3Dungeon

This is a Vintage Story dungeon generator mod

## Features

- creates a dungeon based on the rooms provided and on the config settings during world generation
- handbuild your rooms that the dungeon generator will use to generate the dungeons
- the generated dungeons are deterministic (means if you share your world seed it will generate the same world with the same dungeons) this is requiered to even generate the dungeons across multiple chunks
- procedural dungeon generation, every dungeon will be unique

## Using the th3dungeon mod / making custom rooms

for some basic inspiration on what is needed to make a room take a look at this sample world

https://drive.google.com/file/d/1ibl9OXsPLLmGfWUgXnTWsWlDNPe2i72v/view?usp=sharing

### Configuration
If you use multiple mods that provide more dungeons every mods dungeons share the same chance to spawn. This means that if you have th3dungon and one additional mod that adds dungeons, 50% of the time a th3dungon will spawn and the other 50% the other mods dungeons. Then within each mods dungeons the corresponding chance values will be used to spawn a mods dungeon type.


If you dont want to have the default dungeons you can add a `th3dungeonconfig.json` to your ModConfig folder to overwrite the spawning of the th3dungeon dungeons. So only other mods dungeons will spawn. In there you can also override `ChunkRange,Debug,Chance` but you dont have to.

The default values for the config are:
-  ChunkRange=6
-  Debug=false
-  Chance=0.0002

```json
{
  "ExcludeTh3Dungeons": true
}
```

If you further wanna customize what dungeons to spawn you can pick and choose from each mods dungeons by using the `ModConfig/th3dungeonconfig.json`, this will override what dungeons to use.
Once the "Dungeons": [] is defined in the `ModConfig/th3dungeonconfig.json` it will only use those for generation and further you have to also define `ChunkRange,Debug,Chance`.

```json
{
  "ChunkRange": 6,
  "Debug": false,
  "Chance": 0.001,
  "Dungeons": [
    {
      "BasePath": "th3dungeon:worldgen/dungeon/default/categories/",
      "Chance": 0.5,
      "ReinforcementLevel": 0,
      "SealevelOffset": -16,
      "RoomsToGenerate": 50,
      "StartTopOffsetY": -1,
      "StartRoomPath": "th3dungeon:worldgen/dungeon/default/start/",
      "StartRoomTopPath": "th3dungeon:worldgen/dungeon/default/starttop/",
      "StairsPath": "th3dungeon:worldgen/dungeon/default/stairs2.json",
      "StairsRotation": true,
      "EndRoomPath": "th3dungeon:worldgen/dungeon/default/end/",
      "GenerateEntrance": true,
      "categories": [
        {
          "name": "straight",
          "chance": 0.2
        },
        {
          "name": "turn",
          "chance": 0.2
        },
        {
          "name": "updown",
          "chance": 0.2
        },
        {
          "name": "rooms",
          "chance": 0.4
        }
      ]
    },
    {
      "BasePath": "yourmod:worldgen/dungeon/yourDungeonType1/categories/",
      "Chance": 0.5,
      "ReinforcementLevel": 0,
      "SealevelOffset": -16,
      "RoomsToGenerate": 50,
      "StartTopOffsetY": -1,
      "StartRoomPath": "yourmod:worldgen/dungeon/yourDungeonType1/start/",
      "StartRoomTopPath": "yourmod:worldgen/dungeon/yourDungeonType1/starttop/",
      "StairsPath": "yourmod:worldgen/dungeon/yourDungeonType1/stairs2.json",
      "StairsRotation": true,
      "EndRoomPath": "yourmod:worldgen/dungeon/yourDungeonType1/end/",
      "GenerateEntrance": true,
      "categories": [
        {
          "name": "straight",
          "chance": 0.2
        },
        {
          "name": "turn",
          "chance": 0.2
        },
        {
          "name": "updown",
          "chance": 0.2
        },
        {
          "name": "rooms",
          "chance": 0.4
        }
      ]
    }
  ]
}
```


### Making a room:

- install the mod and load into a creative flat map preferably (just for loading speed)

there you can create rooms with these rules:

- every room needs to have atleast one doorway block, so it can be linked to the dungeon (other rooms) you can find the doorwayblock in the creative inventory search for doorway or `th3doorway` (press ctrl + f4 to see them in the world)

  make sure to point the arrow on the doorway block outwards of the room you build
  the generator will then match rooms based on those blocks facing each other

- make the start rooms bigger and make it have multiple doorways so the dungeon can branch out from there
- the starttop room should not be greater than one chunk in size since there are issues with it for now\
  it will also be placed in the center of one chunk to so a full chunk can be used for the starttop room

Tips:
if you wanna replace everything (you are very welcome to)
make sure you have at least a few categories like i have and for each category have a few rooms so there is some variation, also pay attention to the chance values since those are the most important config values

they will sort of control how the dungeon looks in terms of the layout

for example a category with long straight rooms and a high chance will produce dungeons with long corridors

```json
{
  // used to for the generation it will determine how big the dungeon can be from the center so 6 will allow a dungeon to be the size of 6 chunks to for each direction so 6 in north, east , south and west so total a 12 by 12 chunks
  // be carefull a too high value my slow worldgen drastically
  "ChunkRange": 6,
  // if set to true will only spawn one dungoen at x=0 z=0
  "Debug": true,
  // if debug is false this is the chance that a dungeon can spawn in a chunk
  "Chance": 0.001,
  "Dungeons": [
    {
      // base path for this dungeon config, this folder should contain the category folders
      "BasePath": "th3dungeon:worldgen/dungeon/noentrance/categories/",
      // chance for this configuration to be choosen out of all , needs to add up to 1
      "Chance": 1,
      // this is where the dungeon will start underground and most of its rooms will be around that height
      // the value SealevelOffset is added to the sealevel, so a negative value will make sure the dungeon spawns underground
      "SealevelOffset": -50,
      // the generator will try to generate that amount of rooms (may not be able to since collision with other dungeon parts)
      "RoomsToGenerate": 50,
      // if a starttop room and starirs should be generated
      "GenerateEntrance": true,
      // to overlap the startroomtop with the stairs
        "StartTopOffsetY": -1,
      // path to the start room (room under ground sealevel + SealevelOffset)
      "StartRoomPath": "th3dungeon:worldgen/dungeon/default/start/",
      // room on the surface, connects to start room (underground) witht the variable height stairs room
      "StartRoomTopPath": "th3dungeon:worldgen/dungeon/default/starttop/",
      // path to the stars room (the stairs room is a partial room, this means it will be stacked ontop of it and rotated 90 degrees each step until it reaches the top)
      "StairsPath": "th3dungeon:worldgen/dungeon/stairs.json",
      // if stairs should be rotated (false: usefull for stairs using ladders)
      "StairsRotation": true,
      // path to folder where the endrooms are, they are used to close off open ends
      "EndRoomPath": "th3dungeon:worldgen/dungeon/default/end/",
      // those are the main part of the dungeon, the categories here correspond to folder within the mod to load the rooms from
      "categories": [
        {
          // BasePath/category_name
          "name": "straight",
          // chance for the room to spawn (all values need to add up to 1)
          "chance": 0.4
        },
        {
          "name": "turn",
          "chance": 0.2
        },
        {
          "name": "updown",
          "chance": 0.2
        },
        {
          "name": "rooms",
          "chance": 0.2
        }
      ]
    }
  ]
}
```

### Export a schematic and Building

Using [worldedit](https://wiki.vintagestory.at/index.php?title=How_to_use_WorldEdit)

mark the area with the wand item ingame and type

use `/we mex roomname`

this will create a roomname.json in the Worldedit folder copy that file into the category you want it to be in or make it the start or stairs room (see config file)

ONE VERY IMPORTANT thing after exporting is to prefix every block from the game with the `game:` prefix see the files included in the mod

use the worldedit fill tool to fill your rooms with the `game:meta-filler` block, that one will be replaced with air when the dungeon is generated also the pathway block will be replaced with air

to spawn the meta-filler block type `/giveblock meta-filler`

to be able to see the meta blocks you placed and to remove them you need to press ctrl + f4

if you have any questions feel free to DM me (Th3Dilli on Vintagestory server)


### Making a mod for this mod

This mod contains the dungeon generator, the needed doorway-block to connect rooms and a few dungeons so it can be used without any other mods.

If you want to add your own dungeons you can do so by:
Making your own mod that adds more rooms and place everything in the following folder
`yourmod/worldgen/dungeon/`
from here you can add a folder structure like the following:

```
assets/yourmod/worldgen/dungeon/
├─ yourDungeonType1/
│  ├─ categories/
│  │  ├─ rooms/
│  │  │  ├─ roomNr1.json
│  │  ├─ straight/
│  │  │  ├─ roomNr1.json
│  │  ├─ updown/
│  │  │  ├─ roomNr1.json
│  │  ├─ turn/
│  ├─ start/
│  │  ├─ roomNr1.json
│  ├─ starttop/
│  │  ├─ roomNr1.json
│  ├─ end/
│  │  ├─ roomNr1.json
│  ├─ stairs.json
├─ th3dungeonconfig.json
```

in every folder you can put as many rooms/schematics as you want. And with the following config you should be good to go. You can name the folders what ever you want. Here is a `th3dungeonconfig.json` for the above folder structure. It is important that you have the `th3dungeonconfig.json` at exactly this location in your mod `assets/yourmod/worldgen/dungeon/th3dungeonconfig.json`, since the th3dungoen mod will search all mods for `.../worldgen/dungeon/th3dungeonconfig.json` files to merge them into one big config.



```json
{
  "ChunkRange": 6,
  "Debug": true,
  "Chance": 0.001,
  "Dungeons": [
    {
      "BasePath": "yourmod:worldgen/dungeon/yourDungeonType1/categories/",
      "Chance": 1,
      "ReinforcementLevel": 0,
      "SealevelOffset": -16,
      "RoomsToGenerate": 50,
      "StartTopOffsetY": -1,
      "StartRoomPath": "yourmod:worldgen/dungeon/yourDungeonType1/start/",
      "StartRoomTopPath": "yourmod:worldgen/dungeon/yourDungeonType1/starttop/",
      "StairsPath": "yourmod:worldgen/dungeon/yourDungeonType1/stairs2.json",
      "StairsRotation": true,
      "EndRoomPath": "yourmod:worldgen/dungeon/yourDungeonType1/end/",
      "GenerateEntrance": true,
      "categories": [
        {
          "name": "straight",
          "chance": 0.2
        },
        {
          "name": "turn",
          "chance": 0.2
        },
        {
          "name": "updown",
          "chance": 0.2
        },
        {
          "name": "rooms",
          "chance": 0.4
        }
      ]
    }
  ]
}
```