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
  }
  class Th3DungeonRoomCategory
  {
    [JsonProperty]
    public string Name;

    [JsonProperty]
    public float Chance;
  }
}