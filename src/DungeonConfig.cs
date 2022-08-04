using System.Collections.Generic;

namespace Th3Dungeon
{
    public class DungeonsConfig
    {
        public List<DungeonConfig> Dungeons;
    }

    public class DungeonConfig
    {
        public string Name;

        public float Chance;

        public List<DungeonRoomCategory> Categories;

        public int ChunkRange;

        public int RoomsToGenerate;

        public string StartRoomPath;

        public string StairsPath;

        public string EndRoomPath;

        public string StartRoomTopPath;

        public int StartTopOffsetY;

        public bool GenerateEntrance;

        public Dictionary<string, List<DungeonRoom>> Rooms;

        public DungeonRoom StartRoom;

        public DungeonRoom StartRoomTop;

        public DungeonRoom Stairs;

        public DungeonRoom EndRoom;
    }

    public class DungeonRoomCategory
    {
        public string Name;

        public float Chance;
    }
}