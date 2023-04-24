using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace th3dungeon.Data
{
    public class DungeonData
    {
        public SpawnTransform NextSpawn;

        public List<DoorPos> DoorPos;

        public List<Cuboidi> GeneratedRooms;

        public BlockSchematic Schematic;

        public DungeonConfig DungeonConfig;

        /// <summary>
        /// chunk x coordinate
        /// </summary>
        public int ChunkX { get; }

        /// <summary>
        /// chunk z coordinate
        /// </summary>
        public int ChunkZ { get; }

        /// <summary>
        /// chunk x offset
        /// </summary>
        public int Dx { get; set; }

        /// <summary>
        /// chunk z offset
        /// </summary>
        public int Dz { get; set; }
        
        /// <summary>
        /// chunk x coordinate + offset
        /// </summary>
        public int ChunkXd;
        
        /// <summary>
        /// chunk z coordinate + offset
        /// </summary>
        public int ChunkZd;

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