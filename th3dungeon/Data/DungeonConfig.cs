using System.Collections.Generic;

namespace th3dungeon.Data
{
    public class DungeonsConfig
    {
        public List<DungeonConfig> Dungeons;

        public int ChunkRange;

        public bool Debug;

        public float Chance;
    }

    public class DungeonConfig
    {
        public string BasePath;

        public float Chance;

        public int ReinforcementLevel;

        public List<DungeonRoomCategory> Categories;

        public int RoomsToGenerate;

        public string StartRoomPath;

        public string StairsPath;

        public string EndRoomPath;

        public string StartRoomTopPath;

        public int StartTopOffsetY;

        public int SealevelOffset;

        public bool GenerateEntrance;

        public bool StairsRotation;

        public Dictionary<string, List<DungeonRoom>> Rooms;

        public List<DungeonRoom> StartRooms;

        public List<DungeonRoom> StartRoomsTop;

        public DungeonRoom Stairs;

        public List<DungeonRoom> EndRooms;
    }

    public class DungeonRoomCategory
    {
        public string Name;

        public float Chance;
    }
}