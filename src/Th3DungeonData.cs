using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace Th3Dungeon
{
  public class Th3DungeonData
  {
    public Th3SpawnTransform NextSpawn;

    public List<Th3DoorPos> DoorPos;

    public List<Cuboidi> GeneratedRooms;

    public Th3BlockSchematic Schematic;

    public bool Initialized;

    public Th3DungeonData()
    {
      DoorPos = new List<Th3DoorPos>();
      NextSpawn = new Th3SpawnTransform();
      GeneratedRooms = new List<Cuboidi>();
    }
  }
}