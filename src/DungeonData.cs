using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Th3Dungeon
{
    public class DungeonData
    {
        public SpawnTransform NextSpawn;

        public List<DoorPos> DoorPos;

        public List<Cuboidi> GeneratedRooms;

        public BlockSchematic Schematic;

        public DungeonConfig DungeonConfig;

        public int ChunkX { get; }

        public int ChunkZ { get; }

        public bool Initialized;

        public List<GeneratedStructure> GeneratedStructures;

        public IServerChunk[] Chunks;

        public Dictionary<int, BlockReinforcement>[] Reinforcements;

        public DungeonData(int chunkX, int chunkZ, IServerChunk[] chunks)
        {
            DoorPos = new List<DoorPos>();
            NextSpawn = new SpawnTransform();
            GeneratedRooms = new List<Cuboidi>();
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            GeneratedStructures = chunks[0].MapChunk.MapRegion.GeneratedStructures;
            Chunks = chunks;
        }
    }
}