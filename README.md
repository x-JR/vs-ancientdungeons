# Th3Dungeon

This is a Vintage Story dungeon generator

## Features

- creates a dugeon based on the rooms provided and on the config settings during world generation
- handbuild your rooms that the dongeon generator will use to generate the dungeons
- the generated dungeons are deterministic (means if you share your world seed it will generate the same world with the same dungeons) this is requiered to even generate the dungeons across multiple chunks

## Using the th3dungeon mod / making custom rooms

for some basic inspiration on what is needed to make a room take a look at this sample world

https://drive.google.com/file/d/1ibl9OXsPLLmGfWUgXnTWsWlDNPe2i72v/view?usp=sharing

### Making a room:

- install the mod and load into a creative flat map preferably (just for loading speed)

there you can create rooms with these rules:

- every room needs to have atleast one doorway block, so it can be linked to the dungeon (other rooms) you can find the doorwayblock in the creative inventory search for doorway or `th3doorway` (press ctrl + f4 to see them in the world)

  make sure to poin the arrow on the doorway block outwards of the room you build
  the generator will then match rooms based on those blocks facing eachother

- make the start rooms bigger and make it have multiple doorways so the dungeon can branch out from there

Tips:
if you wanna replace everything (you are very welcome to)
make sure you have atleast a few categories like i have and for each category have a few rooms so there is some variation, also pay attention to the chance values since those are the most important config values

they will sort of control how the dungeon looks in terms of the layout

for example a category with long straight rooms and a high chance will produce dungeons with long corridors

```json
{
  // used to for the generation it will determine how big the dungeon can be from the center so 6 will allow a dungeon to be the size of 6 chunks to for each direction so 6 in north, east , south and west so total a 12 by 12 chunks
  // be carefull a too high value my slow worldgen drastically
  "ChunkRange": 6,
  // if set to true will only spawn one dungoen at x=0 z=0
  "Debug": true,
  // if debug is false this is the chance that a dungeon can spanw in a chunk
  "Chance": 0.0002,
  "Dungeons": [
    {
      // base path for this dungeon config, this folder should contain the category folders
      "BasePath": "th3dungeon:worldgen/dungeon/noentrance/categories/",
      // chance for this configuration to be choosen out of all , needs to add up to 1
      "Chance": 0.1,
      "SealevelOffset": -50,
      // the generator will try to generate that amount of rooms (may not be able to since collision with other dungeon parts)
      "RoomsToGenerate": 50,
      // if a starttop room and starirs should be generated
      "GenerateEntrance": true,
      // to overlap the startroomtop with the stairs
        "StartTopOffsetY": -1,
      // path to the start room (room under ground sealevel + SealevelOffset)
      "StartRoomPath": "th3dungeon:worldgen/dungeon/default/start/",
      // room on top of surface connects to start room (underground) witht the variable stairs room
      "StartRoomTopPath": "th3dungeon:worldgen/dungeon/default/starttop/",
      // path to the stars room (the stairs room is a partial room, this means ith will be stacked ontop of it and rotated 90 degrees each step until it reaches the top)
      "StairsPath": "th3dungeon:worldgen/dungeon/stairs.json",
      // if stairs should be rotated usefull for stairs using ladders
      "StairsRotation": true,
      // path to folder where the endrooms are, they are used to close off open ends
      "EndRoomPath": "th3dungeon:worldgen/dungeon/default/end/",
      // those are the main part of the dungeon define categories here that correspond to folder within the mod to load the rooms from
      "categories": [
        {
          // BasePath/category name
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
