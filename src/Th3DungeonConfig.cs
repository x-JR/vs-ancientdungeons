using System.Collections.Generic;
using Newtonsoft.Json;

namespace Th3Dungeon
{
    class Th3DungeonConfig
    {
        public List<Th3DungeonRoomCategory> Categories;

        public int ChunkRange;

        public int RoomsToGenerate;

        public string StartRoom;

        public string Stairs;

        public string EndRoom;

        public string StartRoomTop;


        public int StartTopOffsetY;
    }
    class Th3DungeonRoomCategory
    {
        public string Name;

        public float Chance;
    }
}