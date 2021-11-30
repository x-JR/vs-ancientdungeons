using System.Collections.Generic;
using Newtonsoft.Json;

namespace Th3Dungeon
{
  class Th3DungeonConfig
  {
    [JsonProperty]
    public List<Th3DungeonRoomCategory> Categories;

    [JsonProperty]
    public int ChunkRange;

    [JsonProperty]
    public int RoomsToGenerate;

    [JsonProperty]
    public string StartRoom;

    [JsonProperty]
    public string Stairs;

    [JsonProperty]
    public string StartRoomTop;


    [JsonProperty]
    public int StartTopOffsetY;
  }
  class Th3DungeonRoomCategory
  {
    [JsonProperty]
    public string Name;

    [JsonProperty]
    public float Chance;
  }
}