using System.Collections.Generic;

namespace Th3Dungeon
{
    public class Th3DungeonConfig
    {
        public List<DungeonConfig> Dungeons;
    }

    public class DungeonConfig
    {
        public string Name;

        public List<Th3DungeonRoomCategory> Categories;

        public int ChunkRange;

        public int RoomsToGenerate;

        public string StartRoomPath;

        public string StairsPath;

        public string EndRoomPath;

        public string StartRoomTopPath;

        public int StartTopOffsetY;

        public bool GenerateEntrance;

        public Dictionary<string, List<Th3DungeonRoom>> Rooms;

        public Th3DungeonRoom StartRoom;

        public Th3DungeonRoom StartRoomTop;

        public Th3DungeonRoom Stairs;

        public Th3DungeonRoom EndRoom;
    }

    public class Th3DungeonRoomCategory
    {
        public string Name;

        public float Chance;
    }
}