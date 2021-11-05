using System.Collections.Generic;

namespace Th3Dungeon
{
  public class Th3DungeonData
  {
    public Th3SpawnTransform nextSpawn;

    public List<Th3DoorPos> DoorPos;

    public Th3DungeonData()
    {
      DoorPos = new List<Th3DoorPos>();
      nextSpawn = new Th3SpawnTransform();
    }
  }
}