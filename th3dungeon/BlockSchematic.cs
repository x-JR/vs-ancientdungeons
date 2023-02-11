using System.Collections.Generic;
using System.IO;
using th3dungeon.Data;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace th3dungeon
{
    public class BlockSchematic : Vintagestory.API.Common.BlockSchematic
    {
        private int _chunkSize;
        private int _worldHeight;

        private Block _doorNorth, _doorEast, _doorSouth, _doorWest;

        public List<DoorPos> Doors;

        public new void Init(IBlockAccessor blockAccessor)
        {
            base.Init(blockAccessor);
            _chunkSize = blockAccessor.ChunkSize;
            _worldHeight = blockAccessor.MapSizeY;

            _doorNorth = blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-north"));
            _doorEast = blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-east"));
            _doorSouth = blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-south"));
            _doorWest = blockAccessor.GetBlock(new AssetLocation("th3dungeon:th3doorway-west"));

            Doors = new List<DoorPos>();
        }

        public void LoadMeta(IBlockAccessor blockAccessor, IWorldAccessor worldForResolve, string fileNameForLogging)
        {
            LoadMetaInformationAndValidate(blockAccessor, worldForResolve, fileNameForLogging);

            for (var i = 0; i < BlockIds.Count; i++)
            {
                var storedBlockId = BlockIds[i];

                var blockCode = BlockCodes[storedBlockId];
                var newBlock = blockAccessor.GetBlock(blockCode);

                if (!IsDoor(newBlock)) continue;
                
                var index = Indices[i];
                var dx = (int)(index & 0x1ff);
                var dy = (int)((index >> 20) & 0x1ff);
                var dz = (int)((index >> 10) & 0x1ff);

                BlockFacing facing = null;

                switch (blockCode.Path)
                {
                    case "th3doorway-north":
                        facing = BlockFacing.NORTH;
                        break;
                    case "th3doorway-east":
                        facing = BlockFacing.EAST;
                        break;
                    case "th3doorway-south":
                        facing = BlockFacing.SOUTH;
                        break;
                    case "th3doorway-west":
                        facing = BlockFacing.WEST;
                        break;
                }
                if (facing != null)
                {
                    Doors.Add(new DoorPos(new BlockPos(dx, dy, dz), facing));
                }
            }
        }

        private bool IsDoor(Block newBlock)
        {
            return newBlock == _doorNorth || newBlock == _doorEast || newBlock == _doorSouth || newBlock == _doorWest;
        }

        /// <summary>
        /// Gets the height index at the position dx and dy in the schematic
        /// </summary>
        public int GetHeightAtPos(int dx, int dz)
        {
            var height = 0;
            for (var dy = 0; dy < SizeY; dy++)
            {
                var index = (uint)(dy << 20 | dz << 10 | dx);
                if (Indices.Find(i => i == index) != 0)
                {
                    height = dy;
                }
            }
            return height;
        }

        public int Place(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, DungeonData data, bool replaceMetaBlocks = true)
        {
            var curPos = new BlockPos();
            var placed = 0;

            PlaceBlockDelegate handler = null;
            switch (ReplaceMode)
            {
                case EnumReplaceMode.ReplaceAll:
                    handler = PlaceReplaceAll;
                    for (var i = 0; i < SizeX; i++)
                    {
                        for (var j = 0; j < SizeY; j++)
                        {
                            for (var k = 0; k < SizeZ; k++)
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


            for (var i = 0; i < Indices.Count; i++)
            {
                var index = Indices[i];

                var dx = (int)(index & 0x1ff);
                var dy = (int)((index >> 20) & 0x1ff);
                var dz = (int)((index >> 10) & 0x1ff);
                curPos.Set(dx + data.NextSpawn.Position.X, dy + data.NextSpawn.Position.Y, dz + data.NextSpawn.Position.Z);

                if (curPos.X / _chunkSize != data.ChunkX || curPos.Z / _chunkSize != data.ChunkZ) continue;
                
                if (data.DungeonConfig.ReinforcementLevel > 0)
                {
                    ReinforceBlock(data, curPos);
                }
                var storedBlockId = BlockIds[i];
                var blockCode = BlockCodes[storedBlockId];

                var newBlock = blockAccessor.GetBlock(blockCode);

                if (newBlock == null || (replaceMetaBlocks && newBlock == undergroundBlock)) continue;


                var oldBlock = blockAccessor.GetBlock(curPos);
                placed += handler(blockAccessor, curPos, newBlock, replaceMetaBlocks);

                if (newBlock.LightHsv[2] > 0 && blockAccessor is IWorldGenBlockAccessor accessor)
                {
                    accessor.ScheduleBlockLightUpdate(curPos.Copy(), oldBlock.BlockId, newBlock.BlockId);
                    accessor.ExchangeBlock(newBlock.Id, curPos);
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
            var index3d = ((pos.Y % _chunkSize) << 16) | ((pos.Z % _chunkSize) << 8) | (pos.X % _chunkSize);
            Dictionary<int, BlockReinforcement> reinforcementsOfChunk;
            if (data.Reinforcements == null)
            {
                data.Reinforcements = new Dictionary<int, BlockReinforcement>[data.Chunks.Length];
                reinforcementsOfChunk = data.Chunks[pos.Y / _chunkSize].GetModdata<Dictionary<int, BlockReinforcement>>("reinforcements");
            }
            else if (data.Reinforcements[pos.Y / _chunkSize] == null)
            {
                reinforcementsOfChunk = data.Chunks[pos.Y / _chunkSize].GetModdata<Dictionary<int, BlockReinforcement>>("reinforcements");
            }
            else
            {
                reinforcementsOfChunk = data.Reinforcements[pos.Y / _chunkSize];
            }

            if (reinforcementsOfChunk is null)
            {
                reinforcementsOfChunk = new Dictionary<int, BlockReinforcement>();
            }
            data.Reinforcements[pos.Y / _chunkSize] = reinforcementsOfChunk;

            reinforcementsOfChunk[index3d] = new BlockReinforcement
            {
                PlayerUID = "dungeon-UID",
                LastPlayername = "th3dungeons",
                Strength = data.DungeonConfig.ReinforcementLevel
            };
        }

        public void PlaceDecors(IBlockAccessor blockAccessor, BlockPos startPos, bool synchronize, int chunkX, int chunkZ)
        {
            var curPos = new BlockPos();
            for (var i = 0; i < DecorIndices.Count; i++)
            {
                var index = DecorIndices[i];

                var dx = (int)(index & 0x1ff);
                var dy = (int)((index >> 20) & 0x1ff);
                var dz = (int)((index >> 10) & 0x1ff);

                if ((dx + startPos.X) / _chunkSize != chunkX || (dz + startPos.Z) / _chunkSize != chunkZ) continue;
                
                var storedBlockId = DecorIds[i];
                var faceIndex = (byte)(storedBlockId >> 24);
                if (faceIndex > 5) continue;

                var face = BlockFacing.ALLFACES[faceIndex];
                storedBlockId &= 0xFFFFFF;

                var blockCode = BlockCodes[storedBlockId];

                var newBlock = blockAccessor.GetBlock(blockCode);

                if (newBlock == null) continue;

                curPos.Set(dx + startPos.X, dy + startPos.Y, dz + startPos.Z);

                var chunk = blockAccessor.GetChunkAtBlockPos(curPos);
                if (chunk == null) continue;
                if (synchronize) blockAccessor.MarkChunkDecorsModified(curPos);
                chunk.SetDecor(blockAccessor, newBlock, curPos, face);
                chunk.MarkModified();
            }
        }

        protected override int PlaceReplaceable(IBlockAccessor blockAccessor, BlockPos pos, Block newBlock, bool replaceMeta)
        {
            if (newBlock.ForFluidsLayer || blockAccessor.GetBlock(pos, 4).Replaceable > newBlock.Replaceable)
            {
                blockAccessor.SetBlock(replaceMeta && (newBlock == fillerBlock || newBlock == pathwayBlock || IsDoor(newBlock)) ? empty : newBlock.BlockId, pos);
                return 1;
            }
            return 0;
        }

        protected override int PlaceReplaceAllNoAir(IBlockAccessor blockAccessor, BlockPos pos, Block newBlock, bool replaceMeta)
        {
            if (newBlock.BlockId == 0) return 0;
            
            blockAccessor.SetBlock(replaceMeta && (newBlock == fillerBlock || newBlock == pathwayBlock || IsDoor(newBlock)) ? empty : newBlock.BlockId, pos);
            return 1;
        }

        protected override int PlaceReplaceOnlyAir(IBlockAccessor blockAccessor, BlockPos pos, Block newBlock, bool replaceMeta)
        {
            if (blockAccessor.GetMostSolidBlock(pos).BlockId != 0) return 0;
            
            blockAccessor.SetBlock(replaceMeta && (newBlock == fillerBlock || newBlock == pathwayBlock || IsDoor(newBlock)) ? empty : newBlock.BlockId, pos);
            return 1;
        }

        public void PlaceEntitiesAndBlockEntities(IBlockAccessor blockAccessor, IWorldAccessor worldForCollectibleResolve, BlockPos startPos, int chunkX, int chunkZ)
        {
            var curPos = new BlockPos();

            var schematicSeed = worldForCollectibleResolve.Rand.Next();

            foreach (var val in BlockEntities)
            {
                var index = val.Key;
                var dx = (int)(index & 0x1ff);
                var dy = (int)((index >> 20) & 0x1ff);
                var dz = (int)((index >> 10) & 0x1ff);

                if ((dx + startPos.X) / _chunkSize != chunkX || (dz + startPos.Z) / _chunkSize != chunkZ) continue;
                
                curPos.Set(dx + startPos.X, dy + startPos.Y, dz + startPos.Z);

                var be = blockAccessor.GetBlockEntity(curPos);


                // Block entities need to be manually initialized for world gen block access
                if (be == null && blockAccessor is IWorldGenBlockAccessor)
                {
                    var block1 = blockAccessor.GetBlock(curPos);

                    if (block1.EntityClass != null)
                    {
                        blockAccessor.SpawnBlockEntity(block1.EntityClass, curPos);
                        be = blockAccessor.GetBlockEntity(curPos);
                    }
                }

                if (be == null) continue;
                
                var block = blockAccessor.GetBlock(curPos);
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

            foreach (var entityData in Entities)
            {
                using (var ms = new MemoryStream(Ascii85.Decode(entityData)))
                {
                    var reader = new BinaryReader(ms);

                    var className = reader.ReadString();
                    var entity = worldForCollectibleResolve.ClassRegistry.CreateEntity(className);

                    entity.FromBytes(reader, false);
                    entity.DidImportOrExport(startPos);

                    // Not ideal but whatever
                    if (blockAccessor is IWorldGenBlockAccessor accessor)
                    {
                        accessor.AddEntity(entity);
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