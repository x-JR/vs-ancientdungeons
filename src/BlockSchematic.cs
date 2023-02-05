using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Th3Dungeon
{
    public class BlockSchematic : Vintagestory.API.Common.BlockSchematic
    {
        private int _chunkSize;
        private int _worldHeight;

        private Block DoorNorth, DoorEast, DoorSouth, DoorWest;

        public List<DoorPos> Doors;

        public new void Init(IBlockAccessor blockAccessor)
        {
            base.Init(blockAccessor);
            _chunkSize = blockAccessor.ChunkSize;
            _worldHeight = blockAccessor.MapSizeY;

            DoorNorth = blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-north"));
            DoorEast = blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-east"));
            DoorSouth = blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-south"));
            DoorWest = blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-west"));

            Doors = new List<DoorPos>();
        }

        public void LoadMeta(IBlockAccessor blockAccessor, IWorldAccessor worldForResolve, string fileNameForLogging)
        {
            LoadMetaInformationAndValidate(blockAccessor, worldForResolve, fileNameForLogging);

            for (int i = 0; i < BlockIds.Count; i++)
            {
                int storedBlockid = BlockIds[i];

                AssetLocation blockCode = BlockCodes[storedBlockid];
                Block newBlock = blockAccessor.GetBlock(blockCode);

                if (IsDoor(newBlock))
                {
                    uint index = Indices[i];
                    int dx = (int)(index & 0x1ff);
                    int dy = (int)((index >> 20) & 0x1ff);
                    int dz = (int)((index >> 10) & 0x1ff);

                    BlockFacing facing = null;

                    if (blockCode.Path == "th3doorway-north")
                    {
                        facing = BlockFacing.NORTH;
                    }
                    else if (blockCode.Path == "th3doorway-east")
                    {
                        facing = BlockFacing.EAST;
                    }
                    else if (blockCode.Path == "th3doorway-south")
                    {
                        facing = BlockFacing.SOUTH;
                    }
                    else if (blockCode.Path == "th3doorway-west")
                    {
                        facing = BlockFacing.WEST;
                    }
                    if (facing != null)
                    {
                        //   Doors.Add(new DoorPos(new BlockPos(dx + facing.Normali.X, dy + facing.Normali.Y - 1, dz + facing.Normali.Z), facing));
                        Doors.Add(new DoorPos(new BlockPos(dx, dy, dz), facing));
                    }
                }
            }
        }

        private bool IsDoor(Block newBlock)
        {
            return newBlock == DoorNorth || newBlock == DoorEast || newBlock == DoorSouth || newBlock == DoorWest;
        }

        /// <summary>
        /// Gets the height index at the position dx and dy in the schematic
        /// </summary>
        public int GetHeightAtPos(int dx, int dz)
        {
            int height = 0;
            for (int dy = 0; dy < SizeY; dy++)
            {
                uint index = (uint)(dy << 20 | dz << 10 | dx);
                if (Indices.Find(i => i == index) != 0)
                {
                    height = dy;
                }
            }
            return height;
        }

        public int Place(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, DungeonData data, bool replaceMetaBlocks = true)
        {
            BlockPos curPos = new BlockPos();
            int placed = 0;

            PlaceBlockDelegate handler = null;
            switch (ReplaceMode)
            {
                case EnumReplaceMode.ReplaceAll:
                    handler = PlaceReplaceAll;
                    for (int i = 0; i < SizeX; i++)
                    {
                        for (int j = 0; j < SizeY; j++)
                        {
                            for (int k = 0; k < SizeZ; k++)
                            {
                                curPos.Set(i + data.NextSpawn.Position.X, j + data.NextSpawn.Position.Y, k + data.NextSpawn.Position.Z);
                                blockAccessor.SetBlock(0, curPos);
                            }
                        }
                    }
                    break;

                case EnumReplaceMode.Replaceable:
                    handler = PlaceReplaceable;
                    break;

                case EnumReplaceMode.ReplaceAllNoAir:
                    handler = PlaceReplaceAllNoAir;
                    break;

                case EnumReplaceMode.ReplaceOnlyAir:
                    handler = PlaceReplaceOnlyAir;
                    break;
            }


            for (int i = 0; i < Indices.Count; i++)
            {
                uint index = Indices[i];

                int dx = (int)(index & 0x1ff);
                int dy = (int)((index >> 20) & 0x1ff);
                int dz = (int)((index >> 10) & 0x1ff);
                curPos.Set(dx + data.NextSpawn.Position.X, dy + data.NextSpawn.Position.Y, dz + data.NextSpawn.Position.Z);

                if (curPos.X / _chunkSize == data.ChunkX && curPos.Z / _chunkSize == data.ChunkZ)
                {
                    if (data.DungeonConfig.ReinforcementLevel > 0)
                    {
                        ReinforceBlock(data, curPos);
                    }
                    int storedBlockid = BlockIds[i];
                    AssetLocation blockCode = BlockCodes[storedBlockid];

                    Block newBlock = blockAccessor.GetBlock(blockCode);

                    if (newBlock == null || (replaceMetaBlocks && newBlock == undergroundBlock)) continue;


                    Block oldBlock = blockAccessor.GetBlock(curPos);
                    placed += handler(blockAccessor, curPos, newBlock, replaceMetaBlocks);

                    if (newBlock.LightHsv[2] > 0 && blockAccessor is IWorldGenBlockAccessor accessor)
                    {
                        accessor.ScheduleBlockLightUpdate(curPos.Copy(), oldBlock.BlockId, newBlock.BlockId);
                        accessor.ExchangeBlock(newBlock.Id, curPos);
                    }
                }
            }

            if (!(blockAccessor is IBlockAccessorRevertable))
            {
                PlaceEntitiesAndBlockEntities(blockAccessor, worldForCollectibleResolve, data.NextSpawn.Position, data.ChunkX, data.ChunkZ);
            }
            return placed;
        }

        private void ReinforceBlock(DungeonData data, BlockPos pos)
        {
            int index3d = ((pos.Y % _chunkSize) << 16) | ((pos.Z % _chunkSize) << 8) | (pos.X % _chunkSize);
            Dictionary<int, BlockReinforcement> reinforcmentsOfChunk;
            if (data.Reinforcements == null)
            {
                data.Reinforcements = new Dictionary<int, BlockReinforcement>[data.Chunks.Length];
                reinforcmentsOfChunk = data.Chunks[pos.Y / _chunkSize].GetModdata<Dictionary<int, BlockReinforcement>>("reinforcements");
            }
            else if (data.Reinforcements[pos.Y / _chunkSize] == null)
            {
                reinforcmentsOfChunk = data.Chunks[pos.Y / _chunkSize].GetModdata<Dictionary<int, BlockReinforcement>>("reinforcements");
            }
            else
            {
                reinforcmentsOfChunk = data.Reinforcements[pos.Y / _chunkSize];
            }

            if (reinforcmentsOfChunk is null)
            {
                reinforcmentsOfChunk = new Dictionary<int, BlockReinforcement>();
            }
            data.Reinforcements[pos.Y / _chunkSize] = reinforcmentsOfChunk;

            reinforcmentsOfChunk[index3d] = new BlockReinforcement()
            {
                PlayerUID = "dungeon-UID",
                LastPlayername = "th3dungeons",
                Strength = data.DungeonConfig.ReinforcementLevel
            };
        }

        public void PlaceDecors(IBlockAccessor blockAccessor, BlockPos startPos, bool synchronize, int chunkX, int chunkZ)
        {
            BlockPos curPos = new BlockPos();
            for (int i = 0; i < DecorIndices.Count; i++)
            {
                uint index = DecorIndices[i];

                int dx = (int)(index & 0x1ff);
                int dy = (int)((index >> 20) & 0x1ff);
                int dz = (int)((index >> 10) & 0x1ff);

                if ((dx + startPos.X) / _chunkSize == chunkX && (dz + startPos.Z) / _chunkSize == chunkZ)
                {
                    int storedBlockid = DecorIds[i];
                    byte faceIndex = (byte)(storedBlockid >> 24);
                    if (faceIndex > 5) continue;

                    BlockFacing face = BlockFacing.ALLFACES[faceIndex];
                    storedBlockid &= 0xFFFFFF;

                    AssetLocation blockCode = BlockCodes[storedBlockid];

                    Block newBlock = blockAccessor.GetBlock(blockCode);

                    if (newBlock == null) continue;

                    curPos.Set(dx + startPos.X, dy + startPos.Y, dz + startPos.Z);

                    IWorldChunk chunk = blockAccessor.GetChunkAtBlockPos(curPos);
                    if (chunk == null) continue;
                    if (synchronize) blockAccessor.MarkChunkDecorsModified(curPos);
                    chunk.SetDecor(blockAccessor, newBlock, curPos, face);
                    chunk.MarkModified();
                }
            }
        }

        override protected int PlaceReplaceable(IBlockAccessor blockAccessor, BlockPos pos, Block newBlock, bool replaceMeta)
        {
            if (newBlock.ForFluidsLayer || blockAccessor.GetBlock(pos, 4).Replaceable > newBlock.Replaceable)
            {
                blockAccessor.SetBlock((replaceMeta && (newBlock == fillerBlock || newBlock == pathwayBlock || IsDoor(newBlock))) ? empty : newBlock.BlockId, pos);
                return 1;
            }
            return 0;
        }

        override protected int PlaceReplaceAllNoAir(IBlockAccessor blockAccessor, BlockPos pos, Block newBlock, bool replaceMeta)
        {
            if (newBlock.BlockId != 0)
            {
                blockAccessor.SetBlock((replaceMeta && (newBlock == fillerBlock || newBlock == pathwayBlock || IsDoor(newBlock))) ? empty : newBlock.BlockId, pos);
                return 1;
            }
            return 0;
        }

        override protected int PlaceReplaceOnlyAir(IBlockAccessor blockAccessor, BlockPos pos, Block newBlock, bool replaceMeta)
        {
            if (blockAccessor.GetMostSolidBlock(pos).BlockId == 0)
            {
                blockAccessor.SetBlock((replaceMeta && (newBlock == fillerBlock || newBlock == pathwayBlock || IsDoor(newBlock))) ? empty : newBlock.BlockId, pos);
                return 1;
            }
            return 0;
        }

        public void PlaceEntitiesAndBlockEntities(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos, int chunkX, int chunkZ)
        {
            BlockPos curPos = new BlockPos();

            int schematicSeed = worldForCollectibleResolve.Rand.Next();

            foreach (var val in BlockEntities)
            {
                uint index = val.Key;
                int dx = (int)(index & 0x1ff);
                int dy = (int)((index >> 20) & 0x1ff);
                int dz = (int)((index >> 10) & 0x1ff);

                if ((dx + startPos.X) / _chunkSize == chunkX && (dz + startPos.Z) / _chunkSize == chunkZ)
                {

                    curPos.Set(dx + startPos.X, dy + startPos.Y, dz + startPos.Z);

                    BlockEntity be = blockAccessor.GetBlockEntity(curPos);


                    // Block entities need to be manually initialized for world gen block access
                    if (be == null && blockAccessor is IWorldGenBlockAccessor)
                    {
                        Block block = blockAccessor.GetBlock(curPos);

                        if (block.EntityClass != null)
                        {
                            blockAccessor.SpawnBlockEntity(block.EntityClass, curPos);
                            be = blockAccessor.GetBlockEntity(curPos);
                        }
                    }

                    if (be != null)
                    {
                        Block block = blockAccessor.GetBlock(curPos);
                        if (block.EntityClass != worldForCollectibleResolve.ClassRegistry.GetBlockEntityClass(be.GetType()))
                        {
                            worldForCollectibleResolve.Logger.Warning("Could not import block entity data for schematic at {0}. There is already {1}, expected {2}. Probably overlapping ruins.", curPos, be.GetType(), block.EntityClass);
                            continue;
                        }

                        ITreeAttribute tree = DecodeBlockEntityData(val.Value);
                        tree.SetInt("posx", curPos.X);
                        tree.SetInt("posy", curPos.Y);
                        tree.SetInt("posz", curPos.Z);

                        be.FromTreeAttributes(tree, worldForCollectibleResolve);
                        be.OnLoadCollectibleMappings(worldForCollectibleResolve, BlockCodes, ItemCodes, schematicSeed);
                        be.Pos = curPos.Copy();
                    }
                }
            }

            foreach (string entityData in Entities)
            {
                using (MemoryStream ms = new MemoryStream(Ascii85.Decode(entityData)))
                {
                    BinaryReader reader = new BinaryReader(ms);

                    string className = reader.ReadString();
                    Entity entity = worldForCollectibleResolve.ClassRegistry.CreateEntity(className);

                    entity.FromBytes(reader, false);
                    entity.DidImportOrExport(startPos);

                    // Not ideal but whatever
                    if (blockAccessor is IWorldGenBlockAccessor)
                    {
                        (blockAccessor as IWorldGenBlockAccessor).AddEntity(entity);
                        entity.OnInitialized += delegate
                        {
                            entity.OnLoadCollectibleMappings(worldForCollectibleResolve, BlockCodes, ItemCodes, schematicSeed);
                        };
                    }
                    else
                    {
                        worldForCollectibleResolve.SpawnEntity(entity);
                        entity.OnLoadCollectibleMappings(worldForCollectibleResolve, BlockCodes, ItemCodes, schematicSeed);
                    }

                }
            }
        }

        public new BlockSchematic ClonePacked()
        {
            return new BlockSchematic
            {
                SizeX = SizeX,
                SizeY = SizeY,
                SizeZ = SizeZ,
                GameVersion = GameVersion,
                BlockCodes = new Dictionary<int, AssetLocation>(BlockCodes),
                ItemCodes = new Dictionary<int, AssetLocation>(ItemCodes),
                Indices = new List<uint>(Indices),
                BlockIds = new List<int>(BlockIds),
                BlockEntities = new Dictionary<uint, string>(BlockEntities),
                Entities = new List<string>(Entities),
                ReplaceMode = ReplaceMode
            };
        }
    }
}