using System.Collections.Generic;
using Vintagestory.API.Common;

namespace th3dungeon.Data
{
    public class DungeonsConfig
    {
        public List<DungeonConfig> Dungeons;

        public int ChunkRange;

        public int Debug;

        public float Chance;

        public bool ExcludeTh3Dungeons;

        public DungeonsConfig()
        {
            Chance = 0.0008f;
            ChunkRange = 6;
        }
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

        public bool OnlyBelowSurface;

        public Dictionary<AssetLocation, AssetLocation> ReplaceWithRockType { get; set; }
        
        public Dictionary<int, Dictionary<int, int>> ResolvedReplaceWithRockType { get; set; }
        //public bool SuppressRivulets { get; set; }
    }

    public class DungeonRoomCategory
    {
        public string Name;

        public float Chance;
    }
}